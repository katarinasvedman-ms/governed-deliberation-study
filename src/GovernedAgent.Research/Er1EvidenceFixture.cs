using GovernedAgent.Simulator;

namespace GovernedAgent.Research;

public enum Er1DevelopmentVariant
{
    Straightforward,
    AmbiguousCpuSymptom
}

public sealed record Er1FixtureOptions(
    Er1DevelopmentVariant DevelopmentVariant,
    bool ExternalRecovery)
{
    public static Er1FixtureOptions Straightforward { get; } =
        new(Er1DevelopmentVariant.Straightforward, ExternalRecovery: false);
}

public sealed record Er1EvidenceMetadata(
    string EvidenceId,
    DateTimeOffset SourceTime,
    uint CollectionTick);

public sealed record Er1FixtureSample(
    string SourceKind,
    string SourceId,
    TargetId TargetId,
    uint SampledTick,
    uint AvailableTick,
    uint ApplicabilityEpoch,
    IReadOnlyList<string> EvidenceIds,
    ObservationContent Content)
{
    public Observation CreateObservation(
        string runId,
        string observationId,
        string? diagnosticId,
        uint observedTick,
        uint historyRevision)
    {
        var observation = new Observation(
            ResearchContractVersions.SchemaVersion,
            "observation",
            runId,
            observationId,
            EvidenceIds,
            SourceKind,
            SourceId,
            TargetId,
            diagnosticId,
            AvailableTick,
            observedTick,
            historyRevision,
            ApplicabilityEpoch,
            "shared-history",
            Content);
        var issues = ResearchContractValidator.Validate(observation);
        if (issues.Count != 0)
        {
            throw new InvalidOperationException(
                $"Fixture produced an invalid observation: {string.Join("; ", issues.Select(issue => $"{issue.Code}: {issue.Message}"))}");
        }

        return observation;
    }

    public Observation CreatePostTerminationObservation(
        string runId,
        string observationId,
        string? diagnosticId)
    {
        var observation = new Observation(
            ResearchContractVersions.SchemaVersion,
            "observation",
            runId,
            observationId,
            EvidenceIds,
            SourceKind,
            SourceId,
            TargetId,
            diagnosticId,
            AvailableTick,
            null,
            null,
            ApplicabilityEpoch,
            "post-termination",
            Content);
        ResearchContractValidator.ValidateAndThrow(observation);
        return observation;
    }
}

public sealed class Er1EvidenceFixture
{
    public const string IncidentId = "INC-1042";
    public const string PaymentsServiceId = "payments-api";
    public const string AuthorizationServiceId = "authorization-service";

    private static readonly DateTimeOffset T0755 = At("2026-09-18T07:55:00Z");
    private static readonly DateTimeOffset T0758 = At("2026-09-18T07:58:00Z");
    private static readonly DateTimeOffset T075930 = At("2026-09-18T07:59:30Z");
    private static readonly DateTimeOffset T0800 = At("2026-09-18T08:00:00Z");
    private static readonly DateTimeOffset T0804 = At("2026-09-18T08:04:00Z");
    private static readonly DateTimeOffset T0806 = At("2026-09-18T08:06:00Z");
    private static readonly DateTimeOffset T0806005 = At("2026-09-18T08:06:00.500Z");
    private static readonly DateTimeOffset T0806025 = At("2026-09-18T08:06:02.500Z");
    private static readonly DateTimeOffset T08060305 = At("2026-09-18T08:06:03.050Z");
    private static readonly DateTimeOffset T08060313 = At("2026-09-18T08:06:03.130Z");
    private static readonly DateTimeOffset T0807 = At("2026-09-18T08:07:00Z");
    private static readonly DateTimeOffset T0807004 = At("2026-09-18T08:07:00.400Z");
    private static readonly DateTimeOffset T0807024 = At("2026-09-18T08:07:02.400Z");
    private static readonly DateTimeOffset T0807032 = At("2026-09-18T08:07:03.200Z");
    private static readonly DateTimeOffset T080703275 = At("2026-09-18T08:07:03.275Z");
    private static readonly DateTimeOffset T0808 = At("2026-09-18T08:08:00Z");
    private static readonly DateTimeOffset T0810 = At("2026-09-18T08:10:00Z");
    private static readonly DateTimeOffset T0811002 = At("2026-09-18T08:11:00.200Z");
    private static readonly DateTimeOffset T08110026 = At("2026-09-18T08:11:00.260Z");
    private static readonly DateTimeOffset T081100335 = At("2026-09-18T08:11:00.335Z");
    private static readonly DateTimeOffset T0812 = At("2026-09-18T08:12:00Z");
    private static readonly DateTimeOffset T0814 = At("2026-09-18T08:14:00Z");
    private static readonly DateTimeOffset T0816 = At("2026-09-18T08:16:00Z");

    private readonly Er1FixtureOptions _options;

    public Er1EvidenceFixture(Er1FixtureOptions? options = null)
    {
        _options = options ?? Er1FixtureOptions.Straightforward;
    }

    public Er1FixtureOptions Options => _options;

    public IReadOnlyList<Er1EvidenceMetadata> EvidenceRegistry =>
        Array.AsReadOnly(AllEvidence()
            .Where(item => item.Applies(_options))
            .Select(item => new Er1EvidenceMetadata(
                item.EvidenceId,
                SourceTime(item.EvidenceId),
                item.CollectionTick))
            .OrderBy(item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray());

    public uint GetApplicabilityEpoch(uint tick)
    {
        ValidateTick(tick);
        return _options.ExternalRecovery && tick >= 10 ? 1u : 0u;
    }

    public Er1FixtureSample SampleDiagnostic(
        ResearchOperation operation,
        TargetId targetId,
        uint dispatchTick)
    {
        ValidateTick(dispatchTick);
        ValidatePair(operation, targetId);
        var epoch = GetApplicabilityEpoch(dispatchTick);
        return operation switch
        {
            ResearchOperation.GetIncident => CurrentIncident(dispatchTick, epoch),
            ResearchOperation.GetServiceHealth => CurrentHealth(targetId, dispatchTick, epoch),
            ResearchOperation.QueryMetrics => CurrentMetrics(targetId, dispatchTick, epoch),
            ResearchOperation.QueryLogs => CurrentLogs(targetId, dispatchTick, epoch),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    public Er1FixtureSample SampleExplicitUnavailableDiagnostic(
        ResearchOperation operation,
        TargetId targetId,
        uint dispatchTick)
    {
        ValidateTick(dispatchTick);
        ValidatePair(operation, targetId);
        return DiagnosticSample(
            operation,
            targetId,
            dispatchTick,
            dispatchTick,
            GetApplicabilityEpoch(dispatchTick),
            [],
            new UnavailableObservationContent("not-yet-available"));
    }

    public IReadOnlyList<Er1FixtureSample> GetNotifications(uint tick)
    {
        ValidateTick(tick);
        var result = new List<Er1FixtureSample>();
        switch (tick)
        {
            case 0:
                result.Add(Notification(
                    "notification-00-v1",
                    TargetId.PaymentsApi,
                    tick,
                    "ev-notification-00-v1",
                    "Payments API alert: error rate 18%; p95 latency 2600 ms."));
                break;
            case 4:
                result.Add(_options.DevelopmentVariant == Er1DevelopmentVariant.Straightforward
                    ? Notification(
                        "notification-04-v1",
                        TargetId.PaymentsApi,
                        tick,
                        "ev-notification-04-v1",
                        "Payments API: 3/3 instances ready; aggregate CPU 49%; memory 60%; no restart recorded.")
                    : Notification(
                        "notification-04-v2",
                        TargetId.PaymentsApi,
                        tick,
                        "ev-notification-04-v2",
                        "Payments API: 3/3 instances ready; payments-api-03 CPU 87%; memory 64%; no restart recorded."));
                break;
            case 8 when _options.DevelopmentVariant == Er1DevelopmentVariant.Straightforward:
                result.Add(Notification(
                    "notification-08-v1",
                    TargetId.PaymentsApi,
                    tick,
                    "ev-notification-08-v1",
                    "Two sampled payment failures, req-a17 and req-b09, timed out calling authorization-service after 2000 ms."));
                break;
            case 10 when _options.ExternalRecovery:
                result.Add(Notification(
                    "notification-10-v1",
                    TargetId.PaymentsApi,
                    tick,
                    "ev-notification-10-v1",
                    "Recovery observed: payments error rate 1.2%, p95 latency 240 ms; authorization p95 latency 110 ms."));
                break;
            case 16:
                result.Add(Notification(
                    "notification-16-v1",
                    TargetId.PaymentsApi,
                    tick,
                    "ev-notification-16-v1",
                    "merchant-profile cache refresh warnings: 1 in 07:52-08:00 and 1 in 08:08-08:16."));
                break;
        }

        return result.AsReadOnly();
    }

    public IReadOnlyList<string> GetEvidenceCollectedAtTick(uint tick)
    {
        ValidateTick(tick);
        var ids = new List<string>();
        ids.AddRange(AllEvidence()
            .Where(item => item.CollectionTick == tick)
            .Where(item => item.Applies(_options))
            .Select(item => item.EvidenceId));
        return SortedIds(ids);
    }

    public void RegisterWorldSchedule(
        ScriptedExperimentScheduler scheduler,
        Action<uint, IReadOnlyList<string>> onEvidenceCollected,
        Action<Er1FixtureSample> onNotificationDelivered,
        Action<uint, uint, string>? onEpochChanged = null)
    {
        ArgumentNullException.ThrowIfNull(scheduler);
        ArgumentNullException.ThrowIfNull(onEvidenceCollected);
        ArgumentNullException.ThrowIfNull(onNotificationDelivered);

        var scheduledTicks = EvidenceRegistry
            .Select(item => item.CollectionTick)
            .Concat([0u, 4u, 8u, 10u, 16u])
            .Where(tick => tick < ApprovedScriptedExperiment.Clock.EndTickExclusive)
            .Distinct()
            .Order()
            .ToArray();
        foreach (var tick in scheduledTicks)
        {
            var scheduledTick = tick;
            scheduler.Schedule(
                scheduledTick,
                ResearchPhase.WorldDelivery,
                $"er1-world-{scheduledTick}",
                _ =>
                {
                    if (_options.ExternalRecovery && scheduledTick == 10)
                    {
                        onEpochChanged?.Invoke(0, 1, "external-recovery-10-v1");
                    }

                    var collected = GetEvidenceCollectedAtTick(scheduledTick);
                    if (collected.Count != 0)
                    {
                        onEvidenceCollected(scheduledTick, collected);
                    }

                    foreach (var notification in GetNotifications(scheduledTick))
                    {
                        onNotificationDelivered(notification);
                    }
                });
        }
    }

    private Er1FixtureSample CurrentIncident(uint tick, uint epoch)
    {
        var recovered = _options.ExternalRecovery && tick >= 10;
        var content = new IncidentObservationContent(new IncidentSnapshot(
            IncidentId,
            PaymentsServiceId,
            "Payments API elevated error rate",
            recovered ? IncidentStatus.Mitigating : IncidentStatus.Open,
            1,
            recovered ? 2 : 1,
            recovered ? T0810 : T0800));
        return DiagnosticSample(
            ResearchOperation.GetIncident,
            TargetId.Incident,
            tick,
            recovered ? 10u : 0u,
            epoch,
            [recovered ? "ev-inc-1042-v2" : "ev-inc-1042-v1"],
            content);
    }

    private Er1FixtureSample CurrentHealth(TargetId targetId, uint tick, uint epoch)
    {
        var isPayments = targetId == TargetId.PaymentsApi;
        var serviceId = isPayments ? PaymentsServiceId : AuthorizationServiceId;
        var instances = isPayments
            ? new[]
            {
                Instance("payments-api-01"),
                Instance("payments-api-02"),
                Instance("payments-api-03")
            }
            : new[]
            {
                Instance("authorization-service-01"),
                Instance("authorization-service-02")
            };
        return DiagnosticSample(
            ResearchOperation.GetServiceHealth,
            targetId,
            tick,
            0,
            epoch,
            [isPayments ? "ev-pay-health-v1" : "ev-auth-health-v1"],
            new ServiceHealthObservationContent(new ServiceHealthSnapshot(
                serviceId,
                ServiceHealth.Healthy,
                1,
                Array.AsReadOnly(instances))));
    }

    private Er1FixtureSample CurrentMetrics(TargetId targetId, uint tick, uint epoch)
    {
        var records = targetId == TargetId.PaymentsApi
            ? PaymentsMetrics(tick)
            : AuthorizationMetrics(tick);
        return DiagnosticSample(
            ResearchOperation.QueryMetrics,
            targetId,
            tick,
            records.Count == 0 ? tick : records.Max(item => item.CollectionTick),
            epoch,
            SortedIds(records.Select(item => item.EvidenceId)),
            new MetricsObservationContent(Array.AsReadOnly(records
                .OrderBy(item => item.Value.Timestamp)
                .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
                .Select(item => item.Value)
                .ToArray())));
    }

    private Er1FixtureSample CurrentLogs(TargetId targetId, uint tick, uint epoch)
    {
        var records = targetId == TargetId.PaymentsApi
            ? PaymentsLogs(tick)
            : AuthorizationLogs(tick);
        return DiagnosticSample(
            ResearchOperation.QueryLogs,
            targetId,
            tick,
            records.Count == 0 ? tick : records.Max(item => item.CollectionTick),
            epoch,
            SortedIds(records.Select(item => item.EvidenceId)),
            new LogsObservationContent(Array.AsReadOnly(records
                .OrderBy(item => item.Value.Timestamp)
                .ThenBy(item => item.EvidenceId, StringComparer.Ordinal)
                .Select(item => item.Value)
                .ToArray())));
    }

    private List<EvidenceValue<MetricSample>> PaymentsMetrics(uint tick)
    {
        var records = new List<EvidenceValue<MetricSample>>
        {
            Metric("ev-pay-err-0755", 0, "http.server.error_rate", 0.011, "ratio", T0755),
            Metric("ev-pay-p95-0755", 0, "http.server.p95_latency", 220, "ms", T0755),
            Metric("ev-pay-cpu-00", 0, "process.cpu.utilization", 0.47, "ratio", T0800),
            Metric("ev-pay-err-00", 0, "http.server.error_rate", 0.18, "ratio", T0800),
            Metric("ev-pay-mem-00", 0, "process.memory.utilization", 0.58, "ratio", T0800),
            Metric("ev-pay-p95-00", 0, "http.server.p95_latency", 2600, "ms", T0800)
        };

        if (_options.DevelopmentVariant == Er1DevelopmentVariant.Straightforward)
        {
            AddIfCollected(records, tick,
                Metric("ev-pay-cpu-04", 4, "process.cpu.utilization", 0.49, "ratio", T0804),
                Metric("ev-pay-mem-04", 4, "process.memory.utilization", 0.60, "ratio", T0804));
        }
        else
        {
            AddIfCollected(records, tick,
                Metric("ev-pay-cpu-p03-04", 4, "process.cpu.utilization.payments-api-03", 0.87, "ratio", T0804),
                Metric("ev-pay-mem-p03-04", 4, "process.memory.utilization.payments-api-03", 0.64, "ratio", T0804),
                Metric("ev-pay-cpu-p03-08", 8, "process.cpu.utilization.payments-api-03", 0.79, "ratio", T0808),
                Metric("ev-pay-cpu-p03-12", 12, "process.cpu.utilization.payments-api-03", 0.61, "ratio", T0812),
                Metric("ev-pay-cpu-p03-16", 16, "process.cpu.utilization.payments-api-03", 0.49, "ratio", T0816));
        }

        if (_options.ExternalRecovery)
        {
            AddIfCollected(records, tick,
                Metric("ev-pay-err-0810", 10, "http.server.error_rate", 0.012, "ratio", T0810),
                Metric("ev-pay-p95-0810", 10, "http.server.p95_latency", 240, "ms", T0810));
        }

        return records;
    }

    private List<EvidenceValue<MetricSample>> AuthorizationMetrics(uint tick)
    {
        var records = new List<EvidenceValue<MetricSample>>
        {
            Metric("ev-auth-count-0755", 0, "http.server.request_count", 480, "count_per_minute", T0755),
            Metric("ev-auth-lat-0755", 0, "http.server.p95_latency", 95, "ms", T0755)
        };
        if (_options.ExternalRecovery)
        {
            AddIfCollected(records, tick,
                Metric("ev-auth-lat-0810", 10, "http.server.p95_latency", 110, "ms", T0810));
        }

        AddIfCollected(records, tick,
            Metric("ev-auth-lat-0806", 12, "http.server.p95_latency", 2550, "ms", T0806),
            Metric("ev-auth-count-0807", 12, "http.server.request_count", 512, "count_per_minute", T0807),
            Metric("ev-auth-lat-0807", 12, "http.server.p95_latency", 2800, "ms", T0807));
        return records;
    }

    private static List<EvidenceValue<LogEntry>> PaymentsLogs(uint tick)
    {
        var records = new List<EvidenceValue<LogEntry>>();
        AddIfCollected(records, tick,
            Log("ev-pay-log-a17-1", 8, T0806025, "error", "requestId=req-a17 authorization-service call timed out after 2000 ms"),
            Log("ev-pay-log-b09-1", 8, T0807024, "error", "requestId=req-b09 authorization-service call timed out after 2000 ms"),
            Log("ev-pay-log-0758", 16, T0758, "warning", "cache=merchant-profile refresh exceeded 400 ms"),
            Log("ev-pay-log-0814", 16, T0814, "warning", "cache=merchant-profile refresh exceeded 400 ms"));
        return records;
    }

    private List<EvidenceValue<LogEntry>> AuthorizationLogs(uint tick)
    {
        var records = new List<EvidenceValue<LogEntry>>();
        AddIfCollected(records, tick,
            Log("ev-auth-log-a17-1", 12, T0806005, "information", "requestId=req-a17 stage=arrived"),
            Log("ev-auth-log-a17-2", 12, T08060305, "information", "requestId=req-a17 stage=processing-start"),
            Log("ev-auth-log-a17-3", 12, T08060313, "information", "requestId=req-a17 stage=processing-end"),
            Log("ev-auth-log-b09-1", 12, T0807004, "information", "requestId=req-b09 stage=arrived"),
            Log("ev-auth-log-b09-2", 12, T0807032, "information", "requestId=req-b09 stage=processing-start"),
            Log("ev-auth-log-b09-3", 12, T080703275, "information", "requestId=req-b09 stage=processing-end"));
        if (_options.ExternalRecovery)
        {
            AddIfCollected(records, tick,
                Log("ev-auth-log-c17-1", 12, T0811002, "information", "requestId=req-c17 stage=arrived"),
                Log("ev-auth-log-c17-2", 12, T08110026, "information", "requestId=req-c17 stage=processing-start"),
                Log("ev-auth-log-c17-3", 12, T081100335, "information", "requestId=req-c17 stage=processing-end"));
        }

        return records;
    }

    private Er1FixtureSample Notification(
        string sourceId,
        TargetId targetId,
        uint tick,
        string evidenceId,
        string text) =>
        new(
            "shared-notification",
            sourceId,
            targetId,
            tick,
            tick,
            GetApplicabilityEpoch(tick),
            Array.AsReadOnly(new[] { evidenceId }),
            new NotificationObservationContent(text));

    private static Er1FixtureSample DiagnosticSample(
        ResearchOperation operation,
        TargetId targetId,
        uint sampledTick,
        uint availableTick,
        uint epoch,
        IEnumerable<string> evidenceIds,
        ObservationContent content) =>
        new(
            "diagnostic",
            $"{OperationName(operation)}.{TargetName(targetId)}",
            targetId,
            sampledTick,
            availableTick,
            epoch,
            SortedIds(evidenceIds),
            content);

    private static ServiceInstanceSnapshot Instance(string id) =>
        new(id, ServiceHealth.Healthy, 0, T075930);

    private static EvidenceValue<MetricSample> Metric(
        string evidenceId,
        uint collectionTick,
        string name,
        double value,
        string unit,
        DateTimeOffset timestamp) =>
        new(evidenceId, collectionTick, new MetricSample(name, value, unit, timestamp));

    private static EvidenceValue<LogEntry> Log(
        string evidenceId,
        uint collectionTick,
        DateTimeOffset timestamp,
        string level,
        string message) =>
        new(evidenceId, collectionTick, new LogEntry(timestamp, level, message, false));

    private static void AddIfCollected<T>(
        ICollection<EvidenceValue<T>> destination,
        uint tick,
        params EvidenceValue<T>[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate.CollectionTick <= tick)
            {
                destination.Add(candidate);
            }
        }
    }

    private static IReadOnlyList<string> SortedIds(IEnumerable<string> ids) =>
        Array.AsReadOnly(ids
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray());

    private static void ValidatePair(ResearchOperation operation, TargetId targetId)
    {
        var valid = operation switch
        {
            ResearchOperation.GetIncident => targetId == TargetId.Incident,
            ResearchOperation.GetServiceHealth or
                ResearchOperation.QueryMetrics or
                ResearchOperation.QueryLogs =>
                targetId is TargetId.PaymentsApi or TargetId.AuthorizationService,
            _ => false
        };
        if (!valid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(targetId),
                "Operation and target are outside the approved seven-pair catalogue.");
        }
    }

    private static void ValidateTick(uint tick)
    {
        if (tick >= ApprovedScriptedExperiment.Clock.EndTickExclusive)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tick),
                "Fixture dispatch and delivery ticks must be before the exclusive horizon.");
        }
    }

    private static string OperationName(ResearchOperation operation) =>
        operation switch
        {
            ResearchOperation.GetIncident => "get_incident",
            ResearchOperation.GetServiceHealth => "get_service_health",
            ResearchOperation.QueryMetrics => "query_metrics",
            ResearchOperation.QueryLogs => "query_logs",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };

    private static string TargetName(TargetId targetId) =>
        targetId switch
        {
            TargetId.Incident => IncidentId,
            TargetId.PaymentsApi => PaymentsServiceId,
            TargetId.AuthorizationService => AuthorizationServiceId,
            _ => throw new ArgumentOutOfRangeException(nameof(targetId))
        };

    private static DateTimeOffset SourceTime(string evidenceId) =>
        evidenceId switch
        {
            "ev-auth-count-0755" or
            "ev-auth-lat-0755" or
            "ev-pay-err-0755" or
            "ev-pay-p95-0755" => T0755,
            "ev-pay-log-0758" => T0758,
            "ev-auth-health-v1" or
            "ev-pay-health-v1" => T075930,
            "ev-inc-1042-v1" or
            "ev-notification-00-v1" or
            "ev-pay-cpu-00" or
            "ev-pay-err-00" or
            "ev-pay-mem-00" or
            "ev-pay-p95-00" => T0800,
            "ev-notification-04-v1" or
            "ev-notification-04-v2" or
            "ev-pay-cpu-04" or
            "ev-pay-cpu-p03-04" or
            "ev-pay-mem-04" or
            "ev-pay-mem-p03-04" => T0804,
            "ev-auth-lat-0806" => T0806,
            "ev-auth-log-a17-1" => T0806005,
            "ev-pay-log-a17-1" => T0806025,
            "ev-auth-log-a17-2" => T08060305,
            "ev-auth-log-a17-3" => T08060313,
            "ev-auth-count-0807" or
            "ev-auth-lat-0807" => T0807,
            "ev-auth-log-b09-1" => T0807004,
            "ev-pay-log-b09-1" => T0807024,
            "ev-auth-log-b09-2" => T0807032,
            "ev-auth-log-b09-3" => T080703275,
            "ev-notification-08-v1" or
            "ev-pay-cpu-p03-08" => T0808,
            "ev-auth-lat-0810" or
            "ev-inc-1042-v2" or
            "ev-notification-10-v1" or
            "ev-pay-err-0810" or
            "ev-pay-p95-0810" => T0810,
            "ev-auth-log-c17-1" => T0811002,
            "ev-auth-log-c17-2" => T08110026,
            "ev-auth-log-c17-3" => T081100335,
            "ev-pay-cpu-p03-12" => T0812,
            "ev-pay-log-0814" => T0814,
            "ev-notification-16-v1" or
            "ev-pay-cpu-p03-16" => T0816,
            _ => throw new InvalidOperationException(
                $"Evidence '{evidenceId}' has no source-time definition.")
        };

    private static DateTimeOffset At(string value) =>
        DateTimeOffset.Parse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal);

    private static IReadOnlyList<EvidenceDefinition> AllEvidence() =>
    [
        new("ev-inc-1042-v1", 0),
        new("ev-pay-health-v1", 0),
        new("ev-auth-health-v1", 0),
        new("ev-pay-err-0755", 0),
        new("ev-pay-p95-0755", 0),
        new("ev-pay-err-00", 0),
        new("ev-pay-p95-00", 0),
        new("ev-pay-cpu-00", 0),
        new("ev-pay-mem-00", 0),
        new("ev-pay-cpu-04", 4, Er1DevelopmentVariant.Straightforward),
        new("ev-pay-mem-04", 4, Er1DevelopmentVariant.Straightforward),
        new("ev-pay-cpu-p03-04", 4, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-pay-mem-p03-04", 4, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-pay-cpu-p03-08", 8, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-pay-cpu-p03-12", 12, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-pay-cpu-p03-16", 16, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-pay-log-a17-1", 8),
        new("ev-pay-log-b09-1", 8),
        new("ev-auth-lat-0755", 0),
        new("ev-auth-count-0755", 0),
        new("ev-auth-lat-0806", 12),
        new("ev-auth-lat-0807", 12),
        new("ev-auth-count-0807", 12),
        new("ev-auth-log-a17-1", 12),
        new("ev-auth-log-a17-2", 12),
        new("ev-auth-log-a17-3", 12),
        new("ev-auth-log-b09-1", 12),
        new("ev-auth-log-b09-2", 12),
        new("ev-auth-log-b09-3", 12),
        new("ev-pay-log-0758", 16),
        new("ev-pay-log-0814", 16),
        new("ev-inc-1042-v2", 10, RequiresRecovery: true),
        new("ev-pay-err-0810", 10, RequiresRecovery: true),
        new("ev-pay-p95-0810", 10, RequiresRecovery: true),
        new("ev-auth-lat-0810", 10, RequiresRecovery: true),
        new("ev-auth-log-c17-1", 12, RequiresRecovery: true),
        new("ev-auth-log-c17-2", 12, RequiresRecovery: true),
        new("ev-auth-log-c17-3", 12, RequiresRecovery: true),
        new("ev-notification-00-v1", 0),
        new("ev-notification-04-v1", 4, Er1DevelopmentVariant.Straightforward),
        new("ev-notification-04-v2", 4, Er1DevelopmentVariant.AmbiguousCpuSymptom),
        new("ev-notification-08-v1", 8, Er1DevelopmentVariant.Straightforward),
        new("ev-notification-10-v1", 10, RequiresRecovery: true),
        new("ev-notification-16-v1", 16)
    ];

    private sealed record EvidenceValue<T>(
        string EvidenceId,
        uint CollectionTick,
        T Value);

    private sealed record EvidenceDefinition(
        string EvidenceId,
        uint CollectionTick,
        Er1DevelopmentVariant? Variant = null,
        bool RequiresRecovery = false)
    {
        public bool Applies(Er1FixtureOptions options) =>
            (Variant is null || Variant == options.DevelopmentVariant) &&
            (!RequiresRecovery || options.ExternalRecovery);
    }
}

public sealed class Er1FixtureIncidentSimulator(
    Er1EvidenceFixture fixture,
    Func<uint> currentTick) : IIncidentSimulator
{
    public IncidentSnapshot GetIncident(string incidentId) =>
        Extract<IncidentObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.GetIncident,
                ParseIncident(incidentId),
                currentTick())).Value;

    public IReadOnlyList<MetricSample> QueryMetrics(string serviceId) =>
        Extract<MetricsObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.QueryMetrics,
                ParseService(serviceId),
                currentTick())).Values;

    public IReadOnlyList<LogEntry> QueryLogs(string serviceId) =>
        Extract<LogsObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.QueryLogs,
                ParseService(serviceId),
                currentTick())).Values;

    public ServiceHealthSnapshot GetServiceHealth(string serviceId) =>
        Extract<ServiceHealthObservationContent>(
            fixture.SampleDiagnostic(
                ResearchOperation.GetServiceHealth,
                ParseService(serviceId),
                currentTick())).Value;

    public SimulatorWriteResult<IncidentSnapshot> UpdateIncident(
        string incidentId,
        IncidentStatus status,
        long expectedVersion,
        string idempotencyKey) =>
        throw ReadOnly();

    public SimulatorWriteResult<ServiceStateCheckpoint> RestartService(
        string serviceId,
        string instanceId,
        long expectedVersion,
        string idempotencyKey) =>
        throw ReadOnly();

    public SimulatorWriteResult<ServiceHealthSnapshot> RestoreServiceState(
        ServiceStateCheckpoint checkpoint,
        long expectedVersion,
        string idempotencyKey) =>
        throw ReadOnly();

    public void Reset()
    {
    }

    private static T Extract<T>(Er1FixtureSample sample)
        where T : ObservationContent =>
        sample.Content as T
        ?? throw new InvalidOperationException("Fixture returned an unexpected observation branch.");

    private static TargetId ParseIncident(string incidentId) =>
        string.Equals(incidentId, Er1EvidenceFixture.IncidentId, StringComparison.Ordinal)
            ? TargetId.Incident
            : throw new ArgumentOutOfRangeException(nameof(incidentId));

    private static TargetId ParseService(string serviceId) =>
        serviceId switch
        {
            Er1EvidenceFixture.PaymentsServiceId => TargetId.PaymentsApi,
            Er1EvidenceFixture.AuthorizationServiceId => TargetId.AuthorizationService,
            _ => throw new ArgumentOutOfRangeException(nameof(serviceId))
        };

    private static NotSupportedException ReadOnly() =>
        new("ER1-fixture-1 is a read-only diagnostic fixture.");
}
