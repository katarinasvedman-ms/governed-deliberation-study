using System.Diagnostics;
using GovernedAgent.Core.Contracts;
using GovernedAgent.Host.Verification;
using GovernedAgent.Simulator;
using Microsoft.Agents.AI.Workflows;
using Xunit.Abstractions;

namespace GovernedAgent.IntegrationTests.T0;

public sealed class FrameworkFeasibilityTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(ProbeDecision.Query)]
    [InlineData(ProbeDecision.Report)]
    public async Task IndependentRunsProgressAndSuppressCancellationIgnoringActor(ProbeDecision obsoleteDecision)
    {
        var first = await RunInterruptionAsync(obsoleteDecision);
        var second = await RunInterruptionAsync(obsoleteDecision);
        Assert.Equal(first, second);
        output.WriteLine("Canonical causal trace reproduced exactly across two independent runs.");
    }

    private async Task<string[]> RunInterruptionAsync(ProbeDecision obsoleteDecision)
    {
        var trace = new ProbeTrace(output);
        await using var owner = new GovernedProbeOwner(trace);
        var review = owner.Start("review");
        await review.Started;

        trace.AdvanceTo(1);
        owner.ObserveExternalEvent();
        var first = owner.Start("actor-1");
        await first.Started;
        first.Release.SetResult(ProbeDecision.Query);
        Assert.Equal("executed", await owner.ProcessAsync(first, await first.Result));
        Assert.False(review.Result.IsCompleted);
        Assert.Contains("external-notification", first.Snapshot.Observations);
        Assert.DoesNotContain("external-notification", review.Snapshot.Observations);
        trace.Record("parallel.progress diagnostic-completed-while-review-pending");

        trace.AdvanceTo(2);
        var obsolete = owner.Start("actor-obsolete");
        await obsolete.Started;
        review.Release.SetResult(ProbeDecision.Focus);
        owner.AcceptGuidance(review, await review.Result, obsolete);
        var replacement = owner.Start("actor-replacement");
        await replacement.Started;
        Assert.Equal("review", replacement.Snapshot.Guidance);
        Assert.Null(obsolete.Snapshot.Guidance);
        Assert.Equal(0, obsolete.Snapshot.Generation);
        Assert.Equal(1, replacement.Snapshot.Generation);
        replacement.Release.SetResult(ProbeDecision.Query);
        Assert.Equal("executed", await owner.ProcessAsync(replacement, await replacement.Result));
        Assert.False(obsolete.Result.IsCompleted);
        trace.Record("reconsideration.completed obsolete-call-still-pending");

        trace.AdvanceTo(3);
        obsolete.Release.SetResult(obsoleteDecision);
        Assert.Equal("suppressed", await owner.ProcessAsync(obsolete, await obsolete.Result));
        Assert.True(obsolete.CancellationWasIgnored);
        Assert.False(owner.ReportSubmitted);
        Assert.Equal(2, owner.DispatchCount);
        Assert.Equal(2, owner.DiagnosticResults.Count);
        Assert.Equal(4, owner.Audit.ReadAll().Count);
        Assert.True(owner.Audit.VerifyIntegrity());
        Assert.Equal(3, obsolete.Snapshot.Observations.Length);
        Assert.DoesNotContain("diagnostic-actor-replacement", obsolete.Snapshot.Observations);
        AssertBaselineUnchanged(owner);

        var report = owner.Start("actor-current-report");
        await report.Started;
        report.Release.SetResult(ProbeDecision.Report);
        Assert.Equal("reported", await owner.ProcessAsync(report, await report.Result));
        Assert.True(owner.ReportSubmitted);
        return trace.Entries;
    }

    [Theory]
    [InlineData(ProbeDecision.Query)]
    [InlineData(ProbeDecision.Report)]
    public async Task GuidanceWinsBeforeDispatchOfAlreadyReturnedDecision(ProbeDecision decision)
    {
        var trace = new ProbeTrace(output);
        await using var owner = new GovernedProbeOwner(trace);
        var review = owner.Start("review");
        await review.Started;
        var actor = owner.Start("actor");
        await actor.Started;
        actor.Release.SetResult(decision);
        var readyDecision = await actor.Result;
        review.Release.SetResult(ProbeDecision.Focus);
        var readyGuidance = await review.Result;

        trace.AdvanceTo(1);
        trace.Record("same-stage.priority guidance-before-decision-dispatch");
        owner.AcceptGuidance(review, readyGuidance);
        Assert.Equal("suppressed", await owner.ProcessAsync(actor, readyDecision));
        Assert.Equal(0, owner.DispatchCount);
        Assert.Empty(owner.Audit.ReadAll());
        Assert.False(owner.ReportSubmitted);
    }

    [Fact]
    public async Task InterruptDuringVerificationIsRecheckedAtActualDispatchBoundary()
    {
        var trace = new ProbeTrace(output);
        var verifier = new PausedVerifier();
        await using var owner = new GovernedProbeOwner(trace, verifier);
        var review = owner.Start("review");
        await review.Started;
        var actor = owner.Start("actor");
        await actor.Started;
        actor.Release.SetResult(ProbeDecision.Query);
        var processing = owner.ProcessAsync(actor, await actor.Result);
        try
        {
            await verifier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            review.Release.SetResult(ProbeDecision.Focus);
            owner.AcceptGuidance(review, await review.Result);
        }
        finally
        {
            verifier.Release.TrySetResult(true);
        }

        Assert.Equal("obsolete_decision", await processing);
        Assert.Equal(0, owner.DispatchCount);
        Assert.Empty(owner.DiagnosticResults);
        Assert.False(owner.ReportSubmitted);
        Assert.True(owner.Audit.VerifyIntegrity());
    }

    [Fact]
    public async Task AlreadyDispatchedDiagnosticCompletesOnceAndRetainsObservation()
    {
        var trace = new ProbeTrace(output);
        await using var owner = new GovernedProbeOwner(trace)
        {
            DiagnosticRelease = FrameworkProbe.Signal<bool>()
        };
        var review = owner.Start("review");
        await review.Started;
        var actor = owner.Start("actor");
        await actor.Started;
        actor.Release.SetResult(ProbeDecision.Query);
        var processing = owner.ProcessAsync(actor, await actor.Result);
        await owner.DiagnosticDispatched.Task.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.False(processing.IsCompleted);
        Assert.Empty(owner.DiagnosticResults);
        review.Release.SetResult(ProbeDecision.Focus);
        owner.AcceptGuidance(review, await review.Result);
        owner.DiagnosticRelease.SetResult(true);

        Assert.Equal("executed", await processing);
        var retained = Assert.Single(owner.DiagnosticResults).GetRawText();
        Assert.Equal("suppressed", await owner.ProcessAsync(actor, ProbeDecision.Query));
        Assert.Equal(retained, Assert.Single(owner.DiagnosticResults).GetRawText());
        Assert.Single(owner.Observations, item => item == "diagnostic-actor");
        Assert.Equal(1, owner.DispatchCount);
        Assert.Equal(2, owner.Audit.ReadAll().Count);
        Assert.True(owner.Audit.VerifyIntegrity());
        AssertBaselineUnchanged(owner);
    }

    [Theory]
    [InlineData("kill_switch_active")]
    [InlineData("verifier_unavailable")]
    [InlineData("budget_exhausted")]
    public async Task PendingReviewDoesNotBypassGovernance(string reason)
    {
        var trace = new ProbeTrace(output);
        await using var owner = new GovernedProbeOwner(
            trace,
            reason == "verifier_unavailable"
                ? new NodePlanVerifier("node-t0-intentionally-missing", "missing.js", TimeSpan.FromSeconds(1))
                : null,
            toolBudget: reason == "budget_exhausted" ? 0 : 12);
        if (reason == "kill_switch_active") { owner.KillSwitch.Activate(); }
        var review = owner.Start("review");
        await review.Started;
        var actor = owner.Start("actor");
        await actor.Started;
        actor.Release.SetResult(ProbeDecision.Query);

        Assert.Equal(reason, await owner.ProcessAsync(actor, await actor.Result));
        Assert.False(review.Result.IsCompleted);
        Assert.Equal(0, owner.DispatchCount);
        Assert.Empty(owner.DiagnosticResults);
        Assert.False(owner.ReportSubmitted);
        AssertBaselineUnchanged(owner);
    }

    [Fact]
    public async Task SharedFanOutGraphHasASuperstepBarrier()
    {
        var trace = new ProbeTrace(output);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var actorReturned = FrameworkProbe.Signal<bool>();
        var reviewEntered = FrameworkProbe.Signal<bool>();
        var releaseReview = FrameworkProbe.Signal<bool>();
        var downstreamEntered = FrameworkProbe.Signal<bool>();
        long actorReturnedAt = 0;
        long downstreamEnteredAt = 0;
        long reviewReleasedAt = 0;
        var root = new ProbeExecutor<int, int>("root", (value, _) => ValueTask.FromResult(value));
        var actor = new ProbeExecutor<int, int>("actor", (value, _) =>
        {
            actorReturnedAt = Stopwatch.GetTimestamp();
            actorReturned.SetResult(true);
            return ValueTask.FromResult(value);
        });
        var review = new ProbeExecutor<int, int>("review", async (value, token) =>
        {
            reviewEntered.SetResult(true);
            await releaseReview.Task.WaitAsync(token);
            trace.Record("shared-graph.review-returning");
            return value;
        });
        var downstream = new ProbeExecutor<int, int>("downstream", (value, _) =>
        {
            downstreamEnteredAt = Stopwatch.GetTimestamp();
            trace.Record("shared-graph.downstream-entered");
            downstreamEntered.SetResult(true);
            return ValueTask.FromResult(value);
        });
        var graph = new WorkflowBuilder(root)
            .AddFanOutEdge(root, [actor, review])
            .AddEdge(actor, downstream)
            .WithOutputFrom(downstream)
            .Build();
        var task = InProcessExecution.RunAsync(graph, 1, cancellationToken: lifetime.Token).AsTask();
        WorkflowEvent[] events = [];
        try
        {
            await Task.WhenAll(actorReturned.Task, reviewEntered.Task).WaitAsync(lifetime.Token);
            Assert.False(downstreamEntered.Task.IsCompleted);
            trace.Record("shared-graph.downstream-blocked-while-review-pending");
        }
        finally
        {
            reviewReleasedAt = Stopwatch.GetTimestamp();
            releaseReview.TrySetResult(true);
            await using var run = await task;
            events = run.NewEvents.ToArray();
        }

        Assert.True(downstreamEntered.Task.IsCompletedSuccessfully);
        Assert.Single(events.OfType<WorkflowOutputEvent>());
        Assert.Collection(trace.Entries,
            entry => Assert.Contains("downstream-blocked", entry),
            entry => Assert.Contains("review-returning", entry),
            entry => Assert.Contains("downstream-entered", entry));
        output.WriteLine(
            $"Observed actor-return-to-downstream={Stopwatch.GetElapsedTime(actorReturnedAt, downstreamEnteredAt).TotalMilliseconds:F3} ms; " +
            $"review-release-to-downstream={Stopwatch.GetElapsedTime(reviewReleasedAt, downstreamEnteredAt).TotalMilliseconds:F3} ms. " +
            "Controlled hold plus runtime overhead, not model latency or an experimental tick.");
    }

    private static void AssertBaselineUnchanged(GovernedProbeOwner owner)
    {
        var service = owner.Simulator.GetServiceHealth(IncidentSimulator.DemoServiceId);
        Assert.Equal(ServiceHealth.Degraded, service.Health);
        Assert.All(service.Instances, instance => Assert.Equal(0, instance.RestartCount));
        Assert.Equal(IncidentStatus.Open, owner.Simulator.GetIncident(IncidentSimulator.DemoIncidentId).Status);
    }

    private sealed class PausedVerifier : IPlanVerifier
    {
        public TaskCompletionSource<bool> Entered { get; } = FrameworkProbe.Signal<bool>();
        public TaskCompletionSource<bool> Release { get; } = FrameworkProbe.Signal<bool>();

        public async ValueTask<PlanVerificationDecision> VerifyAsync(
            PlanVerificationRequest request, CancellationToken cancellationToken)
        {
            Entered.SetResult(true);
            await Release.Task.WaitAsync(cancellationToken);
            return await GovernedProbeOwner.CreateVerifier().VerifyAsync(request, cancellationToken);
        }
    }
}
