using System.Text.Json;

namespace GovernedAgent.Research;

public static class ResearchEventSequenceValidator
{
    public static IReadOnlyList<ResearchValidationIssue> Validate(
        IReadOnlyList<ResearchEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var issues = new List<ResearchValidationIssue>();
        if (events.Count == 0)
        {
            return issues;
        }

        var runId = events[0].RunId;
        uint previousGeneration = 0;
        uint previousMemoryRevision = 0;
        uint previousHistoryRevision = 0;
        uint previousEpoch = 0;
        uint? previousTick = null;
        ResearchPhase? previousPhase = null;
        var terminated = false;
        var closed = false;
        (uint Generation, uint MemoryRevision, uint HistoryRevision, uint Epoch)? frozenCoordinates = null;

        for (var index = 0; index < events.Count; index++)
        {
            var item = events[index];
            issues.AddRange(ResearchContractValidator.Validate(item));

            var expectedSequence = checked((uint)index + 1);
            if (item.Sequence != expectedSequence)
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "invalid-reference",
                        "Research event sequences must be contiguous from one."));
            }

            if (!string.Equals(item.RunId, runId, StringComparison.Ordinal))
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "run-mismatch",
                        "A research event ledger cannot mix runs."));
            }

            if (item.CausedBySequences.Any(cause => cause >= item.Sequence))
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "invalid-reference",
                        "Event causes must refer to earlier ledger entries."));
            }

            if (item.CurrentGeneration < previousGeneration ||
                item.CurrentMemoryRevision < previousMemoryRevision ||
                item.HistoryRevision < previousHistoryRevision ||
                item.ApplicabilityEpoch < previousEpoch)
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "inconsistent-update",
                        "Trusted event coordinates cannot move backward."));
            }

            if (closed)
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "episode-ended",
                        "No events may follow run.closed."));
            }

            if (previousTick is not null &&
                item.Tick is not null &&
                item.Tick < previousTick)
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "inconsistent-update",
                        "Research event ticks cannot move backward."));
            }

            if (!terminated &&
                previousTick == item.Tick &&
                previousPhase is not null &&
                PhaseRank(item.Phase) < PhaseRank(previousPhase.Value))
            {
                issues.Add(
                    new ResearchValidationIssue(
                        "inconsistent-update",
                        "Research event phases cannot move backward within a tick."));
            }

            if (terminated)
            {
                if (item.Phase != ResearchPhase.Drain ||
                    !DrainEventTypes.Contains(item.EventType))
                {
                    issues.Add(
                        new ResearchValidationIssue(
                            "episode-ended",
                            "Only approved drain/accounting events may follow termination."));
                }

                if (frozenCoordinates is { } frozen &&
                    (item.CurrentGeneration != frozen.Generation ||
                        item.CurrentMemoryRevision != frozen.MemoryRevision ||
                        item.HistoryRevision != frozen.HistoryRevision ||
                        item.ApplicabilityEpoch != frozen.Epoch))
                {
                    issues.Add(
                        new ResearchValidationIssue(
                            "inconsistent-update",
                            "Drain events must preserve the coordinates frozen at termination."));
                }
            }

            if (item.EventType == "episode.terminated")
            {
                if (terminated)
                {
                    issues.Add(
                        new ResearchValidationIssue(
                            "episode-ended",
                            "An episode may terminate only once."));
                }

                terminated = true;
                frozenCoordinates = (
                    item.CurrentGeneration,
                    item.CurrentMemoryRevision,
                    item.HistoryRevision,
                    item.ApplicabilityEpoch);
            }

            if (item.EventType == "run.closed")
            {
                if (!terminated || item.Phase != ResearchPhase.Drain)
                {
                    issues.Add(
                        new ResearchValidationIssue(
                            "episode-ended",
                            "run.closed requires a preceding termination and drain phase."));
                }

                closed = true;
            }

            previousGeneration = item.CurrentGeneration;
            previousMemoryRevision = item.CurrentMemoryRevision;
            previousHistoryRevision = item.HistoryRevision;
            previousEpoch = item.ApplicabilityEpoch;
            previousTick = item.Tick ?? previousTick;
            previousPhase = item.Phase;
        }

        return issues;
    }

    public static IReadOnlyList<ResearchValidationIssue> Validate(
        IReadOnlyList<ResearchEvent> events,
        Termination termination)
    {
        ArgumentNullException.ThrowIfNull(termination);
        var issues = Validate(events).ToList();
        issues.AddRange(ResearchContractValidator.Validate(termination));

        if (events.Any(item =>
                !string.Equals(item.RunId, termination.RunId, StringComparison.Ordinal)))
        {
            issues.Add(new ResearchValidationIssue("run-mismatch", "Termination run does not match the ledger."));
        }

        var terminationEvents = events.Where(
            item => item.EventType == "episode.terminated").ToList();
        if (terminationEvents.Count != 1)
        {
            issues.Add(new ResearchValidationIssue("invalid-reference", "Ledger must contain exactly one termination event."));
            return issues;
        }

        var terminalEvent = terminationEvents[0];
        if (terminalEvent.Tick != termination.Tick ||
            terminalEvent.Phase != termination.Phase ||
            terminalEvent.CurrentGeneration != termination.FinalGeneration ||
            terminalEvent.CurrentMemoryRevision != termination.FinalMemoryRevision ||
            terminalEvent.HistoryRevision != termination.FinalHistoryRevision ||
            terminalEvent.ApplicabilityEpoch != termination.FinalApplicabilityEpoch)
        {
            issues.Add(new ResearchValidationIssue("inconsistent-update", "Termination root does not match its ledger event."));
        }

        var terminationIndex = events
            .Select((item, index) => (item, index))
            .Single(pair => ReferenceEquals(pair.item, terminalEvent))
            .index;
        var prefix = events.Take(terminationIndex + 1).ToList();
        ValidateCount(
            termination.Counts.ActorTurnsStarted,
            prefix.Count(item => item.EventType == "actor.started"),
            "actorTurnsStarted",
            issues);
        ValidateCount(
            termination.Counts.ReviewsStarted,
            prefix.Count(item => item.EventType == "review.started"),
            "reviewsStarted",
            issues);
        ValidateCount(
            termination.Counts.DiagnosticAttempts,
            prefix.Count(item => item.EventType == "diagnostic.requested"),
            "diagnosticAttempts",
            issues);
        ValidateCount(
            termination.Counts.DiagnosticsDispatched,
            prefix.Count(item => item.EventType == "diagnostic.dispatched"),
            "diagnosticsDispatched",
            issues);
        ValidateCount(
            termination.Counts.DiagnosticsCompleted,
            prefix.Count(item => item.EventType == "diagnostic.completed"),
            "diagnosticsCompleted",
            issues);
        ValidateCount(
            termination.Counts.ActorResultsSuppressed,
            prefix.Count(item =>
                item.EventType == "result.suppressed" &&
                IsInvocationConsumer(item.Data, "actor")),
            "actorResultsSuppressed",
            issues);
        ValidateCount(
            termination.Counts.ReviewResultsSuppressed,
            prefix.Count(item =>
                item.EventType == "result.suppressed" &&
                IsInvocationConsumer(item.Data, "supervisor")),
            "reviewResultsSuppressed",
            issues);
        ValidateCount(
            termination.Counts.CancellationRequests,
            prefix.Count(item => item.EventType == "cancellation.requested"),
            "cancellationRequests",
            issues);
        ValidateCount(
            termination.Counts.CancellationsConfirmed,
            prefix.Count(item => item.EventType == "cancellation.confirmed"),
            "cancellationsConfirmed",
            issues);
        ValidateCount(
            termination.Counts.CancellationsUnsupported,
            prefix.Count(item => item.EventType == "cancellation.unsupported"),
            "cancellationsUnsupported",
            issues);
        ValidateCount(
            termination.Counts.CancellationRequestsFailed,
            prefix.Count(item => item.EventType == "cancellation.failed"),
            "cancellationRequestsFailed",
            issues);
        ValidateCount(
            termination.Counts.CancellationsIgnored,
            prefix.Count(item => item.EventType == "cancellation.ignored"),
            "cancellationsIgnored",
            issues);
        ValidateCount(
            termination.Counts.ReportsSubmitted,
            prefix.Count(item => item.EventType == "report.submitted"),
            "reportsSubmitted",
            issues);
        ValidateCount(
            termination.Counts.MemoryUpdatesCommitted,
            prefix.Count(item => item.EventType == "memory.committed"),
            "memoryUpdatesCommitted",
            issues);
        ValidateCount(
            termination.Counts.MemoryUpdatesRejected,
            prefix.Count(item => item.EventType == "memory.update-rejected"),
            "memoryUpdatesRejected",
            issues);
        ValidateCount(
            termination.Counts.MemoryUpdatesNoOp,
            prefix.Count(item => item.EventType == "memory.update-no-op"),
            "memoryUpdatesNoOp",
            issues);
        ValidateCount(
            termination.Counts.BeliefReassertionsCommitted,
            prefix.Count(item => item.EventType == "belief.reasserted"),
            "beliefReassertionsCommitted",
            issues);
        ValidateCount(
            termination.Counts.ActionableCommits,
            prefix.Count(item =>
                item.EventType == "memory.committed" &&
                IsBooleanField(item.Data, "actionable", true)),
            "actionableCommits",
            issues);
        var actionableUpdateIds = prefix
            .Where(item =>
                item.EventType == "memory.committed" &&
                IsBooleanField(item.Data, "actionable", true))
            .Select(item => GetStringField(item.Data, "memoryUpdateId"))
            .Where(value => value is not null)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        ValidateCount(
            termination.Counts.ReassertionInterrupts,
            prefix
                .Where(item => item.EventType == "belief.reasserted")
                .Select(item => GetStringField(item.Data, "memoryUpdateId"))
                .Where(value =>
                    value is not null &&
                    actionableUpdateIds.Contains(value))
                .Distinct(StringComparer.Ordinal)
                .Count(),
            "reassertionInterrupts",
            issues);
        ValidateCount(
            termination.Counts.ReconsiderationAcknowledgments,
            prefix.Count(item => item.EventType == "reconsideration.acknowledged"),
            "reconsiderationAcknowledgments",
            issues);

        return issues;
    }

    public static IReadOnlyList<ResearchValidationIssue> Validate(
        IReadOnlyList<ResearchEvent> events,
        IReadOnlySet<string> registeredEvidenceIds)
    {
        ArgumentNullException.ThrowIfNull(registeredEvidenceIds);
        var issues = Validate(events).ToList();
        foreach (var item in events.Where(
                     item => item.EventType == "evidence.available"))
        {
            if (!item.Data.TryGetProperty("evidenceIds", out var evidenceIds) ||
                evidenceIds.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var evidenceId in evidenceIds.EnumerateArray())
            {
                if (evidenceId.ValueKind == JsonValueKind.String &&
                    !registeredEvidenceIds.Contains(evidenceId.GetString()!))
                {
                    issues.Add(
                        new ResearchValidationIssue(
                            "invalid-reference",
                            "Available evidence is absent from the supplied fixture registry."));
                }
            }
        }

        return issues;
    }

    private static readonly IReadOnlySet<string> DrainEventTypes =
        new HashSet<string>(
            [
                "actor.completed",
                "actor.failed",
                "actor.cancelled",
                "review.completed",
                "review.failed",
                "review.cancelled",
                "diagnostic.completed",
                "diagnostic.rejected",
                "diagnostic.failed",
                "diagnostic.duplicate-delivery",
                "cancellation.requested",
                "cancellation.unsupported",
                "cancellation.failed",
                "cancellation.confirmed",
                "cancellation.ignored",
                "result.suppressed",
                "result.duplicate-delivery",
                "result.conflicting-delivery",
                "run.closed"
            ],
            StringComparer.Ordinal);

    private static int PhaseRank(ResearchPhase phase) => phase switch
    {
        ResearchPhase.Setup => 0,
        ResearchPhase.EndCheck => 1,
        ResearchPhase.WorldDelivery => 2,
        ResearchPhase.PriorDiagnosticResults => 3,
        ResearchPhase.ReviewProcessing => 4,
        ResearchPhase.ActorProcessing => 5,
        ResearchPhase.ReviewStart => 6,
        ResearchPhase.ActorStart => 7,
        ResearchPhase.Drain => 8,
        _ => int.MaxValue
    };

    private static bool IsInvocationConsumer(JsonElement data, string consumer) =>
        data.TryGetProperty("invocation", out var invocation) &&
        invocation.ValueKind == JsonValueKind.Object &&
        invocation.TryGetProperty("consumer", out var value) &&
        value.ValueKind == JsonValueKind.String &&
        string.Equals(value.GetString(), consumer, StringComparison.Ordinal);

    private static bool IsBooleanField(
        JsonElement data,
        string propertyName,
        bool expected) =>
        data.TryGetProperty(propertyName, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean() == expected;

    private static string? GetStringField(
        JsonElement data,
        string propertyName) =>
        data.TryGetProperty(propertyName, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void ValidateCount(
        uint recorded,
        int observed,
        string name,
        ICollection<ResearchValidationIssue> issues)
    {
        if (recorded != checked((uint)observed))
        {
            issues.Add(
                new ResearchValidationIssue(
                    "inconsistent-update",
                    $"Termination count '{name}' does not match the event ledger."));
        }
    }
}
