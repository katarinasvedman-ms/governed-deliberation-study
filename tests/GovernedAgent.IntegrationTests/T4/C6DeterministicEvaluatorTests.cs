using System.Text.Json;
using GovernedAgent.Research;

namespace GovernedAgent.IntegrationTests.T4;

public sealed class C6DeterministicEvaluatorTests
{
    private static readonly string Schema =
        ResearchContractVersions.SchemaVersion;

    [Fact]
    public void ClassifiesEveryDiagnosisBranchAndAppliesAntiOracleSymmetrically()
    {
        var supportedQueue = Evaluate(
            Hypothesis.DependencySideQueueDelay,
            NextStep.InvestigateDependencyQueue,
            Uncertainty.Medium,
            [
                Evidence(false, "ev-notification-08-v1"),
                Evidence(true, "ev-auth-log-a17-1")
            ]);
        AssertDiagnosis(supportedQueue, "supported", "supported");

        var uncitedE3 = Evaluate(
            Hypothesis.DependencySideQueueDelay,
            NextStep.InvestigateDependencyQueue,
            Uncertainty.Medium,
            [
                Evidence(true, "ev-notification-08-v1"),
                Evidence(false, "ev-auth-log-a17-1")
            ]);
        AssertDiagnosis(uncitedE3, "unsupported", "unsupported");
        Assert.True(uncitedE3.Report!.NeutralFacts.ObservedE3);
        Assert.False(uncitedE3.Report.NeutralFacts.CitedE3DerivedObservation);

        var luckyQueueGuess = Evaluate(
            Hypothesis.DependencySideQueueDelay,
            NextStep.InvestigateDependencyQueue,
            Uncertainty.Low,
            []);
        AssertDiagnosis(luckyQueueGuess, "unsupported", "unsupported");
        Assert.Equal(
            "inconsistent",
            luckyQueueGuess.Report!.UncertaintyConsistency);

        var supportedPath = Evaluate(
            Hypothesis.DependencyPathIssue,
            NextStep.FurtherDependencyDiagnostics,
            Uncertainty.Low,
            [Evidence(true, "ev-notification-08-v1")]);
        AssertDiagnosis(
            supportedPath,
            "supported-as-hypothesis",
            "supported-as-hypothesis");
        Assert.Equal("consistent", supportedPath.Report!.UncertaintyConsistency);

        var unsupportedPath = Evaluate(
            Hypothesis.DependencyPathIssue,
            NextStep.FurtherDependencyDiagnostics,
            Uncertainty.Medium,
            [Evidence(true, "ev-notification-00-v1")]);
        AssertDiagnosis(unsupportedPath, "unsupported", "unsupported");

        var reasonableLocalHypothesis = Evaluate(
            Hypothesis.LocalInstanceIssue,
            NextStep.FurtherLocalDiagnostics,
            Uncertainty.Medium,
            [Evidence(true, "ev-notification-04-v2")]);
        AssertDiagnosis(
            reasonableLocalHypothesis,
            "supported-as-hypothesis",
            "supported-as-hypothesis");
        Assert.False(
            reasonableLocalHypothesis.Report!.NeutralFacts
                .ContradictsCitedEvidence);

        var contradictedLocalHypothesis = Evaluate(
            Hypothesis.LocalInstanceIssue,
            NextStep.FurtherLocalDiagnostics,
            Uncertainty.Medium,
            [
                Evidence(true, "ev-notification-04-v2"),
                Evidence(true, "ev-notification-08-v1")
            ]);
        AssertDiagnosis(
            contradictedLocalHypothesis,
            "unsupported",
            "unsupported");
        Assert.True(
            contradictedLocalHypothesis.Report!.NeutralFacts
                .ContradictsCitedEvidence);

        var unresolved = Evaluate(
            Hypothesis.Unresolved,
            NextStep.HumanHandoff,
            Uncertainty.High,
            [Evidence(true, "ev-notification-00-v1")],
            diagnosticAttempts: 2);
        AssertDiagnosis(
            unresolved,
            "justified-uncertainty",
            "justified-uncertainty");
        Assert.Equal("acceptable", unresolved.Report!.NextStepClass);
        Assert.Equal(
            2u,
            unresolved.Report.NeutralFacts.HandoffTrajectory!
                .DiagnosticAttemptCount);

        var unjustifiedUnresolved = Evaluate(
            Hypothesis.Unresolved,
            NextStep.HumanHandoff,
            Uncertainty.High,
            [Evidence(true, "ev-notification-08-v1")]);
        AssertDiagnosis(
            unjustifiedUnresolved,
            "unsupported",
            "unsupported");

        var recovered = Evaluate(
            Hypothesis.NoCurrentlyActiveIncident,
            NextStep.Monitor,
            Uncertainty.Low,
            [Evidence(true, "ev-notification-10-v1")]);
        AssertDiagnosis(recovered, "supported", "supported");
        Assert.Equal("current", recovered.Report!.TemporalRelevance);

        var falseRecovery = Evaluate(
            Hypothesis.NoCurrentlyActiveIncident,
            NextStep.Monitor,
            Uncertainty.Low,
            [Evidence(true, "ev-notification-00-v1")]);
        AssertDiagnosis(falseRecovery, "unsupported", "unsupported");
        Assert.True(
            falseRecovery.Report!.NeutralFacts.ContradictsCitedEvidence);
    }

    [Fact]
    public void ComputesPrimaryAndStrictCitationPoliciesWithoutSelectingBetweenThem()
    {
        var evaluation = Evaluate(
            Hypothesis.DependencySideQueueDelay,
            NextStep.InvestigateDependencyQueue,
            Uncertainty.Medium,
            [
                Evidence(false, "ev-notification-08-v1"),
                Evidence(true, "ev-auth-log-a17-1"),
                Evidence(false, "ev-auth-log-a17-2")
            ]);

        AssertDiagnosis(evaluation, "supported", "unsupported");
        Assert.True(evaluation.Report!.NeutralFacts.ObservedE3);
        Assert.True(evaluation.Report.NeutralFacts.CitedE3DerivedObservation);
        Assert.Contains("E3", evaluation.Report.EvidenceCoverage.Observed);
        Assert.Contains("E3", evaluation.Report.EvidenceCoverage.Cited);
        Assert.Equal(
            "b-primary-at-least-one-e3-derived-observation",
            evaluation.PrimaryCitationPolicy);
        Assert.Equal(
            "a-strict-all-observed-e3-records",
            evaluation.RobustnessCitationPolicy);
        Assert.Matches(
            "^[0-9a-f]{64}$",
            evaluation.RubricDefinition.Sha256);
    }

    [Fact]
    public void RecordsMechanismNamingAndE2OnlyCitationFactsWithoutPenalizingStep()
    {
        var evaluation = Evaluate(
            Hypothesis.DependencyPathIssue,
            NextStep.InvestigateDependencyQueue,
            Uncertainty.Low,
            [Evidence(true, "ev-notification-08-v1")]);

        Assert.Equal("acceptable", evaluation.Report!.NextStepClass);
        Assert.True(evaluation.Report.NeutralFacts.CitedOnlyE2);
        Assert.True(
            evaluation.Report.NeutralFacts.MechanismNamingBeforeEvidence);
    }

    [Fact]
    public void JudgesMonitorAgainstObservedStateAndRecoveryTemporalRelevance()
    {
        var activeUnresolved = Evaluate(
            Hypothesis.Unresolved,
            NextStep.Monitor,
            Uncertainty.High,
            [Evidence(true, "ev-notification-00-v1")]);
        Assert.Equal(
            "inappropriate",
            activeUnresolved.Report!.NextStepClass);

        var provisionalPathMonitor = Evaluate(
            Hypothesis.DependencyPathIssue,
            NextStep.Monitor,
            Uncertainty.Medium,
            [Evidence(true, "ev-notification-08-v1")]);
        Assert.Equal(
            "premature",
            provisionalPathMonitor.Report!.NextStepClass);

        var recoveredUnresolved = Evaluate(
            Hypothesis.Unresolved,
            NextStep.Monitor,
            Uncertainty.High,
            [
                Evidence(false, "ev-notification-00-v1"),
                Evidence(false, "ev-notification-10-v1")
            ]);
        Assert.Equal(
            "acceptable",
            recoveredUnresolved.Report!.NextStepClass);
        Assert.Equal("current", recoveredUnresolved.Report.TemporalRelevance);

        var staleFault = Evaluate(
            Hypothesis.DependencySideQueueDelay,
            NextStep.Monitor,
            Uncertainty.Medium,
            [
                Evidence(false, "ev-notification-08-v1"),
                Evidence(true, "ev-auth-log-a17-1"),
                Evidence(true, "ev-notification-10-v1")
            ]);
        AssertDiagnosis(staleFault, "supported", "supported");
        Assert.Equal("acceptable", staleFault.Report!.NextStepClass);
        Assert.Equal("stale", staleFault.Report.TemporalRelevance);
    }

    [Fact]
    public void FailsClosedOnAnInvalidPersistedRoot()
    {
        using var fixture = CreateEpisode(
            Hypothesis.Unresolved,
            NextStep.HumanHandoff,
            Uncertainty.High,
            [Evidence(true, "ev-notification-00-v1")]);
        var path = Path.Combine(fixture.Path, "observations.jsonl");
        var line = File.ReadAllText(path).TrimEnd();
        File.WriteAllText(
            path,
            line[..^1] + ",\"unexpected\":true}" + Environment.NewLine);

        Assert.Throws<C6EvaluationException>(
            () => C6DeterministicEvaluator.Evaluate(fixture.Path));
    }

    [Fact]
    public void IgnoresForbiddenCaseAndAnswerKeyFiles()
    {
        using var fixture = CreateEpisode(
            Hypothesis.LocalInstanceIssue,
            NextStep.FurtherLocalDiagnostics,
            Uncertainty.Medium,
            [Evidence(true, "ev-notification-04-v2")]);
        File.WriteAllText(
            Path.Combine(fixture.Path, "case-label.json"),
            "{not valid json");
        File.WriteAllText(
            Path.Combine(fixture.Path, "answer-key.txt"),
            "dependency-side-queue-delay");

        var evaluation = C6DeterministicEvaluator.Evaluate(fixture.Path);

        AssertDiagnosis(
            evaluation,
            "supported-as-hypothesis",
            "supported-as-hypothesis");
    }

    private static void AssertDiagnosis(
        C6EpisodeEvaluation evaluation,
        string primary,
        string strict)
    {
        var report = Assert.IsType<C6ReportEvaluation>(evaluation.Report);
        Assert.Equal(primary, report.DiagnosisClass);
        Assert.Equal(strict, report.StrictCitationPolicyDiagnosisClass);
    }

    private static C6EpisodeEvaluation Evaluate(
        Hypothesis hypothesis,
        NextStep nextStep,
        Uncertainty uncertainty,
        IReadOnlyList<TestEvidence> evidence,
        uint diagnosticAttempts = 0)
    {
        using var fixture = CreateEpisode(
            hypothesis,
            nextStep,
            uncertainty,
            evidence,
            diagnosticAttempts);
        return C6DeterministicEvaluator.Evaluate(fixture.Path);
    }

    private static EpisodeDirectory CreateEpisode(
        Hypothesis hypothesis,
        NextStep nextStep,
        Uncertainty uncertainty,
        IReadOnlyList<TestEvidence> evidence,
        uint diagnosticAttempts = 0)
    {
        var directory = new EpisodeDirectory();
        var runId = Guid.NewGuid().ToString("D");
        var observations = evidence.Select((item, index) =>
            new Observation(
                Schema,
                "observation",
                runId,
                $"observation-{index + 1}",
                item.EvidenceIds.Order(StringComparer.Ordinal).ToArray(),
                "shared-notification",
                $"notification-{index + 1}",
                TargetId.PaymentsApi,
                null,
                (uint)index,
                (uint)index,
                (uint)index + 1,
                0,
                "shared-history",
                new NotificationObservationContent(
                    $"Synthetic evaluator test observation {index + 1}.")))
            .ToArray();
        var citations = evidence.Select((item, index) => new { item, index })
            .Where(value => value.item.Cited)
            .Select(value => $"observation-{value.index + 1}")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var report = new ReportActorDecision(
            Schema,
            "actor-decision",
            null,
            [],
            hypothesis,
            citations,
            nextStep,
            uncertainty);
        using var decisionDocument = JsonDocument.Parse(
            ResearchContractSerializer.Serialize(report));
        var action = new ActionHistoryItem(
            "actor-1",
            "snapshot-1",
            "result-1",
            0,
            decisionDocument.RootElement.Clone(),
            "applied",
            null,
            null,
            []);
        var counts = Counts(diagnosticAttempts);
        var termination = new Termination(
            Schema,
            "termination",
            runId,
            12,
            ResearchPhase.ActorProcessing,
            "report",
            "result-1",
            null,
            (uint)observations.Length,
            0,
            0,
            0,
            null,
            false,
            counts,
            [],
            []);
        var closure = new RunClosure(
            Schema,
            "run-closure",
            runId,
            "complete",
            0,
            counts,
            [],
            [],
            null,
            "unavailable-scripted");

        WriteRoots(
            Path.Combine(directory.Path, "observations.jsonl"),
            observations);
        File.WriteAllLines(
            Path.Combine(directory.Path, "actions.jsonl"),
            [
                JsonSerializer.Serialize(
                    action,
                    ResearchContractSerializer.Options)
            ]);
        File.WriteAllText(
            Path.Combine(directory.Path, "reviews.jsonl"),
            string.Empty);
        File.WriteAllText(
            Path.Combine(directory.Path, "memory-updates.jsonl"),
            string.Empty);
        File.WriteAllText(
            Path.Combine(directory.Path, "beliefs.jsonl"),
            string.Empty);
        File.WriteAllText(
            Path.Combine(directory.Path, "termination.json"),
            ResearchContractSerializer.Serialize(termination));
        File.WriteAllText(
            Path.Combine(directory.Path, "run-closure.json"),
            ResearchContractSerializer.Serialize(closure));
        return directory;
    }

    private static ResourceCounts Counts(uint diagnosticAttempts) => new(
        ActorTurnsStarted: 1,
        ReviewsStarted: 0,
        DiagnosticAttempts: diagnosticAttempts,
        DiagnosticsDispatched: diagnosticAttempts,
        DiagnosticsCompleted: diagnosticAttempts,
        ActorResultsSuppressed: 0,
        ReviewResultsSuppressed: 0,
        CancellationRequests: 0,
        CancellationsConfirmed: 0,
        CancellationsUnsupported: 0,
        CancellationRequestsFailed: 0,
        CancellationsIgnored: 0,
        ReportsSubmitted: 1,
        MemoryUpdatesCommitted: 0,
        MemoryUpdatesRejected: 0,
        MemoryUpdatesNoOp: 0,
        MemoryRevisionConflicts: 0,
        RepeatedDirectionsNormalized: 0,
        BeliefReassertionsCommitted: 0,
        ReassertionInterrupts: 0,
        ActionableCommits: 0,
        ReconsiderationAcknowledgments: 0);

    private static void WriteRoots<T>(
        string path,
        IReadOnlyList<T> roots)
        where T : IResearchRoot => File.WriteAllLines(
            path,
            roots.Select(root => ResearchContractSerializer.Serialize(root)));

    private static TestEvidence Evidence(
        bool cited,
        params string[] evidenceIds) => new(cited, evidenceIds);

    private sealed record TestEvidence(
        bool Cited,
        IReadOnlyList<string> EvidenceIds);

    private sealed class EpisodeDirectory : IDisposable
    {
        public EpisodeDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "governed-deliberation-c6",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
