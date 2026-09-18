using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GovernedAgent.Research;

public enum SupervisionArchitecture
{
    ActorOnly,
    BlockingSupervision,
    AsynchronousSupervision
}

public sealed record GovernedDiagnosticBinding(
    Guid PlanId,
    string StepId,
    string PlanDigest,
    string ActionDigest,
    Guid? RequestId,
    string SessionId);

public sealed record GovernedDiagnosticRequest(
    string DiagnosticId,
    string ActorTurnId,
    string ResultId,
    QueryActorDecision Decision,
    uint RequestTick,
    uint CapturedGeneration,
    uint CapturedApplicabilityEpoch);

public sealed record GovernedDiagnosticAuthorization(
    bool Authorized,
    uint DispatchTick,
    ResearchPhase DispatchPhase);

public enum DiagnosticAuthorizationProcessingOutcome
{
    Authorized,
    Rejected,
    DiagnosticSettledWithoutAuthorization,
    EpisodeTerminated
}

internal enum DiagnosticAuthorizationLifecycle
{
    Requested,
    DiagnosticSettledWithoutAuthorization,
    EpisodeTerminated
}

public sealed record GovernedDiagnosticResult(
    string Status,
    uint DeliveryTick,
    Er1FixtureSample? Sample,
    GovernedDiagnosticBinding? Binding,
    IReadOnlyList<Guid> AuditRecordIds,
    Reason? Reason,
    GovernedDiagnosticFailureKind FailureKind =
        GovernedDiagnosticFailureKind.Reconsiderable);

public enum GovernedDiagnosticFailureKind
{
    Reconsiderable,
    GovernanceStop,
    InfrastructureFailure
}

public interface IGovernedDiagnosticDispatcher
{
    ValueTask<GovernedDiagnosticResult> DispatchAsync(
        GovernedDiagnosticRequest request,
        Func<
            GovernedDiagnosticBinding,
            CancellationToken,
            ValueTask<GovernedDiagnosticAuthorization>> authorizeDispatch,
        CancellationToken cancellationToken);
}

public sealed record CoordinatorActorStart(
    string ActorTurnId,
    InputSnapshot Snapshot,
    Task Started);

public sealed record CoordinatorReviewStart(
    string ReviewId,
    InputSnapshot Snapshot,
    Task Started);

public sealed class ScriptedSupervisionCoordinator : IAsyncDisposable
{
    private static readonly IReadOnlyList<AllowedQuery> AllowedQueries =
    [
        Allowed(ResearchOperation.GetIncident, TargetId.Incident),
        Allowed(ResearchOperation.GetServiceHealth, TargetId.PaymentsApi),
        Allowed(ResearchOperation.GetServiceHealth, TargetId.AuthorizationService),
        Allowed(ResearchOperation.QueryMetrics, TargetId.PaymentsApi),
        Allowed(ResearchOperation.QueryMetrics, TargetId.AuthorizationService),
        Allowed(ResearchOperation.QueryLogs, TargetId.PaymentsApi),
        Allowed(ResearchOperation.QueryLogs, TargetId.AuthorizationService)
    ];

    private readonly object _sync = new();
    private readonly SupervisionArchitecture _architecture;
    private readonly ScriptedExperimentScheduler _scheduler;
    private readonly ScriptedResourceBudget _budget;
    private readonly IsolatedAgentFrameworkRuntime _runtime;
    private readonly IGovernedDiagnosticDispatcher _diagnosticDispatcher;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly CoordinatorMemoryStore _memory;
    private readonly List<Observation> _observations = [];
    private readonly List<Observation> _postTerminationObservations = [];
    private readonly List<ActionHistoryItem> _actionHistory = [];
    private readonly List<ReviewHistoryItem> _reviewHistory = [];
    private readonly List<InvocationResult> _invocationResults = [];
    private readonly List<ResearchEvent> _events = [];
    private readonly Dictionary<string, ActorCall> _actors = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ReviewCall> _reviews = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DiagnosticCall> _diagnostics = new(StringComparer.Ordinal);
    private uint _actorNumber;
    private uint _reviewNumber;
    private uint _snapshotNumber;
    private uint _resultNumber;
    private uint _diagnosticNumber;
    private uint _observationNumber;
    private uint _generation;
    private uint _epoch;
    private uint? _waitUntilTick;
    private string? _waitActorTurnId;
    private string? _pendingTriggerId;
    private string? _latestActionableTriggerId;
    private string? _currentActorId;
    private string? _diagnosticReadinessOwnerId;
    private string? _activeReviewId;
    private InvestigativeDirection? _investigativeDirection;
    private bool _actorReady = true;
    private bool _supervisionDegraded;
    private Termination? _termination;
    private RunClosure? _closure;
    private MutableCounts _counts = new();

    public ScriptedSupervisionCoordinator(
        string runId,
        SupervisionArchitecture architecture,
        ScriptedExperimentScheduler scheduler,
        IsolatedAgentFrameworkRuntime runtime,
        IGovernedDiagnosticDispatcher diagnosticDispatcher)
    {
        if (!Guid.TryParseExact(runId, "D", out _))
        {
            throw new ArgumentException("Run identifier must be a UUID.", nameof(runId));
        }

        RunId = runId;
        _architecture = architecture;
        _scheduler = scheduler;
        _budget = scheduler.CreateResourceBudget();
        _runtime = runtime;
        _diagnosticDispatcher = diagnosticDispatcher;
        _memory = new CoordinatorMemoryStore(runId);
        Record(null, ResearchPhase.Setup, "run.started", new { });
    }

    public string RunId { get; }

    public uint DecisionGeneration { get { lock (_sync) { return _generation; } } }

    public uint ApplicabilityEpoch { get { lock (_sync) { return _epoch; } } }

    public uint MemoryRevision { get { lock (_sync) { return _memory.Current.MemoryRevision; } } }

    public string? PendingTriggerId { get { lock (_sync) { return _pendingTriggerId; } } }

    public bool SupervisionDegraded { get { lock (_sync) { return _supervisionDegraded; } } }

    public IReadOnlyList<Observation> Observations
    {
        get
        {
            lock (_sync)
            {
                return _observations
                    .Select(ResearchContractFreezer.Freeze)
                    .ToImmutableArray();
            }
        }
    }

    public IReadOnlyList<Observation> PostTerminationObservations
    {
        get
        {
            lock (_sync)
            {
                return _postTerminationObservations
                    .Select(ResearchContractFreezer.Freeze)
                    .ToImmutableArray();
            }
        }
    }

    public IReadOnlyList<ActionHistoryItem> ActionHistory
    {
        get { lock (_sync) { return _actionHistory.ToImmutableArray(); } }
    }

    public IReadOnlyList<ReviewHistoryItem> ReviewHistory
    {
        get { lock (_sync) { return _reviewHistory.ToImmutableArray(); } }
    }

    public IReadOnlyList<ResearchEvent> Events
    {
        get { lock (_sync) { return _events.ToImmutableArray(); } }
    }

    public IReadOnlyList<MemoryUpdate> MemoryUpdates
    {
        get
        {
            lock (_sync)
            {
                return _memory.Updates
                    .Select(ResearchContractFreezer.Freeze)
                    .ToImmutableArray();
            }
        }
    }

    public IReadOnlyList<Belief> Beliefs
    {
        get
        {
            lock (_sync)
            {
                return _memory.Beliefs
                    .Select(ResearchContractFreezer.Freeze)
                    .ToImmutableArray();
            }
        }
    }

    public WorkingMemorySnapshot WorkingMemory
    {
        get { lock (_sync) { return ResearchContractFreezer.Freeze(_memory.Current); } }
    }

    public Termination? Termination
    {
        get
        {
            lock (_sync)
            {
                return _termination is null
                    ? null
                    : ResearchContractFreezer.Freeze(_termination);
            }
        }
    }

    public void RecordExternalObservation(Er1FixtureSample sample, uint tick)
    {
        lock (_sync)
        {
            if (_termination is not null)
            {
                return;
            }

            RecordObservation(sample, diagnosticId: null, tick, ResearchPhase.WorldDelivery);
            if (_waitUntilTick is not null)
            {
                EndWait(tick, ResearchPhase.WorldDelivery, new Reason(
                    ReasonDomain.Context,
                    "shared-notification",
                    "A new observation became visible."));
            }
        }
    }

    public void ChangeEpoch(uint newEpoch, string sourceId, uint tick)
    {
        IsolatedFrameworkInvocation<ActorDecision>? actorToCancel = null;
        string? invalidatedActorId = null;
        CoordinatorMemoryTransition transition;
        lock (_sync)
        {
            if (_termination is not null)
            {
                return;
            }
            var epochEvent = Record(
                tick,
                ResearchPhase.WorldDelivery,
                "epoch.changed",
                new { previousEpoch = _epoch, newEpoch, sourceId });
            var previousGeneration = _generation;
            var previousPending = _pendingTriggerId;
            transition = _memory.InvalidateEpoch(
                newEpoch,
                epochEvent.Sequence,
                _generation,
                tick,
                _latestActionableTriggerId);
            _epoch = newEpoch;
            PublishTransition(
                transition,
                tick,
                ResearchPhase.WorldDelivery,
                previousGeneration,
                previousPending,
                ref actorToCancel,
                ref invalidatedActorId);
            if (_currentActorId is not null)
            {
                var actor = _actors[_currentActorId];
                if (actor.CapturedEpoch < newEpoch)
                {
                    if (!actor.SlotReleased)
                    {
                        _budget.InvalidateActorTurn();
                        actor.SlotReleased = true;
                    }

                    _currentActorId = null;
                    _actorReady = true;
                    actorToCancel = actor.Invocation;
                    invalidatedActorId = actor.ActorTurnId;
                }
            }

            if (_diagnosticReadinessOwnerId is not null &&
                _diagnostics[_diagnosticReadinessOwnerId].CapturedEpoch < newEpoch &&
                _currentActorId is null &&
                _waitUntilTick is null)
            {
                _diagnosticReadinessOwnerId = null;
                _actorReady = true;
            }
        }

        RequestCancellation(
            actorToCancel,
            invalidatedActorId,
            tick,
            ResearchPhase.WorldDelivery,
            new Reason(
                ReasonDomain.Context,
                "epoch-changed",
                "The applicability epoch changed after actor snapshot capture."));
    }

    public void ExpireDirection(uint tick)
    {
        lock (_sync)
        {
            if (_termination is not null)
            {
                return;
            }
            if (_memory.Current.RecommendedDirection is not { } direction ||
                tick < direction.ExpiresAtTick)
            {
                return;
            }

            var transition = _memory.ExpireDirection(tick, _generation);
            _counts.MemoryUpdatesCommitted++;
            RecordMemoryCommit(transition, tick, ResearchPhase.ReviewProcessing);
        }
    }

    public CoordinatorActorStart? StartActor(
        uint tick,
        Func<InputSnapshot, CancellationToken, ValueTask<ActorDecision>> component,
        InvocationCancellationCapability cancellationCapability =
            InvocationCancellationCapability.Supported)
    {
        IReadOnlyList<PendingCancellation> terminationCancellations = [];
        try
        {
            lock (_sync)
            {
                if (_termination is not null)
                {
                    return null;
                }

                ReleaseWaitIfDue(tick);
                if (!_actorReady ||
                    _currentActorId is not null ||
                    (_architecture == SupervisionArchitecture.BlockingSupervision &&
                        _activeReviewId is not null))
                {
                    return null;
                }

                if (!_budget.TryStartActorTurn())
                {
                    terminationCancellations = TerminateLocked(
                        tick,
                        ResearchPhase.ActorStart,
                        "budget-exhausted",
                        new Reason(
                            ReasonDomain.Governance,
                            "actor-turn-budget-exhausted",
                            "The actor-turn budget was exhausted."),
                        null);
                    return null;
                }

                var actorTurnId = $"actor-{++_actorNumber}";
                var snapshot = CreateSnapshot("actor", actorTurnId, null, tick);
                var invocation = _runtime.StartActor(
                    actorTurnId,
                    snapshot,
                    component,
                    cancellationCapability,
                    _lifetime.Token);
                var call = new ActorCall(
                    actorTurnId,
                    snapshot,
                    invocation,
                    _generation,
                    _epoch);
                _actors.Add(actorTurnId, call);
                _currentActorId = actorTurnId;
                _diagnosticReadinessOwnerId = null;
                _actorReady = false;
                _counts.ActorTurnsStarted++;
                Record(
                    tick,
                    ResearchPhase.ActorStart,
                    "actor.started",
                    new
                    {
                        actorTurnId,
                        snapshotId = snapshot.SnapshotId,
                        consumedMemoryRevision = snapshot.MemoryRevision,
                        requiredTriggerId = snapshot.Reconsideration?.Trigger.TriggerId
                    });
                return new CoordinatorActorStart(actorTurnId, snapshot, invocation.Started);
            }
        }
        finally
        {
            RequestTerminationCancellations(
                terminationCancellations,
                tick);
        }
    }

    public CoordinatorReviewStart? StartReview(
        uint tick,
        Func<InputSnapshot, CancellationToken, ValueTask<SupervisorOutput>> component,
        InvocationCancellationCapability cancellationCapability =
            InvocationCancellationCapability.Supported)
    {
        IReadOnlyList<PendingCancellation> terminationCancellations = [];
        try
        {
            lock (_sync)
            {
                if (_termination is not null ||
                    _architecture == SupervisionArchitecture.ActorOnly)
                {
                    return null;
                }

                var disposition = _budget.EvaluateReviewCheckpoint();
                if (disposition == ReviewCheckpointDisposition.SkipActiveReview)
                {
                    Record(
                        tick,
                        ResearchPhase.ReviewStart,
                        "review.checkpoint-skipped",
                        new { checkpointTick = tick, activeReviewId = _activeReviewId! });
                    return null;
                }

                if (disposition == ReviewCheckpointDisposition.ReviewBudgetExhausted)
                {
                    terminationCancellations = TerminateLocked(
                        tick,
                        ResearchPhase.ReviewStart,
                        "budget-exhausted",
                        new Reason(
                            ReasonDomain.Governance,
                            "review-budget-exhausted",
                            "The review budget was exhausted."),
                        null);
                    return null;
                }

                var reviewId = $"review-{++_reviewNumber}";
                var snapshot = CreateSnapshot("supervisor", null, reviewId, tick);
                var invocation = _runtime.StartSupervisor(
                    reviewId,
                    snapshot,
                    component,
                    cancellationCapability,
                    _lifetime.Token);
                var call = new ReviewCall(
                    reviewId,
                    snapshot,
                    invocation,
                    tick,
                    _scheduler.ReviewTimeoutTick(tick));
                _reviews.Add(reviewId, call);
                _activeReviewId = reviewId;
                _counts.ReviewsStarted++;
                Record(
                    tick,
                    ResearchPhase.ReviewStart,
                    "review.started",
                    new
                    {
                        reviewId,
                        snapshotId = snapshot.SnapshotId,
                        consumedMemoryRevision = snapshot.MemoryRevision,
                        timeoutAtTick = call.TimeoutTick
                    });
                return new CoordinatorReviewStart(reviewId, snapshot, invocation.Started);
            }
        }
        finally
        {
            RequestTerminationCancellations(
                terminationCancellations,
                tick);
        }
    }

    public async ValueTask ProcessActorResultAsync(
        string actorTurnId,
        uint tick,
        CancellationToken cancellationToken = default)
    {
        ActorCall call;
        lock (_sync)
        {
            call = _actors[actorTurnId];
        }

        var outcome = await call.Invocation.Settlement.WaitAsync(cancellationToken);
        QueryDispatchWork? dispatch = null;
        string? queryAttemptId = null;
        IReadOnlyList<PendingCancellation> terminationCancellations = [];
        try
        {
            lock (_sync)
            {
            if (call.Processed)
            {
                RecordDuplicateDelivery(
                    call,
                    outcome.Output,
                    tick,
                    ResearchPhase.ActorProcessing);
                return;
            }

            call.Processed = true;
            if (!call.SlotReleased)
            {
                _budget.CompleteActorTurn();
                call.SlotReleased = true;
            }

            var ownedCurrentReadiness = _currentActorId == actorTurnId;
            if (ownedCurrentReadiness)
            {
                _currentActorId = null;
            }

            var result = CreateInvocationResult(call, outcome, tick);
            call.Result = result;
            _invocationResults.Add(result);
            RecordActorSettlement(call, result, outcome, tick);
            if (call.CancellationAccepted && outcome.Settlement == "returned")
            {
                _counts.CancellationsIgnored++;
                Record(
                    tick,
                    PhaseAfterTermination(ResearchPhase.ActorProcessing),
                    "cancellation.ignored",
                    new InvocationResultEventData(
                        new ActorInvocationRef(actorTurnId),
                        result.ResultId));
            }
            else if (call.CancellationAccepted && outcome.Settlement == "cancelled")
            {
                _counts.CancellationsConfirmed++;
                Record(
                    tick,
                    PhaseAfterTermination(ResearchPhase.ActorProcessing),
                    "cancellation.confirmed",
                    new InvocationResultEventData(
                        new ActorInvocationRef(actorTurnId),
                        result.ResultId));
            }

            var obsolete = _termination is not null ||
                call.CapturedGeneration != _generation ||
                call.CapturedEpoch != _epoch;
            if (obsolete)
            {
                SuppressActor(call, result, tick, ObsoleteReason(call));
                if (_termination is null &&
                    ownedCurrentReadiness &&
                    _currentActorId is null &&
                    _waitUntilTick is null)
                {
                    _actorReady = true;
                }

                return;
            }

            if (outcome.Settlement != "returned" || outcome.Output is null)
            {
                terminationCancellations = TerminateLocked(
                    tick,
                    ResearchPhase.ActorProcessing,
                    "infrastructure-failure",
                    result.Reason,
                    null);
                return;
            }

            if (outcome.Output is QueryActorDecision)
            {
                if (!_budget.TryDispatchDiagnostic())
                {
                    terminationCancellations = TerminateLocked(
                        tick,
                        ResearchPhase.ActorProcessing,
                        "budget-exhausted",
                        new Reason(
                            ReasonDomain.Governance,
                            "diagnostic-budget-exhausted",
                            "The diagnostic-attempt budget was exhausted."),
                        null);
                    return;
                }

                queryAttemptId = $"diagnostic-{++_diagnosticNumber}";
                _counts.DiagnosticAttempts++;
                Record(
                    tick,
                    ResearchPhase.ActorProcessing,
                    "diagnostic.requested",
                    new
                    {
                        diagnosticId = queryAttemptId,
                        actorTurnId,
                        resultId = result.ResultId
                    });
            }

            if (result.ParseStatus != "well-formed")
            {
                var reason = result.Reason!;
                Record(
                    tick,
                    ResearchPhase.ActorProcessing,
                    "actor.rejected",
                    new
                    {
                        actorTurnId,
                        resultId = result.ResultId,
                        reason
                    });
                _actionHistory.Add(
                    ActionItem(
                        call,
                        result,
                        null,
                        "rejected",
                        reason,
                        queryAttemptId,
                        []));
                _actorReady = true;
                return;
            }

            var decision = ResearchContractFreezer.Freeze(
                (ActorDecision)ResearchContractSerializer.DeserializeRoot(
                    result.Output!.Value.GetRawText()));
            var contextualIssue = ValidateActorContext(call.Snapshot, decision);
            if (contextualIssue is not null)
            {
                Record(
                    tick,
                    ResearchPhase.ActorProcessing,
                    "actor.rejected",
                    new
                    {
                        actorTurnId,
                        resultId = result.ResultId,
                        reason = contextualIssue
                    });
                _actionHistory.Add(
                    ActionItem(
                        call,
                        result,
                        decision,
                        "rejected",
                        contextualIssue,
                        queryAttemptId,
                        []));
                _actorReady = true;
                return;
            }

            HandleMemoryDisposition(call, result, decision, tick);
            switch (decision)
            {
                case QueryActorDecision query:
                    var diagnostic = new DiagnosticCall(
                        queryAttemptId!,
                        call,
                        result,
                        query,
                        tick,
                        _generation,
                        _epoch);
                    _diagnostics.Add(queryAttemptId!, diagnostic);
                    _diagnosticReadinessOwnerId = queryAttemptId;
                    dispatch = new QueryDispatchWork(diagnostic, tick);
                    break;
                case WaitActorDecision wait:
                    var untilTick = checked(tick + wait.Ticks);
                    _scheduler.ValidateWait(tick, untilTick);
                    _actionHistory.Add(
                        ActionItem(call, result, decision, "applied", null, null, []));
                    if (_observations.Count > call.Snapshot.HistoryRevision)
                    {
                        _actorReady = true;
                        Record(
                            tick,
                            ResearchPhase.ActorProcessing,
                            "actor.wait-ended",
                            new
                            {
                                actorTurnId,
                                reason = new Reason(
                                    ReasonDomain.Context,
                                    "wake-already-observed",
                                    "A qualifying observation arrived after snapshot capture.")
                            });
                        break;
                    }

                    _waitUntilTick = untilTick;
                    _waitActorTurnId = actorTurnId;
                    Record(
                        tick,
                        ResearchPhase.ActorProcessing,
                        "actor.wait-started",
                        new
                        {
                            actorTurnId,
                            resultId = result.ResultId,
                            untilTick
                        });
                    break;
                case ReportActorDecision:
                    _actionHistory.Add(
                        ActionItem(call, result, decision, "applied", null, null, []));
                    _counts.ReportsSubmitted++;
                    Record(
                        tick,
                        ResearchPhase.ActorProcessing,
                        "report.submitted",
                        new
                        {
                            actorTurnId,
                            resultId = result.ResultId,
                            consumedMemoryRevision = call.Snapshot.MemoryRevision
                        });
                    terminationCancellations = TerminateLocked(
                        tick,
                        ResearchPhase.ActorProcessing,
                        "report",
                        null,
                        result.ResultId);
                    break;
            }
            }

            if (dispatch is not null)
            {
                BeginDiagnosticDispatch(dispatch, cancellationToken);
            }
        }
        finally
        {
            RequestTerminationCancellations(
                terminationCancellations,
                tick);
        }
    }

    public async ValueTask ProcessReviewResultAsync(
        string reviewId,
        uint tick,
        CancellationToken cancellationToken = default)
    {
        ReviewCall call;
        lock (_sync)
        {
            call = _reviews[reviewId];
        }

        var outcome = await call.Invocation.Settlement.WaitAsync(cancellationToken);
        IsolatedFrameworkInvocation<ActorDecision>? actorToCancel = null;
        string? invalidatedActorId = null;
        lock (_sync)
        {
            if (call.Processed)
            {
                RecordDuplicateDelivery(
                    call,
                    outcome.Output,
                    tick,
                    ResearchPhase.ReviewProcessing);
                return;
            }

            call.Processed = true;
            if (!call.BudgetReleased)
            {
                _budget.CompleteReview();
                call.BudgetReleased = true;
            }

            if (_activeReviewId == reviewId)
            {
                _activeReviewId = null;
            }

            var result = CreateInvocationResult(call, outcome, tick);
            call.Result = result;
            _invocationResults.Add(result);
            RecordReviewSettlement(call, result, outcome, tick);
            if (call.CancellationAccepted && outcome.Settlement == "returned")
            {
                _counts.CancellationsIgnored++;
                Record(
                    tick,
                    PhaseAfterTermination(ResearchPhase.ReviewProcessing),
                    "cancellation.ignored",
                    new InvocationResultEventData(
                        new SupervisorInvocationRef(reviewId),
                        result.ResultId));
            }
            else if (call.CancellationAccepted && outcome.Settlement == "cancelled")
            {
                _counts.CancellationsConfirmed++;
                Record(
                    tick,
                    PhaseAfterTermination(ResearchPhase.ReviewProcessing),
                    "cancellation.confirmed",
                    new InvocationResultEventData(
                        new SupervisorInvocationRef(reviewId),
                        result.ResultId));
            }

            if (_termination is not null ||
                call.TimedOut ||
                call.Snapshot.ApplicabilityEpoch != _epoch)
            {
                _counts.ReviewResultsSuppressed++;
                RecordSuppressed(
                    new SupervisorInvocationRef(reviewId),
                    result,
                    tick,
                    ResearchPhase.ReviewProcessing,
                    new Reason(
                        ReasonDomain.Context,
                        call.TimedOut ? "review-timed-out" : "obsolete-generation",
                        "The supervisor result is no longer eligible."));
                ReleaseBlockingActor();
                return;
            }

            if (outcome.Settlement != "returned" || outcome.Output is null)
            {
                _supervisionDegraded = true;
                _reviewHistory.Add(
                    new ReviewHistoryItem(
                        reviewId,
                        call.Snapshot.SnapshotId,
                        result.ResultId,
                        outcome.Settlement == "cancelled" ? "cancelled" : "failed",
                        null,
                        null,
                        result.Reason));
                ReleaseBlockingActor();
                return;
            }

            if (result.ParseStatus != "well-formed")
            {
                var reason = result.Reason!;
                _reviewHistory.Add(
                    new ReviewHistoryItem(
                        reviewId,
                        call.Snapshot.SnapshotId,
                        result.ResultId,
                        "completed",
                        null,
                        null,
                        reason));
                Record(
                    tick,
                    ResearchPhase.ReviewProcessing,
                    "review.rejected",
                    new { reviewId, resultId = result.ResultId, reason });
                ReleaseBlockingActor();
                return;
            }

            var output = ResearchContractFreezer.Freeze(
                (SupervisorOutput)ResearchContractSerializer.DeserializeRoot(
                    result.Output!.Value.GetRawText()));
            var structural = ResearchContractValidator.Validate(output);
            var contextIssue = ValidateReviewContext(call.Snapshot, output);
            if (structural.Count != 0 || contextIssue is not null)
            {
                var reason = contextIssue ??
                    new Reason(
                        ReasonDomain.Contract,
                        structural[0].Code,
                        structural[0].Message);
                _reviewHistory.Add(
                    new ReviewHistoryItem(
                        reviewId,
                        call.Snapshot.SnapshotId,
                        result.ResultId,
                        "completed",
                        null,
                        null,
                        reason));
                Record(
                    tick,
                    ResearchPhase.ReviewProcessing,
                    "review.rejected",
                    new { reviewId, resultId = result.ResultId, reason });
                ReleaseBlockingActor();
                return;
            }

            var outputJson = ToElement(output);
            if (output is NoChangeSupervisorOutput)
            {
                _reviewHistory.Add(
                    new ReviewHistoryItem(
                        reviewId,
                        call.Snapshot.SnapshotId,
                        result.ResultId,
                        "completed",
                        outputJson,
                        null,
                        null));
                Record(
                    tick,
                    ResearchPhase.ReviewProcessing,
                    "review.no-change",
                    new { reviewId, resultId = result.ResultId });
                ReleaseBlockingActor();
                return;
            }

            var proposal = (ProposeMemoryUpdateSupervisorOutput)output;
            Record(
                tick,
                ResearchPhase.ReviewProcessing,
                "memory.update-proposed",
                new
                {
                    reviewId,
                    resultId = result.ResultId,
                    baseMemoryRevision = proposal.BaseMemoryRevision
                });
            var previousGeneration = _generation;
            var previousPending = _pendingTriggerId;
            var transition = _memory.ApplySupervisorProposal(
                proposal,
                reviewId,
                result.ResultId,
                call.Snapshot.SnapshotId,
                call.StartTick,
                call.Snapshot.Observations
                    .Select(item => item.ObservationId)
                    .ToHashSet(StringComparer.Ordinal),
                _generation,
                tick,
                _latestActionableTriggerId);
            if (transition.Update.Outcome == "rejected")
            {
                _counts.MemoryUpdatesRejected++;
                if (transition.Update.Errors.Any(
                        reason => reason.Code == "memory-revision-conflict"))
                {
                    _counts.MemoryRevisionConflicts++;
                }

                Record(
                    tick,
                    ResearchPhase.ReviewProcessing,
                    "memory.update-rejected",
                    new { memoryUpdateId = transition.Update.MemoryUpdateId });
            }
            else if (transition.Update.Outcome == "no-op")
            {
                _counts.MemoryUpdatesNoOp++;
                _counts.RepeatedDirectionsNormalized += checked((uint)
                    transition.Update.Normalization.Count(
                        note => note.Rule == "drop-repeated-direction"));
                Record(
                    tick,
                    ResearchPhase.ReviewProcessing,
                    "memory.update-no-op",
                    new { memoryUpdateId = transition.Update.MemoryUpdateId });
            }
            else
            {
                PublishTransition(
                    transition,
                    tick,
                    ResearchPhase.ReviewProcessing,
                    previousGeneration,
                    previousPending,
                    ref actorToCancel,
                    ref invalidatedActorId);
            }

            _reviewHistory.Add(
                new ReviewHistoryItem(
                    reviewId,
                    call.Snapshot.SnapshotId,
                    result.ResultId,
                    "completed",
                    outputJson,
                    transition.Update.MemoryUpdateId,
                    null));
            ReleaseBlockingActor();
        }

        RequestCancellation(
            actorToCancel,
            invalidatedActorId,
            tick,
            ResearchPhase.ReviewProcessing,
            new Reason(
                ReasonDomain.Context,
                "memory-update-interrupt",
                "An actionable memory update invalidated the actor generation."));
    }

    public void ProcessReviewTimeout(string reviewId, uint tick)
    {
        lock (_sync)
        {
            var call = _reviews[reviewId];
            if (_termination is not null ||
                call.Processed ||
                call.TimedOut ||
                tick < call.TimeoutTick)
            {
                return;
            }

            call.TimedOut = true;
            if (!call.BudgetReleased)
            {
                _budget.CompleteReview();
                call.BudgetReleased = true;
            }

            if (_activeReviewId == reviewId)
            {
                _activeReviewId = null;
            }

            _supervisionDegraded = true;
            var reason = new Reason(
                ReasonDomain.Infrastructure,
                "supervisor-invocation-failed",
                "The review exceeded its exclusive timeout.");
            _reviewHistory.Add(
                new ReviewHistoryItem(
                    reviewId,
                    call.Snapshot.SnapshotId,
                    null,
                    "timed-out",
                    null,
                    null,
                    reason));
            Record(
                tick,
                ResearchPhase.ReviewProcessing,
                "review.timed-out",
                new { reviewId, timeoutAtTick = call.TimeoutTick });
            ReleaseBlockingActor();
        }
    }

    public void ReleaseWaitIfDue(uint tick)
    {
        lock (_sync)
        {
            if (_termination is null &&
                _waitUntilTick is not null &&
                tick >= _waitUntilTick)
            {
                EndWait(
                    tick,
                    ResearchPhase.ActorStart,
                    new Reason(
                        ReasonDomain.Context,
                        "wait-timer",
                        "The requested wait duration elapsed."));
            }
        }
    }

    public void Terminate(
        uint tick,
        ResearchPhase phase,
        string kind,
        Reason? reason = null)
    {
        IReadOnlyList<PendingCancellation> pending;
        lock (_sync)
        {
            pending = TerminateLocked(tick, phase, kind, reason, null);
        }

        RequestTerminationCancellations(pending, tick);
    }

    public RunClosure Close()
    {
        lock (_sync)
        {
            if (_termination is null)
            {
                throw new InvalidOperationException("The run must terminate before closure.");
            }

            if (_closure is not null)
            {
                return ResearchContractFreezer.Freeze(_closure);
            }

            var unsettled = _actors.Values
                .Where(item => !item.Processed)
                .Select(item => (InvocationRef)new ActorInvocationRef(item.ActorTurnId))
                .Concat(
                    _reviews.Values
                        .Where(item => !item.Processed)
                        .Select(item => (InvocationRef)new SupervisorInvocationRef(item.ReviewId)))
                .ToList();
            var unsettledDiagnostics = _diagnostics.Values
                .Where(item => !item.Completed)
                .Select(item => item.DiagnosticId)
                .ToList();
            _closure = ResearchContractFreezer.Freeze(new RunClosure(
                ResearchContractVersions.SchemaVersion,
                "run-closure",
                RunId,
                unsettled.Count == 0 && unsettledDiagnostics.Count == 0
                    ? "complete"
                    : "incomplete",
                _memory.Current.MemoryRevision,
                _counts.Snapshot(),
                unsettled,
                unsettledDiagnostics,
                unsettled.Count == 0 && unsettledDiagnostics.Count == 0
                    ? null
                    : new Reason(
                        ReasonDomain.Infrastructure,
                        "drain-incomplete",
                        "Bounded cleanup ended with unsettled work."),
                "unavailable-scripted"));
            ResearchContractValidator.ValidateAndThrow(_closure);
            Record(null, ResearchPhase.Drain, "run.closed", new { });
            return ResearchContractFreezer.Freeze(_closure);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _lifetime.Cancel();
        var invocations = _actors.Values
            .Select(item => (IAsyncDisposable)item.Invocation)
            .Concat(_reviews.Values.Select(item => (IAsyncDisposable)item.Invocation))
            .ToArray();
        foreach (var invocation in invocations)
        {
            try
            {
                await invocation.DisposeAsync();
            }
            catch (OperationCanceledException)
            {
            }
        }

        _lifetime.Dispose();
    }

    private void BeginDiagnosticDispatch(
        QueryDispatchWork work,
        CancellationToken cancellationToken)
    {
        var request = new GovernedDiagnosticRequest(
            work.Call.DiagnosticId,
            work.Call.Actor.ActorTurnId,
            work.Call.Result.ResultId,
            work.Call.Decision,
            work.Tick,
            work.Call.CapturedGeneration,
            work.Call.CapturedEpoch);
        try
        {
            work.Call.CompletionTask =
                DispatchAndTrackAuthorizationLifecycleAsync(
                    work.Call,
                    request,
                    cancellationToken);
        }
        catch (Exception exception)
        {
            work.Call.CompletionTask =
                Task.FromException<GovernedDiagnosticResult>(exception);
        }
    }

    private async Task<GovernedDiagnosticResult>
        DispatchAndTrackAuthorizationLifecycleAsync(
            DiagnosticCall diagnostic,
            GovernedDiagnosticRequest request,
            CancellationToken cancellationToken)
    {
        try
        {
            return await _diagnosticDispatcher.DispatchAsync(
                request,
                (binding, token) =>
                    AwaitScheduledAuthorizationAsync(diagnostic, binding, token),
                cancellationToken);
        }
        finally
        {
            diagnostic.AuthorizationLifecycle.TrySetResult(
                DiagnosticAuthorizationLifecycle
                    .DiagnosticSettledWithoutAuthorization);
        }
    }

    public async ValueTask ProcessDiagnosticResultAsync(
        string diagnosticId,
        uint tick,
        ResearchPhase handlingPhase,
        CancellationToken cancellationToken = default)
    {
        DiagnosticCall diagnostic;
        Task<GovernedDiagnosticResult> completionTask;
        lock (_sync)
        {
            diagnostic = _diagnostics[diagnosticId];
            completionTask = diagnostic.CompletionTask ??
                throw new InvalidOperationException(
                    "Diagnostic dispatch has not been initiated.");
        }

        GovernedDiagnosticResult completion;
        try
        {
            completion = await completionTask.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            completion = new GovernedDiagnosticResult(
                "failed",
                tick,
                null,
                diagnostic.Binding,
                [],
                new Reason(
                    ReasonDomain.Infrastructure,
                    "diagnostic-execution-failed",
                    exception.GetType().Name),
                GovernedDiagnosticFailureKind.InfrastructureFailure);
        }

        IReadOnlyList<PendingCancellation> terminationCancellations = [];
        lock (_sync)
        {
            if (diagnostic.Completed)
            {
                Record(
                    tick,
                    PhaseAfterTermination(handlingPhase),
                    "diagnostic.duplicate-delivery",
                    new
                    {
                        diagnosticId = diagnostic.DiagnosticId,
                        originalCompletionSequence = diagnostic.CompletionSequence!.Value
                    });
                return;
            }

            diagnostic.Completed = true;
            diagnostic.Binding ??= completion.Binding;
            if (completion.Status == "completed" &&
                completion.Sample is not null &&
                diagnostic.Dispatched)
            {
                var observation = _termination is null
                    ? RecordObservation(
                        completion.Sample,
                        diagnostic.DiagnosticId,
                        tick,
                        handlingPhase)
                    : RecordPostTerminationObservation(
                        completion.Sample,
                        diagnostic.DiagnosticId);
                var completed = Record(
                    tick,
                    PhaseAfterTermination(handlingPhase),
                    "diagnostic.completed",
                    new
                    {
                        diagnosticId = diagnostic.DiagnosticId,
                        observationId = observation.ObservationId,
                        auditRecordIds = completion.AuditRecordIds
                    });
                diagnostic.CompletionSequence = completed.Sequence;
                if (_termination is null)
                {
                    _counts.DiagnosticsCompleted++;
                    _actionHistory.Add(
                        ActionItem(
                            diagnostic.Actor,
                            diagnostic.Result,
                            diagnostic.Decision,
                            "applied",
                            null,
                            diagnostic.DiagnosticId,
                            [observation.ObservationId]));
                    ReleaseDiagnosticReadiness(diagnostic);
                }

                return;
            }

            var reason = completion.Reason ??
                new Reason(
                    ReasonDomain.Context,
                    "obsolete-generation",
                    "The diagnostic was suppressed before dispatch.");
            var eventType = completion.Status == "failed"
                ? "diagnostic.failed"
                : "diagnostic.rejected";
            var rejected = Record(
                tick,
                PhaseAfterTermination(handlingPhase),
                eventType,
                new
                {
                    diagnosticId = diagnostic.DiagnosticId,
                    reason,
                    binding = completion.Binding,
                    auditRecordIds = completion.AuditRecordIds
                });
            diagnostic.CompletionSequence = rejected.Sequence;
            if (_termination is null)
            {
                var failureKind = ClassifyDiagnosticFailure(completion);
                if (failureKind is not null)
                {
                    terminationCancellations = TerminateLocked(
                        tick,
                        handlingPhase,
                        failureKind,
                        reason,
                        null);
                }
                else
                {
                    _actionHistory.Add(
                        ActionItem(
                            diagnostic.Actor,
                            diagnostic.Result,
                            diagnostic.Decision,
                            "suppressed",
                            reason,
                            diagnostic.DiagnosticId,
                            []));
                    if (reason.Code == "obsolete-generation")
                    {
                        _counts.ActorResultsSuppressed++;
                        RecordSuppressed(
                            new ActorInvocationRef(diagnostic.Actor.ActorTurnId),
                            diagnostic.Result,
                            tick,
                            handlingPhase,
                            reason);
                    }

                    ReleaseDiagnosticReadiness(diagnostic);
                }
            }
        }

        RequestTerminationCancellations(
            terminationCancellations,
            tick);
    }

    private static string? ClassifyDiagnosticFailure(
        GovernedDiagnosticResult completion)
    {
        if (completion.Reason?.Code is "kill_switch_active" or
            "verifier_unavailable" or
            "policy_unavailable" or
            "audit_unavailable")
        {
            return "governance-stop";
        }

        return completion.FailureKind switch
        {
            GovernedDiagnosticFailureKind.GovernanceStop => "governance-stop",
            GovernedDiagnosticFailureKind.InfrastructureFailure =>
                "infrastructure-failure",
            _ => null
        };
    }

    public async ValueTask<DiagnosticAuthorizationProcessingOutcome>
        ProcessDiagnosticAuthorizationAsync(
        string diagnosticId,
        uint tick,
        ResearchPhase dispatchPhase,
        CancellationToken cancellationToken = default)
    {
        DiagnosticCall diagnostic;
        lock (_sync)
        {
            diagnostic = _diagnostics[diagnosticId];
        }

        var lifecycle = await diagnostic.AuthorizationLifecycle.Task
            .WaitAsync(cancellationToken);
        if (lifecycle ==
            DiagnosticAuthorizationLifecycle.DiagnosticSettledWithoutAuthorization)
        {
            return DiagnosticAuthorizationProcessingOutcome
                .DiagnosticSettledWithoutAuthorization;
        }

        if (lifecycle == DiagnosticAuthorizationLifecycle.EpisodeTerminated)
        {
            return DiagnosticAuthorizationProcessingOutcome.EpisodeTerminated;
        }

        return AuthorizeScheduledDispatch(diagnostic, tick, dispatchPhase)
            .Authorized
            ? DiagnosticAuthorizationProcessingOutcome.Authorized
            : DiagnosticAuthorizationProcessingOutcome.Rejected;
    }

    private static async ValueTask<GovernedDiagnosticAuthorization>
        AwaitScheduledAuthorizationAsync(
            DiagnosticCall diagnostic,
            GovernedDiagnosticBinding binding,
            CancellationToken cancellationToken)
    {
            diagnostic.PendingBinding = binding;
            diagnostic.AuthorizationLifecycle.TrySetResult(
                DiagnosticAuthorizationLifecycle.Requested);
        return await diagnostic.AuthorizationBoundary.Task.WaitAsync(cancellationToken);
    }

    private GovernedDiagnosticAuthorization AuthorizeScheduledDispatch(
        DiagnosticCall diagnostic,
        uint tick,
        ResearchPhase dispatchPhase)
    {
        GovernedDiagnosticAuthorization authorization;
        lock (_sync)
        {
            if (_termination is not null ||
                diagnostic.Dispatched ||
                diagnostic.CapturedGeneration != _generation ||
                diagnostic.CapturedEpoch != _epoch)
            {
                authorization = new GovernedDiagnosticAuthorization(
                    false,
                    tick,
                    dispatchPhase);
            }
            else
            {
                var binding = diagnostic.PendingBinding ??
                    throw new InvalidOperationException(
                        "The dispatcher did not provide its governed binding.");
                diagnostic.Dispatched = true;
                diagnostic.DispatchTick = tick;
                diagnostic.DispatchPhase = dispatchPhase;
                diagnostic.Binding = binding;
                _counts.DiagnosticsDispatched++;
                _investigativeDirection = new InvestigativeDirection(
                    diagnostic.Decision.Operation,
                    diagnostic.Decision.TargetId);
                Record(
                    tick,
                    dispatchPhase,
                    "diagnostic.dispatched",
                    new
                    {
                        diagnosticId = diagnostic.DiagnosticId,
                        actorTurnId = diagnostic.Actor.ActorTurnId,
                        resultId = diagnostic.Result.ResultId,
                        consumedMemoryRevision = diagnostic.Actor.Snapshot.MemoryRevision,
                        binding
                    });
                authorization = new GovernedDiagnosticAuthorization(
                    true,
                    tick,
                    dispatchPhase);
            }
        }

        diagnostic.AuthorizationBoundary.TrySetResult(authorization);
        return authorization;
    }

    private void PublishTransition(
        CoordinatorMemoryTransition transition,
        uint tick,
        ResearchPhase phase,
        uint previousGeneration,
        string? previousPending,
        ref IsolatedFrameworkInvocation<ActorDecision>? actorToCancel,
        ref string? invalidatedActorId)
    {
        _counts.MemoryUpdatesCommitted++;
        if (transition.Update.Actionable)
        {
            _counts.ActionableCommits++;
            _generation = transition.Update.NewGeneration;
            _pendingTriggerId = transition.Trigger!.TriggerId;
            _latestActionableTriggerId = transition.Trigger.TriggerId;
        }

        RecordMemoryCommit(transition, tick, phase);
        if (!transition.Update.Actionable)
        {
            return;
        }

        Record(
            tick,
            phase,
            "reconsideration.required",
            new
            {
                triggerId = transition.Trigger!.TriggerId,
                previousPendingTriggerId = previousPending
            });
        if (_waitUntilTick is not null)
        {
            EndWait(
                tick,
                phase,
                new Reason(
                    ReasonDomain.Context,
                    "memory-update-interrupt",
                    "Actionable memory interrupted the current wait."));
        }

        if (_currentActorId is not null)
        {
            var actor = _actors[_currentActorId];
            actor.Invalidated = true;
            if (!actor.SlotReleased)
            {
                _budget.InvalidateActorTurn();
                actor.SlotReleased = true;
            }

            Record(
                tick,
                phase,
                "actor.invalidated",
                new
                {
                    actorTurnId = actor.ActorTurnId,
                    snapshotId = actor.Snapshot.SnapshotId,
                    triggerId = transition.Trigger.TriggerId,
                    previousGeneration,
                    newGeneration = _generation
                });
            _currentActorId = null;
            _actorReady = true;
            actorToCancel = actor.Invocation;
            invalidatedActorId = actor.ActorTurnId;
        }

        foreach (var diagnostic in _diagnostics.Values.Where(
                     item =>
                         !item.Completed &&
                         item.CapturedGeneration < _generation))
        {
            if (!diagnostic.Actor.Invalidated)
            {
                diagnostic.Actor.Invalidated = true;
                Record(
                    tick,
                    phase,
                    "actor.invalidated",
                    new
                    {
                        actorTurnId = diagnostic.Actor.ActorTurnId,
                        snapshotId = diagnostic.Actor.Snapshot.SnapshotId,
                        triggerId = transition.Trigger!.TriggerId,
                        previousGeneration = diagnostic.CapturedGeneration,
                        newGeneration = _generation
                    });
            }

            if (_diagnosticReadinessOwnerId == diagnostic.DiagnosticId)
            {
                _diagnosticReadinessOwnerId = null;
                _actorReady = true;
            }
        }
    }

    private void RecordMemoryCommit(
        CoordinatorMemoryTransition transition,
        uint tick,
        ResearchPhase phase)
    {
        Record(
            tick,
            phase,
            "memory.committed",
            new
            {
                memoryUpdateId = transition.Update.MemoryUpdateId,
                previousMemoryRevision = transition.Update.BaseMemoryRevision,
                newMemoryRevision = transition.Update.CommittedMemoryRevision!.Value,
                previousGeneration = transition.Update.PreviousGeneration,
                newGeneration = transition.Update.NewGeneration,
                actionable = transition.Update.Actionable,
                triggerId = transition.Update.TriggerId
            });
        foreach (var change in transition.StateChanges)
        {
            Record(
                tick,
                phase,
                "belief.state-changed",
                new
                {
                    memoryUpdateId = transition.Update.MemoryUpdateId,
                    beliefId = change.Current.BeliefId,
                    previousState = change.Previous?.State,
                    newState = change.Current.State
                });
        }

        foreach (var reassertion in transition.Reassertions)
        {
            _counts.BeliefReassertionsCommitted++;
            if (transition.Update.Actionable)
            {
                _counts.ReassertionInterrupts++;
            }

            Record(
                tick,
                phase,
                "belief.reasserted",
                new
                {
                    memoryUpdateId = transition.Update.MemoryUpdateId,
                    predecessorBeliefId = reassertion.PredecessorId,
                    beliefId = reassertion.BeliefId,
                    triggerId = transition.Update.TriggerId
                });
        }

        if (transition.DirectionEndedId is not null)
        {
            Record(
                tick,
                phase,
                "direction.ended",
                new
                {
                    memoryUpdateId = transition.Update.MemoryUpdateId,
                    directionId = transition.DirectionEndedId,
                    state = transition.DirectionEndState!.Value
                });
        }
    }

    private void RequestCancellation(
        IsolatedFrameworkInvocation<ActorDecision>? invocation,
        string? actorTurnId,
        uint tick,
        ResearchPhase phase,
        Reason reason)
    {
        if (invocation is null || actorTurnId is null)
        {
            return;
        }

        lock (_sync)
        {
            _counts.CancellationRequests++;
            Record(
                tick,
                phase,
                "cancellation.requested",
                new InvocationReasonEventData(
                    new ActorInvocationRef(actorTurnId),
                    reason));
        }

        var result = invocation.RequestCancellation();
        lock (_sync)
        {
            switch (result)
            {
                case InvocationCancellationRequest.Requested:
                    _actors[actorTurnId].CancellationAccepted = true;
                    break;
                case InvocationCancellationRequest.Unsupported:
                    _counts.CancellationsUnsupported++;
                    Record(
                        tick,
                        phase,
                        "cancellation.unsupported",
                        new InvocationReasonEventData(
                            new ActorInvocationRef(actorTurnId),
                            new Reason(
                                ReasonDomain.Infrastructure,
                                "cancellation-unsupported",
                                "The invocation does not support cancellation.")));
                    break;
                case InvocationCancellationRequest.Failed:
                    _counts.CancellationRequestsFailed++;
                    Record(
                        tick,
                        phase,
                        "cancellation.failed",
                        new InvocationReasonEventData(
                            new ActorInvocationRef(actorTurnId),
                            new Reason(
                                ReasonDomain.Infrastructure,
                                "cancellation-request-failed",
                                "The best-effort cancellation request failed.")));
                    break;
            }
        }

    }

    private void ReleaseDiagnosticReadiness(DiagnosticCall diagnostic)
    {
        if (_diagnosticReadinessOwnerId == diagnostic.DiagnosticId &&
            _currentActorId is null &&
            _waitUntilTick is null)
        {
            _diagnosticReadinessOwnerId = null;
            _actorReady = true;
        }
    }

    private void HandleMemoryDisposition(
        ActorCall call,
        InvocationResult result,
        ActorDecision decision,
        uint tick)
    {
        if (decision.MemoryDisposition is null)
        {
            return;
        }

        Record(
            tick,
            ResearchPhase.ActorProcessing,
            "actor.memory-disposition",
            new
            {
                actorTurnId = call.ActorTurnId,
                resultId = result.ResultId,
                consumedMemoryRevision = call.Snapshot.MemoryRevision,
                disposition = decision.MemoryDisposition
            });
        if (_pendingTriggerId == decision.MemoryDisposition.TriggerId)
        {
            _pendingTriggerId = null;
            _counts.ReconsiderationAcknowledgments++;
            Record(
                tick,
                ResearchPhase.ActorProcessing,
                "reconsideration.acknowledged",
                new
                {
                    triggerId = decision.MemoryDisposition.TriggerId,
                    actorTurnId = call.ActorTurnId,
                    resultId = result.ResultId
                });
        }
    }

    private Reason? ValidateActorContext(InputSnapshot snapshot, ActorDecision decision)
    {
        var structural = ResearchContractValidator.Validate(decision);
        if (structural.Count != 0)
        {
            return new Reason(
                ReasonDomain.Contract,
                structural[0].Code,
                structural[0].Message);
        }

        var eligibleBeliefs = snapshot.WorkingMemory.BeliefStates
            .Where(item => item.State is BeliefState.Provisional or BeliefState.Contested)
            .Select(item => item.BeliefId)
            .ToHashSet(StringComparer.Ordinal);
        if (decision.UsedBeliefIds.Any(id => !eligibleBeliefs.Contains(id)))
        {
            return new Reason(
                ReasonDomain.Context,
                "belief-not-in-snapshot",
                "The actor cited a belief absent from its eligible snapshot.");
        }

        if (decision is ReportActorDecision report)
        {
            var observations = snapshot.Observations
                .Select(item => item.ObservationId)
                .ToHashSet(StringComparer.Ordinal);
            if (report.ObservationIds.Any(id => !observations.Contains(id)))
            {
                return new Reason(
                    ReasonDomain.Context,
                    "observation-not-in-snapshot",
                    "The report cited an observation absent from its snapshot.");
            }
        }

        var required = snapshot.Reconsideration?.Trigger;
        if (required is null && decision.MemoryDisposition is not null)
        {
            return new Reason(
                ReasonDomain.Context,
                "trigger-mismatch",
                "No reconsideration trigger was captured by this actor turn.");
        }

        if (required is not null &&
            (decision.MemoryDisposition is null ||
                decision.MemoryDisposition.TriggerId != required.TriggerId ||
                decision.MemoryDisposition.MemoryUpdateId != required.MemoryUpdateId))
        {
            return new Reason(
                ReasonDomain.Contract,
                "missing-memory-disposition",
                "The actor did not return the exact captured reconsideration disposition.");
        }

        return null;
    }

    private static Reason? ValidateReviewContext(
        InputSnapshot snapshot,
        SupervisorOutput output)
    {
        if (output.BaseMemoryRevision != snapshot.MemoryRevision)
        {
            return new Reason(
                ReasonDomain.Context,
                "memory-base-mismatch",
                "The supervisor output base differs from its immutable snapshot.");
        }

        var observations = snapshot.Observations
            .Select(item => item.ObservationId)
            .ToHashSet(StringComparer.Ordinal);
        if (output.ObservationIds.Any(id => !observations.Contains(id)))
        {
            return new Reason(
                ReasonDomain.Context,
                "observation-not-in-snapshot",
                "The supervisor cited an observation absent from its snapshot.");
        }

        return null;
    }

    private InputSnapshot CreateSnapshot(
        string consumer,
        string? actorTurnId,
        string? reviewId,
        uint tick)
    {
        var snapshot = ResearchContractFreezer.Freeze(new InputSnapshot(
            ResearchContractVersions.SchemaVersion,
            "input-snapshot",
            RunId,
            $"snapshot-{++_snapshotNumber}",
            consumer,
            actorTurnId,
            reviewId,
            tick,
            checked((uint)_observations.Count),
            _events.Count == 0 ? null : checked((uint)_events.Count),
            _epoch,
            _generation,
            _memory.Current.MemoryRevision,
            new InputScope(
                Er1EvidenceFixture.IncidentId,
                Er1EvidenceFixture.PaymentsServiceId,
                "Diagnose the incident and report a justified next step."),
            new RemainingBudgets(
                ApprovedScriptedExperiment.Limits.ActorTurns - _budget.ActorTurns,
                ApprovedScriptedExperiment.Limits.DiagnosticAttempts - _budget.DiagnosticAttempts,
                ApprovedScriptedExperiment.Limits.Reviews - _budget.Reviews),
            AllowedQueries,
            _observations.ToArray(),
            _actionHistory.ToArray(),
            _reviewHistory.ToArray(),
            _memory.Current,
            _memory.Beliefs.ToArray(),
            consumer == "actor" && _pendingTriggerId is not null
                ? _memory.CaptureRequirement(_pendingTriggerId)
                : null,
            _investigativeDirection));
        ResearchContractValidator.ValidateAndThrow(snapshot);
        return snapshot;
    }

    private Observation RecordObservation(
        Er1FixtureSample sample,
        string? diagnosticId,
        uint tick,
        ResearchPhase phase)
    {
        var observation = ResearchContractFreezer.Freeze(sample.CreateObservation(
            RunId,
            $"observation-{++_observationNumber}",
            diagnosticId,
            tick,
            checked((uint)_observations.Count + 1)));
        _observations.Add(observation);
        Record(
            tick,
            phase,
            "observation.recorded",
            new { observationId = observation.ObservationId });
        return observation;
    }

    private Observation RecordPostTerminationObservation(
        Er1FixtureSample sample,
        string diagnosticId)
    {
        var observation = ResearchContractFreezer.Freeze(
            sample.CreatePostTerminationObservation(
            RunId,
            $"observation-{++_observationNumber}",
            diagnosticId));
        _postTerminationObservations.Add(observation);
        return observation;
    }

    private InvocationResult CreateInvocationResult<T>(
        InvocationCall<T> call,
        IsolatedInvocationOutcome<T> outcome,
        uint tick)
        where T : class, IResearchRoot
    {
        JsonElement? output = null;
        string? digest = null;
        string? redacted = null;
        Reason? reason = null;
        var parseStatus = "not-attempted";
        if (outcome.Settlement == "returned" && outcome.Output is not null)
        {
            string json;
            try
            {
                json = ResearchContractSerializer.Serialize(outcome.Output);
                output = JsonDocument.Parse(json).RootElement.Clone();
                parseStatus = "well-formed";
            }
            catch (ResearchContractException exception)
            {
                json = SerializeUnvalidatedOutput(outcome.Output);
                parseStatus = "invalid";
                reason = new Reason(
                    ContractFailureDomain(exception.Code),
                    exception.Code,
                    exception.Message);
            }

            digest = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
        }
        else
        {
            reason = new Reason(
                ReasonDomain.Infrastructure,
                call.Consumer == "actor"
                    ? "actor-invocation-failed"
                    : "supervisor-invocation-failed",
                "The isolated component invocation did not return a usable output.");
        }

        var result = new InvocationResult(
            ResearchContractVersions.SchemaVersion,
            "invocation-result",
            RunId,
            $"result-{++_resultNumber}",
            call.Consumer,
            call is ActorCall actor ? actor.ActorTurnId : null,
            call is ReviewCall review ? review.ReviewId : null,
            call.Snapshot.SnapshotId,
            call.Snapshot.MemoryRevision,
            call.Snapshot.DecisionGeneration,
            call.Snapshot.ApplicabilityEpoch,
            tick,
            outcome.Settlement,
            parseStatus,
            output,
            digest,
            redacted,
            reason);
        ResearchContractValidator.ValidateAndThrow(result);
        return result;
    }

    private static string SerializeUnvalidatedOutput<T>(T output)
        where T : class, IResearchRoot =>
        output switch
        {
            ActorDecision actor => JsonSerializer.Serialize<ActorDecision>(
                actor,
                ResearchContractSerializer.Options),
            SupervisorOutput supervisor => JsonSerializer.Serialize<SupervisorOutput>(
                supervisor,
                ResearchContractSerializer.Options),
            _ => JsonSerializer.Serialize(
                output,
                output.GetType(),
                ResearchContractSerializer.Options)
        };

    private static ReasonDomain ContractFailureDomain(string code) =>
        code is
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
                : ReasonDomain.Context;

    private void RecordActorSettlement(
        ActorCall call,
        InvocationResult result,
        IsolatedInvocationOutcome<ActorDecision> outcome,
        uint tick) =>
        Record(
            tick,
            PhaseAfterTermination(ResearchPhase.ActorProcessing),
            outcome.Settlement switch
            {
                "returned" => "actor.completed",
                "cancelled" => "actor.cancelled",
                _ => "actor.failed"
            },
            new
            {
                actorTurnId = call.ActorTurnId,
                resultId = result.ResultId,
                consumedMemoryRevision = call.Snapshot.MemoryRevision
            });

    private void RecordReviewSettlement(
        ReviewCall call,
        InvocationResult result,
        IsolatedInvocationOutcome<SupervisorOutput> outcome,
        uint tick) =>
        Record(
            tick,
            PhaseAfterTermination(ResearchPhase.ReviewProcessing),
            outcome.Settlement switch
            {
                "returned" => "review.completed",
                "cancelled" => "review.cancelled",
                _ => "review.failed"
            },
            new
            {
                reviewId = call.ReviewId,
                resultId = result.ResultId,
                consumedMemoryRevision = call.Snapshot.MemoryRevision
            });

    private void SuppressActor(
        ActorCall call,
        InvocationResult result,
        uint tick,
        Reason reason)
    {
        _counts.ActorResultsSuppressed++;
        RecordSuppressed(
            new ActorInvocationRef(call.ActorTurnId),
            result,
            tick,
            ResearchPhase.ActorProcessing,
            reason);
        if (_termination is null)
        {
            _actionHistory.Add(
                ActionItem(
                    call,
                    result,
                    result.Output is { } output
                        ? ResearchContractSerializer.DeserializeRoot(output.GetRawText()) as ActorDecision
                        : null,
                    "suppressed",
                    reason,
                    null,
                    []));
        }
    }

    private void RecordSuppressed(
        InvocationRef invocation,
        InvocationResult result,
        uint tick,
        ResearchPhase handlingPhase,
        Reason reason) =>
        Record(
            tick,
            PhaseAfterTermination(handlingPhase),
            "result.suppressed",
            new SuppressedEventData(
                invocation,
                result.ResultId,
                result.ConsumedMemoryRevision,
                reason));

    private void RecordDuplicateDelivery<T>(
        InvocationCall<T> call,
        T? output,
        uint tick,
        ResearchPhase phase)
        where T : class, IResearchRoot
    {
        var digest = output is null
            ? new string('0', 64)
            : Convert.ToHexStringLower(
                SHA256.HashData(
                    Encoding.UTF8.GetBytes(
                        ResearchContractSerializer.Serialize(output))));
        Record(
            tick,
            PhaseAfterTermination(phase),
            "result.duplicate-delivery",
            new DuplicateEventData(
                call.InvocationReference,
                call.Result!.ResultId,
                digest));
    }

    private Reason ObsoleteReason(ActorCall call) =>
        new(
            ReasonDomain.Context,
            _termination is not null
                ? "episode-ended"
                : call.CapturedEpoch != _epoch
                    ? "epoch-mismatch"
                    : "obsolete-generation",
            "The actor result is no longer dispatch eligible.");

    private ActionHistoryItem ActionItem(
        ActorCall call,
        InvocationResult result,
        ActorDecision? decision,
        string disposition,
        Reason? reason,
        string? diagnosticId,
        IReadOnlyList<string> observationIds) =>
        new(
            call.ActorTurnId,
            call.Snapshot.SnapshotId,
            result.ResultId,
            call.Snapshot.MemoryRevision,
            decision is null ? null : ToElement(decision),
            disposition,
            reason,
            diagnosticId,
            observationIds.ToImmutableArray());

    private void EndWait(uint tick, ResearchPhase phase, Reason reason)
    {
        var actorTurnId = _waitActorTurnId!;
        _waitUntilTick = null;
        _waitActorTurnId = null;
        _actorReady = true;
        Record(
            tick,
            phase,
            "actor.wait-ended",
            new { actorTurnId, reason });
    }

    private void ReleaseBlockingActor()
    {
        if (_architecture == SupervisionArchitecture.BlockingSupervision &&
            _currentActorId is null &&
            _waitUntilTick is null)
        {
            _actorReady = true;
        }
    }

    private IReadOnlyList<PendingCancellation> TerminateLocked(
        uint tick,
        ResearchPhase phase,
        string kind,
        Reason? reason,
        string? reportResultId)
    {
        if (_termination is not null)
        {
            return [];
        }

        var pendingInvocations = _actors.Values
            .Where(item => !item.Processed)
            .Select(item => (InvocationRef)new ActorInvocationRef(item.ActorTurnId))
            .Concat(
                _reviews.Values
                    .Where(item => !item.Processed)
                    .Select(item => (InvocationRef)new SupervisorInvocationRef(item.ReviewId)))
            .ToList();
        var cancellationRequests = _actors.Values
            .Where(item => !item.Processed)
            .Select(item => new PendingCancellation(
                new ActorInvocationRef(item.ActorTurnId),
                item.Invocation.RequestCancellation,
                accepted => item.CancellationAccepted = accepted))
            .Concat(
                _reviews.Values
                    .Where(item => !item.Processed)
                    .Select(item => new PendingCancellation(
                        new SupervisorInvocationRef(item.ReviewId),
                        item.Invocation.RequestCancellation,
                        accepted => item.CancellationAccepted = accepted)))
            .ToList();
        _termination = ResearchContractFreezer.Freeze(new Termination(
            ResearchContractVersions.SchemaVersion,
            "termination",
            RunId,
            tick,
            phase,
            kind,
            reportResultId,
            reason,
            checked((uint)_observations.Count),
            _epoch,
            _generation,
            _memory.Current.MemoryRevision,
            _pendingTriggerId,
            _supervisionDegraded,
            _counts.Snapshot(),
            pendingInvocations,
            _diagnostics.Values
                .Where(item => item.Dispatched && !item.Completed)
                .Select(item => item.DiagnosticId)
                .ToList()));
        ResearchContractValidator.ValidateAndThrow(_termination);
        Record(tick, phase, "episode.terminated", new { });
        foreach (var diagnostic in _diagnostics.Values.Where(
                     item => !item.Completed && !item.Dispatched))
        {
            diagnostic.AuthorizationLifecycle.TrySetResult(
                DiagnosticAuthorizationLifecycle.EpisodeTerminated);
            diagnostic.AuthorizationBoundary.TrySetResult(
                new GovernedDiagnosticAuthorization(false, tick, phase));
        }

        _diagnosticReadinessOwnerId = null;
        _actorReady = false;
        return cancellationRequests;
    }

    private void RequestTerminationCancellations(
        IReadOnlyList<PendingCancellation> pending,
        uint tick)
    {
        if (pending.Count == 0)
        {
            return;
        }

        var reason = new Reason(
            ReasonDomain.Context,
            "episode-ended",
            "The episode terminated with this invocation still pending.");
        foreach (var cancellation in pending)
        {
            lock (_sync)
            {
                _counts.CancellationRequests++;
                Record(
                    tick,
                    ResearchPhase.Drain,
                    "cancellation.requested",
                    new InvocationReasonEventData(cancellation.Invocation, reason));
            }

            var outcome = cancellation.Request();
            lock (_sync)
            {
                if (outcome == InvocationCancellationRequest.Requested)
                {
                    cancellation.MarkAccepted(true);
                    continue;
                }

                var eventType = outcome == InvocationCancellationRequest.Unsupported
                    ? "cancellation.unsupported"
                    : "cancellation.failed";
                if (outcome == InvocationCancellationRequest.Unsupported)
                {
                    _counts.CancellationsUnsupported++;
                }
                else
                {
                    _counts.CancellationRequestsFailed++;
                }

                Record(
                    tick,
                    ResearchPhase.Drain,
                    eventType,
                    new InvocationReasonEventData(
                        cancellation.Invocation,
                        new Reason(
                            ReasonDomain.Infrastructure,
                            outcome == InvocationCancellationRequest.Unsupported
                                ? "cancellation-unsupported"
                                : "cancellation-request-failed",
                            outcome == InvocationCancellationRequest.Unsupported
                                ? "The invocation does not support cancellation."
                                : "The best-effort cancellation request failed.")));
            }
        }
    }

    private ResearchEvent Record(
        uint? tick,
        ResearchPhase phase,
        string eventType,
        object data)
    {
        var item = new ResearchEvent(
            ResearchContractVersions.SchemaVersion,
            "research-event",
            RunId,
            checked((uint)_events.Count + 1),
            tick,
            phase,
            eventType,
            [],
            _generation,
            _memory.Current.MemoryRevision,
            checked((uint)_observations.Count),
            _epoch,
            JsonSerializer.SerializeToElement(
                data,
                data.GetType(),
                ResearchContractSerializer.Options));
        ResearchContractValidator.ValidateAndThrow(item);
        _events.Add(item);
        return item;
    }

    private ResearchPhase PhaseAfterTermination(ResearchPhase phase) =>
        _termination is null ? phase : ResearchPhase.Drain;

    private static JsonElement ToElement(IResearchRoot root) =>
        JsonDocument.Parse(ResearchContractSerializer.Serialize(root)).RootElement.Clone();

    private static AllowedQuery Allowed(ResearchOperation operation, TargetId targetId) =>
        new(operation, targetId, new Dictionary<string, JsonElement>());

    private abstract class InvocationCall<T>(
        string consumer,
        InputSnapshot snapshot)
        where T : class, IResearchRoot
    {
        public string Consumer { get; } = consumer;
        public InputSnapshot Snapshot { get; } = snapshot;
        public abstract InvocationRef InvocationReference { get; }
        public bool Processed { get; set; }
        public InvocationResult? Result { get; set; }
    }

    private sealed class ActorCall(
        string actorTurnId,
        InputSnapshot snapshot,
        IsolatedFrameworkInvocation<ActorDecision> invocation,
        uint capturedGeneration,
        uint capturedEpoch)
        : InvocationCall<ActorDecision>("actor", snapshot)
    {
        public string ActorTurnId { get; } = actorTurnId;
        public override InvocationRef InvocationReference =>
            new ActorInvocationRef(ActorTurnId);
        public IsolatedFrameworkInvocation<ActorDecision> Invocation { get; } = invocation;
        public uint CapturedGeneration { get; } = capturedGeneration;
        public uint CapturedEpoch { get; } = capturedEpoch;
        public bool Invalidated { get; set; }
        public bool SlotReleased { get; set; }
        public bool CancellationAccepted { get; set; }
    }

    private sealed class ReviewCall(
        string reviewId,
        InputSnapshot snapshot,
        IsolatedFrameworkInvocation<SupervisorOutput> invocation,
        uint startTick,
        uint timeoutTick)
        : InvocationCall<SupervisorOutput>("supervisor", snapshot)
    {
        public string ReviewId { get; } = reviewId;
        public override InvocationRef InvocationReference =>
            new SupervisorInvocationRef(ReviewId);
        public IsolatedFrameworkInvocation<SupervisorOutput> Invocation { get; } = invocation;
        public uint StartTick { get; } = startTick;
        public uint TimeoutTick { get; } = timeoutTick;
        public bool TimedOut { get; set; }
        public bool BudgetReleased { get; set; }
        public bool CancellationAccepted { get; set; }
    }

    private sealed class DiagnosticCall(
        string diagnosticId,
        ActorCall actor,
        InvocationResult result,
        QueryActorDecision decision,
        uint requestTick,
        uint capturedGeneration,
        uint capturedEpoch)
    {
        public string DiagnosticId { get; } = diagnosticId;
        public ActorCall Actor { get; } = actor;
        public InvocationResult Result { get; } = result;
        public QueryActorDecision Decision { get; } = decision;
        public uint RequestTick { get; } = requestTick;
        public uint? DispatchTick { get; set; }
        public ResearchPhase? DispatchPhase { get; set; }
        public uint CapturedGeneration { get; } = capturedGeneration;
        public uint CapturedEpoch { get; } = capturedEpoch;
        public bool Dispatched { get; set; }
        public bool Completed { get; set; }
        public uint? CompletionSequence { get; set; }
        public GovernedDiagnosticBinding? Binding { get; set; }
        public GovernedDiagnosticBinding? PendingBinding { get; set; }
        public Task<GovernedDiagnosticResult>? CompletionTask { get; set; }
        public TaskCompletionSource<DiagnosticAuthorizationLifecycle>
            AuthorizationLifecycle { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<GovernedDiagnosticAuthorization>
            AuthorizationBoundary { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed record QueryDispatchWork(DiagnosticCall Call, uint Tick);

    private sealed record PendingCancellation(
        InvocationRef Invocation,
        Func<InvocationCancellationRequest> Request,
        Action<bool> MarkAccepted);

    private sealed record InvocationReasonEventData(
        InvocationRef Invocation,
        Reason Reason);

    private sealed record InvocationResultEventData(
        InvocationRef Invocation,
        string ResultId);

    private sealed record SuppressedEventData(
        InvocationRef Invocation,
        string ResultId,
        uint ConsumedMemoryRevision,
        Reason Reason);

    private sealed record DuplicateEventData(
        InvocationRef Invocation,
        string OriginalResultId,
        string DeliveredOutputSha256);

    private sealed record MutableCounts
    {
        public uint ActorTurnsStarted { get; set; }
        public uint ReviewsStarted { get; set; }
        public uint DiagnosticAttempts { get; set; }
        public uint DiagnosticsDispatched { get; set; }
        public uint DiagnosticsCompleted { get; set; }
        public uint ActorResultsSuppressed { get; set; }
        public uint ReviewResultsSuppressed { get; set; }
        public uint CancellationRequests { get; set; }
        public uint CancellationsConfirmed { get; set; }
        public uint CancellationsUnsupported { get; set; }
        public uint CancellationRequestsFailed { get; set; }
        public uint CancellationsIgnored { get; set; }
        public uint ReportsSubmitted { get; set; }
        public uint MemoryUpdatesCommitted { get; set; }
        public uint MemoryUpdatesRejected { get; set; }
        public uint MemoryUpdatesNoOp { get; set; }
        public uint MemoryRevisionConflicts { get; set; }
        public uint RepeatedDirectionsNormalized { get; set; }
        public uint BeliefReassertionsCommitted { get; set; }
        public uint ReassertionInterrupts { get; set; }
        public uint ActionableCommits { get; set; }
        public uint ReconsiderationAcknowledgments { get; set; }

        public ResourceCounts Snapshot() =>
            new(
                ActorTurnsStarted,
                ReviewsStarted,
                DiagnosticAttempts,
                DiagnosticsDispatched,
                DiagnosticsCompleted,
                ActorResultsSuppressed,
                ReviewResultsSuppressed,
                CancellationRequests,
                CancellationsConfirmed,
                CancellationsUnsupported,
                CancellationRequestsFailed,
                CancellationsIgnored,
                ReportsSubmitted,
                MemoryUpdatesCommitted,
                MemoryUpdatesRejected,
                MemoryUpdatesNoOp,
                MemoryRevisionConflicts,
                RepeatedDirectionsNormalized,
                BeliefReassertionsCommitted,
                ReassertionInterrupts,
                ActionableCommits,
                ReconsiderationAcknowledgments);
    }
}
