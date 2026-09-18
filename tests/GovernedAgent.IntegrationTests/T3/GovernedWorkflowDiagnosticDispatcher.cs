using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GovernedAgent.Core.Contracts;
using GovernedAgent.Core.Serialization;
using GovernedAgent.Governance;
using GovernedAgent.Host.Verification;
using GovernedAgent.Host.Workflow;
using GovernedAgent.Research;
using GovernedAgent.Simulator;

namespace GovernedAgent.IntegrationTests.T3;

internal sealed class GovernedWorkflowDiagnosticDispatcher : IGovernedDiagnosticDispatcher
{
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-09-18T12:00:00Z");
    private readonly Er1EvidenceFixture _fixture =
        new(Er1FixtureOptions.Straightforward);
    private readonly List<AuditRecord> _auditRecords = [];

    public int DispatchCount { get; private set; }
    public int AuditRecordCount { get; private set; }
    public bool KillSwitchActive { get; init; }
    public bool VerifierUnavailable { get; init; }
    public Func<Guid>? PlanIdFactory { get; init; }
    public Func<Guid>? RequestIdFactory { get; init; }
    public Func<Guid>? AuditRecordIdFactory { get; init; }
    public IReadOnlyList<AuditRecord> AuditRecords => _auditRecords.AsReadOnly();

    public async ValueTask<GovernedDiagnosticResult> DispatchAsync(
        GovernedDiagnosticRequest request,
        Func<
            GovernedDiagnosticBinding,
            CancellationToken,
            ValueTask<GovernedDiagnosticAuthorization>> authorizeDispatch,
        CancellationToken cancellationToken)
    {
        var registry = new ToolRegistry(
            new ToolRegistry().Tools.Where(
                item => item.Name == ToolName(request.Decision.Operation)));
        var tool = registry.Tools.Single();
        var canonicalizer = new ActionCanonicalizer(registry);
        var plan = CreatePlan(request, tool);
        var step = Assert.Single(plan.Steps);
        var planDigest = Convert.ToHexStringLower(
            SHA256.HashData(
                Encoding.UTF8.GetBytes(CanonicalJson.Serialize(plan))));
        var actionDigest = canonicalizer.CreateDigest(plan, step).Value;
        var binding = new GovernedDiagnosticBinding(
            plan.PlanId,
            step.StepId,
            planDigest,
            actionDigest,
            null,
            "t3-scripted-session");
        var actualDispatchTick = request.RequestTick;
        var simulator = new Er1FixtureIncidentSimulator(
            _fixture,
            () => actualDispatchTick);
        var audit = new InMemoryAuditChain();
        var inner = new SimulatorGovernedToolExecutor(simulator);
        GovernedDiagnosticAuthorization? authorization = null;
        var executor = new ImmediateAuthorizationExecutor(
            inner,
            binding,
            candidate =>
            {
                authorization = authorizeDispatch(candidate, cancellationToken)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult();
                actualDispatchTick = authorization.DispatchTick;
                return authorization.Authorized;
            },
            () => DispatchCount++);
        var clock = new FixedTimeProvider();
        var killSwitch = new InMemoryKillSwitch();
        if (KillSwitchActive)
        {
            killSwitch.Activate();
        }

        var gateway = new GovernedToolGateway(
            registry,
            canonicalizer,
            new DefaultDenyPolicyEvaluator(clock),
            new InMemoryApprovalStore(),
            new InMemoryExecutionBudgetStore(
                new ExecutionBudgetLimits(12, TimeSpan.FromMinutes(3))),
            killSwitch,
            audit,
            executor,
            clock,
            AuditRecordIdFactory);
        var verifier = VerifierUnavailable
            ? new NodePlanVerifier(
                "node-t3-intentionally-missing",
                "missing-t3-verifier.js",
                TimeSpan.FromSeconds(1))
            : new NodePlanVerifier(
                "node",
                Path.Combine(AppContext.BaseDirectory, "Hosted", "verifier", "cli.js"),
                TimeSpan.FromSeconds(5));
        var workflow = new LocalDeterministicAgentWorkflow(
            verifier,
            canonicalizer,
            gateway,
            new SimulatorWorkflowCompletionEvaluator(simulator),
            [tool.Capability],
            new Dictionary<string, VerifierToolMetadata>
            {
                [tool.Name] = new(
                    tool.Capability,
                    tool.Effect,
                    tool.ApprovalClass,
                    ResourceArgument(request.Decision.Operation))
            },
            timeProvider: clock,
            requestIdFactory: RequestIdFactory);
        var workflowResult = await workflow.ExecuteAsync(
            new AgentWorkflowRequest(
                plan,
                step.StepId,
                new UserIdentity("synthetic-operator", ["incident-operator"]),
                new AgentIdentity("t3-scripted-actor", "synthetic-identity", "1.0.0"),
                new SessionIdentity("t3-scripted-session", Er1EvidenceFixture.IncidentId),
                $"t3-{request.DiagnosticId}",
                1,
                new WorkflowCompletionCriteria(
                    Er1EvidenceFixture.IncidentId,
                    ServiceId: Er1EvidenceFixture.PaymentsServiceId,
                    ServiceHealth: ServiceHealth.Healthy)),
            cancellationToken);
        Assert.True(audit.VerifyIntegrity());
        var auditRecords = audit.ReadAll();
        _auditRecords.AddRange(auditRecords);
        var auditIds = auditRecords.Select(item => item.RecordId).ToArray();
        AuditRecordCount += auditIds.Length;
        if (workflowResult.Status == AgentWorkflowStatus.Failed)
        {
            return new GovernedDiagnosticResult(
                "rejected",
                authorization?.DispatchTick ?? request.RequestTick,
                null,
                binding,
                auditIds,
                new Reason(
                    ReasonDomain.Governance,
                    workflowResult.ReasonCode,
                    "The governed diagnostic workflow rejected dispatch."),
                workflowResult.ReasonCode is "kill_switch_active" or
                    "verifier_unavailable" or
                    "policy_unavailable" or
                    "audit_unavailable"
                    ? GovernedDiagnosticFailureKind.GovernanceStop
                    : GovernedDiagnosticFailureKind.Reconsiderable);
        }

        Assert.Equal(GatewayOutcome.Executed, workflowResult.GatewayResult!.Outcome);
        Assert.Equal(VerificationResult.Verified, workflowResult.Envelope!.Verification.Result);
        return new GovernedDiagnosticResult(
            "completed",
            authorization!.DispatchTick,
            _fixture.SampleDiagnostic(
                request.Decision.Operation,
                request.Decision.TargetId,
                authorization.DispatchTick),
            binding with { RequestId = workflowResult.Envelope.RequestId },
            auditIds,
            null);
    }

    private ActionPlan CreatePlan(
        GovernedDiagnosticRequest request,
        ToolMetadata tool)
    {
        var resourceId = request.Decision.TargetId switch
        {
            TargetId.Incident => Er1EvidenceFixture.IncidentId,
            TargetId.PaymentsApi => Er1EvidenceFixture.PaymentsServiceId,
            TargetId.AuthorizationService => Er1EvidenceFixture.AuthorizationServiceId,
            _ => throw new InvalidOperationException("Unsupported fixture target.")
        };
        var argument = ResourceArgument(request.Decision.Operation);
        var step = new PlanStep(
            request.DiagnosticId,
            tool.Capability,
            tool.Name,
            new ResourceReference(
                request.Decision.TargetId == TargetId.Incident ? "incident" : "service",
                resourceId,
                TargetEnvironment.Production,
                DataClassification.Internal),
            [new DataSourceReference($"{resourceId}-synthetic", DataClassification.Internal)],
            new DestinationReference(resourceId, DataClassification.InternalTrusted),
            new Dictionary<string, JsonElement>
            {
                [argument] = JsonSerializer.SerializeToElement(resourceId)
            },
            [],
            tool.Effect,
            tool.ApprovalClass,
            null);
        return new ActionPlan(
            "1.0",
            PlanIdFactory?.Invoke() ?? Guid.NewGuid(),
            Er1EvidenceFixture.IncidentId,
            "t3-scripted-actor",
            "1.0.0",
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            [step]);
    }

    private static string ToolName(ResearchOperation operation) => operation switch
    {
        ResearchOperation.GetIncident => "get_incident",
        ResearchOperation.GetServiceHealth => "get_service_health",
        ResearchOperation.QueryMetrics => "query_metrics",
        ResearchOperation.QueryLogs => "query_logs",
        _ => throw new InvalidOperationException("Unsupported diagnostic operation.")
    };

    private static string ResourceArgument(ResearchOperation operation) =>
        operation == ResearchOperation.GetIncident ? "incidentId" : "serviceId";

    private sealed class ImmediateAuthorizationExecutor(
        IGovernedToolExecutor inner,
        GovernedDiagnosticBinding binding,
        Func<GovernedDiagnosticBinding, bool> authorize,
        Action onDispatch) : IGovernedToolExecutor
    {
        public void Validate(PlanStep step, long expectedResourceVersion) =>
            inner.Validate(step, expectedResourceVersion);

        public ValueTask<JsonElement> ExecuteAsync(
            PlanStep step,
            string idempotencyKey,
            long expectedResourceVersion,
            CancellationToken cancellationToken)
        {
            if (!authorize(binding))
            {
                throw new GovernanceException(
                    ErrorCategory.Validation,
                    "obsolete_decision",
                    "The actor decision was invalidated before actual dispatch.");
            }

            onDispatch();
            return inner.ExecuteAsync(
                step,
                idempotencyKey,
                expectedResourceVersion,
                cancellationToken);
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
