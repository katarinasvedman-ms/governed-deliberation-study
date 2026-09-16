using System.Text.Json;

namespace GovernedAgent.Research;

public sealed record MemoryBeliefFixture(
    Belief Belief,
    BeliefStateEntry State,
    uint StateChangedAtMemoryRevision);

public sealed record MemoryProposalContext(
    string RunId,
    uint CapturedMemoryRevision,
    uint CurrentMemoryRevision,
    uint ApplicabilityEpoch,
    IReadOnlySet<string> CapturedObservationIds,
    IReadOnlyList<MemoryBeliefFixture> Beliefs,
    Direction? ActiveDirection,
    IReadOnlyList<Direction> HistoricalDirections);

public sealed record ProposedBelief(
    string Key,
    Claim Claim,
    string? ReassertedFromBeliefId);

public sealed record ProposalKeyBinding(string Key, string Reference);
public sealed record PlannedBeliefLink(string FromBeliefId, string ToReference);

public sealed record MemoryProposalValidationResult(
    IReadOnlyList<ResearchValidationIssue> Issues,
    IReadOnlyList<NormalizationNote> Normalization,
    IReadOnlyList<ProposalKeyBinding> KeyBindings,
    IReadOnlyList<ProposedBelief> CreatedBeliefs,
    IReadOnlyList<PlannedBeliefLink> Supersessions,
    IReadOnlyList<PlannedBeliefLink> Reassertions,
    IReadOnlyList<string> Retractions,
    IReadOnlyList<ConflictEdge> AddedConflicts,
    bool DirectionChanged,
    bool IsNoOp)
{
    public bool IsValid => Issues.Count == 0;
}

public static class MemoryProposalValidator
{
    public static MemoryProposalValidationResult Validate(
        ProposeMemoryUpdateSupervisorOutput proposal,
        MemoryProposalContext context)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(context);

        var issues = ResearchContractValidator.Validate(proposal).ToList();
        ValidateContext(proposal, context, issues);
        if (issues.Count != 0)
        {
            return EmptyResult(issues);
        }

        var fixtures = context.Beliefs.ToDictionary(
            item => item.Belief.BeliefId,
            StringComparer.Ordinal);
        var eligible = context.Beliefs
            .Where(item =>
                item.Belief.ApplicabilityEpoch == context.ApplicabilityEpoch &&
                item.State.State is BeliefState.Provisional or BeliefState.Contested)
            .ToList();
        var eligibleByClaimKey = eligible
            .GroupBy(item => ClaimKey(item.Belief.Claim), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(item => item.Belief.CreatedAtMemoryRevision)
                    .ThenBy(item => IdNumber(item.Belief.BeliefId))
                    .First(),
                StringComparer.Ordinal);
        var terminal = context.Beliefs
            .Where(item =>
                item.Belief.ApplicabilityEpoch == context.ApplicabilityEpoch &&
                item.State.State is BeliefState.Superseded or
                    BeliefState.Retracted or
                    BeliefState.Invalidated)
            .ToList();
        var terminalClaimKeys = terminal
            .Select(item => ClaimKey(item.Belief.Claim))
            .ToHashSet(StringComparer.Ordinal);
        var definitions = CollectDefinitions(proposal, context, fixtures, terminal, issues);
        var normalization = new List<NormalizationNote>();
        var keyBindings = new List<ProposalKeyBinding>();
        var created = new Dictionary<string, ProposedBelief>(StringComparer.Ordinal);
        var reassertions = new List<PlannedBeliefLink>();

        BindDefinitions(
            definitions,
            eligibleByClaimKey,
            terminalClaimKeys,
            normalization,
            keyBindings,
            created,
            reassertions,
            issues);

        var supersessions = new List<PlannedBeliefLink>();
        var retractions = new List<string>();
        var retired = new HashSet<string>(StringComparer.Ordinal);
        var baseConflictEdges = context.Beliefs
            .SelectMany(item => item.State.ConflictingBeliefIds.Select(
                other => OrderedEdge(item.Belief.BeliefId, other)))
            .ToHashSet();
        var addedConflicts = new HashSet<(string Left, string Right)>();
        var directionOperationCount = proposal.Operations.Count(
            operation => operation is SetDirectionOperation or ClearDirectionOperation);
        if (directionOperationCount > 1)
        {
            Add(issues, "inconsistent-update", "At most one raw direction operation is allowed.");
        }

        var directionChanged = false;
        var directionCleared = false;
        SetDirectionOperation? retainedDirection = null;
        var rawDirectionSupport = new List<string>();

        for (var index = 0; index < proposal.Operations.Count; index++)
        {
            switch (proposal.Operations[index])
            {
                case ReplaceBeliefOperation replace:
                    if (!TryGetEligible(replace.BeliefId, fixtures, context.ApplicabilityEpoch))
                    {
                        Add(issues, "belief-not-in-snapshot", "Replacement target is not eligible.");
                        break;
                    }

                    if (!retired.Add(replace.BeliefId))
                    {
                        Add(issues, "inconsistent-update", "A belief cannot be retired twice.");
                    }

                    var destination = ResolveBinding(replace.Key, keyBindings);
                    if (destination is null)
                    {
                        break;
                    }

                    if (string.Equals(destination, replace.BeliefId, StringComparison.Ordinal))
                    {
                        retired.Remove(replace.BeliefId);
                        ReplaceNote(
                            normalization,
                            Note(index, "drop-self-replacement", replace.BeliefId));
                    }
                    else
                    {
                        supersessions.Add(new PlannedBeliefLink(replace.BeliefId, destination));
                    }

                    break;
                case RetractBeliefOperation retract:
                    if (!TryGetEligible(retract.BeliefId, fixtures, context.ApplicabilityEpoch))
                    {
                        Add(issues, "belief-not-in-snapshot", "Retraction target is not eligible.");
                    }
                    else if (!retired.Add(retract.BeliefId))
                    {
                        Add(issues, "inconsistent-update", "A belief cannot be retired twice.");
                    }
                    else
                    {
                        retractions.Add(retract.BeliefId);
                    }

                    break;
                case DeclareConflictOperation conflict:
                    ResolveConflict(
                        index,
                        conflict,
                        fixtures,
                        keyBindings,
                        baseConflictEdges,
                        addedConflicts,
                        normalization,
                        issues);
                    break;
                case SetDirectionOperation setDirection:
                    var resolvedSupport = setDirection.SupportingBeliefs
                        .Select(reference => ResolveReference(reference, fixtures, keyBindings))
                        .ToList();
                    if (resolvedSupport.Any(reference => reference is null))
                    {
                        Add(
                            issues,
                            "invalid-direction-dependency",
                            "Direction contains an unresolved supporting belief reference.");
                    }
                    else
                    {
                        rawDirectionSupport.AddRange(resolvedSupport.Cast<string>());
                    }

                    var directionKey = DirectionKey(
                        context.RunId,
                        context.ApplicabilityEpoch,
                        setDirection);
                    var duplicate = context.HistoricalDirections.FirstOrDefault(
                        direction => string.Equals(
                            DirectionKey(direction),
                            directionKey,
                            StringComparison.Ordinal));
                    if (duplicate is not null)
                    {
                        normalization.Add(
                            Note(
                                index,
                                "drop-repeated-direction",
                                duplicateDirectionId: duplicate.DirectionId));
                    }
                    else
                    {
                        retainedDirection = setDirection;
                        directionChanged = true;
                    }

                    break;
                case ClearDirectionOperation:
                    if (context.ActiveDirection is null)
                    {
                        normalization.Add(Note(index, "drop-empty-clear"));
                    }
                    else
                    {
                        directionCleared = true;
                        directionChanged = true;
                    }

                    break;
            }
        }

        var currentIds = eligible
            .Select(item => item.Belief.BeliefId)
            .Where(id => !retired.Contains(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var proposed in created.Values)
        {
            currentIds.Add(ProposedReference(proposed.Key));
        }

        var conflictEdges = baseConflictEdges
            .Where(edge =>
                currentIds.Contains(edge.Left) &&
                currentIds.Contains(edge.Right))
            .ToHashSet();
        foreach (var edge in addedConflicts)
        {
            if (!currentIds.Contains(edge.Left) || !currentIds.Contains(edge.Right))
            {
                Add(issues, "invalid-conflict", "Conflict endpoints must survive the successor.");
            }
            else
            {
                conflictEdges.Add(edge);
            }
        }

        var contested = conflictEdges
            .SelectMany(edge => new[] { edge.Left, edge.Right })
            .ToHashSet(StringComparer.Ordinal);
        ValidateDirectionDependencies(
            context,
            retainedDirection,
            directionCleared,
            rawDirectionSupport,
            currentIds,
            contested,
            fixtures,
            keyBindings,
            issues);

        foreach (var binding in keyBindings)
        {
            if (retired.Contains(binding.Reference))
            {
                Add(
                    issues,
                    "inconsistent-update",
                    "A normalized alias cannot target a belief retired by the transaction.");
            }
        }

        var materializedConflicts = addedConflicts
            .Select(edge => new ConflictEdge(edge.Left, edge.Right))
            .OrderBy(edge => edge.Left, StringComparer.Ordinal)
            .ThenBy(edge => edge.Right, StringComparer.Ordinal)
            .ToList();
        var isNoOp =
            issues.Count == 0 &&
            created.Count == 0 &&
            supersessions.Count == 0 &&
            retractions.Count == 0 &&
            materializedConflicts.Count == 0 &&
            !directionChanged;

        return new MemoryProposalValidationResult(
            issues,
            normalization.OrderBy(note => note.OperationIndex).ToList(),
            keyBindings.OrderBy(binding => binding.Key, StringComparer.Ordinal).ToList(),
            created.Values.OrderBy(item => item.Key, StringComparer.Ordinal).ToList(),
            supersessions,
            reassertions,
            retractions,
            materializedConflicts,
            directionChanged,
            isNoOp);
    }

    private static void ValidateContext(
        ProposeMemoryUpdateSupervisorOutput proposal,
        MemoryProposalContext context,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!IsValidRunId(context.RunId))
        {
            Add(issues, "run-mismatch", "Proposal context has an invalid run identifier.");
        }

        if (proposal.BaseMemoryRevision != context.CapturedMemoryRevision)
        {
            Add(issues, "memory-base-mismatch", "Proposal base differs from its captured snapshot.");
        }

        if (proposal.BaseMemoryRevision != context.CurrentMemoryRevision)
        {
            Add(issues, "memory-revision-conflict", "Proposal base is not the current memory revision.");
        }

        foreach (var observationId in proposal.ObservationIds)
        {
            if (!context.CapturedObservationIds.Contains(observationId))
            {
                Add(issues, "observation-not-in-snapshot", "Proposal cites an uncaptured observation.");
            }
        }

        foreach (var fixture in context.Beliefs)
        {
            foreach (var issue in ResearchContractValidator.Validate(fixture.Belief))
            {
                issues.Add(issue);
            }

            if (!string.Equals(fixture.Belief.RunId, context.RunId, StringComparison.Ordinal) ||
                fixture.State.BeliefId != fixture.Belief.BeliefId ||
                fixture.Belief.ApplicabilityEpoch > context.ApplicabilityEpoch)
            {
                Add(issues, "run-mismatch", "Belief fixture is not a member of this memory context.");
            }
        }

        if (context.ActiveDirection is not null)
        {
            foreach (var issue in ResearchContractValidator.Validate(context.ActiveDirection))
            {
                issues.Add(issue);
            }

            if (context.ActiveDirection.RunId != context.RunId ||
                context.ActiveDirection.ApplicabilityEpoch != context.ApplicabilityEpoch)
            {
                Add(issues, "epoch-mismatch", "Active direction is outside the current run/epoch.");
            }
        }

        foreach (var direction in context.HistoricalDirections)
        {
            foreach (var issue in ResearchContractValidator.Validate(direction))
            {
                issues.Add(issue);
            }

            if (direction.RunId != context.RunId ||
                direction.ApplicabilityEpoch > context.ApplicabilityEpoch)
            {
                Add(issues, "run-mismatch", "Historical direction is outside the proposal context.");
            }
        }
    }

    private static IReadOnlyList<DefinitionCandidate> CollectDefinitions(
        ProposeMemoryUpdateSupervisorOutput proposal,
        MemoryProposalContext context,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        IReadOnlyList<MemoryBeliefFixture> terminal,
        ICollection<ResearchValidationIssue> issues)
    {
        var definitions = new List<DefinitionCandidate>();
        for (var index = 0; index < proposal.Operations.Count; index++)
        {
            switch (proposal.Operations[index])
            {
                case AddBeliefOperation add:
                    definitions.Add(new DefinitionCandidate(index, add.Key, add.Claim, null, true));
                    break;
                case ReplaceBeliefOperation replace:
                    definitions.Add(
                        new DefinitionCandidate(index, replace.Key, replace.Claim, null, true));
                    break;
                case ReassertBeliefOperation reassert:
                    definitions.Add(
                        new DefinitionCandidate(
                            index,
                            reassert.Key,
                            reassert.Claim,
                            reassert.PredecessorBeliefId,
                            ValidateReassertionPredecessor(
                                reassert,
                                context,
                                fixtures,
                                terminal,
                                issues)));
                    break;
            }
        }

        return definitions;
    }

    private static void BindDefinitions(
        IReadOnlyList<DefinitionCandidate> definitions,
        IReadOnlyDictionary<string, MemoryBeliefFixture> eligibleByClaimKey,
        IReadOnlySet<string> terminalClaimKeys,
        ICollection<NormalizationNote> normalization,
        ICollection<ProposalKeyBinding> keyBindings,
        IDictionary<string, ProposedBelief> created,
        ICollection<PlannedBeliefLink> reassertions,
        ICollection<ResearchValidationIssue> issues)
    {
        foreach (var group in definitions.GroupBy(
                     definition => ClaimKey(definition.Claim),
                     StringComparer.Ordinal))
        {
            var candidates = group.OrderBy(
                    definition => definition.Key,
                    StringComparer.Ordinal)
                .ToList();
            if (eligibleByClaimKey.TryGetValue(group.Key, out var existing))
            {
                foreach (var candidate in candidates)
                {
                    keyBindings.Add(
                        new ProposalKeyBinding(candidate.Key, existing.Belief.BeliefId));
                    normalization.Add(
                        Note(
                            candidate.OperationIndex,
                            "alias-existing-claim",
                            existing.Belief.BeliefId));
                }

                continue;
            }

            if (candidates.Any(candidate => !candidate.PredecessorValid))
            {
                continue;
            }

            var lineage = candidates
                .Select(candidate => candidate.PredecessorBeliefId)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (lineage.Count != 1)
            {
                Add(
                    issues,
                    "inconsistent-update",
                    "Equal proposed claims must have compatible reassertion lineage.");
                continue;
            }

            var predecessorId = lineage[0];
            if (predecessorId is null && terminalClaimKeys.Contains(group.Key))
            {
                Add(
                    issues,
                    "terminal-belief-lineage-required",
                    "A plain definition cannot recreate an exact terminal ClaimKey.");
                continue;
            }

            var representative = candidates[0];
            created.Add(
                representative.Key,
                new ProposedBelief(
                    representative.Key,
                    representative.Claim,
                    predecessorId));
            foreach (var candidate in candidates)
            {
                keyBindings.Add(
                    new ProposalKeyBinding(
                        candidate.Key,
                        ProposedReference(representative.Key)));
                normalization.Add(
                    candidate.Key == representative.Key
                        ? Note(candidate.OperationIndex, "unchanged")
                        : Note(
                            candidate.OperationIndex,
                            "coalesce-proposed-claim",
                            proposedKey: representative.Key));
            }

            if (predecessorId is not null)
            {
                reassertions.Add(
                    new PlannedBeliefLink(
                        predecessorId,
                        ProposedReference(representative.Key)));
            }
        }
    }

    private static void ResolveConflict(
        int operationIndex,
        DeclareConflictOperation conflict,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        IReadOnlyList<ProposalKeyBinding> keyBindings,
        IReadOnlySet<(string Left, string Right)> baseConflictEdges,
        ISet<(string Left, string Right)> addedConflicts,
        ICollection<NormalizationNote> normalization,
        ICollection<ResearchValidationIssue> issues)
    {
        var resolved = conflict.Beliefs
            .Select(reference => ResolveReference(reference, fixtures, keyBindings))
            .ToList();
        if (resolved.Any(item => item is null))
        {
            Add(issues, "invalid-conflict", "Conflict contains an unresolved belief.");
            return;
        }

        var distinct = resolved.Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (distinct.Count < 2)
        {
            Add(issues, "invalid-conflict", "Conflict endpoints must be distinct.");
            return;
        }

        var operationAddedEdge = false;
        for (var left = 0; left < distinct.Count; left++)
        {
            for (var right = left + 1; right < distinct.Count; right++)
            {
                var edge = (distinct[left], distinct[right]);
                if (!baseConflictEdges.Contains(edge))
                {
                    operationAddedEdge |= addedConflicts.Add(edge);
                }
            }
        }

        if (!operationAddedEdge)
        {
            normalization.Add(Note(operationIndex, "drop-existing-conflict"));
        }
    }

    private static void ValidateDirectionDependencies(
        MemoryProposalContext context,
        SetDirectionOperation? retainedDirection,
        bool directionCleared,
        IReadOnlyList<string> rawDirectionSupport,
        IReadOnlySet<string> currentIds,
        IReadOnlySet<string> contested,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        IReadOnlyList<ProposalKeyBinding> keyBindings,
        ICollection<ResearchValidationIssue> issues)
    {
        if (rawDirectionSupport.Any(reference =>
                !fixtures.ContainsKey(reference) &&
                !reference.StartsWith("proposed:", StringComparison.Ordinal)))
        {
            Add(issues, "invalid-direction-dependency", "Direction support is not in the transaction context.");
        }

        if (!directionCleared && retainedDirection is null && context.ActiveDirection is not null)
        {
            foreach (var supporter in context.ActiveDirection.SupportingBeliefIds)
            {
                if (!currentIds.Contains(supporter) || contested.Contains(supporter))
                {
                    Add(
                        issues,
                        "invalid-direction-dependency",
                        "The retained direction depends on an ineligible or contested belief.");
                }
            }
        }

        if (retainedDirection is null)
        {
            return;
        }

        foreach (var reference in retainedDirection.SupportingBeliefs)
        {
            var supporter = ResolveReference(reference, fixtures, keyBindings);
            if (supporter is null ||
                !currentIds.Contains(supporter) ||
                contested.Contains(supporter))
            {
                Add(
                    issues,
                    "invalid-direction-dependency",
                    "The proposed direction depends on an ineligible or contested belief.");
            }
        }
    }

    private static bool ValidateReassertionPredecessor(
        ReassertBeliefOperation operation,
        MemoryProposalContext context,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        IReadOnlyList<MemoryBeliefFixture> terminal,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!fixtures.TryGetValue(operation.PredecessorBeliefId, out var predecessor) ||
            predecessor.State.State is not (BeliefState.Superseded or
                BeliefState.Retracted or
                BeliefState.Invalidated) ||
            predecessor.Belief.ApplicabilityEpoch != context.ApplicabilityEpoch ||
            predecessor.Belief.Claim.TargetId != operation.Claim.TargetId ||
            predecessor.Belief.Claim.Hypothesis != operation.Claim.Hypothesis)
        {
            Add(issues, "invalid-reassertion-lineage", "Reassertion predecessor is not a matching terminal belief.");
            return false;
        }

        var latest = terminal
            .Where(item =>
                item.Belief.Claim.TargetId == operation.Claim.TargetId &&
                item.Belief.Claim.Hypothesis == operation.Claim.Hypothesis)
            .OrderByDescending(item => item.StateChangedAtMemoryRevision)
            .ThenByDescending(item => IdNumber(item.Belief.BeliefId))
            .FirstOrDefault();
        if (latest is null ||
            !string.Equals(
                latest.Belief.BeliefId,
                operation.PredecessorBeliefId,
                StringComparison.Ordinal))
        {
            Add(issues, "invalid-reassertion-lineage", "Reassertion must name the latest terminal predecessor.");
            return false;
        }

        return true;
    }

    private static string? ResolveReference(
        BeliefRef reference,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        IReadOnlyList<ProposalKeyBinding> bindings) =>
        reference switch
        {
            ExistingBeliefRef existing when fixtures.ContainsKey(existing.BeliefId) =>
                existing.BeliefId,
            ProposedBeliefRef proposed => ResolveBinding(proposed.Key, bindings),
            _ => null
        };

    private static string? ResolveBinding(
        string key,
        IReadOnlyList<ProposalKeyBinding> bindings) =>
        bindings.SingleOrDefault(
            item => string.Equals(item.Key, key, StringComparison.Ordinal))?.Reference;

    private static bool TryGetEligible(
        string beliefId,
        IReadOnlyDictionary<string, MemoryBeliefFixture> fixtures,
        uint epoch) =>
        fixtures.TryGetValue(beliefId, out var fixture) &&
        fixture.Belief.ApplicabilityEpoch == epoch &&
        fixture.State.State is BeliefState.Provisional or BeliefState.Contested;

    private static string ClaimKey(Claim claim) =>
        string.Join(
            "|",
            claim.TargetId,
            claim.Hypothesis,
            string.Join(",", claim.ObservationIds.OrderBy(IdNumber)),
            claim.Uncertainty);

    private static string DirectionKey(
        string runId,
        uint epoch,
        SetDirectionOperation operation) =>
        $"{runId}|{epoch}|" +
        $"{JsonSerializer.Serialize(operation.Recommendation, ResearchContractSerializer.Options)}|" +
        string.Join(",", operation.ObservationIds.OrderBy(IdNumber));

    private static string DirectionKey(Direction direction) =>
        $"{direction.RunId}|{direction.ApplicabilityEpoch}|" +
        $"{JsonSerializer.Serialize(direction.Recommendation, ResearchContractSerializer.Options)}|" +
        string.Join(",", direction.ObservationIds.OrderBy(IdNumber));

    private static (string Left, string Right) OrderedEdge(string left, string right) =>
        string.CompareOrdinal(left, right) < 0 ? (left, right) : (right, left);

    private static NormalizationNote Note(
        int operationIndex,
        string rule,
        string? existingBeliefId = null,
        string? proposedKey = null,
        string? duplicateDirectionId = null) =>
        new(
            checked((uint)operationIndex),
            rule,
            existingBeliefId,
            proposedKey,
            duplicateDirectionId);

    private static void ReplaceNote(
        IList<NormalizationNote> notes,
        NormalizationNote replacement)
    {
        for (var index = notes.Count - 1; index >= 0; index--)
        {
            if (notes[index].OperationIndex == replacement.OperationIndex)
            {
                notes.RemoveAt(index);
            }
        }

        notes.Add(replacement);
    }

    private static string ProposedReference(string key) => $"proposed:{key}";

    private static int IdNumber(string value)
    {
        var separator = value.LastIndexOf('-');
        return separator >= 0 &&
            int.TryParse(value[(separator + 1)..], out var result)
            ? result
            : 0;
    }

    private static bool IsValidRunId(string value) =>
        Guid.TryParseExact(value, "D", out _) &&
        string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal);

    private static void Add(
        ICollection<ResearchValidationIssue> issues,
        string code,
        string message) =>
        issues.Add(new ResearchValidationIssue(code, message));

    private static MemoryProposalValidationResult EmptyResult(
        IReadOnlyList<ResearchValidationIssue> issues) =>
        new(
            issues,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            false);

    private sealed record DefinitionCandidate(
        int OperationIndex,
        string Key,
        Claim Claim,
        string? PredecessorBeliefId,
        bool PredecessorValid);
}
