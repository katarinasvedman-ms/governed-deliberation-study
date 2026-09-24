using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovernedAgent.Core.Contracts;
using GovernedAgent.Core.Serialization;
using GovernedAgent.Research;
using Microsoft.Agents.AI.Workflows;

namespace GovernedAgent.IntegrationTests.T3;

public sealed class Er1StraightforwardSmokeRunTests
{
    private static readonly string Schema = ResearchContractVersions.SchemaVersion;
    private static readonly string[] ExcludedCanonicalFields =
    [
        "runId",
        "planId",
        "requestId and request-derived correlationId",
        "audit recordId and auditRecordIds",
        "encounter-normalized actor, review, diagnostic, observation, snapshot, " +
            "result, memory-update, belief, direction, and trigger identifiers",
        "artifactPath"
    ];

    [Fact]
    public async Task Er1StraightforwardSmokeRun()
    {
        var repositoryRoot = FindRepositoryRoot();
        var repository = ReadRepositoryState(repositoryRoot);
        var runtimeVersions = ReadRuntimeVersions(repositoryRoot);
        var runSetId =
            $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}";
        var runSetDirectory = Path.Combine(
            repositoryRoot,
            ".artifacts",
            "deliberation-study",
            "smoke-er1",
            runSetId);
        Directory.CreateDirectory(runSetDirectory);

        var episodes = new List<SmokeEpisode>();
        foreach (var architecture in Enum.GetValues<SupervisionArchitecture>())
        {
            for (var repetition = 1; repetition <= 2; repetition++)
            {
                episodes.Add(await RunEpisodeAsync(
                    repositoryRoot,
                    runSetDirectory,
                    architecture,
                    repetition,
                    repository,
                    runtimeVersions));
            }
        }

        var comparisons = new List<ReproducibilityResult>();
        foreach (var architecture in Enum.GetValues<SupervisionArchitecture>())
        {
            var pair = episodes
                .Where(item => item.Architecture == architecture)
                .OrderBy(item => item.Repetition)
                .ToArray();
            Assert.Equal(2, pair.Length);
            var firstMismatch = FindFirstMismatch(
                pair[0].CanonicalRecords,
                pair[1].CanonicalRecords,
                "$");
            comparisons.Add(new ReproducibilityResult(
                ArchitectureName(architecture),
                firstMismatch is null,
                firstMismatch));
            Assert.Null(firstMismatch);
        }

        var reproducibilityPath = Path.Combine(
            runSetDirectory,
            "reproducibility.json");
        await File.WriteAllTextAsync(
            reproducibilityPath,
            JsonSerializer.Serialize(
                new
                {
                    runSetId,
                    excludedFields = ExcludedCanonicalFields,
                    architectures = comparisons
                },
                new JsonSerializerOptions { WriteIndented = true }));

        Assert.All(comparisons, item => Assert.True(item.Equivalent));
        Assert.All(episodes, AssertEpisodeOutcome);
        Assert.Contains(
            episodes,
            item =>
                item.Architecture ==
                    SupervisionArchitecture.AsynchronousSupervision &&
                item.Events.Any(
                    researchEvent =>
                        researchEvent.EventType == "result.suppressed"));
        Assert.All(
            episodes.Where(
                item => item.Architecture != SupervisionArchitecture.ActorOnly),
            item => Assert.Contains(
                item.Events,
                researchEvent =>
                    researchEvent.EventType == "memory.committed"));

        System.Console.WriteLine($"Smoke artifacts: {runSetDirectory}");
        var asynchronousTimeline = await File.ReadAllLinesAsync(
            episodes.Single(
                item =>
                    item.Architecture ==
                        SupervisionArchitecture.AsynchronousSupervision &&
                    item.Repetition == 1)
                .TimelinePath);
        System.Console.WriteLine(
            string.Join(
                Environment.NewLine,
                asynchronousTimeline.Where(
                    line =>
                        line.StartsWith("tick |", StringComparison.Ordinal) ||
                        line.StartsWith("11 |", StringComparison.Ordinal) ||
                        line.StartsWith("12 |", StringComparison.Ordinal) ||
                        line.StartsWith("13 |", StringComparison.Ordinal) ||
                        line.StartsWith("15 |", StringComparison.Ordinal))));
    }

    private static async Task<SmokeEpisode> RunEpisodeAsync(
        string repositoryRoot,
        string runSetDirectory,
        SupervisionArchitecture architecture,
        int repetition,
        RepositoryState repository,
        RuntimeVersions runtimeVersions)
    {
        var runId = Guid.NewGuid().ToString("D");
        var ids = new DeterministicOpaqueIdSource();
        var dispatcher = new GovernedWorkflowDiagnosticDispatcher
        {
            PlanIdFactory = ids.NextPlanId,
            RequestIdFactory = ids.NextRequestId,
            AuditRecordIdFactory = ids.NextAuditRecordId
        };
        var scheduler = new ScriptedExperimentScheduler();
        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        await using var coordinator = new ScriptedSupervisionCoordinator(
            runId,
            architecture,
            scheduler,
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);
        var actorDue = new Dictionary<uint, List<string>>();
        var reviewDue = new Dictionary<uint, List<string>>();
        var processedActors = new HashSet<string>(StringComparer.Ordinal);
        var processedReviews = new HashSet<string>(StringComparer.Ordinal);
        var processedDiagnostics = new HashSet<string>(StringComparer.Ordinal);

        fixture.RegisterWorldSchedule(
            scheduler,
            (_, _) => { },
            notification => coordinator.RecordExternalObservation(
                notification,
                notification.AvailableTick));

        for (uint tick = 0; tick < scheduler.Clock.EndTickExclusive; tick++)
        {
            var scheduledTick = tick;
            scheduler.ScheduleAsync(
                scheduledTick,
                ResearchPhase.ReviewProcessing,
                $"smoke-review-processing-{scheduledTick}",
                async (_, cancellationToken) =>
                {
                    coordinator.ExpireDirection(scheduledTick);
                    if (!reviewDue.TryGetValue(
                            scheduledTick,
                            out var dueReviews))
                    {
                        return;
                    }

                    foreach (var reviewId in dueReviews)
                    {
                        await coordinator.ProcessReviewResultAsync(
                            reviewId,
                            scheduledTick,
                            cancellationToken);
                        processedReviews.Add(reviewId);
                    }
                });
            scheduler.ScheduleAsync(
                scheduledTick,
                ResearchPhase.ActorProcessing,
                $"smoke-actor-processing-{scheduledTick}",
                async (_, cancellationToken) =>
                {
                    if (actorDue.TryGetValue(
                            scheduledTick,
                            out var dueActors))
                    {
                        foreach (var actorTurnId in dueActors)
                        {
                            await coordinator.ProcessActorResultAsync(
                                actorTurnId,
                                scheduledTick,
                                cancellationToken);
                            processedActors.Add(actorTurnId);
                        }
                    }

                    await ProcessNewDiagnosticsAsync(
                        coordinator,
                        processedDiagnostics,
                        scheduledTick,
                        ResearchPhase.ActorProcessing,
                        cancellationToken);
                });

            if (scheduler.ReviewCheckpoints.Contains(scheduledTick))
            {
                scheduler.Schedule(
                    scheduledTick,
                    ResearchPhase.ReviewStart,
                    $"smoke-review-start-{scheduledTick}",
                    _ =>
                    {
                        var review = coordinator.StartReview(
                            scheduledTick,
                            (snapshot, cancellationToken) =>
                                ValueTask.FromResult(
                                    ScriptedSupervisor(
                                        snapshot,
                                        cancellationToken)));
                        if (review is not null)
                        {
                            AddDue(
                                reviewDue,
                                scheduler.ReviewCompletionTick(scheduledTick),
                                review.ReviewId);
                        }
                    });
            }

            scheduler.Schedule(
                scheduledTick,
                ResearchPhase.ActorStart,
                $"smoke-actor-start-{scheduledTick}",
                _ =>
                {
                    var actor = coordinator.StartActor(
                        scheduledTick,
                        (snapshot, cancellationToken) =>
                            ValueTask.FromResult(
                                ScriptedActor(snapshot, cancellationToken)));
                    if (actor is not null)
                    {
                        AddDue(
                            actorDue,
                            scheduler.ActorCompletionTick(scheduledTick),
                            actor.ActorTurnId);
                    }
                });
        }

        scheduler.Schedule(
            scheduler.Clock.EndTickExclusive,
            ResearchPhase.EndCheck,
            "smoke-exclusive-horizon",
            _ =>
            {
                if (coordinator.Termination is null)
                {
                    coordinator.Terminate(
                        scheduler.Clock.EndTickExclusive,
                        ResearchPhase.EndCheck,
                        "timeout",
                        new Reason(
                            ReasonDomain.Context,
                            "horizon-reached",
                            "The exclusive horizon was reached."));
                }
            });

        var scheduleResult = await scheduler.RunAsync();
        await DrainAsync(
            coordinator,
            actorDue,
            reviewDue,
            processedActors,
            processedReviews,
            processedDiagnostics,
            scheduler.Clock.EndTickExclusive);
        var closure = coordinator.Close();
        var termination = Assert.IsType<Termination>(coordinator.Termination);
        var manifest = CreateManifest(
            repositoryRoot,
            runId,
            architecture,
            repository,
            runtimeVersions);
        ValidateEpisode(
            manifest,
            coordinator,
            termination,
            closure,
            dispatcher,
            scheduleResult);

        var architectureName = ArchitectureName(architecture);
        var episodeDirectory = Path.Combine(
            runSetDirectory,
            architectureName,
            repetition.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(episodeDirectory);
        var timelinePath = await WriteArtifactsAsync(
            episodeDirectory,
            manifest,
            coordinator,
            termination,
            closure,
            dispatcher.AuditRecords);
        var evaluation = await C6DeterministicEvaluator.EvaluateAndWriteAsync(
            episodeDirectory);
        var canonical = CreateCanonicalRecords(
            manifest,
            coordinator,
            termination,
            closure,
            dispatcher.AuditRecords);
        return new SmokeEpisode(
            architecture,
            repetition,
            coordinator.Events,
            termination,
            closure,
            dispatcher.DispatchCount,
            dispatcher.AuditRecords.Count,
            timelinePath,
            evaluation,
            canonical);
    }

    private static ActorDecision ScriptedActor(
        InputSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var e3 = snapshot.Observations.LastOrDefault(
            observation =>
                observation.SourceKind == "diagnostic" &&
                observation.SourceId ==
                    "query_logs.authorization-service" &&
                observation.EvidenceIds.Contains(
                    "ev-auth-log-a17-1",
                    StringComparer.Ordinal));
        var disposition = CreateDisposition(snapshot);
        var usedBeliefIds = snapshot.WorkingMemory.BeliefStates
            .Where(item => item.State == BeliefState.Provisional)
            .Select(item => item.BeliefId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (e3 is not null)
        {
            return new ReportActorDecision(
                Schema,
                "actor-decision",
                disposition,
                usedBeliefIds,
                Hypothesis.DependencySideQueueDelay,
                [e3.ObservationId],
                NextStep.InvestigateDependencyQueue,
                Uncertainty.Medium);
        }

        if (snapshot.WorkingMemory.RecommendedDirection?.Recommendation is
            FocusDirectionChoice
            {
                Focus: DiagnosticFocus
                {
                    Operation: ResearchOperation.QueryLogs,
                    TargetId: TargetId.AuthorizationService
                }
            })
        {
            return new QueryActorDecision(
                Schema,
                "actor-decision",
                disposition,
                usedBeliefIds,
                ResearchOperation.QueryLogs,
                TargetId.AuthorizationService,
                new Dictionary<string, JsonElement>());
        }

        if (snapshot.Tick is 0 or 3 or 4 or 7 or 8 or 9 or 10 or 16)
        {
            return new QueryActorDecision(
                Schema,
                "actor-decision",
                disposition,
                usedBeliefIds,
                ResearchOperation.QueryMetrics,
                TargetId.PaymentsApi,
                new Dictionary<string, JsonElement>());
        }

        return new WaitActorDecision(
            Schema,
            "actor-decision",
            disposition,
            usedBeliefIds,
            4);
    }

    private static SupervisorOutput ScriptedSupervisor(
        InputSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var e2 = snapshot.Observations.LastOrDefault(
            observation =>
                observation.EvidenceIds.Contains(
                    "ev-notification-08-v1",
                    StringComparer.Ordinal));
        var hasE3 = snapshot.Observations.Any(
            observation =>
                observation.EvidenceIds.Contains(
                    "ev-auth-log-a17-1",
                    StringComparer.Ordinal));
        if (e2 is null || hasE3)
        {
            return new NoChangeSupervisorOutput(
                Schema,
                "supervisor-output",
                snapshot.MemoryRevision,
                [],
                "No actionable change.",
                Uncertainty.Medium);
        }

        return new ProposeMemoryUpdateSupervisorOutput(
            Schema,
            "supervisor-output",
            snapshot.MemoryRevision,
            [e2.ObservationId],
            "Inspect authorization-service request processing records.",
            Uncertainty.Medium,
            [
                new AddBeliefOperation(
                    "dependency-path",
                    new Claim(
                        Hypothesis.DependencyPathIssue,
                        TargetId.AuthorizationService,
                        [e2.ObservationId],
                        Uncertainty.Medium)),
                new SetDirectionOperation(
                    new FocusDirectionChoice(
                        new DiagnosticFocus(
                            ResearchOperation.QueryLogs,
                            TargetId.AuthorizationService,
                            new Dictionary<string, JsonElement>())),
                    [e2.ObservationId],
                    [new ProposedBeliefRef("dependency-path")])
            ]);
    }

    private static MemoryDisposition? CreateDisposition(InputSnapshot snapshot)
    {
        if (snapshot.Reconsideration is not { } reconsideration)
        {
            return null;
        }

        return new MemoryDisposition(
            reconsideration.Trigger.TriggerId,
            reconsideration.Trigger.MemoryUpdateId,
            Stance.Adapt,
            DispositionReason.FollowSuggestedDirection);
    }

    private static async Task ProcessNewDiagnosticsAsync(
        ScriptedSupervisionCoordinator coordinator,
        ISet<string> processedDiagnostics,
        uint tick,
        ResearchPhase phase,
        CancellationToken cancellationToken)
    {
        var requested = coordinator.Events
            .Where(item => item.EventType == "diagnostic.requested")
            .Select(item => item.Data.GetProperty("diagnosticId").GetString()!)
            .Where(item => !processedDiagnostics.Contains(item))
            .ToArray();
        foreach (var diagnosticId in requested)
        {
            await coordinator.ProcessDiagnosticAuthorizationAsync(
                diagnosticId,
                tick,
                phase,
                cancellationToken);
            await coordinator.ProcessDiagnosticResultAsync(
                diagnosticId,
                tick,
                phase,
                cancellationToken);
            processedDiagnostics.Add(diagnosticId);
        }
    }

    private static async Task DrainAsync(
        ScriptedSupervisionCoordinator coordinator,
        IReadOnlyDictionary<uint, List<string>> actorDue,
        IReadOnlyDictionary<uint, List<string>> reviewDue,
        ISet<string> processedActors,
        ISet<string> processedReviews,
        ISet<string> processedDiagnostics,
        uint tick)
    {
        foreach (var actorTurnId in actorDue.Values
                     .SelectMany(item => item)
                     .Where(item => !processedActors.Contains(item)))
        {
            await coordinator.ProcessActorResultAsync(actorTurnId, tick);
            processedActors.Add(actorTurnId);
        }

        foreach (var reviewId in reviewDue.Values
                     .SelectMany(item => item)
                     .Where(item => !processedReviews.Contains(item)))
        {
            await coordinator.ProcessReviewResultAsync(reviewId, tick);
            processedReviews.Add(reviewId);
        }

        await ProcessNewDiagnosticsAsync(
            coordinator,
            processedDiagnostics,
            tick,
            ResearchPhase.Drain,
            CancellationToken.None);
    }

    private static void ValidateEpisode(
        RunManifest manifest,
        ScriptedSupervisionCoordinator coordinator,
        Termination termination,
        RunClosure closure,
        GovernedWorkflowDiagnosticDispatcher dispatcher,
        ScriptedScheduleResult scheduleResult)
    {
        var roots = new List<IResearchRoot>
        {
            manifest,
            coordinator.WorkingMemory,
            termination,
            closure
        };
        roots.AddRange(coordinator.Events);
        roots.AddRange(coordinator.Observations);
        roots.AddRange(coordinator.InvocationResults);
        roots.AddRange(coordinator.MemoryUpdates);
        roots.AddRange(coordinator.Beliefs);
        foreach (var root in roots)
        {
            ResearchContractValidator.ValidateAndThrow(root);
            ResearchContractSerializer.DeserializeRoot(
                ResearchContractSerializer.Serialize(root));
        }

        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                termination));
        Assert.True(scheduleResult.HorizonReached);
        Assert.Equal("complete", closure.Status);
        Assert.Empty(closure.UnsettledInvocations);
        Assert.Empty(closure.UnsettledDiagnosticIds);

        var requested = coordinator.Events
            .Where(item => item.EventType == "diagnostic.requested")
            .ToArray();
        var dispatched = coordinator.Events
            .Where(item => item.EventType == "diagnostic.dispatched")
            .ToArray();
        var completed = coordinator.Events
            .Where(item => item.EventType == "diagnostic.completed")
            .ToArray();
        Assert.Equal(requested.Length, dispatcher.DispatchCount);
        Assert.Equal(requested.Length, dispatched.Length);
        Assert.Equal(requested.Length, completed.Length);
        Assert.Equal(dispatcher.AuditRecordCount, dispatcher.AuditRecords.Count);
        Assert.NotEmpty(dispatcher.AuditRecords);
        Assert.Equal(
            dispatcher.DispatchCount,
            dispatched
                .Select(item =>
                    item.Data.GetProperty("binding")
                        .GetProperty("planId")
                        .GetString())
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            dispatcher.DispatchCount,
            dispatcher.AuditRecords
                .Select(item => item.RequestId)
                .Distinct()
                .Count());
        Assert.Equal(
            dispatcher.DispatchCount,
            dispatcher.AuditRecords
                .Select(item => item.PlanId)
                .Distinct()
                .Count());
        Assert.Equal(
            dispatcher.AuditRecords.Count,
            dispatcher.AuditRecords
                .Select(item => item.RecordId)
                .Distinct()
                .Count());
        Assert.All(
            dispatcher.AuditRecords,
            item => Assert.Equal(
                item.RequestId.ToString("D"),
                item.CorrelationId));
        Assert.Equal(
            "governed-adapter-deterministic-id-seam",
            manifest.GovernanceConfig.Id);

        var auditIds = dispatcher.AuditRecords
            .Select(item => item.RecordId.ToString("D"))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var completion in completed)
        {
            var ids = completion.Data.GetProperty("auditRecordIds")
                .EnumerateArray()
                .Select(item => item.GetString()!)
                .ToArray();
            Assert.NotEmpty(ids);
            Assert.All(ids, item => Assert.Contains(item, auditIds));
        }

        foreach (var observation in coordinator.Observations.Where(
                     item => item.DiagnosticId is not null))
        {
            Assert.Contains(
                dispatched,
                item =>
                    item.Data.GetProperty("diagnosticId").GetString() ==
                    observation.DiagnosticId);
            Assert.Contains(
                completed,
                item =>
                    item.Data.GetProperty("diagnosticId").GetString() ==
                        observation.DiagnosticId &&
                    item.Data.GetProperty("observationId").GetString() ==
                        observation.ObservationId);
        }

        foreach (var sameContent in coordinator.Observations
                     .Where(item => item.SourceKind == "diagnostic")
                     .GroupBy(
                         item => new
                         {
                             item.SourceId,
                             Content = JsonSerializer.Serialize(
                                 item.Content,
                                 ResearchContractSerializer.Options)
                         })
                     .Where(item => item.Count() > 1))
        {
            var expected = sameContent.First().EvidenceIds;
            Assert.All(
                sameContent,
                item => Assert.Equal(expected, item.EvidenceIds));
            Assert.Equal(
                sameContent.Count(),
                sameContent.Select(item => item.ObservationId).Distinct().Count());
        }

        var eventAuditDigests = dispatched
            .Select(item =>
                item.Data.GetProperty("binding")
                    .GetProperty("actionDigest")
                    .GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(
            dispatcher.AuditRecords,
            item => Assert.Contains(item.ActionDigest, eventAuditDigests));
        Assert.All(
            dispatcher.AuditRecords,
            item => Assert.Matches("^[0-9a-f]{64}$", item.RecordHash));
    }

    private static void AssertEpisodeOutcome(SmokeEpisode episode)
    {
        Assert.Equal("complete", episode.Closure.Status);
        Assert.True(episode.DispatchCount > 0);
        Assert.True(episode.AuditRecordCount > 0);
        switch (episode.Architecture)
        {
            case SupervisionArchitecture.ActorOnly:
                Assert.Equal("timeout", episode.Termination.Kind);
                Assert.Equal(24u, episode.Termination.Tick);
                Assert.Null(episode.Evaluation.Report);
                break;
            case SupervisionArchitecture.BlockingSupervision:
                Assert.Equal("report", episode.Termination.Kind);
                Assert.Equal(16u, episode.Termination.Tick);
                AssertSmokeEvaluation(episode.Evaluation);
                break;
            case SupervisionArchitecture.AsynchronousSupervision:
                Assert.Equal("report", episode.Termination.Kind);
                Assert.Equal(13u, episode.Termination.Tick);
                AssertSmokeEvaluation(episode.Evaluation);
                Assert.Contains(
                    episode.Events,
                    item => item.EventType == "actor.invalidated");
                Assert.Contains(
                    episode.Events,
                    item => item.EventType == "result.suppressed");
                break;
            default:
                throw new InvalidOperationException("Unknown architecture.");
        }
    }

    private static void AssertSmokeEvaluation(C6EpisodeEvaluation evaluation)
    {
        var report = Assert.IsType<C6ReportEvaluation>(evaluation.Report);
        Assert.Equal("supported", report.DiagnosisClass);
        Assert.Equal(
            "supported",
            report.StrictCitationPolicyDiagnosisClass);
        Assert.Equal("acceptable", report.NextStepClass);
        Assert.True(report.NeutralFacts.ObservedE3);
        Assert.True(report.NeutralFacts.CitedE3DerivedObservation);
        Assert.False(report.NeutralFacts.CitedOnlyE2);
        Assert.False(report.NeutralFacts.MechanismNamingBeforeEvidence);
    }

    private static RunManifest CreateManifest(
        string repositoryRoot,
        string runId,
        SupervisionArchitecture architecture,
        RepositoryState repository,
        RuntimeVersions runtimeVersions)
    {
        var actor = Definition(
            "section-7-scripted-actor",
            "report-e3|follow-accepted-dependency-focus|bounded-local-query-wait");
        var supervisor = Definition(
            "section-7-scripted-supervisor",
            "propose-dependency-focus-when-e2-without-e3|otherwise-no-change");
        return new RunManifest(
            Schema,
            "run-manifest",
            runId,
            new StudySpecificationRef(
                ResearchContractVersions.StudyBaseline,
                ResearchContractVersions.StudyAmendment,
                ResearchContractVersions.StudyCandidate),
            ResearchContractVersions.ContractBaseline,
            ArchitectureName(architecture),
            "isolated-invocations-single-coordinator",
            "supervision-plus-within-incident-memory-adaptation",
            "scripted",
            "synthetic",
            repository.Revision,
            repository.Dirty,
            SourceFiles(repositoryRoot),
            actor,
            architecture == SupervisionArchitecture.ActorOnly
                ? null
                : supervisor,
            Definition(
                "er1-fixture-1-straightforward",
                "ER1-fixture-1|straightforward|external-recovery=false"),
            Definition(
                "er1-approved-external-schedule",
                "E0@0|E1@4|E2@8|silent-E3-collection@12|E4@16"),
            Definition(
                "seven-query-catalogue",
                "approved-seven-operation-target-pairs|arguments={}"),
            Definition(
                "governed-adapter-deterministic-id-seam",
                "real-verifier-policy-gateway-audit-fixture-simulator|" +
                "deterministic-plan-request-audit-identities-only"),
            Definition(
                "candidate-3-memory-rules",
                "supervisor-only-writes|atomic-publication|generation-invalidation|" +
                "applicability-expiry-duplicate-handling"),
            null,
            null,
            ApprovedScriptedExperiment.Clock,
            ApprovedScriptedExperiment.Limits,
            new EngineeringLimits(
                ResearchEngineeringLimits.MaximumComponentOutputBytes,
                ResearchEngineeringLimits.MaximumOperationsPerUpdate,
                ResearchEngineeringLimits.MaximumComponentTextBytes,
                ResearchEngineeringLimits.MaximumCitationsPerField,
                ResearchEngineeringLimits.MaximumInputSnapshotBytes,
                ResearchEngineeringLimits.MaximumEventBytes),
            runtimeVersions,
            "unavailable-scripted");
    }

    private static IReadOnlyList<SourceFileRef> SourceFiles(
        string repositoryRoot)
    {
        string[] paths =
        [
            "src/GovernedAgent.Research/ScriptedExperimentScheduler.cs",
            "src/GovernedAgent.Research/Er1EvidenceFixture.cs",
            "src/GovernedAgent.Research/CoordinatorMemoryStore.cs",
            "src/GovernedAgent.Research/IsolatedAgentFrameworkRuntime.cs",
            "src/GovernedAgent.Research/ScriptedSupervisionCoordinator.cs",
            "tests/GovernedAgent.IntegrationTests/T3/" +
                "GovernedWorkflowDiagnosticDispatcher.cs",
            "tests/GovernedAgent.IntegrationTests/T3/" +
                "Er1StraightforwardSmokeRunTests.cs"
        ];
        return paths
            .Select(path => new SourceFileRef(
                path,
                Sha256(File.ReadAllText(
                    Path.Combine(
                        repositoryRoot,
                        path.Replace('/', Path.DirectorySeparatorChar))))))
            .ToArray();
    }

    private static DefinitionRef Definition(string id, string content) =>
        new(id, Sha256(content));

    private static async Task<string> WriteArtifactsAsync(
        string directory,
        RunManifest manifest,
        ScriptedSupervisionCoordinator coordinator,
        Termination termination,
        RunClosure closure,
        IReadOnlyList<AuditRecord> auditRecords)
    {
        await WriteRootAsync(
            Path.Combine(directory, "run-manifest.json"),
            manifest);
        await WriteRootsAsync(
            Path.Combine(directory, "events.jsonl"),
            coordinator.Events);
        await WriteRootsAsync(
            Path.Combine(directory, "observations.jsonl"),
            coordinator.Observations);
        await WriteRootsAsync(
            Path.Combine(directory, "invocation-results.jsonl"),
            coordinator.InvocationResults);
        await WriteJsonLinesAsync(
            Path.Combine(directory, "actions.jsonl"),
            coordinator.ActionHistory);
        await WriteJsonLinesAsync(
            Path.Combine(directory, "reviews.jsonl"),
            coordinator.ReviewHistory);
        await WriteRootsAsync(
            Path.Combine(directory, "memory-updates.jsonl"),
            coordinator.MemoryUpdates);
        await WriteRootsAsync(
            Path.Combine(directory, "beliefs.jsonl"),
            coordinator.Beliefs);
        await WriteJsonLinesAsync(
            Path.Combine(directory, "audit-records.jsonl"),
            auditRecords);
        await WriteRootAsync(
            Path.Combine(directory, "termination.json"),
            termination);
        await WriteRootAsync(
            Path.Combine(directory, "run-closure.json"),
            closure);
        var timelinePath = Path.Combine(directory, "timeline.txt");
        await File.WriteAllTextAsync(
            timelinePath,
            CreateTimeline(coordinator.Events));
        return timelinePath;
    }

    private static async Task WriteRootAsync(
        string path,
        IResearchRoot root)
    {
        var serialized = ResearchContractSerializer.Serialize(root);
        ResearchContractSerializer.DeserializeRoot(serialized);
        await File.WriteAllTextAsync(path, serialized + Environment.NewLine);
    }

    private static async Task WriteRootsAsync<T>(
        string path,
        IReadOnlyList<T> roots)
        where T : IResearchRoot
    {
        var lines = roots
            .Select(root => ResearchContractSerializer.Serialize(root))
            .ToArray();
        foreach (var line in lines)
        {
            ResearchContractSerializer.DeserializeRoot(line);
        }

        await File.WriteAllLinesAsync(path, lines);
    }

    private static Task WriteJsonLinesAsync<T>(
        string path,
        IReadOnlyList<T> values) =>
        File.WriteAllLinesAsync(
            path,
            values.Select(
                item => JsonSerializer.Serialize(
                    item,
                    ResearchContractSerializer.Options)));

    private static string CreateTimeline(IReadOnlyList<ResearchEvent> events)
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            "tick | phase | event | actor/review/diagnostic | history | memory | " +
            "generation | summary");
        foreach (var item in events)
        {
            var identifiers = new[]
                {
                    GetString(item.Data, "actorTurnId"),
                    GetString(item.Data, "reviewId"),
                    GetString(item.Data, "diagnosticId")
                }
                .Where(value => value is not null);
            var summary = new[]
                {
                    Pair(item.Data, "observationId"),
                    Pair(item.Data, "snapshotId"),
                    Pair(item.Data, "resultId"),
                    Pair(item.Data, "memoryUpdateId"),
                    NestedPair(item.Data, "reason", "code")
                }
                .Where(value => value is not null);
            builder.Append(item.Tick?.ToString() ?? "-")
                .Append(" | ")
                .Append(item.Phase)
                .Append(" | ")
                .Append(item.EventType)
                .Append(" | ")
                .Append(string.Join(",", identifiers))
                .Append(" | ")
                .Append(item.HistoryRevision)
                .Append(" | ")
                .Append(item.CurrentMemoryRevision)
                .Append(" | ")
                .Append(item.CurrentGeneration)
                .Append(" | ")
                .AppendLine(string.Join(",", summary));
        }

        return builder.ToString();
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.ValueKind == JsonValueKind.Object &&
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static string? Pair(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return value is null ? null : $"{propertyName}={value}";
    }

    private static string? NestedPair(
        JsonElement element,
        string parentName,
        string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object ||
            !element.TryGetProperty(parentName, out var parent) ||
            parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var value = GetString(parent, propertyName);
        return value is null ? null : $"{parentName}.{propertyName}={value}";
    }

    private static JsonNode CreateCanonicalRecords(
        RunManifest manifest,
        ScriptedSupervisionCoordinator coordinator,
        Termination termination,
        RunClosure closure,
        IReadOnlyList<AuditRecord> auditRecords)
    {
        var records = new JsonObject
        {
            ["manifest"] = ToNode(manifest),
            ["events"] = ToArray(coordinator.Events.Select(ToNode)),
            ["observations"] = ToArray(coordinator.Observations.Select(ToNode)),
            ["invocationResults"] =
                ToArray(coordinator.InvocationResults.Select(ToNode)),
            ["actions"] = ToArray(
                coordinator.ActionHistory.Select(
                    item => JsonSerializer.SerializeToNode(
                        item,
                        ResearchContractSerializer.Options)!)),
            ["reviews"] = ToArray(
                coordinator.ReviewHistory.Select(
                    item => JsonSerializer.SerializeToNode(
                        item,
                        ResearchContractSerializer.Options)!)),
            ["memoryUpdates"] =
                ToArray(coordinator.MemoryUpdates.Select(ToNode)),
            ["beliefs"] = ToArray(coordinator.Beliefs.Select(ToNode)),
            ["auditRecords"] = ToArray(
                auditRecords.Select(
                    item => JsonNode.Parse(CanonicalJson.Serialize(item))!)),
            ["termination"] = ToNode(termination),
            ["closure"] = ToNode(closure)
        };
        Normalize(records, new Dictionary<string, string>(StringComparer.Ordinal));
        return records;
    }

    private static JsonNode ToNode(IResearchRoot root) =>
        JsonNode.Parse(ResearchContractSerializer.Serialize(root))!;

    private static JsonArray ToArray(IEnumerable<JsonNode> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static void Normalize(
        JsonNode? node,
        IDictionary<string, string> identifiers,
        string? propertyName = null)
    {
        switch (node)
        {
            case JsonObject value:
                foreach (var property in value.ToArray())
                {
                    if (property.Key is "runId")
                    {
                        value[property.Key] = "<run-id>";
                    }
                    else if (property.Key is "planId")
                    {
                        value[property.Key] = "<plan-id>";
                    }
                    else if (property.Key is "requestId" or "correlationId")
                    {
                        value[property.Key] = "<request-id>";
                    }
                    else if (property.Key is "recordId")
                    {
                        value[property.Key] = "<audit-record-id>";
                    }
                    else if (property.Key == "auditRecordIds" &&
                             property.Value is JsonArray auditIds)
                    {
                        for (var index = 0; index < auditIds.Count; index++)
                        {
                            auditIds[index] = $"<audit-record-id-{index + 1}>";
                        }
                    }
                    else
                    {
                        Normalize(property.Value, identifiers, property.Key);
                    }
                }

                break;
            case JsonArray value:
                for (var index = 0; index < value.Count; index++)
                {
                    Normalize(value[index], identifiers, propertyName);
                }

                break;
            case JsonValue value when value.TryGetValue<string>(out var text):
                var prefix = IdentifierPrefix(text);
                if (prefix is null)
                {
                    return;
                }

                if (!identifiers.TryGetValue(text, out var replacement))
                {
                    replacement = $"<{prefix}-{identifiers.Count(
                        item => item.Value.StartsWith(
                            $"<{prefix}-",
                            StringComparison.Ordinal)) + 1}>";
                    identifiers.Add(text, replacement);
                }

                value.ReplaceWith(JsonValue.Create(replacement));
                break;
        }
    }

    private static string? IdentifierPrefix(string value)
    {
        string[] prefixes =
        [
            "actor",
            "review",
            "diagnostic",
            "observation",
            "snapshot",
            "result",
            "memory-update",
            "belief",
            "direction",
            "trigger"
        ];
        return prefixes.FirstOrDefault(
            prefix => value.StartsWith(prefix + "-", StringComparison.Ordinal));
    }

    private static string? FindFirstMismatch(
        JsonNode? left,
        JsonNode? right,
        string path)
    {
        if (left is null || right is null)
        {
            return left is null && right is null ? null : path;
        }

        if (left.GetType() != right.GetType())
        {
            return path;
        }

        if (left is JsonObject leftObject && right is JsonObject rightObject)
        {
            var keys = leftObject.Select(item => item.Key)
                .Union(rightObject.Select(item => item.Key), StringComparer.Ordinal)
                .Order(StringComparer.Ordinal);
            foreach (var key in keys)
            {
                if (!leftObject.TryGetPropertyValue(key, out var leftValue) ||
                    !rightObject.TryGetPropertyValue(key, out var rightValue))
                {
                    return $"{path}.{key}";
                }

                var mismatch = FindFirstMismatch(
                    leftValue,
                    rightValue,
                    $"{path}.{key}");
                if (mismatch is not null)
                {
                    return mismatch;
                }
            }

            return null;
        }

        if (left is JsonArray leftArray && right is JsonArray rightArray)
        {
            if (leftArray.Count != rightArray.Count)
            {
                return $"{path}.length";
            }

            for (var index = 0; index < leftArray.Count; index++)
            {
                var mismatch = FindFirstMismatch(
                    leftArray[index],
                    rightArray[index],
                    $"{path}[{index}]");
                if (mismatch is not null)
                {
                    return mismatch;
                }
            }

            return null;
        }

        return JsonNode.DeepEquals(left, right) ? null : path;
    }

    private static void AddDue(
        IDictionary<uint, List<string>> schedule,
        uint tick,
        string id)
    {
        if (!schedule.TryGetValue(tick, out var values))
        {
            values = [];
            schedule.Add(tick, values);
        }

        values.Add(id);
    }

    private static string ArchitectureName(
        SupervisionArchitecture architecture) => architecture switch
    {
        SupervisionArchitecture.ActorOnly => "actor-only",
        SupervisionArchitecture.BlockingSupervision => "blocking-supervision",
        SupervisionArchitecture.AsynchronousSupervision =>
            "asynchronous-supervision",
        _ => throw new InvalidOperationException("Unknown architecture.")
    };

    private static RepositoryState ReadRepositoryState(string repositoryRoot)
    {
        var revision = Run(repositoryRoot, "git", "rev-parse HEAD").Trim();
        var dirty = !string.IsNullOrWhiteSpace(
            Run(repositoryRoot, "git", "status --porcelain"));
        return new RepositoryState(revision, dirty);
    }

    private static RuntimeVersions ReadRuntimeVersions(string repositoryRoot)
    {
        var workflowVersion = typeof(WorkflowBuilder).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
            ?? typeof(WorkflowBuilder).Assembly.GetName().Version?.ToString()
            ?? "unavailable";
        return new RuntimeVersions(
            Run(repositoryRoot, "dotnet", "--version").Trim(),
            workflowVersion,
            Run(repositoryRoot, "node", "--version").Trim());
    }

    private static string Run(
        string workingDirectory,
        string fileName,
        string arguments)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException($"Could not start {fileName}.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"{fileName} {arguments} failed: {error}");
        }

        return output;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, ".git")) ||
                File.Exists(Path.Combine(current.FullName, ".git")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class DeterministicOpaqueIdSource
    {
        private uint _plan;
        private uint _request;
        private uint _audit;

        public Guid NextPlanId() => Next(0x10000001, ref _plan);

        public Guid NextRequestId() => Next(0x20000002, ref _request);

        public Guid NextAuditRecordId() => Next(0x30000003, ref _audit);

        private static Guid Next(uint category, ref uint counter)
        {
            counter++;
            return Guid.Parse(
                $"{category:x8}-0000-4000-8000-{counter:x12}");
        }
    }

    private sealed record RepositoryState(string Revision, bool Dirty);

    private sealed record ReproducibilityResult(
        string Architecture,
        bool Equivalent,
        string? FirstMismatchPath);

    private sealed record SmokeEpisode(
        SupervisionArchitecture Architecture,
        int Repetition,
        IReadOnlyList<ResearchEvent> Events,
        Termination Termination,
        RunClosure Closure,
        int DispatchCount,
        int AuditRecordCount,
        string TimelinePath,
        C6EpisodeEvaluation Evaluation,
        JsonNode CanonicalRecords);
}
