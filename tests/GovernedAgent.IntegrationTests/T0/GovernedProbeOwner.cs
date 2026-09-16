using System.Collections.Immutable;
using System.Text.Json;
using GovernedAgent.Core.Contracts;
using GovernedAgent.Governance;
using GovernedAgent.Host.Observability;
using GovernedAgent.Host.Verification;
using GovernedAgent.Host.Workflow;
using GovernedAgent.Simulator;

namespace GovernedAgent.IntegrationTests.T0;

internal sealed class GovernedProbeOwner : IAsyncDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-16T12:00:00Z");
    private readonly object _dispatchBoundary = new();
    private readonly CancellationTokenSource _lifetime = new(TimeSpan.FromSeconds(30));
    private readonly List<ProbeCall> _calls = [];
    private readonly Dictionary<string, ProbeCall> _requests = new(StringComparer.Ordinal);
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dispatched = new(StringComparer.Ordinal);
    private readonly List<string> _observations = ["initial-incident"];
    private readonly List<Task> _operations = [];
    private readonly LocalDeterministicAgentWorkflow _workflow;
    private readonly ProbeTrace _trace;
    private int _generation;
    private string? _guidance;

    public GovernedProbeOwner(ProbeTrace trace, IPlanVerifier? verifier = null, int toolBudget = 12)
    {
        _trace = trace;
        var clock = new FixedTimeProvider();
        Simulator = new IncidentSimulator(clock);
        var registry = new ToolRegistry(
            new ToolRegistry().Tools.Where(tool => tool.Name == "query_metrics"));
        var canonicalizer = new ActionCanonicalizer(registry);
        var gateway = new GovernedToolGateway(
            registry, canonicalizer, new DefaultDenyPolicyEvaluator(clock),
            new InMemoryApprovalStore(),
            new InMemoryExecutionBudgetStore(new ExecutionBudgetLimits(toolBudget, TimeSpan.FromMinutes(3))),
            KillSwitch, Audit, new DispatchExecutor(this, new SimulatorGovernedToolExecutor(Simulator)),
            clock);
        _workflow = new LocalDeterministicAgentWorkflow(
            verifier ?? CreateVerifier(), canonicalizer, gateway,
            new SimulatorWorkflowCompletionEvaluator(Simulator),
            ["telemetry.metrics.read"],
            new Dictionary<string, VerifierToolMetadata>
            {
                ["query_metrics"] = new("telemetry.metrics.read", EffectKind.Read, ApprovalClass.None, "serviceId")
            },
            timeProvider: clock);
    }

    public IncidentSimulator Simulator { get; }
    public InMemoryAuditChain Audit { get; } = new();
    public InMemoryKillSwitch KillSwitch { get; } = new();
    public TaskCompletionSource<bool>? DiagnosticRelease { get; set; }
    public TaskCompletionSource<bool> DiagnosticDispatched { get; } = FrameworkProbe.Signal<bool>();
    public bool ReportSubmitted { get; private set; }
    public int DispatchCount { get { lock (_dispatchBoundary) { return _dispatched.Count; } } }
    public List<AgentWorkflowResult> GatewayResults { get; } = [];
    public List<JsonElement> DiagnosticResults { get; } = [];
    public ImmutableArray<string> Observations
    {
        get { lock (_dispatchBoundary) { return _observations.ToImmutableArray(); } }
    }

    public static NodePlanVerifier CreateVerifier() => new(
        "node", Path.Combine(AppContext.BaseDirectory, "Hosted", "verifier", "cli.js"),
        TimeSpan.FromSeconds(5));

    public void ObserveExternalEvent()
    {
        lock (_dispatchBoundary)
        {
            _observations.Add("external-notification");
            _trace.Record("external.observed id=external-notification");
        }
    }

    public ProbeCall Start(string id)
    {
        lock (_dispatchBoundary)
        {
            Assert.False(ReportSubmitted);
            Assert.DoesNotContain(_calls, call => call.Id == id);
            var snapshot = new ProbeSnapshot(
                $"snapshot-{id}", _generation, _observations.ToImmutableArray(), _guidance);
            var call = new ProbeCall(id, snapshot, _trace, _lifetime.Token);
            _calls.Add(call);
            return call;
        }
    }

    public void AcceptGuidance(ProbeCall review, ProbeDecision decision, ProbeCall? pending = null)
    {
        lock (_dispatchBoundary)
        {
            // Only this authored cue is admitted in T0; general guidance validation belongs to T1/T3.
            Assert.Equal(ProbeDecision.Focus, decision);
            Assert.Contains("initial-incident", review.Snapshot.Observations);
            Assert.False(ReportSubmitted);
            _generation++;
            _guidance = review.Id;
            _trace.Record(
                $"guidance.accepted review={review.Id} snapshot={review.Snapshot.Id} generation={_generation}");
            if (pending is not null)
            {
                _trace.Record($"actor.invalidated id={pending.Id} generation={pending.Snapshot.Generation}");
            }
        }

        // Cancellation callbacks must not hold the authority lock; invalidation already took effect.
        pending?.RequestCancellation();
    }

    public Task<string> ProcessAsync(ProbeCall call, ProbeDecision decision)
    {
        var operation = ProcessCoreAsync(call, decision);
        _operations.Add(operation);
        return operation;
    }

    private async Task<string> ProcessCoreAsync(ProbeCall call, ProbeDecision decision)
    {
        AgentWorkflowRequest request;
        lock (_dispatchBoundary)
        {
            if (!IsCurrent(call) || !_processed.Add(call.Id))
            {
                _trace.Record($"decision.suppressed id={call.Id} decision={decision}");
                return "suppressed";
            }

            if (decision == ProbeDecision.Report)
            {
                ReportSubmitted = true;
                _trace.Record($"report.submitted id={call.Id}");
                return "reported";
            }

            Assert.Equal(ProbeDecision.Query, decision);
            _requests.Add(call.Id, call);
            request = CreateRequest(call.Id);
            _trace.Record($"verification.requested id={call.Id}");
        }

        var result = await _workflow.ExecuteAsync(request, _lifetime.Token);
        lock (_dispatchBoundary)
        {
            GatewayResults.Add(result);
            if (result.Status == AgentWorkflowStatus.Failed)
            {
                _trace.Record($"diagnostic.rejected id={call.Id} reason={result.ReasonCode}");
                return result.ReasonCode;
            }

            Assert.Equal(GatewayOutcome.Executed, result.GatewayResult!.Outcome);
            Assert.Equal(VerificationResult.Verified, result.Envelope!.Verification.Result);
            Assert.Equal(AgentWorkflowStatus.InProgress, result.Status);
            var observation = Assert.IsType<JsonElement>(result.GatewayResult.ToolResult);
            DiagnosticResults.Add(observation);
            _observations.Add($"diagnostic-{call.Id}");
            _trace.Record($"diagnostic.completed id={call.Id} observation=diagnostic-{call.Id}");
            _trace.Record($"observation.retained {TelemetryRedactor.RedactToolResult(observation.GetRawText())}");
            return "executed";
        }
    }

    private bool IsCurrent(ProbeCall call) =>
        !ReportSubmitted && call.Snapshot.Generation == _generation;

    private sealed class DispatchExecutor(
        GovernedProbeOwner owner, SimulatorGovernedToolExecutor inner) : IGovernedToolExecutor
    {
        public void Validate(PlanStep step, long expectedResourceVersion) =>
            inner.Validate(step, expectedResourceVersion);

        public async ValueTask<JsonElement> ExecuteAsync(
            PlanStep step, string idempotencyKey, long expectedResourceVersion,
            CancellationToken cancellationToken)
        {
            ValueTask<JsonElement> execution;
            lock (owner._dispatchBoundary)
            {
                var call = owner._requests[step.StepId];
                // Recheck AFTER asynchronous verification/policy, atomically with actual dispatch.
                if (!owner.IsCurrent(call) || !owner._dispatched.Add(call.Id))
                {
                    owner._trace.Record($"dispatch.suppressed id={call.Id}");
                    throw new GovernanceException(
                        ErrorCategory.Validation, "obsolete_decision",
                        "The probe decision was invalidated before dispatch.");
                }

                owner._trace.Record($"diagnostic.dispatched id={call.Id}");
                execution = inner.ExecuteAsync(step, idempotencyKey, expectedResourceVersion, cancellationToken);
                owner.DiagnosticDispatched.TrySetResult(true);
            }

            var result = await execution;
            if (owner.DiagnosticRelease is { } release)
            {
                await release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }

    private static AgentWorkflowRequest CreateRequest(string id)
    {
        var step = new PlanStep(
            id, "telemetry.metrics.read", "query_metrics",
            new ResourceReference("service", IncidentSimulator.DemoServiceId,
                TargetEnvironment.Production, DataClassification.Internal),
            [new DataSourceReference("payments-api-metrics", DataClassification.Internal)],
            new DestinationReference(IncidentSimulator.DemoServiceId, DataClassification.InternalTrusted),
            new Dictionary<string, JsonElement>
            {
                ["serviceId"] = JsonSerializer.SerializeToElement(IncidentSimulator.DemoServiceId)
            },
            [], EffectKind.Read, ApprovalClass.None, null);
        return new AgentWorkflowRequest(
            new ActionPlan("1.0", Guid.NewGuid(), IncidentSimulator.DemoIncidentId,
                "t0-scripted-actor", "1.0.0", Now.AddMinutes(-1), Now.AddMinutes(5), [step]),
            id,
            new UserIdentity("synthetic-operator", ["incident-operator"]),
            new AgentIdentity("t0-scripted-actor", "synthetic-identity", "1.0.0"),
            new SessionIdentity("t0-local", IncidentSimulator.DemoIncidentId),
            $"t0-{id}", 1,
            new WorkflowCompletionCriteria(IncidentSimulator.DemoIncidentId,
                ServiceId: IncidentSimulator.DemoServiceId, ServiceHealth: ServiceHealth.Healthy));
    }

    public async ValueTask DisposeAsync()
    {
        // Release all scripted work even on assertion failures and observe every outstanding task.
        DiagnosticRelease?.TrySetResult(true);
        foreach (var call in _calls)
        {
            call.Release.TrySetResult(ProbeDecision.Report);
        }

        try
        {
            await Task.WhenAll(_calls.Select(call => (Task)call.Result).Concat(_operations));
        }
        finally
        {
            foreach (var call in _calls) { call.Dispose(); }
            _lifetime.Dispose();
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
