using System.Collections.Immutable;
using System.Text.Json;
using GovernedAgent.Simulator;

namespace GovernedAgent.Research;

internal static class ResearchContractFreezer
{
    public static InputSnapshot Freeze(InputSnapshot value) =>
        value with
        {
            AllowedQueries = value.AllowedQueries
                .Select(Freeze)
                .ToImmutableArray(),
            Observations = value.Observations
                .Select(Freeze)
                .ToImmutableArray(),
            ActionHistory = value.ActionHistory
                .Select(Freeze)
                .ToImmutableArray(),
            ReviewHistory = value.ReviewHistory
                .Select(Freeze)
                .ToImmutableArray(),
            WorkingMemory = Freeze(value.WorkingMemory),
            Beliefs = value.Beliefs
                .Select(Freeze)
                .ToImmutableArray(),
            Reconsideration = value.Reconsideration is null
                ? null
                : Freeze(value.Reconsideration)
        };

    public static Observation Freeze(Observation value) =>
        value with
        {
            EvidenceIds = value.EvidenceIds.ToImmutableArray(),
            Content = Freeze(value.Content)
        };

    public static Belief Freeze(Belief value) =>
        value with { Claim = Freeze(value.Claim) };

    public static Direction Freeze(Direction value) =>
        value with
        {
            Recommendation = Freeze(value.Recommendation),
            ObservationIds = value.ObservationIds.ToImmutableArray(),
            SupportingBeliefIds = value.SupportingBeliefIds.ToImmutableArray()
        };

    public static WorkingMemorySnapshot Freeze(WorkingMemorySnapshot value) =>
        value with
        {
            BeliefStates = value.BeliefStates
                .Select(item => item with
                {
                    ConflictingBeliefIds =
                        item.ConflictingBeliefIds.ToImmutableArray()
                })
                .ToImmutableArray(),
            RecommendedDirection = value.RecommendedDirection is null
                ? null
                : Freeze(value.RecommendedDirection)
        };

    public static MemoryUpdate Freeze(MemoryUpdate value) =>
        value with
        {
            Proposal = value.Proposal is null ? null : Freeze(value.Proposal),
            Normalization = value.Normalization.ToImmutableArray(),
            KeyBindings = value.KeyBindings.ToImmutableArray(),
            Effects = value.Effects is null ? null : Freeze(value.Effects),
            Errors = value.Errors.ToImmutableArray()
        };

    public static Termination Freeze(Termination value) =>
        value with
        {
            PendingInvocations = value.PendingInvocations.ToImmutableArray(),
            PendingDiagnosticIds = value.PendingDiagnosticIds.ToImmutableArray()
        };

    public static RunClosure Freeze(RunClosure value) =>
        value with
        {
            UnsettledInvocations = value.UnsettledInvocations.ToImmutableArray(),
            UnsettledDiagnosticIds = value.UnsettledDiagnosticIds.ToImmutableArray()
        };

    public static ActorDecision Freeze(ActorDecision value) =>
        value switch
        {
            QueryActorDecision query => query with
            {
                UsedBeliefIds = query.UsedBeliefIds.ToImmutableArray(),
                Arguments = Freeze(query.Arguments)
            },
            WaitActorDecision wait => wait with
            {
                UsedBeliefIds = wait.UsedBeliefIds.ToImmutableArray()
            },
            ReportActorDecision report => report with
            {
                UsedBeliefIds = report.UsedBeliefIds.ToImmutableArray(),
                ObservationIds = report.ObservationIds.ToImmutableArray()
            },
            _ => throw new InvalidOperationException("Unsupported actor decision.")
        };

    public static SupervisorOutput Freeze(SupervisorOutput value) =>
        value switch
        {
            NoChangeSupervisorOutput noChange => noChange with
            {
                ObservationIds = noChange.ObservationIds.ToImmutableArray()
            },
            ProposeMemoryUpdateSupervisorOutput proposal => proposal with
            {
                ObservationIds = proposal.ObservationIds.ToImmutableArray(),
                Operations = proposal.Operations
                    .Select(Freeze)
                    .ToImmutableArray()
            },
            _ => throw new InvalidOperationException("Unsupported supervisor output.")
        };

    private static AllowedQuery Freeze(AllowedQuery value) =>
        value with { Arguments = Freeze(value.Arguments) };

    private static ActionHistoryItem Freeze(ActionHistoryItem value) =>
        value with
        {
            Decision = value.Decision?.Clone(),
            ObservationIds = value.ObservationIds.ToImmutableArray()
        };

    private static ReviewHistoryItem Freeze(ReviewHistoryItem value) =>
        value with { Output = value.Output?.Clone() };

    private static ReconsiderationRequirement Freeze(
        ReconsiderationRequirement value) =>
        value with
        {
            EarlierTriggerIds = value.EarlierTriggerIds.ToImmutableArray(),
            EffectsAtCapture = value.EffectsAtCapture.ToImmutableArray()
        };

    private static Claim Freeze(Claim value) =>
        value with { ObservationIds = value.ObservationIds.ToImmutableArray() };

    private static ObservationContent Freeze(ObservationContent value) =>
        value switch
        {
            NotificationObservationContent notification => notification,
            IncidentObservationContent incident => incident,
            ServiceHealthObservationContent health => health with
            {
                Value = health.Value with
                {
                    Instances = health.Value.Instances.ToImmutableArray()
                }
            },
            MetricsObservationContent metrics => metrics with
            {
                Values = metrics.Values.ToImmutableArray()
            },
            LogsObservationContent logs => logs with
            {
                Values = logs.Values.ToImmutableArray()
            },
            UnavailableObservationContent unavailable => unavailable,
            _ => throw new InvalidOperationException("Unsupported observation content.")
        };

    private static MemoryOperation Freeze(MemoryOperation value) =>
        value switch
        {
            AddBeliefOperation add => add with { Claim = Freeze(add.Claim) },
            ReassertBeliefOperation reassert => reassert with
            {
                Claim = Freeze(reassert.Claim)
            },
            ReplaceBeliefOperation replace => replace with
            {
                Claim = Freeze(replace.Claim)
            },
            RetractBeliefOperation retract => retract with
            {
                ObservationIds = retract.ObservationIds.ToImmutableArray()
            },
            DeclareConflictOperation conflict => conflict with
            {
                Beliefs = conflict.Beliefs.ToImmutableArray(),
                ObservationIds = conflict.ObservationIds.ToImmutableArray()
            },
            SetDirectionOperation set => set with
            {
                Recommendation = Freeze(set.Recommendation),
                ObservationIds = set.ObservationIds.ToImmutableArray(),
                SupportingBeliefs = set.SupportingBeliefs.ToImmutableArray()
            },
            ClearDirectionOperation clear => clear with
            {
                ObservationIds = clear.ObservationIds.ToImmutableArray()
            },
            _ => throw new InvalidOperationException("Unsupported memory operation.")
        };

    private static DirectionChoice Freeze(DirectionChoice value) =>
        value switch
        {
            FocusDirectionChoice focus => focus with
            {
                Focus = focus.Focus switch
                {
                    TargetFocus target => target,
                    DiagnosticFocus diagnostic => diagnostic with
                    {
                        Arguments = Freeze(diagnostic.Arguments)
                    },
                    _ => throw new InvalidOperationException("Unsupported focus.")
                }
            },
            HandoffDirectionChoice handoff => handoff,
            _ => throw new InvalidOperationException("Unsupported direction choice.")
        };

    private static MemoryEffects Freeze(MemoryEffects value) =>
        value with
        {
            CreatedBeliefIds = value.CreatedBeliefIds.ToImmutableArray(),
            Supersessions = value.Supersessions.ToImmutableArray(),
            Reassertions = value.Reassertions.ToImmutableArray(),
            RetractedBeliefIds = value.RetractedBeliefIds.ToImmutableArray(),
            InvalidatedBeliefIds = value.InvalidatedBeliefIds.ToImmutableArray(),
            AddedConflicts = value.AddedConflicts.ToImmutableArray(),
            RemovedConflicts = value.RemovedConflicts.ToImmutableArray()
        };

    private static IReadOnlyDictionary<string, JsonElement> Freeze(
        IReadOnlyDictionary<string, JsonElement> value) =>
        value.ToImmutableDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
}
