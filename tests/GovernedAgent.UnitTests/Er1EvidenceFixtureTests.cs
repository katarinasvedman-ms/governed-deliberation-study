using System.Text.Json;
using GovernedAgent.Core.Contracts;
using GovernedAgent.Core.Serialization;
using GovernedAgent.Governance;
using GovernedAgent.Research;
using GovernedAgent.Simulator;

namespace GovernedAgent.UnitTests;

public sealed class Er1EvidenceFixtureTests
{
    private const string RunId = "00000000-0000-0000-0000-000000000001";
    private static readonly DateTimeOffset Now =
        DateTimeOffset.Parse("2026-09-18T08:12:00Z");

    [Fact]
    public void SevenApprovedPairsReturnContractValidObservations()
    {
        var fixture = new Er1EvidenceFixture();
        var pairs = new[]
        {
            (ResearchOperation.GetIncident, TargetId.Incident),
            (ResearchOperation.GetServiceHealth, TargetId.PaymentsApi),
            (ResearchOperation.QueryMetrics, TargetId.PaymentsApi),
            (ResearchOperation.QueryLogs, TargetId.PaymentsApi),
            (ResearchOperation.GetServiceHealth, TargetId.AuthorizationService),
            (ResearchOperation.QueryMetrics, TargetId.AuthorizationService),
            (ResearchOperation.QueryLogs, TargetId.AuthorizationService)
        };

        for (var index = 0; index < pairs.Length; index++)
        {
            var sample = fixture.SampleDiagnostic(pairs[index].Item1, pairs[index].Item2, 12);
            var observation = sample.CreateObservation(
                RunId,
                $"observation-{index + 1}",
                $"diagnostic-{index + 1}",
                12,
                (uint)index + 1);

            Assert.Empty(ResearchContractValidator.Validate(observation));
        }

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            fixture.SampleDiagnostic(ResearchOperation.GetIncident, TargetId.PaymentsApi, 0));
    }

    [Fact]
    public void A03ReturnsEmptyAtElevenAndRequestLinkedEvidenceAtTwelve()
    {
        var fixture = new Er1EvidenceFixture();

        var before = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            11);
        var after = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            12);

        Assert.Empty(before.EvidenceIds);
        Assert.Empty(Assert.IsType<LogsObservationContent>(before.Content).Values);
        Assert.Equal(11u, before.AvailableTick);
        Assert.Equal(12u, after.AvailableTick);
        Assert.Equal(
            [
                "ev-auth-log-a17-1",
                "ev-auth-log-a17-2",
                "ev-auth-log-a17-3",
                "ev-auth-log-b09-1",
                "ev-auth-log-b09-2",
                "ev-auth-log-b09-3"
            ],
            after.EvidenceIds);
        Assert.Equal(6, Assert.IsType<LogsObservationContent>(after.Content).Values.Count);
        Assert.Empty(fixture.GetNotifications(12));
        Assert.Contains("ev-auth-log-a17-1", fixture.GetEvidenceCollectedAtTick(12));
    }

    [Fact]
    public void RegistryKeepsSourceAndCollectionTimesDistinct()
    {
        var fixture = new Er1EvidenceFixture();

        var delayed = Assert.Single(
            fixture.EvidenceRegistry,
            item => item.EvidenceId == "ev-auth-lat-0806");

        Assert.Equal(DateTimeOffset.Parse("2026-09-18T08:06:00Z"), delayed.SourceTime);
        Assert.Equal(12u, delayed.CollectionTick);
    }

    [Fact]
    public void AllMappingsExcludeFutureCollectionsAndKeepNeutralOrdinalIds()
    {
        var pairs = new[]
        {
            (ResearchOperation.GetIncident, TargetId.Incident),
            (ResearchOperation.GetServiceHealth, TargetId.PaymentsApi),
            (ResearchOperation.QueryMetrics, TargetId.PaymentsApi),
            (ResearchOperation.QueryLogs, TargetId.PaymentsApi),
            (ResearchOperation.GetServiceHealth, TargetId.AuthorizationService),
            (ResearchOperation.QueryMetrics, TargetId.AuthorizationService),
            (ResearchOperation.QueryLogs, TargetId.AuthorizationService)
        };
        var options = new[]
        {
            new Er1FixtureOptions(Er1DevelopmentVariant.Straightforward, false),
            new Er1FixtureOptions(Er1DevelopmentVariant.AmbiguousCpuSymptom, false),
            new Er1FixtureOptions(Er1DevelopmentVariant.Straightforward, true),
            new Er1FixtureOptions(Er1DevelopmentVariant.AmbiguousCpuSymptom, true)
        };

        foreach (var option in options)
        {
            var fixture = new Er1EvidenceFixture(option);
            var registry = fixture.EvidenceRegistry.ToDictionary(item => item.EvidenceId);
            Assert.All(registry.Keys, evidenceId =>
            {
                Assert.DoesNotContain("normal", evidenceId, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("recover", evidenceId, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("variant", evidenceId, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("cause", evidenceId, StringComparison.OrdinalIgnoreCase);
            });

            for (var tick = 0u; tick < 24; tick++)
            {
                foreach (var pair in pairs)
                {
                    var sample = fixture.SampleDiagnostic(pair.Item1, pair.Item2, tick);
                    Assert.Equal(
                        sample.EvidenceIds.Order(StringComparer.Ordinal),
                        sample.EvidenceIds);
                    Assert.All(sample.EvidenceIds, evidenceId =>
                        Assert.True(registry[evidenceId].CollectionTick <= tick));
                    Assert.IsNotType<UnavailableObservationContent>(sample.Content);
                }
            }
        }
    }

    [Fact]
    public void WorldScheduleCollectsTickTwelveEvidenceWithoutDeliveringNotification()
    {
        var fixture = new Er1EvidenceFixture();
        var scheduler = new ScriptedExperimentScheduler();
        var collections = new Dictionary<uint, IReadOnlyList<string>>();
        var deliveries = new List<Er1FixtureSample>();
        fixture.RegisterWorldSchedule(
            scheduler,
            (tick, evidenceIds) => collections.Add(tick, evidenceIds),
            deliveries.Add);

        var result = scheduler.Run();

        Assert.Contains(12u, collections.Keys);
        Assert.Contains("ev-auth-log-a17-1", collections[12]);
        Assert.DoesNotContain(deliveries, sample => sample.SampledTick == 12);
        Assert.All(
            result.Trace.Where(item => item.ActionId.StartsWith("er1-world-", StringComparison.Ordinal)),
            item => Assert.Equal(ResearchPhase.WorldDelivery, item.Phase));
    }

    [Fact]
    public void RecoveryWorldScheduleChangesEpochBeforeCollectionAndDelivery()
    {
        var fixture = new Er1EvidenceFixture(new Er1FixtureOptions(
            Er1DevelopmentVariant.Straightforward,
            ExternalRecovery: true));
        var scheduler = new ScriptedExperimentScheduler();
        var order = new List<string>();
        fixture.RegisterWorldSchedule(
            scheduler,
            (tick, _) =>
            {
                if (tick == 10)
                {
                    order.Add("collection");
                }
            },
            sample =>
            {
                if (sample.SampledTick == 10)
                {
                    order.Add("delivery");
                }
            },
            (previous, current, sourceId) =>
            {
                Assert.Equal(0u, previous);
                Assert.Equal(1u, current);
                Assert.Equal("external-recovery-10-v1", sourceId);
                order.Add("epoch");
            });

        scheduler.Run();

        Assert.Equal(["epoch", "collection", "delivery"], order);
    }

    [Fact]
    public void RecoveryMetricsAreCurrentAtTenAndRetainDelayedHistoryAtTwelve()
    {
        var fixture = new Er1EvidenceFixture(new Er1FixtureOptions(
            Er1DevelopmentVariant.Straightforward,
            ExternalRecovery: true));

        var atTen = fixture.SampleDiagnostic(
            ResearchOperation.QueryMetrics,
            TargetId.AuthorizationService,
            10);
        var atTwelve = fixture.SampleDiagnostic(
            ResearchOperation.QueryMetrics,
            TargetId.AuthorizationService,
            12);

        Assert.Equal(1u, atTen.ApplicabilityEpoch);
        Assert.Equal(10u, atTen.AvailableTick);
        Assert.Equal(
            ["ev-auth-count-0755", "ev-auth-lat-0755", "ev-auth-lat-0810"],
            atTen.EvidenceIds);
        Assert.Equal(110, Assert.IsType<MetricsObservationContent>(atTen.Content).Values[^1].Value);

        Assert.Equal(1u, atTwelve.ApplicabilityEpoch);
        Assert.Equal(12u, atTwelve.AvailableTick);
        Assert.Equal(
            [
                "ev-auth-count-0755",
                "ev-auth-count-0807",
                "ev-auth-lat-0755",
                "ev-auth-lat-0806",
                "ev-auth-lat-0807",
                "ev-auth-lat-0810"
            ],
            atTwelve.EvidenceIds);
        Assert.Equal(
            [95d, 2550d, 512d, 2800d, 110d],
            Assert.IsType<MetricsObservationContent>(atTwelve.Content)
                .Values
                .Skip(1)
                .Select(value => value.Value));
    }

    [Fact]
    public void RecoveryLogsPreserveHistoricalRecordsAndAddPromptRequest()
    {
        var fixture = new Er1EvidenceFixture(new Er1FixtureOptions(
            Er1DevelopmentVariant.Straightforward,
            ExternalRecovery: true));

        var sample = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            12);
        var logs = Assert.IsType<LogsObservationContent>(sample.Content).Values;

        Assert.Equal(1u, sample.ApplicabilityEpoch);
        Assert.Equal(9, logs.Count);
        Assert.Equal("requestId=req-a17 stage=arrived", logs[0].Message);
        Assert.Equal("requestId=req-c17 stage=processing-end", logs[^1].Message);
        Assert.Equal(
            DateTimeOffset.Parse("2026-09-18T08:06:00.500Z"),
            logs[0].Timestamp);
    }

    [Fact]
    public void DelayedDeliveryRetainsDispatchContentAvailabilityAndEpoch()
    {
        var fixture = new Er1EvidenceFixture(new Er1FixtureOptions(
            Er1DevelopmentVariant.Straightforward,
            ExternalRecovery: true));
        var sampled = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.PaymentsApi,
            9);

        var delivered = sampled.CreateObservation(
            RunId,
            "observation-1",
            "diagnostic-1",
            observedTick: 11,
            historyRevision: 1);

        Assert.Equal(9u, sampled.SampledTick);
        Assert.Equal(8u, delivered.AvailableTick);
        Assert.Equal(11u, delivered.ObservedTick);
        Assert.Equal(0u, delivered.ApplicabilityEpoch);
        Assert.Equal(2, Assert.IsType<LogsObservationContent>(delivered.Content).Values.Count);
    }

    [Fact]
    public void RepeatedRetrievalChangesObservationIdentityButNotEvidenceOrContent()
    {
        var fixture = new Er1EvidenceFixture();
        var firstSample = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            12);
        var secondSample = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            13);
        var first = firstSample.CreateObservation(
            RunId,
            "observation-8",
            "diagnostic-5",
            12,
            8);
        var second = secondSample.CreateObservation(
            RunId,
            "observation-9",
            "diagnostic-6",
            13,
            9);

        Assert.NotEqual(first.ObservationId, second.ObservationId);
        Assert.NotEqual(first.DiagnosticId, second.DiagnosticId);
        Assert.NotEqual(first.HistoryRevision, second.HistoryRevision);
        Assert.Equal(first.AvailableTick, second.AvailableTick);
        Assert.Equal(first.EvidenceIds, second.EvidenceIds);
        Assert.Equal(
            JsonSerializer.Serialize(first.Content, ContractJson.Options),
            JsonSerializer.Serialize(second.Content, ContractJson.Options));
    }

    [Fact]
    public void AmbiguousVariantMakesE2QueryableWithoutPushingIt()
    {
        var fixture = new Er1EvidenceFixture(new Er1FixtureOptions(
            Er1DevelopmentVariant.AmbiguousCpuSymptom,
            ExternalRecovery: false));

        var metrics = Assert.IsType<MetricsObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.QueryMetrics,
                TargetId.PaymentsApi,
                8).Content).Values;
        var logs = Assert.IsType<LogsObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.QueryLogs,
                TargetId.PaymentsApi,
                8).Content).Values;

        Assert.Empty(fixture.GetNotifications(8));
        Assert.Equal(2, logs.Count);
        Assert.Contains(metrics, value =>
            value.Name == "process.cpu.utilization.payments-api-03" &&
            value.Value == 0.87);
        Assert.Contains(metrics, value =>
            value.Name == "process.cpu.utilization.payments-api-03" &&
            value.Value == 0.79);
        Assert.DoesNotContain(
            metrics.Select(value => value.Name),
            name => name.Contains("cause", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void NormalEmptyResultAndExplicitUnavailableFailureRemainDistinct()
    {
        var fixture = new Er1EvidenceFixture();

        var empty = fixture.SampleDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            3);
        var unavailable = fixture.SampleExplicitUnavailableDiagnostic(
            ResearchOperation.QueryLogs,
            TargetId.AuthorizationService,
            3);

        Assert.IsType<LogsObservationContent>(empty.Content);
        Assert.IsType<UnavailableObservationContent>(unavailable.Content);
        Assert.Empty(empty.EvidenceIds);
        Assert.Empty(unavailable.EvidenceIds);
    }

    [Fact]
    public async Task FixtureReadTraversesGovernedGateway()
    {
        var tick = 12u;
        var fixture = new Er1EvidenceFixture();
        var simulator = new Er1FixtureIncidentSimulator(fixture, () => tick);
        var registry = new ToolRegistry();
        var canonicalizer = new ActionCanonicalizer(registry);
        var arguments = new Dictionary<string, JsonElement>
        {
            ["serviceId"] = JsonSerializer.SerializeToElement(
                Er1EvidenceFixture.AuthorizationServiceId)
        };
        var step = new PlanStep(
            "step-1",
            "telemetry.logs.read",
            "query_logs",
            new ResourceReference(
                "service",
                Er1EvidenceFixture.AuthorizationServiceId,
                TargetEnvironment.Development,
                DataClassification.Internal),
            [new DataSourceReference("authorization-service-logs", DataClassification.Internal)],
            new DestinationReference("shared-history", DataClassification.InternalTrusted),
            arguments,
            [],
            EffectKind.Read,
            ApprovalClass.None,
            null);
        var plan = new ActionPlan(
            "1.0",
            Guid.NewGuid(),
            Er1EvidenceFixture.IncidentId,
            "incident-agent",
            "1.0.0",
            Now.AddMinutes(-1),
            Now.AddMinutes(5),
            [step]);
        var digest = canonicalizer.CreateDigest(plan, step);
        var envelope = new TrustedActionEnvelope(
            "1.0",
            Guid.NewGuid(),
            Now,
            new UserIdentity("operator-1", ["incident-operator"]),
            new AgentIdentity("incident-agent", "agent-identity", "1.0.0"),
            new SessionIdentity("session-1", Er1EvidenceFixture.IncidentId),
            new GovernedAction(
                plan.PlanId,
                step.StepId,
                step.Tool,
                step.Capability,
                step.Effect,
                new ActionResource(step.Resource.Id, step.Resource.Environment),
                digest.Value),
            new VerificationAttestation(
                VerificationResult.Verified,
                "1.0",
                "1.0",
                new string('c', 64)));
        var audit = new InMemoryAuditChain();
        var gateway = new GovernedToolGateway(
            registry,
            canonicalizer,
            new DefaultDenyPolicyEvaluator(new FixedTimeProvider(Now)),
            new InMemoryApprovalStore(),
            new InMemoryExecutionBudgetStore(
                new ExecutionBudgetLimits(12, TimeSpan.FromMinutes(3))),
            new InMemoryKillSwitch(),
            audit,
            new SimulatorGovernedToolExecutor(simulator),
            new FixedTimeProvider(Now));

        var result = await gateway.ExecuteAsync(
            new GovernedToolRequest(
                plan,
                step.StepId,
                envelope,
                ApprovalNonce: null,
                "diagnostic-1",
                ExpectedResourceVersion: 0),
            CancellationToken.None);

        Assert.Equal(GatewayOutcome.Executed, result.Outcome);
        Assert.Equal(6, result.ToolResult!.Value.GetArrayLength());
        Assert.Equal(2, audit.ReadAll().Count);
        Assert.True(audit.VerifyIntegrity());
    }

    [Fact]
    public void OriginalSimulatorDefaultsRemainUnchanged()
    {
        var simulator = new IncidentSimulator();

        Assert.Equal(
            ServiceHealth.Degraded,
            simulator.GetServiceHealth(IncidentSimulator.DemoServiceId).Health);
        Assert.Contains(
            simulator.QueryLogs(IncidentSimulator.DemoServiceId),
            entry => entry.ContainsUntrustedContent);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
