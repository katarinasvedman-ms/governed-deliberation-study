using System.Collections.Immutable;

namespace GovernedAgent.Research;

internal sealed record CoordinatorMemoryTransition(
    MemoryUpdate Update,
    WorkingMemorySnapshot? Snapshot,
    ReconsiderationTrigger? Trigger,
    IReadOnlyList<Belief> CreatedBeliefs,
    IReadOnlyList<(BeliefStateEntry? Previous, BeliefStateEntry Current)> StateChanges,
    IReadOnlyList<(string PredecessorId, string BeliefId)> Reassertions,
    Direction? DirectionAfter,
    string? DirectionEndedId,
    DirectionState? DirectionEndState);

internal sealed class CoordinatorMemoryStore
{
    private readonly string _runId;
    private readonly List<Belief> _beliefs = [];
    private readonly Dictionary<string, MemoryBeliefFixture> _fixtures =
        new(StringComparer.Ordinal);
    private readonly List<Direction> _directions = [];
    private readonly Dictionary<string, DirectionState> _directionStates =
        new(StringComparer.Ordinal);
    private readonly List<MemoryUpdate> _updates = [];
    private readonly List<WorkingMemorySnapshot> _snapshots = [];
    private readonly List<ReconsiderationTrigger> _triggers = [];
    private uint _beliefNumber;
    private uint _directionNumber;
    private uint _updateNumber;
    private uint _triggerNumber;

    public CoordinatorMemoryStore(string runId)
    {
        _runId = runId;
        _snapshots.Add(
            ResearchContractFreezer.Freeze(new WorkingMemorySnapshot(
                ResearchContractVersions.SchemaVersion,
                "working-memory-snapshot",
                runId,
                0,
                null,
                0,
                [],
                null)));
    }

    public WorkingMemorySnapshot Current => _snapshots[^1];

    public IReadOnlyList<Belief> Beliefs => _beliefs.AsReadOnly();

    public IReadOnlyList<Direction> Directions => _directions.AsReadOnly();

    public IReadOnlyList<MemoryUpdate> Updates => _updates.AsReadOnly();

    public IReadOnlyList<ReconsiderationTrigger> Triggers => _triggers.AsReadOnly();

    public MemoryUpdate GetUpdate(string memoryUpdateId) =>
        _updates.Single(item => item.MemoryUpdateId == memoryUpdateId);

    public CoordinatorMemoryTransition ApplySupervisorProposal(
        ProposeMemoryUpdateSupervisorOutput proposal,
        string reviewId,
        string resultId,
        string snapshotId,
        uint reviewStartTick,
        IReadOnlySet<string> capturedObservationIds,
        uint currentGeneration,
        uint tick,
        string? previousPendingTriggerId)
    {
        proposal = (ProposeMemoryUpdateSupervisorOutput)
            ResearchContractFreezer.Freeze(proposal);
        var context = new MemoryProposalContext(
            _runId,
            proposal.BaseMemoryRevision,
            Current.MemoryRevision,
            Current.ApplicabilityEpoch,
            capturedObservationIds,
            _fixtures.Values
                .OrderBy(item => IdNumber(item.Belief.BeliefId))
                .ToList(),
            Current.RecommendedDirection,
            _directions.AsReadOnly());
        var validation = MemoryProposalValidator.Validate(proposal, context);
        var updateId = NextUpdateId();
        var origin = new SupervisorUpdateOrigin(reviewId, resultId, snapshotId);

        if (!validation.IsValid)
        {
            var rejected = new MemoryUpdate(
                ResearchContractVersions.SchemaVersion,
                "memory-update",
                _runId,
                updateId,
                origin,
                proposal.BaseMemoryRevision,
                Current.MemoryRevision,
                null,
                "rejected",
                false,
                currentGeneration,
                currentGeneration,
                null,
                proposal,
                [],
                [],
                null,
                validation.Issues.Select(ToReason).ToList());
            rejected = ResearchContractFreezer.Freeze(rejected);
            _updates.Add(rejected);
            return Empty(rejected);
        }

        var resolvedBindings = ResolveBindings(validation);
        if (validation.IsNoOp)
        {
            var noOp = new MemoryUpdate(
                ResearchContractVersions.SchemaVersion,
                "memory-update",
                _runId,
                updateId,
                origin,
                proposal.BaseMemoryRevision,
                Current.MemoryRevision,
                null,
                "no-op",
                false,
                currentGeneration,
                currentGeneration,
                null,
                proposal,
                validation.Normalization,
                resolvedBindings.Bindings,
                null,
                []);
            noOp = ResearchContractFreezer.Freeze(noOp);
            _updates.Add(noOp);
            return Empty(noOp);
        }

        var nextRevision = checked(Current.MemoryRevision + 1);
        var created = new List<Belief>();
        foreach (var proposed in validation.CreatedBeliefs)
        {
            var belief = new Belief(
                ResearchContractVersions.SchemaVersion,
                "belief",
                _runId,
                resolvedBindings.ByReference[$"proposed:{proposed.Key}"],
                updateId,
                nextRevision,
                Current.ApplicabilityEpoch,
                proposed.Claim,
                proposed.ReassertedFromBeliefId);
            created.Add(ResearchContractFreezer.Freeze(belief));
        }

        var states = _fixtures.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        foreach (var belief in created)
        {
            states.Add(
                belief.BeliefId,
                new MemoryBeliefFixture(
                    belief,
                    new BeliefStateEntry(
                        belief.BeliefId,
                        BeliefState.Provisional,
                        updateId,
                        [],
                        null),
                    nextRevision));
        }

        var previousStates = states.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.State,
            StringComparer.Ordinal);
        foreach (var link in validation.Supersessions)
        {
            var destination = ResolveReference(link.ToReference, resolvedBindings.ByReference);
            var fixture = states[link.FromBeliefId];
            states[link.FromBeliefId] = fixture with
            {
                State = fixture.State with
                {
                    State = BeliefState.Superseded,
                    StateChangedByUpdateId = updateId,
                    ConflictingBeliefIds = [],
                    SupersededByBeliefId = destination
                },
                StateChangedAtMemoryRevision = nextRevision
            };
        }

        foreach (var beliefId in validation.Retractions)
        {
            var fixture = states[beliefId];
            states[beliefId] = fixture with
            {
                State = fixture.State with
                {
                    State = BeliefState.Retracted,
                    StateChangedByUpdateId = updateId,
                    ConflictingBeliefIds = [],
                    SupersededByBeliefId = null
                },
                StateChangedAtMemoryRevision = nextRevision
            };
        }

        var active = states.Values
            .Where(item => item.Belief.ApplicabilityEpoch == Current.ApplicabilityEpoch)
            .Where(item => item.State.State is BeliefState.Provisional or BeliefState.Contested)
            .Select(item => item.Belief.BeliefId)
            .ToHashSet(StringComparer.Ordinal);
        var edges = states.Values
            .SelectMany(item => item.State.ConflictingBeliefIds.Select(
                other => OrderedEdge(item.Belief.BeliefId, other)))
            .Where(edge => active.Contains(edge.Left) && active.Contains(edge.Right))
            .ToHashSet();
        foreach (var edge in validation.AddedConflicts)
        {
            edges.Add(
                OrderedEdge(
                    ResolveReference(edge.Left, resolvedBindings.ByReference),
                    ResolveReference(edge.Right, resolvedBindings.ByReference)));
        }

        foreach (var beliefId in active)
        {
            var conflicts = edges
                .Where(edge => edge.Left == beliefId || edge.Right == beliefId)
                .Select(edge => edge.Left == beliefId ? edge.Right : edge.Left)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList();
            var fixture = states[beliefId];
            var nextState = conflicts.Count == 0
                ? BeliefState.Provisional
                : BeliefState.Contested;
            var changed =
                fixture.State.State != nextState ||
                !fixture.State.ConflictingBeliefIds.SequenceEqual(
                    conflicts,
                    StringComparer.Ordinal);
            states[beliefId] = fixture with
            {
                State = changed
                    ? fixture.State with
                    {
                        State = nextState,
                        StateChangedByUpdateId = updateId,
                        ConflictingBeliefIds = conflicts
                    }
                    : fixture.State,
                StateChangedAtMemoryRevision = changed
                    ? nextRevision
                    : fixture.StateChangedAtMemoryRevision
            };
        }

        var directionBefore = Current.RecommendedDirection;
        var directionAfter = directionBefore;
        DirectionState? directionEndState = null;
        if (validation.DirectionChanged)
        {
            var droppedIndexes = validation.Normalization
                .Select(note => note.OperationIndex)
                .ToHashSet();
            var retained = proposal.Operations
                .Select((operation, index) => (operation, index))
                .FirstOrDefault(item =>
                    item.operation is SetDirectionOperation or ClearDirectionOperation &&
                    !droppedIndexes.Contains(checked((uint)item.index)));
            if (retained.operation is SetDirectionOperation set)
            {
                if (directionBefore is not null)
                {
                    directionEndState = DirectionState.Superseded;
                }

                directionAfter = ResearchContractFreezer.Freeze(new Direction(
                    ResearchContractVersions.SchemaVersion,
                    "direction",
                    _runId,
                    NextDirectionId(),
                    updateId,
                    reviewId,
                    reviewStartTick,
                    checked(reviewStartTick + ApprovedScriptedExperiment.Clock.GuidanceLifetimeTicks),
                    Current.ApplicabilityEpoch,
                    set.Recommendation,
                    set.ObservationIds,
                    set.SupportingBeliefs
                        .Select(reference => ResolveBeliefReference(reference, resolvedBindings.ByReference))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToList()));
            }
            else if (retained.operation is ClearDirectionOperation)
            {
                directionAfter = null;
                directionEndState = DirectionState.Cleared;
            }
        }

        var effects = new MemoryEffects(
            created.Select(item => item.BeliefId).ToList(),
            validation.Supersessions.Select(
                item => new BeliefLink(
                    item.FromBeliefId,
                    ResolveReference(item.ToReference, resolvedBindings.ByReference))).ToList(),
            validation.Reassertions.Select(
                item => new BeliefLink(
                    item.FromBeliefId,
                    ResolveReference(item.ToReference, resolvedBindings.ByReference))).ToList(),
            validation.Retractions,
            [],
            CanonicalConflictEdges(
                validation.AddedConflicts.Select(
                    edge => (
                        ResolveReference(edge.Left, resolvedBindings.ByReference),
                        ResolveReference(edge.Right, resolvedBindings.ByReference)))),
            CanonicalConflictEdges(
                previousStates.Values
                    .SelectMany(state => state.ConflictingBeliefIds.Select(
                        other => OrderedEdge(state.BeliefId, other)))
                    .Distinct()
                    .Except(edges)),
            directionBefore?.DirectionId,
            directionAfter?.DirectionId,
            directionEndState);
        var triggerId = NextTriggerId();
        var trigger = new ReconsiderationTrigger(
            ResearchContractVersions.SchemaVersion,
            "reconsideration-trigger",
            _runId,
            triggerId,
            updateId,
            nextRevision,
            checked(currentGeneration + 1),
            tick,
            previousPendingTriggerId);
        var update = new MemoryUpdate(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            _runId,
            updateId,
            origin,
            proposal.BaseMemoryRevision,
            Current.MemoryRevision,
            nextRevision,
            "committed",
            true,
            currentGeneration,
            checked(currentGeneration + 1),
            triggerId,
            proposal,
            validation.Normalization,
            resolvedBindings.Bindings,
            effects,
            []);
        var snapshot = ResearchContractFreezer.Freeze(new WorkingMemorySnapshot(
            ResearchContractVersions.SchemaVersion,
            "working-memory-snapshot",
            _runId,
            nextRevision,
            Current.MemoryRevision,
            Current.ApplicabilityEpoch,
            states.Values
                .Select(item => item.State)
                .OrderBy(item => IdNumber(item.BeliefId))
                .ToList(),
            directionAfter));
        update = ResearchContractFreezer.Freeze(update);
        ResearchContractValidator.ValidateAndThrow(update);
        ResearchContractValidator.ValidateAndThrow(snapshot);
        ResearchContractValidator.ValidateAndThrow(trigger);
        foreach (var belief in created)
        {
            ResearchContractValidator.ValidateAndThrow(belief);
        }

        _updates.Add(update);
        _beliefs.AddRange(created);
        _fixtures.Clear();
        foreach (var pair in states)
        {
            _fixtures.Add(pair.Key, pair.Value);
        }

        if (directionAfter is not null &&
            !ReferenceEquals(directionAfter, directionBefore))
        {
            _directions.Add(directionAfter);
            _directionStates[directionAfter.DirectionId] = DirectionState.Active;
        }

        if (directionBefore is not null &&
            directionBefore.DirectionId != directionAfter?.DirectionId &&
            directionEndState is not null)
        {
            _directionStates[directionBefore.DirectionId] = directionEndState.Value;
        }

        _snapshots.Add(snapshot);
        _triggers.Add(trigger);
        var changes = states.Values
            .Where(item =>
                !previousStates.TryGetValue(item.Belief.BeliefId, out var previous) ||
                previous != item.State)
            .Select(item => (
                previousStates.TryGetValue(item.Belief.BeliefId, out var previous)
                    ? previous
                    : null,
                item.State))
            .ToList();
        return new CoordinatorMemoryTransition(
            update,
            snapshot,
            trigger,
            created,
            changes,
            validation.Reassertions.Select(
                item => (
                    item.FromBeliefId,
                    ResolveReference(item.ToReference, resolvedBindings.ByReference))).ToList(),
            directionAfter,
            directionBefore?.DirectionId != directionAfter?.DirectionId
                ? directionBefore?.DirectionId
                : null,
            directionEndState);
    }

    public CoordinatorMemoryTransition ExpireDirection(uint tick, uint currentGeneration)
    {
        var direction = Current.RecommendedDirection;
        if (direction is null || tick < direction.ExpiresAtTick)
        {
            throw new InvalidOperationException("No direction is eligible to expire.");
        }

        return ApplySystemDirectionEnd(
            new DirectionExpiryUpdateOrigin(direction.DirectionId),
            direction,
            DirectionState.Expired,
            currentGeneration);
    }

    public CoordinatorMemoryTransition InvalidateEpoch(
        uint newEpoch,
        uint causedBySequence,
        uint currentGeneration,
        uint tick,
        string? previousPendingTriggerId)
    {
        if (newEpoch <= Current.ApplicabilityEpoch)
        {
            throw new ArgumentOutOfRangeException(nameof(newEpoch));
        }

        var updateId = NextUpdateId();
        var nextRevision = checked(Current.MemoryRevision + 1);
        var states = _fixtures.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal);
        var invalidated = new List<string>();
        var changes = new List<(BeliefStateEntry? Previous, BeliefStateEntry Current)>();
        foreach (var pair in states.ToList())
        {
            if (pair.Value.Belief.ApplicabilityEpoch != Current.ApplicabilityEpoch ||
                pair.Value.State.State is not (BeliefState.Provisional or BeliefState.Contested))
            {
                continue;
            }

            var state = pair.Value.State with
            {
                State = BeliefState.Invalidated,
                StateChangedByUpdateId = updateId,
                ConflictingBeliefIds = [],
                SupersededByBeliefId = null
            };
            states[pair.Key] = pair.Value with
            {
                State = state,
                StateChangedAtMemoryRevision = nextRevision
            };
            invalidated.Add(pair.Key);
            changes.Add((pair.Value.State, state));
        }

        var directionBefore = Current.RecommendedDirection;
        var actionable = invalidated.Count != 0 || directionBefore is not null;
        var newGeneration = actionable
            ? checked(currentGeneration + 1)
            : currentGeneration;
        var triggerId = actionable ? NextTriggerId() : null;
        var effects = new MemoryEffects(
            [],
            [],
            [],
            [],
            invalidated,
            [],
            Current.BeliefStates
                .SelectMany(state => state.ConflictingBeliefIds.Select(
                    other => OrderedEdge(state.BeliefId, other)))
                .Distinct()
                .Select(edge => new ConflictEdge(edge.Left, edge.Right))
                .ToList(),
            directionBefore?.DirectionId,
            null,
            directionBefore is null ? null : DirectionState.Invalidated);
        var update = new MemoryUpdate(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            _runId,
            updateId,
            new EpochInvalidationUpdateOrigin(causedBySequence),
            Current.MemoryRevision,
            Current.MemoryRevision,
            nextRevision,
            "committed",
            actionable,
            currentGeneration,
            newGeneration,
            triggerId,
            null,
            [],
            [],
            effects,
            []);
        var snapshot = ResearchContractFreezer.Freeze(new WorkingMemorySnapshot(
            ResearchContractVersions.SchemaVersion,
            "working-memory-snapshot",
            _runId,
            nextRevision,
            Current.MemoryRevision,
            newEpoch,
            states.Values
                .Select(item => item.State)
                .OrderBy(item => IdNumber(item.BeliefId))
                .ToList(),
            null));
        ReconsiderationTrigger? trigger = null;
        if (actionable)
        {
            trigger = new ReconsiderationTrigger(
                ResearchContractVersions.SchemaVersion,
                "reconsideration-trigger",
                _runId,
                triggerId!,
                updateId,
                nextRevision,
                newGeneration,
                tick,
                previousPendingTriggerId);
        }

        update = ResearchContractFreezer.Freeze(update);

        ResearchContractValidator.ValidateAndThrow(update);
        ResearchContractValidator.ValidateAndThrow(snapshot);
        if (trigger is not null)
        {
            ResearchContractValidator.ValidateAndThrow(trigger);
            _triggers.Add(trigger);
        }

        _updates.Add(update);
        _snapshots.Add(snapshot);
        if (directionBefore is not null)
        {
            _directionStates[directionBefore.DirectionId] = DirectionState.Invalidated;
        }
        _fixtures.Clear();
        foreach (var pair in states)
        {
            _fixtures.Add(pair.Key, pair.Value);
        }

        return new CoordinatorMemoryTransition(
            update,
            snapshot,
            trigger,
            [],
            changes,
            [],
            null,
            directionBefore?.DirectionId,
            directionBefore is null ? null : DirectionState.Invalidated);
    }

    public ReconsiderationRequirement CaptureRequirement(string triggerId)
    {
        var trigger = _triggers.Single(item => item.TriggerId == triggerId);
        var update = GetUpdate(trigger.MemoryUpdateId);
        var earlier = new List<string>();
        var cursor = trigger.PreviousTriggerId;
        while (cursor is not null)
        {
            earlier.Add(cursor);
            cursor = _triggers.Single(item => item.TriggerId == cursor).PreviousTriggerId;
        }

        earlier.Reverse();
        var effects = new List<TriggerEffectView>();
        foreach (var beliefId in update.Effects?.CreatedBeliefIds
                     .Concat(update.Effects.Supersessions.Select(item => item.FromBeliefId))
                     .Concat(update.Effects.Supersessions.Select(item => item.ToBeliefId))
                     .Concat(update.Effects.RetractedBeliefIds)
                     .Concat(update.Effects.InvalidatedBeliefIds)
                     .Concat(update.Effects.AddedConflicts.SelectMany(
                         edge => new[] { edge.Left, edge.Right }))
                     .Concat(update.Effects.RemovedConflicts.SelectMany(
                         edge => new[] { edge.Left, edge.Right }))
                     .Distinct(StringComparer.Ordinal) ?? [])
        {
            effects.Add(
                new BeliefTriggerEffectView(
                    beliefId,
                    Current.BeliefStates.Single(item => item.BeliefId == beliefId).State));
        }

        foreach (var directionId in new[]
                 {
                     update.Effects?.DirectionBeforeId,
                     update.Effects?.DirectionAfterId
                 }
                 .Where(id => id is not null)
                 .Cast<string>()
                 .Distinct(StringComparer.Ordinal))
        {
            effects.Add(
                new DirectionTriggerEffectView(
                    directionId,
                    _directionStates.TryGetValue(directionId, out var state)
                        ? state
                        : update.Effects?.DirectionEndState
                            ?? DirectionState.Superseded));
        }

        return new ReconsiderationRequirement(
            trigger,
            earlier.ToImmutableArray(),
            effects.ToImmutableArray());
    }

    private CoordinatorMemoryTransition ApplySystemDirectionEnd(
        UpdateOrigin origin,
        Direction direction,
        DirectionState endState,
        uint currentGeneration)
    {
        var updateId = NextUpdateId();
        var nextRevision = checked(Current.MemoryRevision + 1);
        var effects = new MemoryEffects(
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            direction.DirectionId,
            null,
            endState);
        var update = new MemoryUpdate(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            _runId,
            updateId,
            origin,
            Current.MemoryRevision,
            Current.MemoryRevision,
            nextRevision,
            "committed",
            false,
            currentGeneration,
            currentGeneration,
            null,
            null,
            [],
            [],
            effects,
            []);
        var snapshot = ResearchContractFreezer.Freeze(Current with
        {
            MemoryRevision = nextRevision,
            PreviousMemoryRevision = Current.MemoryRevision,
            RecommendedDirection = null
        });
        update = ResearchContractFreezer.Freeze(update);
        ResearchContractValidator.ValidateAndThrow(update);
        ResearchContractValidator.ValidateAndThrow(snapshot);
        _updates.Add(update);
        _snapshots.Add(snapshot);
        _directionStates[direction.DirectionId] = endState;
        return new CoordinatorMemoryTransition(
            update,
            snapshot,
            null,
            [],
            [],
            [],
            null,
            direction.DirectionId,
            endState);
    }

    private ResolvedBindings ResolveBindings(MemoryProposalValidationResult validation)
    {
        var byReference = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var proposed in validation.CreatedBeliefs)
        {
            byReference.Add($"proposed:{proposed.Key}", NextBeliefId());
        }

        var unresolved = validation.KeyBindings.ToList();
        while (unresolved.Count != 0)
        {
            var resolvedAny = false;
            foreach (var binding in unresolved.ToList())
            {
                var localReference = $"proposed:{binding.Key}";
                if (byReference.ContainsKey(localReference))
                {
                    unresolved.Remove(binding);
                    resolvedAny = true;
                    continue;
                }

                string resolvedReference;
                if (binding.Reference.StartsWith("proposed:", StringComparison.Ordinal))
                {
                    if (!byReference.TryGetValue(binding.Reference, out resolvedReference!))
                    {
                        continue;
                    }
                }
                else
                {
                    resolvedReference = binding.Reference;
                }

                byReference.Add(localReference, resolvedReference);
                unresolved.Remove(binding);
                resolvedAny = true;
            }

            if (!resolvedAny)
            {
                throw new InvalidOperationException(
                    "Validated proposal bindings contain an unresolved local-key cycle.");
            }
        }

        var bindings = validation.KeyBindings.Select(
            item => new KeyBinding(
                item.Key,
                ResolveReference($"proposed:{item.Key}", byReference))).ToList();
        return new ResolvedBindings(byReference, bindings);
    }

    private string NextBeliefId() => $"belief-{++_beliefNumber}";
    private string NextDirectionId() => $"direction-{++_directionNumber}";
    private string NextUpdateId() => $"memory-update-{++_updateNumber}";
    private string NextTriggerId() => $"trigger-{++_triggerNumber}";

    private static CoordinatorMemoryTransition Empty(MemoryUpdate update) =>
        new(update, null, null, [], [], [], null, null, null);

    private static Reason ToReason(ResearchValidationIssue issue) =>
        new(
            issue.Code is
                "invalid-json" or
                "duplicate-property" or
                "unsupported-schema" or
                "unknown-record-type" or
                "unknown-field" or
                "missing-field" or
                "wrong-type" or
                "unknown-enum" or
                "invalid-identifier" or
                "invalid-reference" or
                "invalid-shape" or
                "out-of-range" or
                "missing-memory-disposition" or
                "engineering-limit-exceeded"
                    ? ReasonDomain.Contract
                    : ReasonDomain.Context,
            issue.Code,
            issue.Message);

    private static string ResolveBeliefReference(
        BeliefRef reference,
        IReadOnlyDictionary<string, string> bindings) =>
        reference switch
        {
            ExistingBeliefRef existing => existing.BeliefId,
            ProposedBeliefRef proposed => ResolveReference(
                $"proposed:{proposed.Key}",
                bindings),
            _ => throw new InvalidOperationException("Unsupported belief reference.")
        };

    private static string ResolveReference(
        string reference,
        IReadOnlyDictionary<string, string> bindings) =>
        bindings.TryGetValue(reference, out var resolved) ? resolved : reference;

    private static (string Left, string Right) OrderedEdge(string left, string right) =>
        string.CompareOrdinal(left, right) <= 0 ? (left, right) : (right, left);

    private static IReadOnlyList<ConflictEdge> CanonicalConflictEdges(
        IEnumerable<(string Left, string Right)> edges) =>
        edges
            .Select(edge => OrderedEdge(edge.Left, edge.Right))
            .Distinct()
            .OrderBy(edge => edge.Left, StringComparer.Ordinal)
            .ThenBy(edge => edge.Right, StringComparer.Ordinal)
            .Select(edge => new ConflictEdge(edge.Left, edge.Right))
            .ToImmutableArray();

    private static uint IdNumber(string id) =>
        uint.Parse(id[(id.LastIndexOf('-') + 1)..], System.Globalization.CultureInfo.InvariantCulture);

    private sealed record ResolvedBindings(
        IReadOnlyDictionary<string, string> ByReference,
        IReadOnlyList<KeyBinding> Bindings);
}
