using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovernedAgent.Research;

public static partial class ResearchContractValidator
{
    private static readonly IReadOnlySet<string> ContractReasonCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "invalid-json", "duplicate-property", "unsupported-schema",
            "unknown-record-type", "unknown-field", "missing-field", "wrong-type",
            "unknown-enum", "invalid-identifier", "invalid-reference",
            "invalid-shape", "out-of-range", "missing-memory-disposition",
            "engineering-limit-exceeded"
        };

    private static readonly IReadOnlySet<string> ContextReasonCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "out-of-scope-operation", "out-of-scope-target",
            "observation-not-in-snapshot", "belief-not-in-snapshot",
            "trigger-mismatch", "run-mismatch", "obsolete-generation",
            "epoch-mismatch", "review-timed-out", "review-cancelled",
            "episode-ended", "duplicate-result", "conflicting-result-delivery",
            "horizon-reached", "actor-turn-budget-exhausted",
            "diagnostic-budget-exhausted", "review-budget-exhausted",
            "memory-base-mismatch", "memory-revision-conflict",
            "invalid-direction-dependency", "invalid-conflict",
            "inconsistent-update", "terminal-belief-lineage-required",
            "invalid-reassertion-lineage", "memory-update-interrupt",
            "epoch-changed", "wait-timer", "shared-notification",
            "wake-already-observed"
        };

    private static readonly IReadOnlySet<string> InfrastructureReasonCodes =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "actor-invocation-failed", "supervisor-invocation-failed",
            "invocation-cancelled", "cancellation-unsupported",
            "cancellation-request-failed", "diagnostic-execution-failed",
            "record-unavailable", "input-limit-exceeded",
            "record-limit-exceeded", "drain-incomplete"
        };

    public static void ValidateAndThrow(IResearchRoot root)
    {
        var issues = Validate(root);
        if (issues.Count == 0)
        {
            return;
        }

        var first = issues[0];
        throw new ResearchContractException(first.Code, first.Message);
    }

    public static IReadOnlyList<ResearchValidationIssue> Validate(IResearchRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var issues = new List<ResearchValidationIssue>();
        var nullIssue = ResearchObjectGraphValidator.FindExplicitNull(root);
        if (nullIssue is not null)
        {
            issues.Add(nullIssue);
            return issues;
        }

        ValidateRoot(root, issues);
        switch (root)
        {
            case ActorDecision value:
                ValidateActorDecision(value, issues);
                break;
            case SupervisorOutput value:
                ValidateSupervisorOutput(value, issues);
                break;
            case Observation value:
                ValidateObservation(value, issues);
                break;
            case Belief value:
                ValidateBelief(value, issues);
                break;
            case Direction value:
                ValidateDirection(value, issues);
                break;
            case WorkingMemorySnapshot value:
                ValidateWorkingMemory(value, issues);
                break;
            case ReconsiderationTrigger value:
                ValidateTrigger(value, issues);
                break;
            case InputSnapshot value:
                ValidateInputSnapshot(value, issues);
                break;
            case InvocationResult value:
                ValidateInvocationResult(value, issues);
                break;
            case MemoryUpdate value:
                ValidateMemoryUpdate(value, issues);
                break;
            case ResearchEvent value:
                ValidateResearchEvent(value, issues);
                break;
            case RunManifest value:
                ValidateManifest(value, issues);
                break;
            case Termination value:
                ValidateTermination(value, issues);
                break;
            case RunClosure value:
                ValidateClosure(value, issues);
                break;
        }

        return issues;
    }

    private static void ValidateRoot(
        IResearchRoot root,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!string.Equals(
                root.SchemaVersion,
                ResearchContractVersions.SchemaVersion,
                StringComparison.Ordinal))
        {
            Add(issues, "unsupported-schema", "schemaVersion is not the approved T1 value.");
        }

        var expected = root switch
        {
            ActorDecision => "actor-decision",
            SupervisorOutput => "supervisor-output",
            Observation => "observation",
            InputSnapshot => "input-snapshot",
            InvocationResult => "invocation-result",
            Belief => "belief",
            Direction => "direction",
            WorkingMemorySnapshot => "working-memory-snapshot",
            MemoryUpdate => "memory-update",
            ReconsiderationTrigger => "reconsideration-trigger",
            ResearchEvent => "research-event",
            RunManifest => "run-manifest",
            Termination => "termination",
            RunClosure => "run-closure",
            _ => null
        };

        if (expected is null ||
            !string.Equals(root.RecordType, expected, StringComparison.Ordinal))
        {
            Add(issues, "unknown-record-type", "recordType does not match the contract root.");
        }
    }

    private static void ValidateActorDecision(
        ActorDecision value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateIdSet(value.UsedBeliefIds, "belief-", "usedBeliefIds", issues);
        if (value.MemoryDisposition is not null)
        {
            ValidateIdentifier(value.MemoryDisposition.TriggerId, "trigger-", issues);
            ValidateIdentifier(value.MemoryDisposition.MemoryUpdateId, "memory-update-", issues);
        }

        switch (value)
        {
            case QueryActorDecision query:
                ValidateAllowedQuery(query.Operation, query.TargetId, query.Arguments, issues);
                break;
            case WaitActorDecision wait when wait.Ticks is < 1 or > 4:
                Add(issues, "out-of-range", "Wait ticks must be between 1 and 4.");
                break;
            case ReportActorDecision report:
                ValidateIdSet(
                    report.ObservationIds,
                    "observation-",
                    "observationIds",
                    issues);
                break;
        }
    }

    private static void ValidateSupervisorOutput(
        SupervisorOutput value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateIdSet(
            value.ObservationIds,
            "observation-",
            "observationIds",
            issues);
        ValidateOptionalText(value.Rationale, "rationale", issues);

        if (value is not ProposeMemoryUpdateSupervisorOutput proposal)
        {
            return;
        }

        if (proposal.Operations.Count is < 1 or > ResearchEngineeringLimits.MaximumOperationsPerUpdate)
        {
            Add(
                issues,
                "engineering-limit-exceeded",
                "A memory proposal must contain 1 through 16 operations.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in proposal.Operations)
        {
            ValidateMemoryOperation(operation, proposal.ObservationIds, keys, issues);
        }
    }

    private static void ValidateMemoryOperation(
        MemoryOperation operation,
        IReadOnlyList<string> proposalCitations,
        ISet<string> keys,
        ICollection<ResearchValidationIssue> issues)
    {
        switch (operation)
        {
            case AddBeliefOperation add:
                ValidateDefinedKey(add.Key, keys, issues);
                ValidateClaim(add.Claim, proposalCitations, issues);
                break;
            case ReassertBeliefOperation reassert:
                ValidateIdentifier(reassert.PredecessorBeliefId, "belief-", issues);
                ValidateDefinedKey(reassert.Key, keys, issues);
                ValidateClaim(reassert.Claim, proposalCitations, issues);
                ValidateText(reassert.Reason, "reason", issues);
                break;
            case ReplaceBeliefOperation replace:
                ValidateIdentifier(replace.BeliefId, "belief-", issues);
                ValidateDefinedKey(replace.Key, keys, issues);
                ValidateClaim(replace.Claim, proposalCitations, issues);
                break;
            case RetractBeliefOperation retract:
                ValidateIdentifier(retract.BeliefId, "belief-", issues);
                ValidateCitations(retract.ObservationIds, proposalCitations, issues);
                ValidateText(retract.Reason, "reason", issues);
                break;
            case DeclareConflictOperation conflict:
                if (conflict.Beliefs.Count < 2)
                {
                    Add(issues, "invalid-conflict", "A conflict needs at least two beliefs.");
                }

                ValidateCitations(conflict.ObservationIds, proposalCitations, issues);
                ValidateText(conflict.Reason, "reason", issues);
                break;
            case SetDirectionOperation direction:
                ValidateCitations(direction.ObservationIds, proposalCitations, issues);
                if (direction.Recommendation is FocusDirectionChoice focus &&
                    focus.Focus is DiagnosticFocus diagnostic)
                {
                    ValidateAllowedQuery(
                        diagnostic.Operation,
                        diagnostic.TargetId,
                        diagnostic.Arguments,
                        issues);
                }

                break;
            case ClearDirectionOperation clear:
                ValidateCitations(clear.ObservationIds, proposalCitations, issues);
                ValidateText(clear.Reason, "reason", issues);
                break;
        }
    }

    private static void ValidateObservation(
        Observation value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.ObservationId, "observation-", issues);
        ValidateTokenSet(value.EvidenceIds, "evidenceIds", issues);
        ValidateToken(value.SourceId, "sourceId", issues);

        if (value.SourceKind == "shared-notification")
        {
            if (value.DiagnosticId is not null ||
                value.Content is not NotificationObservationContent)
            {
                Add(issues, "invalid-shape", "Shared notifications require notification content.");
            }
        }
        else if (value.SourceKind == "diagnostic")
        {
            if (value.DiagnosticId is null)
            {
                Add(issues, "missing-field", "Diagnostic observations require diagnosticId.");
            }
            else
            {
                ValidateIdentifier(value.DiagnosticId, "diagnostic-", issues);
            }
        }
        else
        {
            Add(issues, "unknown-enum", "sourceKind is not supported.");
        }

        if (value.Visibility == "shared-history")
        {
            if (value.ObservedTick is null ||
                value.HistoryRevision is null ||
                value.ObservedTick < value.AvailableTick)
            {
                Add(issues, "invalid-shape", "Shared observations need valid observed metadata.");
            }
        }
        else if (value.Visibility == "post-termination")
        {
            if (value.ObservedTick is not null || value.HistoryRevision is not null)
            {
                Add(issues, "invalid-shape", "Post-termination observations cannot enter history.");
            }
        }
        else
        {
            Add(issues, "unknown-enum", "visibility is not supported.");
        }

        if (value.Content is UnavailableObservationContent unavailable &&
            (!string.Equals(unavailable.Reason, "not-yet-available", StringComparison.Ordinal) ||
             value.EvidenceIds.Count != 0))
        {
            Add(issues, "invalid-shape", "Unavailable observations have one reason and no evidence.");
        }
    }

    private static void ValidateBelief(
        Belief value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.BeliefId, "belief-", issues);
        ValidateIdentifier(value.CreatedByUpdateId, "memory-update-", issues);
        ValidateClaim(value.Claim, value.Claim.ObservationIds, issues);
        if (value.ReassertedFromBeliefId is not null)
        {
            ValidateIdentifier(value.ReassertedFromBeliefId, "belief-", issues);
            if (string.Equals(
                    value.BeliefId,
                    value.ReassertedFromBeliefId,
                    StringComparison.Ordinal))
            {
                Add(issues, "invalid-reassertion-lineage", "A belief cannot reassert itself.");
            }
        }
    }

    private static void ValidateDirection(
        Direction value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.DirectionId, "direction-", issues);
        ValidateIdentifier(value.CreatedByUpdateId, "memory-update-", issues);
        ValidateIdentifier(value.ReviewId, "review-", issues);
        ValidateIdSet(value.ObservationIds, "observation-", "observationIds", issues);
        ValidateIdSet(value.SupportingBeliefIds, "belief-", "supportingBeliefIds", issues);
        if (value.ReviewStartTick > uint.MaxValue - 8 ||
            value.ExpiresAtTick != value.ReviewStartTick + 8)
        {
            Add(
                issues,
                "out-of-range",
                "Direction expiry must equal reviewStartTick plus the approved eight-tick lifetime.");
        }
    }

    private static void ValidateWorkingMemory(
        WorkingMemorySnapshot value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        if (value.MemoryRevision == 0)
        {
            if (value.PreviousMemoryRevision is not null ||
                value.BeliefStates.Count != 0 ||
                value.RecommendedDirection is not null)
            {
                Add(issues, "inconsistent-update", "Memory revision zero must be empty.");
            }
        }
        else if (value.PreviousMemoryRevision != value.MemoryRevision - 1)
        {
            Add(issues, "inconsistent-update", "Memory revisions must be contiguous.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in value.BeliefStates)
        {
            ValidateIdentifier(entry.BeliefId, "belief-", issues);
            ValidateIdentifier(entry.StateChangedByUpdateId, "memory-update-", issues);
            if (!ids.Add(entry.BeliefId))
            {
                Add(issues, "invalid-reference", "Belief state IDs must be unique.");
            }

            ValidateIdSet(
                entry.ConflictingBeliefIds,
                "belief-",
                "conflictingBeliefIds",
                issues);
            var isSuperseded = entry.State == BeliefState.Superseded;
            if (isSuperseded != (entry.SupersededByBeliefId is not null))
            {
                Add(issues, "inconsistent-update", "Only superseded beliefs name a successor.");
            }
            else if (entry.SupersededByBeliefId is not null)
            {
                ValidateIdentifier(entry.SupersededByBeliefId, "belief-", issues);
            }
        }

        foreach (var entry in value.BeliefStates)
        {
            foreach (var conflictId in entry.ConflictingBeliefIds)
            {
                if (string.Equals(conflictId, entry.BeliefId, StringComparison.Ordinal))
                {
                    Add(issues, "invalid-conflict", "Conflict edges cannot be self-links.");
                    continue;
                }

                var peer = value.BeliefStates.FirstOrDefault(
                    candidate => string.Equals(
                        candidate.BeliefId,
                        conflictId,
                        StringComparison.Ordinal));
                if (peer is null ||
                    !peer.ConflictingBeliefIds.Contains(
                        entry.BeliefId,
                        StringComparer.Ordinal) ||
                    entry.State is not (BeliefState.Provisional or BeliefState.Contested) ||
                    peer.State is not (BeliefState.Provisional or BeliefState.Contested))
                {
                    Add(issues, "invalid-conflict", "Conflict edges must be symmetric and eligible.");
                }
            }

            var expectedState = entry.ConflictingBeliefIds.Count == 0
                ? BeliefState.Provisional
                : BeliefState.Contested;
            if (entry.State is BeliefState.Provisional or BeliefState.Contested &&
                entry.State != expectedState)
            {
                Add(issues, "invalid-conflict", "Eligible belief state must match conflict degree.");
            }
        }

        if (value.RecommendedDirection is not null)
        {
            AddNested(issues, Validate(value.RecommendedDirection));
        }

        if (value.RecommendedDirection is not null &&
            (!string.Equals(value.RecommendedDirection.RunId, value.RunId, StringComparison.Ordinal) ||
             value.RecommendedDirection.ApplicabilityEpoch != value.ApplicabilityEpoch))
        {
            Add(issues, "epoch-mismatch", "Active direction must match memory run and epoch.");
        }
        else if (value.RecommendedDirection is not null)
        {
            foreach (var supportingBeliefId in value.RecommendedDirection.SupportingBeliefIds)
            {
                var supportingEntry = value.BeliefStates.FirstOrDefault(
                    entry => string.Equals(
                        entry.BeliefId,
                        supportingBeliefId,
                        StringComparison.Ordinal));
                if (supportingEntry is null ||
                    supportingEntry.State != BeliefState.Provisional)
                {
                    Add(
                        issues,
                        "invalid-direction-dependency",
                        "Active direction support must reference current provisional beliefs.");
                }
            }
        }
    }

    private static void ValidateTrigger(
        ReconsiderationTrigger value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.TriggerId, "trigger-", issues);
        ValidateIdentifier(value.MemoryUpdateId, "memory-update-", issues);
        if (value.CommittedMemoryRevision == 0 || value.DecisionGeneration == 0)
        {
            Add(issues, "out-of-range", "A reconsideration trigger requires positive revisions.");
        }

        if (value.PreviousTriggerId is not null)
        {
            ValidateIdentifier(value.PreviousTriggerId, "trigger-", issues);
        }
    }

    private static void ValidateInputSnapshot(
        InputSnapshot value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.SnapshotId, "snapshot-", issues);
        ValidateText(value.Scope.Goal, "scope.goal", issues);
        if (value.Scope.IncidentId != "INC-1042" ||
            value.Scope.ServiceId != "payments-api")
        {
            Add(issues, "out-of-scope-target", "Snapshot scope is outside the approved incident.");
        }

        var actor = string.Equals(value.Consumer, "actor", StringComparison.Ordinal);
        var supervisor = string.Equals(value.Consumer, "supervisor", StringComparison.Ordinal);
        if (!actor && !supervisor)
        {
            Add(issues, "unknown-enum", "Snapshot consumer is not supported.");
        }

        if (actor != (value.ActorTurnId is not null) ||
            supervisor != (value.ReviewId is not null))
        {
            Add(issues, "invalid-shape", "Exactly the consumer invocation ID must be present.");
        }

        if (value.ActorTurnId is not null)
        {
            ValidateIdentifier(value.ActorTurnId, "actor-", issues);
        }

        if (value.ReviewId is not null)
        {
            ValidateIdentifier(value.ReviewId, "review-", issues);
        }

        if (!string.Equals(value.WorkingMemory.RunId, value.RunId, StringComparison.Ordinal) ||
            value.WorkingMemory.MemoryRevision != value.MemoryRevision ||
            value.WorkingMemory.ApplicabilityEpoch != value.ApplicabilityEpoch)
        {
            Add(issues, "inconsistent-update", "Snapshot memory coordinates do not match.");
        }
        AddNested(issues, Validate(value.WorkingMemory));

        if (supervisor && value.Reconsideration is not null)
        {
            Add(issues, "invalid-shape", "Supervisor snapshots cannot require reconsideration.");
        }

        foreach (var query in value.AllowedQueries)
        {
            ValidateAllowedQuery(query.Operation, query.TargetId, query.Arguments, issues);
        }

        var expectedRevision = 1u;
        foreach (var observation in value.Observations)
        {
            AddNested(issues, Validate(observation));
            if (!string.Equals(observation.RunId, value.RunId, StringComparison.Ordinal) ||
                observation.Visibility != "shared-history" ||
                observation.HistoryRevision != expectedRevision ||
                observation.ObservedTick > value.Tick)
            {
                Add(issues, "invalid-reference", "Snapshot observations must be a contiguous history.");
                break;
            }

            expectedRevision++;
        }

        if (value.Observations.Count != value.HistoryRevision)
        {
            Add(issues, "invalid-reference", "Snapshot history revision must match observations.");
        }
        var materializedObservationIds = value.Observations
            .Select(observation => observation.ObservationId)
            .ToHashSet(StringComparer.Ordinal);

        var materializedBeliefIds = value.Beliefs
            .Select(belief => belief.BeliefId)
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach (var belief in value.Beliefs)
        {
            AddNested(issues, Validate(belief));
        }

        var stateBeliefIds = value.WorkingMemory.BeliefStates
            .Select(entry => entry.BeliefId)
            .Order(StringComparer.Ordinal)
            .ToList();
        if (!materializedBeliefIds.SequenceEqual(stateBeliefIds, StringComparer.Ordinal) ||
            value.Beliefs.Any(belief =>
                !string.Equals(belief.RunId, value.RunId, StringComparison.Ordinal)))
        {
            Add(issues, "invalid-reference", "Snapshot beliefs must exactly materialize working memory.");
        }

        foreach (var belief in value.Beliefs)
        {
            var state = value.WorkingMemory.BeliefStates.FirstOrDefault(
                entry => string.Equals(entry.BeliefId, belief.BeliefId, StringComparison.Ordinal));
            if (state is null)
            {
                continue;
            }

            var eligible = state.State is BeliefState.Provisional or BeliefState.Contested;
            if (belief.ApplicabilityEpoch > value.ApplicabilityEpoch ||
                (eligible && belief.ApplicabilityEpoch != value.ApplicabilityEpoch))
            {
                Add(
                    issues,
                    "epoch-mismatch",
                    "Only current-epoch beliefs may remain eligible; terminal history may retain older provenance.");
            }
        }

        if (value.WorkingMemory.RecommendedDirection is not null)
        {
            if (value.Tick >= value.WorkingMemory.RecommendedDirection.ExpiresAtTick)
            {
                Add(
                    issues,
                    "inconsistent-update",
                    "An expired direction cannot occupy the active recommendation slot at capture.");
            }
        }

        foreach (var item in value.ActionHistory)
        {
            ValidateIdentifier(item.ActorTurnId, "actor-", issues);
            ValidateIdentifier(item.SnapshotId, "snapshot-", issues);
            ValidateIdentifier(item.ResultId, "result-", issues);
            if (item.ConsumedMemoryRevision > value.MemoryRevision)
            {
                Add(issues, "invalid-reference", "Action history cannot consume a future memory revision.");
            }

            if (item.Decision is { } decision &&
                !TryValidateEmbeddedRoot<ActorDecision>(decision, issues))
            {
                Add(issues, "invalid-shape", "Action history decision must be an actor-decision root.");
            }

            if (item.Disposition == "applied")
            {
                if (item.Decision is null || item.Reason is not null)
                {
                    Add(issues, "invalid-shape", "Applied action history requires a decision and no reason.");
                }
            }
            else if (item.Disposition is "rejected" or "suppressed")
            {
                if (item.Reason is null)
                {
                    Add(issues, "invalid-shape", "Rejected/suppressed history requires a reason.");
                }
            }
            else
            {
                Add(issues, "unknown-enum", "Action history disposition is not supported.");
            }

            if (item.Reason is not null)
            {
                ValidateReason(item.Reason, issues);
            }

            if (item.DiagnosticId is not null)
            {
                ValidateIdentifier(item.DiagnosticId, "diagnostic-", issues);
            }

            ValidateIdSet(item.ObservationIds, "observation-", "observationIds", issues);
            if (item.ObservationIds.Any(
                    id => !materializedObservationIds.Contains(id)))
            {
                Add(issues, "observation-not-in-snapshot", "Action history cites an absent observation.");
            }
        }

        foreach (var item in value.ReviewHistory)
        {
            ValidateIdentifier(item.ReviewId, "review-", issues);
            ValidateIdentifier(item.SnapshotId, "snapshot-", issues);
            if (item.ResultId is not null)
            {
                ValidateIdentifier(item.ResultId, "result-", issues);
            }

            if (item.Output is { } output &&
                !TryValidateEmbeddedRoot<SupervisorOutput>(output, issues))
            {
                Add(issues, "invalid-shape", "Review history output must be a supervisor-output root.");
            }

            if (item.MemoryUpdateId is not null)
            {
                ValidateIdentifier(item.MemoryUpdateId, "memory-update-", issues);
            }

            if (item.Status == "completed")
            {
                if ((item.Output is null) == (item.Reason is null))
                {
                    Add(
                        issues,
                        "invalid-shape",
                        "Completed review history requires either valid output or a rejection reason.");
                }
            }
            else if (item.Status is "timed-out" or "failed" or "cancelled")
            {
                if (item.Output is not null ||
                    item.MemoryUpdateId is not null ||
                    item.Reason is null)
                {
                    Add(issues, "invalid-shape", "Non-completed review history fields are inconsistent.");
                }
            }
            else
            {
                Add(issues, "unknown-enum", "Review history status is not supported.");
            }

            if (item.Reason is not null)
            {
                ValidateReason(item.Reason, issues);
            }
        }

        if (value.Reconsideration is not null)
        {
            var reconsideration = value.Reconsideration;
            AddNested(issues, Validate(reconsideration.Trigger));
            if (!actor ||
                !string.Equals(reconsideration.Trigger.RunId, value.RunId, StringComparison.Ordinal) ||
                reconsideration.Trigger.CommittedMemoryRevision > value.MemoryRevision ||
                reconsideration.Trigger.DecisionGeneration != value.DecisionGeneration ||
                reconsideration.Trigger.CreatedTick > value.Tick ||
                value.DecisionGeneration == 0)
            {
                Add(issues, "trigger-mismatch", "Snapshot reconsideration is not bound to current coordinates.");
            }

            ValidateIdSet(
                reconsideration.EarlierTriggerIds,
                "trigger-",
                "earlierTriggerIds",
                issues);
            foreach (var effect in reconsideration.EffectsAtCapture)
            {
                switch (effect)
                {
                    case BeliefTriggerEffectView beliefEffect:
                        var state = value.WorkingMemory.BeliefStates.FirstOrDefault(
                            entry => entry.BeliefId == beliefEffect.BeliefId);
                        if (state is null || state.State != beliefEffect.State)
                        {
                            Add(issues, "trigger-mismatch", "Belief trigger effect does not match memory.");
                        }

                        break;
                    case DirectionTriggerEffectView directionEffect:
                        var activeDirection = value.WorkingMemory.RecommendedDirection;
                        if (directionEffect.State == DirectionState.Active)
                        {
                            if (activeDirection?.DirectionId != directionEffect.DirectionId)
                            {
                                Add(issues, "trigger-mismatch", "Active direction effect is not current.");
                            }
                        }
                        else if (activeDirection?.DirectionId == directionEffect.DirectionId)
                        {
                            Add(issues, "trigger-mismatch", "Terminal direction effect is still active.");
                        }

                        break;
                }
            }
        }
    }

    private static void ValidateInvocationResult(
        InvocationResult value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.ResultId, "result-", issues);
        ValidateIdentifier(value.SnapshotId, "snapshot-", issues);
        var actor = string.Equals(value.Consumer, "actor", StringComparison.Ordinal);
        var supervisor = string.Equals(value.Consumer, "supervisor", StringComparison.Ordinal);
        if (!actor && !supervisor)
        {
            Add(issues, "unknown-enum", "Result consumer is not supported.");
        }

        if (actor != (value.ActorTurnId is not null) ||
            supervisor != (value.ReviewId is not null))
        {
            Add(issues, "invalid-shape", "Result consumer binding is inconsistent.");
        }
        if (value.ActorTurnId is not null)
        {
            ValidateIdentifier(value.ActorTurnId, "actor-", issues);
        }

        if (value.ReviewId is not null)
        {
            ValidateIdentifier(value.ReviewId, "review-", issues);
        }

        if (value.ParseStatus is not ("well-formed" or "invalid" or "not-attempted"))
        {
            Add(issues, "unknown-enum", "Result parseStatus is not supported.");
        }

        ValidateOptionalText(value.RedactedOutput, "redactedOutput", issues);

        if (value.Settlement == "returned")
        {
            if (value.RawOutputSha256 is null)
            {
                Add(issues, "missing-field", "Returned results require rawOutputSha256.");
            }
            else
            {
                ValidateSha256(value.RawOutputSha256, issues);
            }

            if (value.ParseStatus == "well-formed" && value.Output is null)
            {
                Add(issues, "invalid-shape", "Well-formed returns require typed output.");
            }
            else if (value.ParseStatus == "well-formed" && value.Output is { } output)
            {
                try
                {
                    var parsed = ResearchContractSerializer.DeserializeRoot(output.GetRawText());
                    if ((actor && parsed is not ActorDecision) ||
                        (supervisor && parsed is not SupervisorOutput))
                    {
                        Add(issues, "invalid-shape", "Typed output does not match its consumer.");
                    }
                }
                catch (ResearchContractException)
                {
                    Add(issues, "invalid-shape", "Typed invocation output is not a valid contract root.");
                }
            }

            if (value.ParseStatus == "well-formed" && value.Reason is not null)
            {
                Add(issues, "invalid-shape", "Well-formed returns cannot carry a rejection reason.");
            }
            else if (value.ParseStatus == "invalid" &&
                (value.Output is not null || value.Reason is null))
            {
                Add(issues, "invalid-shape", "Invalid returns require null output and a reason.");
            }
            else if (value.ParseStatus == "not-attempted" &&
                (value.Output is not null || value.Reason is null))
            {
                Add(issues, "invalid-shape", "Unparsed returns require null output and a suppression reason.");
            }
        }
        else if (value.Settlement is "failed" or "cancelled")
        {
            if (value.Output is not null ||
                value.RawOutputSha256 is not null ||
                value.RedactedOutput is not null ||
                value.ParseStatus != "not-attempted" ||
                value.Reason is null)
            {
                Add(issues, "invalid-shape", "Non-return settlement cannot contain output.");
            }
        }
        else
        {
            Add(issues, "unknown-enum", "Result settlement is not supported.");
        }

        if (value.Reason is not null)
        {
            ValidateReason(value.Reason, issues);
        }
    }

    private static void ValidateMemoryUpdate(
        MemoryUpdate value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        ValidateIdentifier(value.MemoryUpdateId, "memory-update-", issues);
        if (value.TriggerId is not null)
        {
            ValidateIdentifier(value.TriggerId, "trigger-", issues);
        }

        switch (value.Outcome)
        {
            case "committed":
                if (value.CommittedMemoryRevision != value.BaseMemoryRevision + 1 ||
                    value.BaseMemoryRevision != value.ObservedCurrentMemoryRevision ||
                    value.Effects is null ||
                    value.Errors.Count != 0)
                {
                    Add(issues, "inconsistent-update", "Committed update fields are inconsistent.");
                }

                ValidateCommittedOrigin(value, issues);

                break;
            case "rejected":
                if (value.CommittedMemoryRevision is not null ||
                    value.Effects is not null ||
                    value.TriggerId is not null ||
                    value.Actionable ||
                    value.NewGeneration != value.PreviousGeneration ||
                    value.Errors.Count == 0 ||
                    value.KeyBindings.Count != 0)
                {
                    Add(issues, "inconsistent-update", "Rejected update fields are inconsistent.");
                }

                break;
            case "no-op":
                if (value.CommittedMemoryRevision is not null ||
                    value.Effects is not null ||
                    value.TriggerId is not null ||
                    value.Actionable ||
                    value.NewGeneration != value.PreviousGeneration ||
                    value.Errors.Count != 0)
                {
                    Add(issues, "inconsistent-update", "No-op update fields are inconsistent.");
                }

                break;
            default:
                Add(issues, "unknown-enum", "Memory update outcome is not supported.");
                break;
        }

        if (value.Outcome is "rejected" or "no-op" &&
            value.Origin is not SupervisorUpdateOrigin)
        {
            Add(issues, "inconsistent-update", "System-origin updates must commit or not exist.");
        }

        switch (value.Origin)
        {
            case SupervisorUpdateOrigin origin:
                ValidateIdentifier(origin.ReviewId, "review-", issues);
                ValidateIdentifier(origin.ResultId, "result-", issues);
                ValidateIdentifier(origin.SnapshotId, "snapshot-", issues);
                if (value.Proposal is null)
                {
                    Add(issues, "inconsistent-update", "Supervisor updates require the typed proposal.");
                }
                else if (value.Proposal.BaseMemoryRevision != value.BaseMemoryRevision)
                {
                    Add(issues, "memory-base-mismatch", "Stored proposal base does not match the update.");
                }
                else
                {
                    AddNested(issues, Validate(value.Proposal));
                    if (value.Proposal is NoChangeSupervisorOutput)
                    {
                        Add(
                            issues,
                            "inconsistent-update",
                            "No-change supervisor output cannot become a memory update.");
                    }
                }

                break;
            case DirectionExpiryUpdateOrigin origin:
                ValidateIdentifier(origin.DirectionId, "direction-", issues);
                if (value.Proposal is not null ||
                    value.Normalization.Count != 0 ||
                    value.KeyBindings.Count != 0)
                {
                    Add(issues, "inconsistent-update", "Direction expiry cannot contain a component proposal.");
                }

                break;
            case EpochInvalidationUpdateOrigin origin:
                if (origin.CausedBySequence == 0 ||
                    value.Proposal is not null ||
                    value.Normalization.Count != 0 ||
                    value.KeyBindings.Count != 0)
                {
                    Add(issues, "inconsistent-update", "Epoch invalidation origin fields are inconsistent.");
                }

                break;
        }

        foreach (var error in value.Errors)
        {
            ValidateReason(error, issues);
        }

        if (value.Effects is not null)
        {
            ValidateMemoryEffects(value.Effects, value.TriggerId, issues);
        }
    }

    private static void ValidateMemoryEffects(
        MemoryEffects effects,
        string? triggerId,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateIdSet(effects.CreatedBeliefIds, "belief-", "createdBeliefIds", issues);
        ValidateIdSet(effects.RetractedBeliefIds, "belief-", "retractedBeliefIds", issues);
        ValidateIdSet(effects.InvalidatedBeliefIds, "belief-", "invalidatedBeliefIds", issues);
        foreach (var link in effects.Supersessions.Concat(effects.Reassertions))
        {
            ValidateIdentifier(link.FromBeliefId, "belief-", issues);
            ValidateIdentifier(link.ToBeliefId, "belief-", issues);
            if (string.Equals(link.FromBeliefId, link.ToBeliefId, StringComparison.Ordinal))
            {
                Add(issues, "invalid-reference", "Belief lineage cannot be self-referential.");
            }
        }

        foreach (var reassertion in effects.Reassertions)
        {
            if (!effects.CreatedBeliefIds.Contains(
                    reassertion.ToBeliefId,
                    StringComparer.Ordinal) ||
                triggerId is null)
            {
                Add(issues, "invalid-reassertion-lineage", "Reassertion effects require a new belief and trigger.");
            }
        }

        foreach (var edge in effects.AddedConflicts.Concat(effects.RemovedConflicts))
        {
            ValidateIdentifier(edge.Left, "belief-", issues);
            ValidateIdentifier(edge.Right, "belief-", issues);
            if (string.CompareOrdinal(edge.Left, edge.Right) >= 0)
            {
                Add(issues, "invalid-conflict", "Conflict effects must be ordered distinct pairs.");
            }
        }

        if (effects.DirectionBeforeId is not null)
        {
            ValidateIdentifier(effects.DirectionBeforeId, "direction-", issues);
        }

        if (effects.DirectionAfterId is not null)
        {
            ValidateIdentifier(effects.DirectionAfterId, "direction-", issues);
        }
    }

    private static void ValidateCommittedOrigin(
        MemoryUpdate value,
        ICollection<ResearchValidationIssue> issues)
    {
        if (value.Effects is null)
        {
            return;
        }

        switch (value.Origin)
        {
            case SupervisorUpdateOrigin:
                if (!value.Actionable ||
                    value.NewGeneration != value.PreviousGeneration + 1 ||
                    value.TriggerId is null ||
                    value.Proposal is not ProposeMemoryUpdateSupervisorOutput ||
                    !HasMemoryEffect(value.Effects))
                {
                    Add(
                        issues,
                        "inconsistent-update",
                        "Meaningful supervisor commits require a valid proposal, a real effect, one generation increment and a trigger.");
                }

                break;
            case DirectionExpiryUpdateOrigin origin:
                if (value.Actionable ||
                    value.NewGeneration != value.PreviousGeneration ||
                    value.TriggerId is not null ||
                    value.Effects.DirectionBeforeId != origin.DirectionId ||
                    value.Effects.DirectionAfterId is not null ||
                    value.Effects.DirectionEndState != DirectionState.Expired ||
                    HasBeliefMutation(value.Effects))
                {
                    Add(
                        issues,
                        "inconsistent-update",
                        "Direction-expiry commits preserve generation and only expire their direction.");
                }

                break;
            case EpochInvalidationUpdateOrigin:
                var hasActionableInvalidation =
                    value.Effects.InvalidatedBeliefIds.Count != 0 ||
                    value.Effects.DirectionEndState == DirectionState.Invalidated;
                if (value.Actionable != hasActionableInvalidation ||
                    value.NewGeneration != value.PreviousGeneration +
                        (hasActionableInvalidation ? 1u : 0u) ||
                    (value.TriggerId is not null) != hasActionableInvalidation ||
                    value.Effects.CreatedBeliefIds.Count != 0 ||
                    value.Effects.Supersessions.Count != 0 ||
                    value.Effects.Reassertions.Count != 0 ||
                    value.Effects.RetractedBeliefIds.Count != 0 ||
                    value.Effects.AddedConflicts.Count != 0)
                {
                    Add(
                        issues,
                        "inconsistent-update",
                        "Epoch invalidation actionability must match its actor-visible invalidations.");
                }

                break;
        }
    }

    private static bool HasBeliefMutation(MemoryEffects effects) =>
        effects.CreatedBeliefIds.Count != 0 ||
        effects.Supersessions.Count != 0 ||
        effects.Reassertions.Count != 0 ||
        effects.RetractedBeliefIds.Count != 0 ||
        effects.InvalidatedBeliefIds.Count != 0 ||
        effects.AddedConflicts.Count != 0 ||
        effects.RemovedConflicts.Count != 0;

    private static bool HasMemoryEffect(MemoryEffects effects) =>
        HasBeliefMutation(effects) ||
        !string.Equals(
            effects.DirectionBeforeId,
            effects.DirectionAfterId,
            StringComparison.Ordinal) ||
        effects.DirectionEndState is not null;

    private static void ValidateResearchEvent(
        ResearchEvent value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        if (value.Sequence == 0)
        {
            Add(issues, "out-of-range", "Research event sequence must be positive.");
        }

        ValidateSortedUnique(value.CausedBySequences, "causedBySequences", issues);
        if (value.CausedBySequences.Any(sequence => sequence >= value.Sequence))
        {
            Add(issues, "invalid-reference", "Event causes must be earlier sequences.");
        }

        if (value.Tick is null && value.Phase is not (ResearchPhase.Setup or ResearchPhase.Drain))
        {
            Add(issues, "invalid-shape", "Null event ticks are limited to setup and drain.");
        }

        ValidateEventData(value.EventType, value.Data, issues);
    }

    private static void ValidateManifest(
        RunManifest value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        if (value.StudySpecification != new StudySpecificationRef(
                ResearchContractVersions.StudyBaseline,
                ResearchContractVersions.StudyAmendment,
                ResearchContractVersions.StudyCandidate) ||
            value.ContractCandidate != ResearchContractVersions.ContractBaseline)
        {
            Add(issues, "unsupported-schema", "Manifest does not identify the approved baseline.");
        }

        if (value.Coordination != "isolated-invocations-single-coordinator" ||
            value.Treatment != "supervision-plus-within-incident-memory-adaptation" ||
            value.ExecutionMode != "scripted" ||
            value.DataMode != "synthetic" ||
            value.ModelMeasurements != "unavailable-scripted")
        {
            Add(issues, "invalid-shape", "Manifest contains an unsupported T1 mode.");
        }

        if (value.Architecture is not (
                "actor-only" or
                "blocking-supervision" or
                "asynchronous-supervision"))
        {
            Add(issues, "unknown-enum", "Manifest architecture is not supported.");
        }

        if ((value.Architecture == "actor-only") != (value.SupervisorConfig is null))
        {
            Add(issues, "invalid-shape", "Only actor-only manifests omit supervisorConfig.");
        }

        if (!Sha40Regex().IsMatch(value.SourceRevision))
        {
            Add(issues, "invalid-identifier", "sourceRevision must be a 40-hex commit.");
        }

        ValidateDefinition(value.ActorConfig, issues);
        if (value.SupervisorConfig is not null)
        {
            ValidateDefinition(value.SupervisorConfig, issues);
        }

        ValidateDefinition(value.CaseDefinition, issues);
        ValidateDefinition(value.ExternalSchedule, issues);
        ValidateDefinition(value.CatalogueConfig, issues);
        ValidateDefinition(value.GovernanceConfig, issues);
        ValidateDefinition(value.MemoryRules, issues);
        if (value.FaultInjectionConfig is not null)
        {
            ValidateDefinition(value.FaultInjectionConfig, issues);
        }

        foreach (var sourceFile in value.SourceFiles)
        {
            ValidateText(sourceFile.Path, "sourceFiles.path", issues);
            ValidateSha256(sourceFile.Sha256, issues);
            if (Path.IsPathRooted(sourceFile.Path) ||
                sourceFile.Path.Split(
                    ['/', '\\'],
                    StringSplitOptions.RemoveEmptyEntries).Contains(
                        "..",
                        StringComparer.Ordinal))
            {
                Add(issues, "invalid-identifier", "Source file paths must be repository-relative.");
            }
        }

        if (value.Clock.Kind != "logical-ticks" ||
            value.Clock.StartTick != 0 ||
            value.Clock.EndTickExclusive != 24 ||
            value.Clock.ActorTurnTicks != 1 ||
            value.Clock.DiagnosticTicks != 0 ||
            value.Clock.ReviewTicks != 3 ||
            !value.Clock.ReviewCheckpoints.SequenceEqual([0u, 4u, 8u, 12u, 16u, 20u]) ||
            value.Clock.ReviewTimeoutTicks != 6 ||
            value.Clock.GuidanceLifetimeTicks != 8 ||
            value.Clock.MaximumWaitTicks != 4)
        {
            Add(issues, "invalid-shape", "Manifest clock differs from the approved scripted clock.");
        }

        if (value.Limits != new ResearchLimits(24, 12, 6, 1, 1))
        {
            Add(issues, "invalid-shape", "Manifest research limits differ from the approved values.");
        }

        var limits = value.EngineeringLimits;
        if (limits.MaximumComponentOutputBytes != ResearchEngineeringLimits.MaximumComponentOutputBytes ||
            limits.MaximumOperationsPerUpdate != ResearchEngineeringLimits.MaximumOperationsPerUpdate ||
            limits.MaximumComponentTextBytes != ResearchEngineeringLimits.MaximumComponentTextBytes ||
            limits.MaximumCitationsPerField != ResearchEngineeringLimits.MaximumCitationsPerField ||
            limits.MaximumInputSnapshotBytes != ResearchEngineeringLimits.MaximumInputSnapshotBytes ||
            limits.MaximumEventBytes != ResearchEngineeringLimits.MaximumEventBytes)
        {
            Add(issues, "engineering-limit-exceeded", "Manifest limits differ from the approved values.");
        }
    }

    private static void ValidateTermination(
        Termination value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        if (value.FinalPendingTriggerId is not null)
        {
            ValidateIdentifier(value.FinalPendingTriggerId, "trigger-", issues);
        }

        foreach (var invocation in value.PendingInvocations)
        {
            ValidateInvocationRef(invocation, issues);
        }
        ValidateIdSet(
            value.PendingDiagnosticIds,
            "diagnostic-",
            "pendingDiagnosticIds",
            issues);
        if (value.Counts.ReportsSubmitted > 1)
        {
            Add(issues, "out-of-range", "At most one report may be submitted.");
        }

        if (value.Kind == "report")
        {
            if (value.ReportResultId is null ||
                value.Reason is not null ||
                value.Tick >= 24 ||
                value.Phase != ResearchPhase.ActorProcessing ||
                value.FinalPendingTriggerId is not null ||
                value.Counts.ReportsSubmitted != 1)
            {
                Add(issues, "invalid-shape", "Report termination fields are inconsistent.");
            }
            else
            {
                ValidateIdentifier(value.ReportResultId, "result-", issues);
            }
        }
        else if (value.Kind is not (
                     "timeout" or
                     "budget-exhausted" or
                     "governance-stop" or
                     "infrastructure-failure"))
        {
            Add(issues, "unknown-enum", "Termination kind is not supported.");
        }
        else if (value.ReportResultId is not null || value.Reason is null)
        {
            Add(issues, "invalid-shape", "Non-report termination requires a reason.");
        }
        else
        {
            ValidateReason(value.Reason, issues);
            if (value.Kind == "timeout" &&
                (value.Tick != 24 ||
                    value.Phase != ResearchPhase.EndCheck ||
                    value.Reason.Domain != ReasonDomain.Context ||
                    value.Reason.Code != "horizon-reached"))
            {
                Add(
                    issues,
                    "invalid-shape",
                    "Timeout termination must occur at the end-check horizon.");
            }
        }
    }

    private static void ValidateClosure(
        RunClosure value,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateRunId(value.RunId, issues);
        if (value.ModelMeasurements != "unavailable-scripted")
        {
            Add(issues, "invalid-shape", "Scripted closure cannot claim model measurements.");
        }

        if (value.Status == "complete")
        {
            if (value.UnsettledInvocations.Count != 0 ||
                value.UnsettledDiagnosticIds.Count != 0 ||
                value.Reason is not null)
            {
                Add(issues, "invalid-shape", "Complete closure cannot retain unsettled work.");
            }
        }
        else if (value.Status != "incomplete" || value.Reason is null)
        {
            Add(issues, "invalid-shape", "Incomplete closure requires a reason.");
        }
    }

    private static void ValidateClaim(
        Claim claim,
        IReadOnlyList<string> proposalCitations,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateCitations(claim.ObservationIds, proposalCitations, issues);
    }

    private static void ValidateDefinition(
        DefinitionRef definition,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateToken(definition.Id, "definition.id", issues);
        ValidateSha256(definition.Sha256, issues);
    }

    private static void ValidateCitations(
        IReadOnlyList<string> citations,
        IReadOnlyList<string> allowed,
        ICollection<ResearchValidationIssue> issues)
    {
        if (citations.Count == 0)
        {
            Add(issues, "missing-field", "Actionable operations require observation citations.");
            return;
        }

        ValidateIdSet(citations, "observation-", "observationIds", issues);
        foreach (var citation in citations)
        {
            if (!allowed.Contains(citation, StringComparer.Ordinal))
            {
                Add(issues, "observation-not-in-snapshot", "Operation citation is absent from proposal citations.");
            }
        }
    }

    private static void ValidateAllowedQuery(
        ResearchOperation operation,
        TargetId target,
        IReadOnlyDictionary<string, JsonElement> arguments,
        ICollection<ResearchValidationIssue> issues)
    {
        if (arguments.Count != 0)
        {
            Add(issues, "invalid-shape", "Research query arguments must be empty.");
        }

        var allowed = operation switch
        {
            ResearchOperation.GetIncident => target == TargetId.Incident,
            ResearchOperation.GetServiceHealth or
                ResearchOperation.QueryMetrics or
                ResearchOperation.QueryLogs =>
                target is TargetId.PaymentsApi or TargetId.AuthorizationService,
            _ => false
        };

        if (!allowed)
        {
            Add(issues, "out-of-scope-target", "Operation/target pair is outside the approved catalogue.");
        }
    }

    private static void ValidateReason(
        Reason reason,
        ICollection<ResearchValidationIssue> issues)
    {
        var known = reason.Domain switch
        {
            ReasonDomain.Contract => ContractReasonCodes.Contains(reason.Code),
            ReasonDomain.Context => ContextReasonCodes.Contains(reason.Code),
            ReasonDomain.Infrastructure => InfrastructureReasonCodes.Contains(reason.Code),
            ReasonDomain.Governance => !string.IsNullOrWhiteSpace(reason.Code),
            _ => false
        };

        if (!known)
        {
            Add(issues, "unknown-enum", "Reason code is not valid for its domain.");
        }

        ValidateOptionalText(reason.Message, "message", issues);
    }

    private static void ValidateEventData(
        string eventType,
        JsonElement data,
        ICollection<ResearchValidationIssue> issues)
    {
        if (data.ValueKind != JsonValueKind.Object)
        {
            Add(issues, "invalid-shape", "Research event data must be an object.");
            return;
        }

        if (!EventFields.TryGetValue(eventType, out var expectedFields))
        {
            Add(issues, "unknown-enum", "Research event type is not supported.");
            return;
        }

        var actual = data.EnumerateObject()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (!actual.SetEquals(expectedFields))
        {
            Add(issues, "invalid-shape", "Research event data does not match its event type.");
            return;
        }

        switch (eventType)
        {
            case "run.started":
            case "episode.terminated":
            case "run.closed":
                break;
            case "evidence.available":
                ValidateJsonTokenArray(data, "evidenceIds", issues);
                ValidateJsonEnum<TargetId>(data, "targetId", issues);
                ValidateJsonU32(data, "availableTick", issues);
                break;
            case "observation.recorded":
                ValidateJsonIdentifier(data, "observationId", "observation-", issues);
                break;
            case "epoch.changed":
                ValidateJsonU32(data, "previousEpoch", issues);
                ValidateJsonU32(data, "newEpoch", issues);
                ValidateJsonToken(data, "sourceId", issues);
                break;
            case "actor.started":
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonIdentifier(data, "snapshotId", "snapshot-", issues);
                ValidateJsonU32(data, "consumedMemoryRevision", issues);
                ValidateJsonNullableIdentifier(data, "requiredTriggerId", "trigger-", issues);
                break;
            case "actor.completed":
            case "actor.failed":
            case "actor.cancelled":
                ValidateActorResultEvent(data, issues);
                break;
            case "actor.rejected":
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                ValidateJsonReason(data, "reason", false, issues);
                break;
            case "actor.invalidated":
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonIdentifier(data, "snapshotId", "snapshot-", issues);
                ValidateJsonIdentifier(data, "triggerId", "trigger-", issues);
                ValidateJsonU32(data, "previousGeneration", issues);
                ValidateJsonU32(data, "newGeneration", issues);
                break;
            case "reconsideration.required":
                ValidateJsonIdentifier(data, "triggerId", "trigger-", issues);
                ValidateJsonNullableIdentifier(
                    data,
                    "previousPendingTriggerId",
                    "trigger-",
                    issues);
                break;
            case "actor.memory-disposition":
                ValidateActorResultEvent(data, issues);
                ValidateJsonRecord<MemoryDisposition>(
                    data,
                    "disposition",
                    ValidateMemoryDisposition,
                    false,
                    issues);
                break;
            case "reconsideration.acknowledged":
                ValidateJsonIdentifier(data, "triggerId", "trigger-", issues);
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                break;
            case "actor.wait-started":
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                ValidateJsonU32(data, "untilTick", issues);
                break;
            case "actor.wait-ended":
                ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
                ValidateJsonReason(data, "reason", false, issues);
                break;
            case "diagnostic.requested":
                ValidateDiagnosticActorResult(data, issues);
                break;
            case "diagnostic.dispatched":
                ValidateDiagnosticActorResult(data, issues);
                ValidateJsonU32(data, "consumedMemoryRevision", issues);
                ValidateOperationalBinding(data, "binding", false, issues);
                break;
            case "diagnostic.completed":
                ValidateJsonIdentifier(data, "diagnosticId", "diagnostic-", issues);
                ValidateJsonIdentifier(data, "observationId", "observation-", issues);
                ValidateJsonUuidArray(data, "auditRecordIds", issues);
                break;
            case "diagnostic.rejected":
            case "diagnostic.failed":
                ValidateJsonIdentifier(data, "diagnosticId", "diagnostic-", issues);
                ValidateJsonReason(data, "reason", false, issues);
                ValidateOperationalBinding(data, "binding", true, issues);
                ValidateJsonUuidArray(data, "auditRecordIds", issues);
                break;
            case "diagnostic.duplicate-delivery":
                ValidateJsonIdentifier(data, "diagnosticId", "diagnostic-", issues);
                ValidateJsonPositiveU32(data, "originalCompletionSequence", issues);
                break;
            case "review.started":
                ValidateJsonIdentifier(data, "reviewId", "review-", issues);
                ValidateJsonIdentifier(data, "snapshotId", "snapshot-", issues);
                ValidateJsonU32(data, "consumedMemoryRevision", issues);
                ValidateJsonU32(data, "timeoutAtTick", issues);
                break;
            case "review.completed":
            case "review.failed":
            case "review.cancelled":
                ValidateReviewResultEvent(data, issues);
                break;
            case "review.rejected":
                ValidateJsonIdentifier(data, "reviewId", "review-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                ValidateJsonReason(data, "reason", false, issues);
                break;
            case "review.timed-out":
                ValidateJsonIdentifier(data, "reviewId", "review-", issues);
                ValidateJsonU32(data, "timeoutAtTick", issues);
                break;
            case "review.checkpoint-skipped":
                ValidateJsonU32(data, "checkpointTick", issues);
                ValidateJsonIdentifier(data, "activeReviewId", "review-", issues);
                break;
            case "review.no-change":
                ValidateJsonIdentifier(data, "reviewId", "review-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                break;
            case "memory.update-proposed":
                ValidateJsonIdentifier(data, "reviewId", "review-", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                ValidateJsonU32(data, "baseMemoryRevision", issues);
                break;
            case "memory.update-rejected":
            case "memory.update-no-op":
                ValidateJsonIdentifier(data, "memoryUpdateId", "memory-update-", issues);
                break;
            case "memory.committed":
                ValidateJsonIdentifier(data, "memoryUpdateId", "memory-update-", issues);
                ValidateJsonU32(data, "previousMemoryRevision", issues);
                ValidateJsonU32(data, "newMemoryRevision", issues);
                ValidateJsonU32(data, "previousGeneration", issues);
                ValidateJsonU32(data, "newGeneration", issues);
                ValidateJsonBoolean(data, "actionable", issues);
                ValidateJsonNullableIdentifier(data, "triggerId", "trigger-", issues);
                break;
            case "belief.state-changed":
                ValidateJsonIdentifier(data, "memoryUpdateId", "memory-update-", issues);
                ValidateJsonIdentifier(data, "beliefId", "belief-", issues);
                ValidateJsonNullableEnum<BeliefState>(data, "previousState", issues);
                ValidateJsonEnum<BeliefState>(data, "newState", issues);
                break;
            case "belief.reasserted":
                ValidateJsonIdentifier(data, "memoryUpdateId", "memory-update-", issues);
                ValidateJsonIdentifier(data, "predecessorBeliefId", "belief-", issues);
                ValidateJsonIdentifier(data, "beliefId", "belief-", issues);
                ValidateJsonIdentifier(data, "triggerId", "trigger-", issues);
                break;
            case "direction.ended":
                ValidateJsonIdentifier(data, "memoryUpdateId", "memory-update-", issues);
                ValidateJsonIdentifier(data, "directionId", "direction-", issues);
                ValidateJsonEnum<DirectionState>(data, "state", issues);
                if (TryReadEnum<DirectionState>(data, "state", out var state) &&
                    state == DirectionState.Active)
                {
                    Add(issues, "invalid-shape", "direction.ended requires a terminal state.");
                }
                break;
            case "cancellation.requested":
            case "cancellation.unsupported":
            case "cancellation.failed":
                ValidateJsonInvocation(data, "invocation", issues);
                ValidateJsonReason(data, "reason", false, issues);
                break;
            case "cancellation.confirmed":
            case "cancellation.ignored":
                ValidateJsonInvocation(data, "invocation", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                break;
            case "result.suppressed":
                ValidateJsonInvocation(data, "invocation", issues);
                ValidateJsonIdentifier(data, "resultId", "result-", issues);
                ValidateJsonU32(data, "consumedMemoryRevision", issues);
                ValidateJsonReason(data, "reason", false, issues);
                break;
            case "result.duplicate-delivery":
            case "result.conflicting-delivery":
                ValidateJsonInvocation(data, "invocation", issues);
                ValidateJsonIdentifier(data, "originalResultId", "result-", issues);
                ValidateJsonSha256(data, "deliveredOutputSha256", issues);
                break;
            case "report.submitted":
                ValidateActorResultEvent(data, issues);
                break;
        }
    }

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> EventFields =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            ["run.started"] = Fields(),
            ["evidence.available"] = Fields("evidenceIds", "targetId", "availableTick"),
            ["observation.recorded"] = Fields("observationId"),
            ["epoch.changed"] = Fields("previousEpoch", "newEpoch", "sourceId"),
            ["actor.started"] = Fields("actorTurnId", "snapshotId", "consumedMemoryRevision", "requiredTriggerId"),
            ["actor.completed"] = Fields("actorTurnId", "resultId", "consumedMemoryRevision"),
            ["actor.failed"] = Fields("actorTurnId", "resultId", "consumedMemoryRevision"),
            ["actor.cancelled"] = Fields("actorTurnId", "resultId", "consumedMemoryRevision"),
            ["actor.rejected"] = Fields("actorTurnId", "resultId", "reason"),
            ["actor.invalidated"] = Fields("actorTurnId", "snapshotId", "triggerId", "previousGeneration", "newGeneration"),
            ["reconsideration.required"] = Fields("triggerId", "previousPendingTriggerId"),
            ["actor.memory-disposition"] = Fields("actorTurnId", "resultId", "consumedMemoryRevision", "disposition"),
            ["reconsideration.acknowledged"] = Fields("triggerId", "actorTurnId", "resultId"),
            ["actor.wait-started"] = Fields("actorTurnId", "resultId", "untilTick"),
            ["actor.wait-ended"] = Fields("actorTurnId", "reason"),
            ["diagnostic.requested"] = Fields("diagnosticId", "actorTurnId", "resultId"),
            ["diagnostic.dispatched"] = Fields("diagnosticId", "actorTurnId", "resultId", "consumedMemoryRevision", "binding"),
            ["diagnostic.completed"] = Fields("diagnosticId", "observationId", "auditRecordIds"),
            ["diagnostic.rejected"] = Fields("diagnosticId", "reason", "binding", "auditRecordIds"),
            ["diagnostic.failed"] = Fields("diagnosticId", "reason", "binding", "auditRecordIds"),
            ["diagnostic.duplicate-delivery"] = Fields("diagnosticId", "originalCompletionSequence"),
            ["review.started"] = Fields("reviewId", "snapshotId", "consumedMemoryRevision", "timeoutAtTick"),
            ["review.completed"] = Fields("reviewId", "resultId", "consumedMemoryRevision"),
            ["review.failed"] = Fields("reviewId", "resultId", "consumedMemoryRevision"),
            ["review.cancelled"] = Fields("reviewId", "resultId", "consumedMemoryRevision"),
            ["review.rejected"] = Fields("reviewId", "resultId", "reason"),
            ["review.timed-out"] = Fields("reviewId", "timeoutAtTick"),
            ["review.checkpoint-skipped"] = Fields("checkpointTick", "activeReviewId"),
            ["review.no-change"] = Fields("reviewId", "resultId"),
            ["memory.update-proposed"] = Fields("reviewId", "resultId", "baseMemoryRevision"),
            ["memory.update-rejected"] = Fields("memoryUpdateId"),
            ["memory.update-no-op"] = Fields("memoryUpdateId"),
            ["memory.committed"] = Fields("memoryUpdateId", "previousMemoryRevision", "newMemoryRevision", "previousGeneration", "newGeneration", "actionable", "triggerId"),
            ["belief.state-changed"] = Fields("memoryUpdateId", "beliefId", "previousState", "newState"),
            ["belief.reasserted"] = Fields("memoryUpdateId", "predecessorBeliefId", "beliefId", "triggerId"),
            ["direction.ended"] = Fields("memoryUpdateId", "directionId", "state"),
            ["cancellation.requested"] = Fields("invocation", "reason"),
            ["cancellation.unsupported"] = Fields("invocation", "reason"),
            ["cancellation.failed"] = Fields("invocation", "reason"),
            ["cancellation.confirmed"] = Fields("invocation", "resultId"),
            ["cancellation.ignored"] = Fields("invocation", "resultId"),
            ["result.suppressed"] = Fields("invocation", "resultId", "consumedMemoryRevision", "reason"),
            ["result.duplicate-delivery"] = Fields("invocation", "originalResultId", "deliveredOutputSha256"),
            ["result.conflicting-delivery"] = Fields("invocation", "originalResultId", "deliveredOutputSha256"),
            ["report.submitted"] = Fields("actorTurnId", "resultId", "consumedMemoryRevision"),
            ["episode.terminated"] = Fields(),
            ["run.closed"] = Fields()
        };

    private static IReadOnlySet<string> Fields(params string[] names) =>
        new HashSet<string>(names, StringComparer.Ordinal);

    private static void ValidateJsonIdentifier(
        JsonElement data,
        string propertyName,
        string prefix,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!data.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be a string.");
            return;
        }

        ValidateIdentifier(property.GetString()!, prefix, issues);
    }

    private static void ValidateJsonNullableIdentifier(
        JsonElement data,
        string propertyName,
        string prefix,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        ValidateJsonIdentifier(data, propertyName, prefix, issues);
    }

    private static void ValidateJsonU32(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetUInt32(out _))
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be U32.");
        }
    }

    private static void ValidateJsonPositiveU32(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetUInt32(out var value))
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be U32.");
        }
        else if (value == 0)
        {
            Add(issues, "out-of-range", $"Event field '{propertyName}' must be positive.");
        }
    }

    private static void ValidateJsonBoolean(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be boolean.");
        }
    }

    private static void ValidateJsonToken(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be a string.");
            return;
        }

        ValidateToken(property.GetString()!, propertyName, issues);
    }

    private static void ValidateJsonSha256(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be a string.");
            return;
        }

        ValidateSha256(property.GetString()!, issues);
    }

    private static void ValidateJsonIdentifierArray(
        JsonElement data,
        string propertyName,
        string prefix,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.Array)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be an array.");
            return;
        }

        var values = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                Add(issues, "wrong-type", $"Event field '{propertyName}' contains a non-string.");
                continue;
            }

            values.Add(element.GetString()!);
        }

        ValidateIdSet(values, prefix, propertyName, issues);
    }

    private static void ValidateJsonTokenArray(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.Array)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be an array.");
            return;
        }

        var values = new List<string>();
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                Add(issues, "wrong-type", $"Event field '{propertyName}' contains a non-string.");
                continue;
            }

            values.Add(element.GetString()!);
        }

        ValidateTokenSet(values, propertyName, issues);
    }

    private static void ValidateJsonUuidArray(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.Array)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be an array.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in property.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.String ||
                !Guid.TryParseExact(element.GetString(), "D", out _) ||
                !seen.Add(element.GetString()!))
            {
                Add(issues, "invalid-identifier", $"Event field '{propertyName}' must contain unique UUIDs.");
            }
        }
    }

    private static void ValidateJsonEnum<TEnum>(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (!TryReadEnum<TEnum>(data, propertyName, out _))
        {
            Add(issues, "unknown-enum", $"Event field '{propertyName}' is not supported.");
        }
    }

    private static void ValidateJsonNullableEnum<TEnum>(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
        where TEnum : struct, Enum
    {
        if (data.GetProperty(propertyName).ValueKind != JsonValueKind.Null)
        {
            ValidateJsonEnum<TEnum>(data, propertyName, issues);
        }
    }

    private static bool TryReadEnum<TEnum>(
        JsonElement data,
        string propertyName,
        out TEnum value)
        where TEnum : struct, Enum
    {
        try
        {
            value = JsonSerializer.Deserialize<TEnum>(
                data.GetProperty(propertyName),
                ResearchContractSerializer.Options);
            return true;
        }
        catch (JsonException)
        {
            value = default;
            return false;
        }
    }

    private static void ValidateJsonReason(
        JsonElement data,
        string propertyName,
        bool nullable,
        ICollection<ResearchValidationIssue> issues) =>
        ValidateJsonRecord<Reason>(
            data,
            propertyName,
            ValidateReason,
            nullable,
            issues);

    private static void ValidateJsonRecord<T>(
        JsonElement data,
        string propertyName,
        Action<T, ICollection<ResearchValidationIssue>> validator,
        bool nullable,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (nullable && property.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        try
        {
            var value = property.Deserialize<T>(ResearchContractSerializer.Options);
            if (value is null)
            {
                Add(issues, "wrong-type", $"Event field '{propertyName}' has the wrong shape.");
            }
            else
            {
                var nullIssue = ResearchObjectGraphValidator.FindExplicitNull(value);
                if (nullIssue is not null)
                {
                    issues.Add(nullIssue);
                    return;
                }

                validator(value, issues);
            }
        }
        catch (Exception exception) when (
            exception is JsonException or ResearchContractException)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' has the wrong shape.");
        }
    }

    private static void ValidateJsonInvocation(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues) =>
        ValidateJsonRecord<InvocationRef>(
            data,
            propertyName,
            ValidateInvocationRef,
            false,
            issues);

    private static void ValidateInvocationRef(
        InvocationRef invocation,
        ICollection<ResearchValidationIssue> issues)
    {
        switch (invocation)
        {
            case ActorInvocationRef actor:
                ValidateIdentifier(actor.ActorTurnId, "actor-", issues);
                break;
            case SupervisorInvocationRef supervisor:
                ValidateIdentifier(supervisor.ReviewId, "review-", issues);
                break;
            default:
                Add(issues, "unknown-enum", "Invocation consumer is not supported.");
                break;
        }
    }

    private static void ValidateMemoryDisposition(
        MemoryDisposition disposition,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateIdentifier(disposition.TriggerId, "trigger-", issues);
        ValidateIdentifier(disposition.MemoryUpdateId, "memory-update-", issues);
    }

    private static void ValidateOperationalBinding(
        JsonElement data,
        string propertyName,
        bool nullable,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (nullable && property.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.Object ||
            !property.EnumerateObject().Select(item => item.Name).ToHashSet(StringComparer.Ordinal)
                .SetEquals(Fields(
                    "planId",
                    "stepId",
                    "planDigest",
                    "actionDigest",
                    "requestId",
                    "sessionId")))
        {
            Add(issues, "invalid-shape", "Operational binding has the wrong shape.");
            return;
        }

        ValidateJsonUuid(property, "planId", false, issues);
        ValidateJsonText(property, "stepId", issues);
        ValidateJsonSha256(property, "planDigest", issues);
        ValidateJsonSha256(property, "actionDigest", issues);
        ValidateJsonUuid(property, "requestId", true, issues);
        ValidateJsonText(property, "sessionId", issues);
    }

    private static void ValidateJsonUuid(
        JsonElement data,
        string propertyName,
        bool nullable,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (nullable && property.ValueKind == JsonValueKind.Null)
        {
            return;
        }

        if (property.ValueKind != JsonValueKind.String ||
            !Guid.TryParseExact(property.GetString(), "D", out _))
        {
            Add(issues, "invalid-identifier", $"Event field '{propertyName}' must be a UUID.");
        }
    }

    private static void ValidateJsonText(
        JsonElement data,
        string propertyName,
        ICollection<ResearchValidationIssue> issues)
    {
        var property = data.GetProperty(propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            Add(issues, "wrong-type", $"Event field '{propertyName}' must be a string.");
            return;
        }

        ValidateText(property.GetString()!, propertyName, issues);
    }

    private static void ValidateActorResultEvent(
        JsonElement data,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
        ValidateJsonIdentifier(data, "resultId", "result-", issues);
        ValidateJsonU32(data, "consumedMemoryRevision", issues);
    }

    private static void ValidateReviewResultEvent(
        JsonElement data,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateJsonIdentifier(data, "reviewId", "review-", issues);
        ValidateJsonIdentifier(data, "resultId", "result-", issues);
        ValidateJsonU32(data, "consumedMemoryRevision", issues);
    }

    private static void ValidateDiagnosticActorResult(
        JsonElement data,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateJsonIdentifier(data, "diagnosticId", "diagnostic-", issues);
        ValidateJsonIdentifier(data, "actorTurnId", "actor-", issues);
        ValidateJsonIdentifier(data, "resultId", "result-", issues);
    }

    private static void ValidateDefinedKey(
        string key,
        ISet<string> keys,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateToken(key, "key", issues);
        if (!keys.Add(key))
        {
            Add(issues, "invalid-reference", "Proposal-local keys must be unique.");
        }
    }

    private static void ValidateOptionalText(
        string? value,
        string field,
        ICollection<ResearchValidationIssue> issues)
    {
        if (value is not null)
        {
            ValidateText(value, field, issues);
        }
    }

    private static void ValidateText(
        string value,
        string field,
        ICollection<ResearchValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(issues, "invalid-shape", $"{field} must be nonempty text.");
        }
        else if (Encoding.UTF8.GetByteCount(value) >
                 ResearchEngineeringLimits.MaximumComponentTextBytes)
        {
            Add(issues, "engineering-limit-exceeded", $"{field} exceeds the approved text limit.");
        }
    }

    private static void ValidateToken(
        string value,
        string field,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!TokenRegex().IsMatch(value))
        {
            Add(issues, "invalid-identifier", $"{field} is not a valid token.");
        }
    }

    private static void ValidateTokenSet(
        IReadOnlyList<string> values,
        string field,
        ICollection<ResearchValidationIssue> issues)
    {
        ValidateSortedUnique(values, field, issues);
        foreach (var value in values)
        {
            ValidateToken(value, field, issues);
        }
    }

    private static void ValidateIdSet(
        IReadOnlyList<string> values,
        string prefix,
        string field,
        ICollection<ResearchValidationIssue> issues)
    {
        if (values.Count > ResearchEngineeringLimits.MaximumCitationsPerField)
        {
            Add(issues, "engineering-limit-exceeded", $"{field} exceeds the approved array limit.");
        }

        long previous = 0;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            ValidateIdentifier(value, prefix, issues);
            if (!seen.Add(value))
            {
                Add(issues, "invalid-reference", $"{field} must be duplicate-free.");
            }

            if (value.StartsWith(prefix, StringComparison.Ordinal) &&
                long.TryParse(value[prefix.Length..], out var current))
            {
                if (current <= previous)
                {
                    Add(issues, "invalid-reference", $"{field} must use numeric ID order.");
                }

                previous = current;
            }
        }
    }

    private static void ValidateSortedUnique<T>(
        IReadOnlyList<T> values,
        string field,
        ICollection<ResearchValidationIssue> issues)
        where T : IComparable<T>
    {
        for (var index = 1; index < values.Count; index++)
        {
            if (values[index - 1].CompareTo(values[index]) >= 0)
            {
                Add(issues, "invalid-reference", $"{field} must be sorted and duplicate-free.");
                return;
            }
        }
    }

    private static void ValidateIdentifier(
        string value,
        string prefix,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            !PositiveDecimalRegex().IsMatch(value[prefix.Length..]))
        {
            Add(issues, "invalid-identifier", $"Identifier '{value}' must use prefix '{prefix}'.");
        }
    }

    private static void ValidateRunId(
        string value,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!Guid.TryParseExact(value, "D", out _) ||
            !string.Equals(value, value.ToLowerInvariant(), StringComparison.Ordinal))
        {
            Add(issues, "invalid-identifier", "runId must be a lowercase D-format GUID.");
        }
    }

    private static void ValidateSha256(
        string value,
        ICollection<ResearchValidationIssue> issues)
    {
        if (!Sha256Regex().IsMatch(value))
        {
            Add(issues, "invalid-identifier", "SHA-256 values must be lowercase hexadecimal.");
        }
    }

    private static void Add(
        ICollection<ResearchValidationIssue> issues,
        string code,
        string message) =>
        issues.Add(new ResearchValidationIssue(code, message));

    private static void AddNested(
        ICollection<ResearchValidationIssue> issues,
        IReadOnlyList<ResearchValidationIssue> nested)
    {
        foreach (var issue in nested)
        {
            issues.Add(issue);
        }
    }

    private static bool TryValidateEmbeddedRoot<TExpected>(
        JsonElement element,
        ICollection<ResearchValidationIssue> issues)
        where TExpected : IResearchRoot
    {
        try
        {
            return ResearchContractSerializer.DeserializeRoot(element.GetRawText()) is TExpected;
        }
        catch (ResearchContractException exception)
        {
            Add(issues, exception.Code, exception.Message);
            return false;
        }
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();

    [GeneratedRegex("^[1-9][0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex PositiveDecimalRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex("^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha40Regex();
}

public sealed record ResearchValidationIssue(string Code, string Message);
