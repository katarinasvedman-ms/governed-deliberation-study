namespace GovernedAgent.Research;

public static class ApprovedScriptedExperiment
{
    public static ScriptedClock Clock { get; } =
        new(
            "logical-ticks",
            0,
            24,
            1,
            0,
            3,
            Array.AsReadOnly(new uint[] { 0, 4, 8, 12, 16, 20 }),
            6,
            8,
            4);

    public static ResearchLimits Limits { get; } = new(24, 12, 6, 1, 1);
}

public sealed record ScriptedScheduleTraceEntry(
    uint Tick,
    ResearchPhase Phase,
    string ActionId,
    uint RegistrationOrder);

public sealed record ScriptedScheduleResult(
    IReadOnlyList<ScriptedScheduleTraceEntry> Trace,
    uint FinalTick,
    bool HorizonReached,
    string? StopReason,
    ScriptedResourceCounts Counts);

public sealed record ScriptedResourceCounts(
    uint ActorTurns,
    uint DiagnosticAttempts,
    uint Reviews);

public enum ReviewCheckpointDisposition
{
    Start,
    SkipActiveReview,
    ReviewBudgetExhausted
}

public sealed class ScriptedResourceBudget
{
    private readonly ResearchLimits _limits;

    internal ScriptedResourceBudget(ResearchLimits limits)
    {
        _limits = limits;
    }

    public uint ActorTurns { get; private set; }

    public uint DiagnosticAttempts { get; private set; }

    public uint Reviews { get; private set; }

    public uint ActiveActorTurns { get; private set; }

    public uint ActiveReviews { get; private set; }

    public bool TryStartActorTurn()
    {
        if (ActorTurns >= _limits.ActorTurns ||
            ActiveActorTurns >= _limits.DispatchEligibleActorTurns)
        {
            return false;
        }

        ActorTurns++;
        ActiveActorTurns++;
        return true;
    }

    public void CompleteActorTurn()
    {
        if (ActiveActorTurns == 0)
        {
            throw new InvalidOperationException("No actor turn is active.");
        }

        ActiveActorTurns--;
    }

    public bool TryDispatchDiagnostic()
    {
        if (DiagnosticAttempts >= _limits.DiagnosticAttempts)
        {
            return false;
        }

        DiagnosticAttempts++;
        return true;
    }

    public ReviewCheckpointDisposition EvaluateReviewCheckpoint()
    {
        if (ActiveReviews >= _limits.ActiveReviews)
        {
            return ReviewCheckpointDisposition.SkipActiveReview;
        }

        if (Reviews >= _limits.Reviews)
        {
            return ReviewCheckpointDisposition.ReviewBudgetExhausted;
        }

        Reviews++;
        ActiveReviews++;
        return ReviewCheckpointDisposition.Start;
    }

    public void CompleteReview()
    {
        if (ActiveReviews == 0)
        {
            throw new InvalidOperationException("No review is active.");
        }

        ActiveReviews--;
    }

    internal ScriptedResourceCounts Snapshot() =>
        new(ActorTurns, DiagnosticAttempts, Reviews);
}

public sealed class ScriptedSchedulerContext
{
    private string? _stopReason;

    internal ScriptedSchedulerContext(ScriptedResourceBudget budget)
    {
        Budget = budget;
    }

    public uint Tick { get; internal set; }

    public ResearchPhase Phase { get; internal set; }

    public ScriptedResourceBudget Budget { get; }

    public bool StopRequested => _stopReason is not null;

    public string? StopReason => _stopReason;

    public void Stop(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _stopReason ??= reason;
    }
}

public sealed class ScriptedExperimentScheduler
{
    private readonly ScriptedClock _clock;
    private readonly ResearchLimits _limits;
    private readonly List<ScheduledAction> _actions = [];
    private uint _registrationOrder;
    private bool _started;

    public ScriptedExperimentScheduler()
        : this(ApprovedScriptedExperiment.Clock, ApprovedScriptedExperiment.Limits)
    {
    }

    public ScriptedExperimentScheduler(ScriptedClock clock, ResearchLimits limits)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(limits);
        ValidateApprovedClock(clock);
        ValidateApprovedLimits(limits);
        _clock = clock with
        {
            ReviewCheckpoints = Array.AsReadOnly(clock.ReviewCheckpoints.ToArray())
        };
        _limits = limits;
    }

    public ScriptedClock Clock => _clock;

    public ResearchLimits Limits => _limits;

    public IReadOnlyList<uint> ReviewCheckpoints => _clock.ReviewCheckpoints;

    public uint ActorCompletionTick(uint startTick) =>
        checked(startTick + _clock.ActorTurnTicks);

    public uint ReviewCompletionTick(uint startTick) =>
        checked(startTick + _clock.ReviewTicks);

    public uint ReviewTimeoutTick(uint startTick) =>
        checked(startTick + _clock.ReviewTimeoutTicks);

    public uint GuidanceExpiryTick(uint reviewStartTick) =>
        checked(reviewStartTick + _clock.GuidanceLifetimeTicks);

    public bool IsReviewTimedOut(uint reviewStartTick, uint currentTick) =>
        currentTick >= ReviewTimeoutTick(reviewStartTick);

    public bool IsGuidanceExpired(uint reviewStartTick, uint currentTick) =>
        currentTick >= GuidanceExpiryTick(reviewStartTick);

    public void ValidateWait(uint startTick, uint untilTick)
    {
        if (untilTick <= startTick ||
            untilTick - startTick > _clock.MaximumWaitTicks ||
            untilTick > _clock.EndTickExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(untilTick),
                "Wait must end within four ticks and no later than the exclusive horizon.");
        }
    }

    public void Schedule(
        uint tick,
        ResearchPhase phase,
        string actionId,
        Action<ScriptedSchedulerContext> action) =>
        Schedule(tick, phase, phaseOrder: 0, actionId, action);

    public void ScheduleReviewTimeout(
        uint tick,
        string actionId,
        Action<ScriptedSchedulerContext> action) =>
        Schedule(tick, ResearchPhase.ReviewProcessing, phaseOrder: -1, actionId, action);

    private void Schedule(
        uint tick,
        ResearchPhase phase,
        int phaseOrder,
        string actionId,
        Action<ScriptedSchedulerContext> action)
    {
        if (_started)
        {
            throw new InvalidOperationException("The schedule is immutable after execution starts.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(actionId);
        ArgumentNullException.ThrowIfNull(action);
        ValidateCoordinate(tick, phase);
        _actions.Add(new ScheduledAction(
            tick,
            phase,
            phaseOrder,
            actionId,
            _registrationOrder++,
            action));
    }

    public ScriptedScheduleResult Run()
    {
        if (_started)
        {
            throw new InvalidOperationException("A scripted schedule can run only once.");
        }

        _started = true;
        var budget = new ScriptedResourceBudget(_limits);
        var context = new ScriptedSchedulerContext(budget);
        var trace = new List<ScriptedScheduleTraceEntry>(_actions.Count);

        foreach (var scheduled in _actions
                     .OrderBy(item => item.Tick)
                     .ThenBy(item => item.Phase)
                     .ThenBy(item => item.PhaseOrder)
                     .ThenBy(item => item.RegistrationOrder))
        {
            context.Tick = scheduled.Tick;
            context.Phase = scheduled.Phase;
            scheduled.Action(context);
            trace.Add(new ScriptedScheduleTraceEntry(
                scheduled.Tick,
                scheduled.Phase,
                scheduled.ActionId,
                scheduled.RegistrationOrder));

            if (context.StopRequested)
            {
                return new ScriptedScheduleResult(
                    trace.AsReadOnly(),
                    scheduled.Tick,
                    HorizonReached: false,
                    context.StopReason,
                    budget.Snapshot());
            }
        }

        return new ScriptedScheduleResult(
            trace.AsReadOnly(),
            _clock.EndTickExclusive,
            HorizonReached: true,
            StopReason: null,
            budget.Snapshot());
    }

    private void ValidateCoordinate(uint tick, ResearchPhase phase)
    {
        if (phase is ResearchPhase.Setup or ResearchPhase.Drain)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                "Setup and drain are outside the logical tick schedule.");
        }

        if (tick < _clock.StartTick || tick > _clock.EndTickExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(tick));
        }

        if (tick == _clock.EndTickExclusive && phase != ResearchPhase.EndCheck)
        {
            throw new ArgumentOutOfRangeException(
                nameof(phase),
                "Only the end-check phase runs at the exclusive horizon.");
        }
    }

    private static void ValidateApprovedClock(ScriptedClock clock)
    {
        var approved = ApprovedScriptedExperiment.Clock;
        if (clock.Kind != approved.Kind ||
            clock.StartTick != approved.StartTick ||
            clock.EndTickExclusive != approved.EndTickExclusive ||
            clock.ActorTurnTicks != approved.ActorTurnTicks ||
            clock.DiagnosticTicks != approved.DiagnosticTicks ||
            clock.ReviewTicks != approved.ReviewTicks ||
            !clock.ReviewCheckpoints.SequenceEqual(approved.ReviewCheckpoints) ||
            clock.ReviewTimeoutTicks != approved.ReviewTimeoutTicks ||
            clock.GuidanceLifetimeTicks != approved.GuidanceLifetimeTicks ||
            clock.MaximumWaitTicks != approved.MaximumWaitTicks)
        {
            throw new ArgumentException(
                "Clock differs from the approved scripted experiment clock.",
                nameof(clock));
        }
    }

    private static void ValidateApprovedLimits(ResearchLimits limits)
    {
        if (limits != ApprovedScriptedExperiment.Limits)
        {
            throw new ArgumentException(
                "Limits differ from the approved scripted experiment limits.",
                nameof(limits));
        }
    }

    private sealed record ScheduledAction(
        uint Tick,
        ResearchPhase Phase,
        int PhaseOrder,
        string ActionId,
        uint RegistrationOrder,
        Action<ScriptedSchedulerContext> Action);
}
