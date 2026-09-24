using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovernedAgent.Research;

public sealed record C6EvidenceCoverage(
    IReadOnlyList<string> Observed,
    IReadOnlyList<string> Cited,
    IReadOnlyList<string> Required,
    IReadOnlyList<string> ObservedRequired,
    IReadOnlyList<string> CitedRequired,
    IReadOnlyList<string> MissingObservedRequired,
    IReadOnlyList<string> MissingCitedRequired);

public sealed record C6HandoffTrajectory(
    IReadOnlyList<string> EvidenceState,
    uint DiagnosticAttemptCount);

public sealed record C6NeutralFacts(
    bool ObservedE3,
    bool CitedE3DerivedObservation,
    bool CitedOnlyE2,
    bool MechanismNamingBeforeEvidence,
    C6HandoffTrajectory? HandoffTrajectory,
    bool ContradictsCitedEvidence,
    bool AbstainedDespiteSupportingEvidence);

public sealed record C6ReportEvaluation(
    string DiagnosisClass,
    C6EvidenceCoverage EvidenceCoverage,
    string NextStepClass,
    string TemporalRelevance,
    string UncertaintyConsistency,
    C6NeutralFacts NeutralFacts,
    string StrictCitationPolicyDiagnosisClass);

public sealed record C6EpisodeEvaluation(
    string Evaluator,
    DefinitionRef RubricDefinition,
    string PrimaryCitationPolicy,
    string RobustnessCitationPolicy,
    string RunId,
    string TerminationKind,
    string? ReportResultId,
    C6ReportEvaluation? Report);

public sealed class C6EvaluationException : Exception
{
    public C6EvaluationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public static partial class C6DeterministicEvaluator
{
    public const string OutputFileName = "c6-evaluation.json";

    private const string EvaluatorName = "c6-deterministic-evaluator-1";
    private const string PrimaryPolicy =
        "b-primary-at-least-one-e3-derived-observation";
    private const string RobustnessPolicy =
        "a-strict-all-observed-e3-records";

    private static readonly string[] RequiredInputFiles =
    [
        "observations.jsonl",
        "actions.jsonl",
        "reviews.jsonl",
        "memory-updates.jsonl",
        "beliefs.jsonl",
        "termination.json",
        "run-closure.json"
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>>
        EvidenceIds = new Dictionary<string, IReadOnlySet<string>>(
            StringComparer.Ordinal)
        {
            ["E0"] = Set(
                "ev-inc-1042-v1",
                "ev-notification-00-v1",
                "ev-pay-err-00",
                "ev-pay-p95-00"),
            ["E1"] = Set(
                "ev-notification-04-v1",
                "ev-pay-health-v1",
                "ev-pay-cpu-04",
                "ev-pay-mem-04"),
            ["E1-local-symptom"] = Set(
                "ev-notification-04-v2",
                "ev-pay-cpu-p03-04",
                "ev-pay-mem-p03-04",
                "ev-pay-cpu-p03-08",
                "ev-pay-cpu-p03-12",
                "ev-pay-cpu-p03-16"),
            ["E2"] = Set(
                "ev-notification-08-v1",
                "ev-pay-log-a17-1",
                "ev-pay-log-b09-1"),
            ["E3"] = Set(
                "ev-auth-log-a17-1",
                "ev-auth-log-a17-2",
                "ev-auth-log-a17-3",
                "ev-auth-log-b09-1",
                "ev-auth-log-b09-2",
                "ev-auth-log-b09-3"),
            ["E4"] = Set(
                "ev-notification-16-v1",
                "ev-pay-log-0758",
                "ev-pay-log-0814"),
            ["recovery"] = Set(
                "ev-notification-10-v1",
                "ev-inc-1042-v2",
                "ev-pay-err-0810",
                "ev-pay-p95-0810",
                "ev-auth-lat-0810",
                "ev-auth-log-c17-1",
                "ev-auth-log-c17-2",
                "ev-auth-log-c17-3")
        };

    private static readonly DefinitionRef Rubric = new(
        "C6-rubric-1",
        Sha256(
            """
            C6-rubric-1
            primary-policy=b-primary-at-least-one-e3-derived-observation
            robustness-policy=a-strict-all-observed-e3-records
            E0=ev-inc-1042-v1,ev-notification-00-v1,ev-pay-err-00,ev-pay-p95-00
            E1=ev-notification-04-v1,ev-pay-health-v1,ev-pay-cpu-04,ev-pay-mem-04
            E1-local-symptom=ev-notification-04-v2,ev-pay-cpu-p03-04,ev-pay-mem-p03-04,ev-pay-cpu-p03-08,ev-pay-cpu-p03-12,ev-pay-cpu-p03-16
            E2=ev-notification-08-v1,ev-pay-log-a17-1,ev-pay-log-b09-1
            E3=ev-auth-log-a17-1,ev-auth-log-a17-2,ev-auth-log-a17-3,ev-auth-log-b09-1,ev-auth-log-b09-2,ev-auth-log-b09-3
            E4=ev-notification-16-v1,ev-pay-log-0758,ev-pay-log-0814
            recovery=ev-notification-10-v1,ev-inc-1042-v2,ev-pay-err-0810,ev-pay-p95-0810,ev-auth-lat-0810,ev-auth-log-c17-1,ev-auth-log-c17-2,ev-auth-log-c17-3
            neutral-fact=abstainedDespiteSupportingEvidence iff unresolved and (citedE2 or citedE3Observation or citedLocalSymptom or citedRecovery)
            rules=docs/C6_EVALUATION_PROPOSAL.md#4-7 frozen 2026-09-24
            """));

    public static C6EpisodeEvaluation Evaluate(string episodeDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(episodeDirectory);
        var fullDirectory = Path.GetFullPath(episodeDirectory);
        if (!Directory.Exists(fullDirectory))
        {
            throw new C6EvaluationException(
                $"Episode artifact directory '{fullDirectory}' does not exist.");
        }

        foreach (var fileName in RequiredInputFiles)
        {
            if (!File.Exists(Path.Combine(fullDirectory, fileName)))
            {
                throw new C6EvaluationException(
                    $"Required persisted evaluator input '{fileName}' is missing.");
            }
        }

        try
        {
            var observations = ReadRoots<Observation>(
                fullDirectory,
                "observations.jsonl");
            var actions = ReadHistory<ActionHistoryItem>(
                fullDirectory,
                "actions.jsonl",
                ValidateAction);
            var reviews = ReadHistory<ReviewHistoryItem>(
                fullDirectory,
                "reviews.jsonl",
                ValidateReview);
            var memoryUpdates = ReadRoots<MemoryUpdate>(
                fullDirectory,
                "memory-updates.jsonl");
            var beliefs = ReadRoots<Belief>(
                fullDirectory,
                "beliefs.jsonl");
            var termination = ReadRoot<Termination>(
                fullDirectory,
                "termination.json");
            var closure = ReadRoot<RunClosure>(
                fullDirectory,
                "run-closure.json");

            ValidateEpisode(
                observations,
                actions,
                reviews,
                memoryUpdates,
                beliefs,
                termination,
                closure);

            var report = termination.Kind == "report"
                ? EvaluateReport(observations, actions, termination)
                : null;
            return new C6EpisodeEvaluation(
                EvaluatorName,
                Rubric,
                PrimaryPolicy,
                RobustnessPolicy,
                termination.RunId,
                termination.Kind,
                termination.ReportResultId,
                report);
        }
        catch (C6EvaluationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or
            JsonException or
            ResearchContractException or
            UnauthorizedAccessException)
        {
            throw new C6EvaluationException(
                "Persisted episode validation failed closed.",
                exception);
        }
    }

    public static async Task<C6EpisodeEvaluation> EvaluateAndWriteAsync(
        string episodeDirectory,
        CancellationToken cancellationToken = default)
    {
        var evaluation = Evaluate(episodeDirectory);
        var options = new JsonSerializerOptions(
            ResearchContractSerializer.Options)
        {
            WriteIndented = true
        };
        await File.WriteAllTextAsync(
            Path.Combine(
                Path.GetFullPath(episodeDirectory),
                OutputFileName),
            JsonSerializer.Serialize(evaluation, options) + Environment.NewLine,
            cancellationToken);
        return evaluation;
    }

    private static C6ReportEvaluation EvaluateReport(
        IReadOnlyList<Observation> observations,
        IReadOnlyList<ActionHistoryItem> actions,
        Termination termination)
    {
        var action = actions.Single(item =>
            string.Equals(
                item.ResultId,
                termination.ReportResultId,
                StringComparison.Ordinal));
        var decision = (ReportActorDecision)ResearchContractSerializer
            .DeserializeRoot(action.Decision!.Value.GetRawText());
        var byId = observations.ToDictionary(
            item => item.ObservationId,
            StringComparer.Ordinal);
        var citedObservations = decision.ObservationIds
            .Select(id => byId[id])
            .ToArray();
        var observedEvidence = observations
            .Where(item =>
                item.Visibility == "shared-history" &&
                item.ObservedTick is not null &&
                item.ObservedTick <= termination.Tick)
            .SelectMany(item => item.EvidenceIds)
            .ToHashSet(StringComparer.Ordinal);
        var citedEvidence = citedObservations
            .SelectMany(item => item.EvidenceIds)
            .ToHashSet(StringComparer.Ordinal);
        var observedClasses = EvidenceClasses(observedEvidence);
        var citedClasses = EvidenceClasses(citedEvidence);
        var observedE2 = HasClass(observedEvidence, "E2");
        var citedE2 = HasClass(citedEvidence, "E2");
        var observedE3 = HasClass(observedEvidence, "E3");
        var citedE3Observation = citedObservations.Any(
            item => item.EvidenceIds.Any(EvidenceIds["E3"].Contains));
        var allObservedE3RecordsCited = observedEvidence
            .Where(EvidenceIds["E3"].Contains)
            .All(citedEvidence.Contains);
        var citedLocalSymptom = HasClass(citedEvidence, "E1-local-symptom");
        var observedRecovery = HasClass(observedEvidence, "recovery");
        var citedRecovery = HasClass(citedEvidence, "recovery");
        var citedActive = HasClass(citedEvidence, "E0");
        var contradicts = decision.Hypothesis switch
        {
            Hypothesis.LocalInstanceIssue => citedE2,
            Hypothesis.NoCurrentlyActiveIncident =>
                citedActive && !citedRecovery,
            _ => false
        };
        var primaryDiagnosis = DiagnosisClass(
            decision.Hypothesis,
            observedE2,
            citedE2,
            citedE3Observation,
            citedLocalSymptom,
            citedRecovery,
            citedActive);
        var strictDiagnosis = decision.Hypothesis ==
                Hypothesis.DependencySideQueueDelay &&
            primaryDiagnosis == "supported" &&
            !allObservedE3RecordsCited
                ? "unsupported"
                : primaryDiagnosis;
        var required = RequiredEvidence(decision.Hypothesis);
        var coverage = new C6EvidenceCoverage(
            observedClasses,
            citedClasses,
            required,
            Intersect(required, observedClasses),
            Intersect(required, citedClasses),
            Except(required, observedClasses),
            Except(required, citedClasses));
        var nextStepClass = NextStepClass(
            decision,
            primaryDiagnosis,
            observedE2,
            citedLocalSymptom,
            observedRecovery,
            observations.Any(item =>
                item.Visibility == "shared-history" &&
                item.ObservedTick is not null &&
                item.ObservedTick <= termination.Tick &&
                item.EvidenceIds.Any(EvidenceIds["E0"].Contains)));
        var temporalRelevance = observedRecovery
            ? citedRecovery &&
                decision.Hypothesis is
                    Hypothesis.LocalInstanceIssue or
                    Hypothesis.DependencyPathIssue or
                    Hypothesis.DependencySideQueueDelay
                ? "stale"
                : "current"
            : "not-applicable";
        var uncertaintyConsistency =
            decision.Uncertainty == Uncertainty.Low &&
            primaryDiagnosis == "unsupported"
                ? "inconsistent"
                : "consistent";
        var citedOnlyE2 = citedE2 &&
            !citedE3Observation &&
            !citedLocalSymptom &&
            !citedRecovery &&
            !HasClass(citedEvidence, "E4");
        var neutralFacts = new C6NeutralFacts(
            observedE3,
            citedE3Observation,
            citedOnlyE2,
            decision.NextStep == NextStep.InvestigateDependencyQueue &&
                !citedE3Observation,
            decision.NextStep == NextStep.HumanHandoff
                ? new C6HandoffTrajectory(
                    observedClasses,
                    termination.Counts.DiagnosticAttempts)
                : null,
            contradicts,
            decision.Hypothesis == Hypothesis.Unresolved &&
                (citedE2 ||
                    citedE3Observation ||
                    citedLocalSymptom ||
                    citedRecovery));
        return new C6ReportEvaluation(
            primaryDiagnosis,
            coverage,
            nextStepClass,
            temporalRelevance,
            uncertaintyConsistency,
            neutralFacts,
            strictDiagnosis);
    }

    private static string DiagnosisClass(
        Hypothesis hypothesis,
        bool observedE2,
        bool citedE2,
        bool citedE3Observation,
        bool citedLocalSymptom,
        bool citedRecovery,
        bool citedActive) => hypothesis switch
    {
        Hypothesis.DependencySideQueueDelay =>
            observedE2 && citedE3Observation
                ? "supported"
                : "unsupported",
        Hypothesis.DependencyPathIssue =>
            citedE2 ? "supported-as-hypothesis" : "unsupported",
        Hypothesis.LocalInstanceIssue =>
            citedLocalSymptom && !citedE2
                ? "supported-as-hypothesis"
                : "unsupported",
        Hypothesis.Unresolved => "justified-uncertainty",
        Hypothesis.NoCurrentlyActiveIncident =>
            citedRecovery
                ? "supported"
                : citedActive
                    ? "unsupported"
                    : "unsupported",
        _ => "unsupported"
    };

    private static string NextStepClass(
        ReportActorDecision decision,
        string diagnosisClass,
        bool observedE2,
        bool citedLocalSymptom,
        bool observedRecovery,
        bool observedActive) => decision.NextStep switch
    {
        NextStep.HumanHandoff => "acceptable",
        NextStep.FurtherDependencyDiagnostics or
            NextStep.InvestigateDependencyQueue =>
                observedE2 ? "acceptable" : "premature",
        NextStep.FurtherLocalDiagnostics =>
            citedLocalSymptom && !observedE2
                ? "acceptable"
                : observedE2
                    ? "inappropriate"
                    : "premature",
        NextStep.Monitor =>
            observedRecovery ||
            diagnosisClass == "supported"
                ? "acceptable"
                : decision.Hypothesis == Hypothesis.Unresolved &&
                    observedActive
                    ? "inappropriate"
                    : "premature",
        _ => "inappropriate"
    };

    private static IReadOnlyList<string> RequiredEvidence(Hypothesis hypothesis) =>
        hypothesis switch
        {
            Hypothesis.DependencySideQueueDelay => ["E2", "E3"],
            Hypothesis.DependencyPathIssue => ["E2"],
            Hypothesis.LocalInstanceIssue => ["E1-local-symptom"],
            Hypothesis.NoCurrentlyActiveIncident => ["recovery"],
            _ => []
        };

    private static IReadOnlyList<string> EvidenceClasses(
        IReadOnlySet<string> evidence) => EvidenceIds
            .Where(pair => pair.Value.Any(evidence.Contains))
            .Select(pair => pair.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static bool HasClass(
        IReadOnlySet<string> evidence,
        string evidenceClass) =>
        EvidenceIds[evidenceClass].Any(evidence.Contains);

    private static IReadOnlyList<string> Intersect(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right) => left
            .Intersect(right, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> Except(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right) => left
            .Except(right, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static void ValidateEpisode(
        IReadOnlyList<Observation> observations,
        IReadOnlyList<ActionHistoryItem> actions,
        IReadOnlyList<ReviewHistoryItem> reviews,
        IReadOnlyList<MemoryUpdate> memoryUpdates,
        IReadOnlyList<Belief> beliefs,
        Termination termination,
        RunClosure closure)
    {
        if (!string.Equals(
                termination.RunId,
                closure.RunId,
                StringComparison.Ordinal))
        {
            throw new C6EvaluationException(
                "Termination and run closure are not the same closed episode.");
        }

        if (observations.Any(item => item.RunId != termination.RunId) ||
            memoryUpdates.Any(item => item.RunId != termination.RunId) ||
            beliefs.Any(item => item.RunId != termination.RunId))
        {
            throw new C6EvaluationException(
                "Persisted roots contain a run mismatch.");
        }

        if (observations
            .GroupBy(item => item.ObservationId, StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            throw new C6EvaluationException(
                "Persisted observations contain duplicate identities.");
        }

        var observationIds = observations
            .Select(item => item.ObservationId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var action in actions)
        {
            if (action.ObservationIds.Any(id => !observationIds.Contains(id)))
            {
                throw new C6EvaluationException(
                    "Action history references a missing observation.");
            }

            if (action.Decision is { } rawDecision)
            {
                var decision = ResearchContractSerializer.DeserializeRoot(
                    rawDecision.GetRawText());
                if (decision is not ActorDecision actorDecision ||
                    actorDecision is ReportActorDecision report &&
                    report.ObservationIds.Any(id => !observationIds.Contains(id)))
                {
                    throw new C6EvaluationException(
                        "Action history contains an invalid actor decision reference.");
                }
            }
        }

        foreach (var review in reviews)
        {
            if (review.Output is { } rawOutput &&
                ResearchContractSerializer.DeserializeRoot(
                    rawOutput.GetRawText()) is not SupervisorOutput)
            {
                throw new C6EvaluationException(
                    "Review history contains a non-supervisor output.");
            }
        }

        var reportActions = actions.Where(item =>
            item.Disposition == "applied" &&
            item.Decision is { } decision &&
            IsReport(decision)).ToArray();
        if (termination.Kind == "report")
        {
            if (reportActions.Length != 1 ||
                !string.Equals(
                    reportActions[0].ResultId,
                    termination.ReportResultId,
                    StringComparison.Ordinal))
            {
                throw new C6EvaluationException(
                    "Report termination is not bound to one applied persisted report.");
            }
        }
        else if (reportActions.Length != 0)
        {
            throw new C6EvaluationException(
                "A non-report termination contains an applied report.");
        }
    }

    private static bool IsReport(JsonElement decision) =>
        ResearchContractSerializer.DeserializeRoot(
            decision.GetRawText()) is ReportActorDecision;

    private static T ReadRoot<T>(string directory, string fileName)
        where T : class, IResearchRoot
    {
        var root = ResearchContractSerializer.DeserializeRoot(
            File.ReadAllText(Path.Combine(directory, fileName)));
        return root as T ??
            throw new C6EvaluationException(
                $"Persisted '{fileName}' has the wrong root type.");
    }

    private static IReadOnlyList<T> ReadRoots<T>(
        string directory,
        string fileName)
        where T : class, IResearchRoot
    {
        var roots = new List<T>();
        foreach (var line in ReadNonemptyLines(directory, fileName))
        {
            var root = ResearchContractSerializer.DeserializeRoot(line);
            if (root is not T typed)
            {
                throw new C6EvaluationException(
                    $"Persisted '{fileName}' contains the wrong root type.");
            }

            roots.Add(typed);
        }

        return roots;
    }

    private static IReadOnlyList<T> ReadHistory<T>(
        string directory,
        string fileName,
        Action<T> validate)
        where T : class
    {
        var values = new List<T>();
        foreach (var line in ReadNonemptyLines(directory, fileName))
        {
            EnsureNoDuplicateProperties(line, fileName);
            var value = JsonSerializer.Deserialize<T>(
                    line,
                    ResearchContractSerializer.Options)
                ?? throw new C6EvaluationException(
                    $"Persisted '{fileName}' contains a null item.");
            var nullIssue = ResearchObjectGraphValidator.FindExplicitNull(value);
            if (nullIssue is not null)
            {
                throw new C6EvaluationException(nullIssue.Message);
            }

            validate(value);
            values.Add(value);
        }

        return values;
    }

    private static IReadOnlyList<string> ReadNonemptyLines(
        string directory,
        string fileName) => File.ReadAllLines(
            Path.Combine(directory, fileName))
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .ToArray();

    private static void ValidateAction(ActionHistoryItem action)
    {
        ValidateIdentifier(action.ActorTurnId, "actor-");
        ValidateIdentifier(action.SnapshotId, "snapshot-");
        ValidateIdentifier(action.ResultId, "result-");
        if (action.Disposition is not ("applied" or "rejected" or "suppressed"))
        {
            throw new C6EvaluationException(
                "Action history contains an unsupported disposition.");
        }

        if (action.Disposition == "applied" &&
            (action.Decision is null || action.Reason is not null))
        {
            throw new C6EvaluationException(
                "Applied action history fields are inconsistent.");
        }

        if (action.Disposition is "rejected" or "suppressed" &&
            action.Reason is null)
        {
            throw new C6EvaluationException(
                "Rejected or suppressed action history lacks a reason.");
        }

        if (action.DiagnosticId is not null)
        {
            ValidateIdentifier(action.DiagnosticId, "diagnostic-");
        }

        foreach (var observationId in action.ObservationIds)
        {
            ValidateIdentifier(observationId, "observation-");
        }

        if (action.Decision is { } decision &&
            ResearchContractSerializer.DeserializeRoot(
                decision.GetRawText()) is not ActorDecision)
        {
            throw new C6EvaluationException(
                "Action history decision is not an actor-decision root.");
        }
    }

    private static void ValidateReview(ReviewHistoryItem review)
    {
        ValidateIdentifier(review.ReviewId, "review-");
        ValidateIdentifier(review.SnapshotId, "snapshot-");
        if (review.ResultId is not null)
        {
            ValidateIdentifier(review.ResultId, "result-");
        }

        if (review.MemoryUpdateId is not null)
        {
            ValidateIdentifier(review.MemoryUpdateId, "memory-update-");
        }

        if (review.Status is not (
                "completed" or "timed-out" or "failed" or "cancelled"))
        {
            throw new C6EvaluationException(
                "Review history contains an unsupported status.");
        }

        if (review.Output is { } output &&
            ResearchContractSerializer.DeserializeRoot(
                output.GetRawText()) is not SupervisorOutput)
        {
            throw new C6EvaluationException(
                "Review history output is not a supervisor-output root.");
        }
    }

    private static void ValidateIdentifier(string value, string prefix)
    {
        if (!RunLocalIdentifierRegex().IsMatch(value) ||
            !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new C6EvaluationException(
                $"Persisted identifier '{value}' is invalid.");
        }
    }

    private static void EnsureNoDuplicateProperties(
        string json,
        string fileName)
    {
        using var document = JsonDocument.Parse(json);
        Visit(document.RootElement, "$", fileName);

        static void Visit(
            JsonElement element,
            string path,
            string fileName)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name))
                    {
                        throw new C6EvaluationException(
                            $"Persisted '{fileName}' has duplicate property " +
                            $"'{property.Name}' at '{path}'.");
                    }

                    Visit(
                        property.Value,
                        $"{path}.{property.Name}",
                        fileName);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    Visit(item, $"{path}[{index}]", fileName);
                    index++;
                }
            }
        }
    }

    private static IReadOnlySet<string> Set(params string[] values) =>
        values.ToHashSet(StringComparer.Ordinal);

    private static string Sha256(string value) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    [GeneratedRegex(
        "^(actor|review|snapshot|result|diagnostic|observation|memory-update)-[1-9][0-9]*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex RunLocalIdentifierRegex();
}
