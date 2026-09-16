using System.Text.Json;
using System.Text.Json.Serialization;
using GovernedAgent.Simulator;

namespace GovernedAgent.Research;

public static class ResearchContractVersions
{
    public const string SchemaVersion = "1.0-candidate.3";
    public const string ContractBaseline = "T1-WM-1-candidate-3";
    public const string StudyBaseline = "1.0";
    public const string StudyAmendment = "WM-1";
    public const string StudyCandidate = "3";
}

public static class ResearchEngineeringLimits
{
    public const int MaximumComponentOutputBytes = 65_536;
    public const int MaximumOperationsPerUpdate = 16;
    public const int MaximumComponentTextBytes = 2_048;
    public const int MaximumCitationsPerField = 64;
    public const int MaximumInputSnapshotBytes = 1_048_576;
    public const int MaximumEventBytes = 131_072;
}

public interface IResearchRoot
{
    string SchemaVersion { get; }
    string RecordType { get; }
}

public enum Hypothesis
{
    LocalInstanceIssue,
    DependencyPathIssue,
    DependencySideQueueDelay,
    NoCurrentlyActiveIncident,
    Unresolved
}

public enum NextStep
{
    FurtherLocalDiagnostics,
    FurtherDependencyDiagnostics,
    InvestigateDependencyQueue,
    Monitor,
    HumanHandoff
}

public enum HandoffRecommendation
{
    UncertaintyReport,
    HumanHandoff
}

public enum Uncertainty
{
    Low,
    Medium,
    High
}

public enum Stance
{
    Adopt,
    Adapt,
    Reject
}

public enum DispositionReason
{
    FollowSuggestedDirection,
    NarrowSuggestedStep,
    CombineWithObservedEvidence,
    RetainCurrentDirection,
    EvidenceInsufficient,
    EvidenceConflicts,
    AlreadyAddressed,
    AdoptBeliefCorrection,
    AdaptBeliefCorrection,
    RetainCurrentBelief,
    TriggerNoLongerApplicable
}

public enum TargetId
{
    [JsonStringEnumMemberName("INC-1042")]
    Incident,
    [JsonStringEnumMemberName("payments-api")]
    PaymentsApi,
    [JsonStringEnumMemberName("authorization-service")]
    AuthorizationService
}

public enum ResearchOperation
{
    [JsonStringEnumMemberName("get_incident")]
    GetIncident,
    [JsonStringEnumMemberName("get_service_health")]
    GetServiceHealth,
    [JsonStringEnumMemberName("query_metrics")]
    QueryMetrics,
    [JsonStringEnumMemberName("query_logs")]
    QueryLogs
}

public enum BeliefState
{
    Provisional,
    Contested,
    Superseded,
    Retracted,
    Invalidated
}

public enum DirectionState
{
    Active,
    Expired,
    Superseded,
    Cleared,
    Invalidated,
    Closed
}

public enum ResearchPhase
{
    Setup,
    EndCheck,
    WorldDelivery,
    PriorDiagnosticResults,
    ReviewProcessing,
    ActorProcessing,
    ReviewStart,
    ActorStart,
    Drain
}

public enum ReasonDomain
{
    Contract,
    Context,
    Governance,
    Infrastructure
}

public sealed record Reason(ReasonDomain Domain, string Code, string? Message);

public sealed record MemoryDisposition(
    string TriggerId,
    string MemoryUpdateId,
    Stance Stance,
    DispositionReason Reason);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(QueryActorDecision), "query")]
[JsonDerivedType(typeof(WaitActorDecision), "wait")]
[JsonDerivedType(typeof(ReportActorDecision), "report")]
public abstract record ActorDecision(
    string SchemaVersion,
    string RecordType,
    MemoryDisposition? MemoryDisposition,
    IReadOnlyList<string> UsedBeliefIds) : IResearchRoot;

public sealed record QueryActorDecision(
    string SchemaVersion,
    string RecordType,
    MemoryDisposition? MemoryDisposition,
    IReadOnlyList<string> UsedBeliefIds,
    ResearchOperation Operation,
    TargetId TargetId,
    IReadOnlyDictionary<string, JsonElement> Arguments)
    : ActorDecision(SchemaVersion, RecordType, MemoryDisposition, UsedBeliefIds);

public sealed record WaitActorDecision(
    string SchemaVersion,
    string RecordType,
    MemoryDisposition? MemoryDisposition,
    IReadOnlyList<string> UsedBeliefIds,
    uint Ticks)
    : ActorDecision(SchemaVersion, RecordType, MemoryDisposition, UsedBeliefIds);

public sealed record ReportActorDecision(
    string SchemaVersion,
    string RecordType,
    MemoryDisposition? MemoryDisposition,
    IReadOnlyList<string> UsedBeliefIds,
    Hypothesis Hypothesis,
    IReadOnlyList<string> ObservationIds,
    NextStep NextStep,
    Uncertainty Uncertainty)
    : ActorDecision(SchemaVersion, RecordType, MemoryDisposition, UsedBeliefIds);

public sealed record Claim(
    Hypothesis Hypothesis,
    TargetId TargetId,
    IReadOnlyList<string> ObservationIds,
    Uncertainty Uncertainty);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ExistingBeliefRef), "existing")]
[JsonDerivedType(typeof(ProposedBeliefRef), "proposed")]
public abstract record BeliefRef;

public sealed record ExistingBeliefRef(string BeliefId) : BeliefRef;
public sealed record ProposedBeliefRef(string Key) : BeliefRef;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TargetFocus), "target")]
[JsonDerivedType(typeof(DiagnosticFocus), "diagnostic")]
public abstract record Focus;

public sealed record TargetFocus(TargetId TargetId) : Focus;

public sealed record DiagnosticFocus(
    ResearchOperation Operation,
    TargetId TargetId,
    IReadOnlyDictionary<string, JsonElement> Arguments) : Focus;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FocusDirectionChoice), "focus")]
[JsonDerivedType(typeof(HandoffDirectionChoice), "handoff")]
public abstract record DirectionChoice;

public sealed record FocusDirectionChoice(Focus Focus) : DirectionChoice;
public sealed record HandoffDirectionChoice(HandoffRecommendation Recommendation) : DirectionChoice;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(AddBeliefOperation), "add-belief")]
[JsonDerivedType(typeof(ReassertBeliefOperation), "reassert-belief")]
[JsonDerivedType(typeof(ReplaceBeliefOperation), "replace-belief")]
[JsonDerivedType(typeof(RetractBeliefOperation), "retract-belief")]
[JsonDerivedType(typeof(DeclareConflictOperation), "declare-conflict")]
[JsonDerivedType(typeof(SetDirectionOperation), "set-direction")]
[JsonDerivedType(typeof(ClearDirectionOperation), "clear-direction")]
public abstract record MemoryOperation;

public sealed record AddBeliefOperation(string Key, Claim Claim) : MemoryOperation;

public sealed record ReassertBeliefOperation(
    string PredecessorBeliefId,
    string Key,
    Claim Claim,
    string Reason) : MemoryOperation;

public sealed record ReplaceBeliefOperation(
    string BeliefId,
    string Key,
    Claim Claim) : MemoryOperation;

public sealed record RetractBeliefOperation(
    string BeliefId,
    IReadOnlyList<string> ObservationIds,
    string Reason) : MemoryOperation;

public sealed record DeclareConflictOperation(
    IReadOnlyList<BeliefRef> Beliefs,
    IReadOnlyList<string> ObservationIds,
    string Reason) : MemoryOperation;

public sealed record SetDirectionOperation(
    DirectionChoice Recommendation,
    IReadOnlyList<string> ObservationIds,
    IReadOnlyList<BeliefRef> SupportingBeliefs) : MemoryOperation;

public sealed record ClearDirectionOperation(
    IReadOnlyList<string> ObservationIds,
    string Reason) : MemoryOperation;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NoChangeSupervisorOutput), "no-change")]
[JsonDerivedType(typeof(ProposeMemoryUpdateSupervisorOutput), "propose-memory-update")]
public abstract record SupervisorOutput(
    string SchemaVersion,
    string RecordType,
    uint BaseMemoryRevision,
    IReadOnlyList<string> ObservationIds,
    string? Rationale,
    Uncertainty? Uncertainty) : IResearchRoot;

public sealed record NoChangeSupervisorOutput(
    string SchemaVersion,
    string RecordType,
    uint BaseMemoryRevision,
    IReadOnlyList<string> ObservationIds,
    string? Rationale,
    Uncertainty? Uncertainty)
    : SupervisorOutput(
        SchemaVersion,
        RecordType,
        BaseMemoryRevision,
        ObservationIds,
        Rationale,
        Uncertainty);

public sealed record ProposeMemoryUpdateSupervisorOutput(
    string SchemaVersion,
    string RecordType,
    uint BaseMemoryRevision,
    IReadOnlyList<string> ObservationIds,
    string? Rationale,
    Uncertainty? Uncertainty,
    IReadOnlyList<MemoryOperation> Operations)
    : SupervisorOutput(
        SchemaVersion,
        RecordType,
        BaseMemoryRevision,
        ObservationIds,
        Rationale,
        Uncertainty);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(NotificationObservationContent), "notification")]
[JsonDerivedType(typeof(IncidentObservationContent), "incident")]
[JsonDerivedType(typeof(ServiceHealthObservationContent), "service-health")]
[JsonDerivedType(typeof(MetricsObservationContent), "metrics")]
[JsonDerivedType(typeof(LogsObservationContent), "logs")]
[JsonDerivedType(typeof(UnavailableObservationContent), "unavailable")]
public abstract record ObservationContent;

public sealed record NotificationObservationContent(string Text) : ObservationContent;
public sealed record IncidentObservationContent(IncidentSnapshot Value) : ObservationContent;
public sealed record ServiceHealthObservationContent(ServiceHealthSnapshot Value) : ObservationContent;
public sealed record MetricsObservationContent(IReadOnlyList<MetricSample> Values) : ObservationContent;
public sealed record LogsObservationContent(IReadOnlyList<LogEntry> Values) : ObservationContent;
public sealed record UnavailableObservationContent(string Reason) : ObservationContent;

public sealed record Observation(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string ObservationId,
    IReadOnlyList<string> EvidenceIds,
    string SourceKind,
    string SourceId,
    TargetId TargetId,
    string? DiagnosticId,
    uint AvailableTick,
    uint? ObservedTick,
    uint? HistoryRevision,
    uint ApplicabilityEpoch,
    string Visibility,
    ObservationContent Content) : IResearchRoot;

public sealed record Belief(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string BeliefId,
    string CreatedByUpdateId,
    uint CreatedAtMemoryRevision,
    uint ApplicabilityEpoch,
    Claim Claim,
    string? ReassertedFromBeliefId) : IResearchRoot;

public sealed record BeliefStateEntry(
    string BeliefId,
    BeliefState State,
    string StateChangedByUpdateId,
    IReadOnlyList<string> ConflictingBeliefIds,
    string? SupersededByBeliefId);

public sealed record Direction(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string DirectionId,
    string CreatedByUpdateId,
    string ReviewId,
    uint ReviewStartTick,
    uint ExpiresAtTick,
    uint ApplicabilityEpoch,
    DirectionChoice Recommendation,
    IReadOnlyList<string> ObservationIds,
    IReadOnlyList<string> SupportingBeliefIds) : IResearchRoot;

public sealed record WorkingMemorySnapshot(
    string SchemaVersion,
    string RecordType,
    string RunId,
    uint MemoryRevision,
    uint? PreviousMemoryRevision,
    uint ApplicabilityEpoch,
    IReadOnlyList<BeliefStateEntry> BeliefStates,
    Direction? RecommendedDirection) : IResearchRoot;

public sealed record ReconsiderationTrigger(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string TriggerId,
    string MemoryUpdateId,
    uint CommittedMemoryRevision,
    uint DecisionGeneration,
    uint CreatedTick,
    string? PreviousTriggerId) : IResearchRoot;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(BeliefTriggerEffectView), "belief")]
[JsonDerivedType(typeof(DirectionTriggerEffectView), "direction")]
public abstract record TriggerEffectView;

public sealed record BeliefTriggerEffectView(
    string BeliefId,
    BeliefState State) : TriggerEffectView;

public sealed record DirectionTriggerEffectView(
    string DirectionId,
    DirectionState State) : TriggerEffectView;

public sealed record ReconsiderationRequirement(
    ReconsiderationTrigger Trigger,
    IReadOnlyList<string> EarlierTriggerIds,
    IReadOnlyList<TriggerEffectView> EffectsAtCapture);

public sealed record InputScope(string IncidentId, string ServiceId, string Goal);
public sealed record RemainingBudgets(uint ActorTurns, uint DiagnosticAttempts, uint Reviews);
public sealed record AllowedQuery(
    ResearchOperation Operation,
    TargetId TargetId,
    IReadOnlyDictionary<string, JsonElement> Arguments);

public sealed record ActionHistoryItem(
    string ActorTurnId,
    string SnapshotId,
    string ResultId,
    uint ConsumedMemoryRevision,
    JsonElement? Decision,
    string Disposition,
    Reason? Reason,
    string? DiagnosticId,
    IReadOnlyList<string> ObservationIds);

public sealed record ReviewHistoryItem(
    string ReviewId,
    string SnapshotId,
    string? ResultId,
    string Status,
    JsonElement? Output,
    string? MemoryUpdateId,
    Reason? Reason);

public sealed record InputSnapshot(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string SnapshotId,
    string Consumer,
    string? ActorTurnId,
    string? ReviewId,
    uint Tick,
    uint HistoryRevision,
    uint? ActionHistoryThroughSequence,
    uint ApplicabilityEpoch,
    uint DecisionGeneration,
    uint MemoryRevision,
    InputScope Scope,
    RemainingBudgets RemainingBudgets,
    IReadOnlyList<AllowedQuery> AllowedQueries,
    IReadOnlyList<Observation> Observations,
    IReadOnlyList<ActionHistoryItem> ActionHistory,
    IReadOnlyList<ReviewHistoryItem> ReviewHistory,
    WorkingMemorySnapshot WorkingMemory,
    IReadOnlyList<Belief> Beliefs,
    ReconsiderationRequirement? Reconsideration,
    InvestigativeDirection? InvestigativeDirection) : IResearchRoot;

public sealed record InvestigativeDirection(
    ResearchOperation Operation,
    TargetId TargetId);

public sealed record InvocationResult(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string ResultId,
    string Consumer,
    string? ActorTurnId,
    string? ReviewId,
    string SnapshotId,
    uint ConsumedMemoryRevision,
    uint CapturedGeneration,
    uint CapturedApplicabilityEpoch,
    uint? SettledTick,
    string Settlement,
    string ParseStatus,
    JsonElement? Output,
    string? RawOutputSha256,
    string? RedactedOutput,
    Reason? Reason) : IResearchRoot;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(SupervisorUpdateOrigin), "supervisor")]
[JsonDerivedType(typeof(DirectionExpiryUpdateOrigin), "direction-expiry")]
[JsonDerivedType(typeof(EpochInvalidationUpdateOrigin), "epoch-invalidation")]
public abstract record UpdateOrigin;

public sealed record SupervisorUpdateOrigin(
    string ReviewId,
    string ResultId,
    string SnapshotId) : UpdateOrigin;

public sealed record DirectionExpiryUpdateOrigin(string DirectionId) : UpdateOrigin;
public sealed record EpochInvalidationUpdateOrigin(uint CausedBySequence) : UpdateOrigin;

public sealed record NormalizationNote(
    uint OperationIndex,
    string Rule,
    string? ExistingBeliefId,
    string? ProposedKey,
    string? DuplicateDirectionId);

public sealed record ConflictEdge(string Left, string Right);
public sealed record BeliefLink(string FromBeliefId, string ToBeliefId);

public sealed record MemoryEffects(
    IReadOnlyList<string> CreatedBeliefIds,
    IReadOnlyList<BeliefLink> Supersessions,
    IReadOnlyList<BeliefLink> Reassertions,
    IReadOnlyList<string> RetractedBeliefIds,
    IReadOnlyList<string> InvalidatedBeliefIds,
    IReadOnlyList<ConflictEdge> AddedConflicts,
    IReadOnlyList<ConflictEdge> RemovedConflicts,
    string? DirectionBeforeId,
    string? DirectionAfterId,
    DirectionState? DirectionEndState);

public sealed record KeyBinding(string Key, string BeliefId);

public sealed record MemoryUpdate(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string MemoryUpdateId,
    UpdateOrigin Origin,
    uint BaseMemoryRevision,
    uint ObservedCurrentMemoryRevision,
    uint? CommittedMemoryRevision,
    string Outcome,
    bool Actionable,
    uint PreviousGeneration,
    uint NewGeneration,
    string? TriggerId,
    SupervisorOutput? Proposal,
    IReadOnlyList<NormalizationNote> Normalization,
    IReadOnlyList<KeyBinding> KeyBindings,
    MemoryEffects? Effects,
    IReadOnlyList<Reason> Errors) : IResearchRoot;

public sealed record ResearchEvent(
    string SchemaVersion,
    string RecordType,
    string RunId,
    uint Sequence,
    uint? Tick,
    ResearchPhase Phase,
    string EventType,
    IReadOnlyList<uint> CausedBySequences,
    uint CurrentGeneration,
    uint CurrentMemoryRevision,
    uint HistoryRevision,
    uint ApplicabilityEpoch,
    JsonElement Data) : IResearchRoot;

public sealed record DefinitionRef(string Id, string Sha256);
public sealed record StudySpecificationRef(string Baseline, string Amendment, string Candidate);
public sealed record SourceFileRef(string Path, string Sha256);

public sealed record ScriptedClock(
    string Kind,
    uint StartTick,
    uint EndTickExclusive,
    uint ActorTurnTicks,
    uint DiagnosticTicks,
    uint ReviewTicks,
    IReadOnlyList<uint> ReviewCheckpoints,
    uint ReviewTimeoutTicks,
    uint GuidanceLifetimeTicks,
    uint MaximumWaitTicks);

public sealed record ResearchLimits(
    uint ActorTurns,
    uint DiagnosticAttempts,
    uint Reviews,
    uint DispatchEligibleActorTurns,
    uint ActiveReviews);

public sealed record EngineeringLimits(
    uint MaximumComponentOutputBytes,
    uint MaximumOperationsPerUpdate,
    uint MaximumComponentTextBytes,
    uint MaximumCitationsPerField,
    uint MaximumInputSnapshotBytes,
    uint MaximumEventBytes);

public sealed record RuntimeVersions(
    string DotnetSdk,
    string AgentFrameworkWorkflows,
    string Node);

public sealed record RunManifest(
    string SchemaVersion,
    string RecordType,
    string RunId,
    StudySpecificationRef StudySpecification,
    string ContractCandidate,
    string Architecture,
    string Coordination,
    string Treatment,
    string ExecutionMode,
    string DataMode,
    string SourceRevision,
    bool SourceDirty,
    IReadOnlyList<SourceFileRef> SourceFiles,
    DefinitionRef ActorConfig,
    DefinitionRef? SupervisorConfig,
    DefinitionRef CaseDefinition,
    DefinitionRef ExternalSchedule,
    DefinitionRef CatalogueConfig,
    DefinitionRef GovernanceConfig,
    DefinitionRef MemoryRules,
    uint? Seed,
    DefinitionRef? FaultInjectionConfig,
    ScriptedClock Clock,
    ResearchLimits Limits,
    EngineeringLimits EngineeringLimits,
    RuntimeVersions RuntimeVersions,
    string ModelMeasurements) : IResearchRoot;

public sealed record ResourceCounts(
    uint ActorTurnsStarted,
    uint ReviewsStarted,
    uint DiagnosticAttempts,
    uint DiagnosticsDispatched,
    uint DiagnosticsCompleted,
    uint ActorResultsSuppressed,
    uint ReviewResultsSuppressed,
    uint CancellationRequests,
    uint CancellationsConfirmed,
    uint CancellationsUnsupported,
    uint CancellationRequestsFailed,
    uint CancellationsIgnored,
    uint ReportsSubmitted,
    uint MemoryUpdatesCommitted,
    uint MemoryUpdatesRejected,
    uint MemoryUpdatesNoOp,
    uint MemoryRevisionConflicts,
    uint RepeatedDirectionsNormalized,
    uint BeliefReassertionsCommitted,
    uint ReassertionInterrupts,
    uint ActionableCommits,
    uint ReconsiderationAcknowledgments);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "consumer")]
[JsonDerivedType(typeof(ActorInvocationRef), "actor")]
[JsonDerivedType(typeof(SupervisorInvocationRef), "supervisor")]
public abstract record InvocationRef;

public sealed record ActorInvocationRef(string ActorTurnId) : InvocationRef;
public sealed record SupervisorInvocationRef(string ReviewId) : InvocationRef;

public sealed record Termination(
    string SchemaVersion,
    string RecordType,
    string RunId,
    uint Tick,
    ResearchPhase Phase,
    string Kind,
    string? ReportResultId,
    Reason? Reason,
    uint FinalHistoryRevision,
    uint FinalApplicabilityEpoch,
    uint FinalGeneration,
    uint FinalMemoryRevision,
    string? FinalPendingTriggerId,
    bool SupervisionDegraded,
    ResourceCounts Counts,
    IReadOnlyList<InvocationRef> PendingInvocations,
    IReadOnlyList<string> PendingDiagnosticIds) : IResearchRoot;

public sealed record RunClosure(
    string SchemaVersion,
    string RecordType,
    string RunId,
    string Status,
    uint FinalMemoryRevision,
    ResourceCounts Counts,
    IReadOnlyList<InvocationRef> UnsettledInvocations,
    IReadOnlyList<string> UnsettledDiagnosticIds,
    Reason? Reason,
    string ModelMeasurements) : IResearchRoot;
