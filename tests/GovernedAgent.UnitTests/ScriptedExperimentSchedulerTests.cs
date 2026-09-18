using GovernedAgent.Research;

namespace GovernedAgent.UnitTests;

public sealed class ScriptedExperimentSchedulerTests
{
    [Fact]
    public void SameTickActionsUseApprovedPhaseAndRegistrationOrder()
    {
        var scheduler = new ScriptedExperimentScheduler();
        foreach (var phase in new[]
                 {
                     ResearchPhase.ActorStart,
                     ResearchPhase.ActorProcessing,
                     ResearchPhase.WorldDelivery,
                     ResearchPhase.EndCheck,
                     ResearchPhase.ReviewStart,
                     ResearchPhase.PriorDiagnosticResults,
                     ResearchPhase.ReviewProcessing
                 })
        {
            scheduler.Schedule(4, phase, $"action-{phase}", _ => { });
        }

        scheduler.Schedule(4, ResearchPhase.ActorProcessing, "action-actor-processing-2", _ => { });

        var result = scheduler.Run();

        Assert.Equal(
            new[]
            {
                ResearchPhase.EndCheck,
                ResearchPhase.WorldDelivery,
                ResearchPhase.PriorDiagnosticResults,
                ResearchPhase.ReviewProcessing,
                ResearchPhase.ActorProcessing,
                ResearchPhase.ActorProcessing,
                ResearchPhase.ReviewStart,
                ResearchPhase.ActorStart
            },
            result.Trace.Select(item => item.Phase));
        Assert.Equal(
            ["action-ActorProcessing", "action-actor-processing-2"],
            result.Trace
                .Where(item => item.Phase == ResearchPhase.ActorProcessing)
                .Select(item => item.ActionId));
    }

    [Fact]
    public void ExclusiveHorizonRunsOnlyEndCheckAtTickTwentyFour()
    {
        var scheduler = new ScriptedExperimentScheduler();
        scheduler.Schedule(23, ResearchPhase.ActorStart, "last-actor-start", _ => { });
        scheduler.Schedule(24, ResearchPhase.EndCheck, "horizon", _ => { });

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            scheduler.Schedule(24, ResearchPhase.WorldDelivery, "too-late", _ => { }));

        var result = scheduler.Run();

        Assert.True(result.HorizonReached);
        Assert.Equal(24u, result.FinalTick);
        Assert.Equal(["last-actor-start", "horizon"], result.Trace.Select(item => item.ActionId));
    }

    [Fact]
    public void EarlyStopPreventsLaterScheduledWork()
    {
        var scheduler = new ScriptedExperimentScheduler();
        scheduler.Schedule(5, ResearchPhase.ActorProcessing, "report", context =>
            context.Stop("report-submitted"));
        scheduler.Schedule(8, ResearchPhase.WorldDelivery, "future-world-change", _ => { });

        var result = scheduler.Run();

        Assert.False(result.HorizonReached);
        Assert.Equal(5u, result.FinalTick);
        Assert.Equal("report-submitted", result.StopReason);
        Assert.Single(result.Trace);
    }

    [Fact]
    public void BudgetsConsumeOnlyAcceptedStartsAndDispatches()
    {
        var scheduler = new ScriptedExperimentScheduler();
        for (var index = 0; index < 25; index++)
        {
            var turn = index;
            scheduler.Schedule((uint)Math.Min(turn, 23), ResearchPhase.ActorStart, $"actor-{turn}", context =>
            {
                Assert.Equal(turn < 24, context.Budget.TryStartActorTurn());
                if (turn < 24)
                {
                    context.Budget.CompleteActorTurn();
                }
            });
        }

        for (var index = 0; index < 13; index++)
        {
            var attempt = index;
            scheduler.Schedule((uint)attempt, ResearchPhase.ActorProcessing, $"diagnostic-{attempt}", context =>
                Assert.Equal(attempt < 12, context.Budget.TryDispatchDiagnostic()));
        }

        foreach (var checkpoint in scheduler.ReviewCheckpoints)
        {
            scheduler.Schedule(checkpoint, ResearchPhase.ReviewStart, $"review-{checkpoint}", context =>
            {
                Assert.Equal(
                    ReviewCheckpointDisposition.Start,
                    context.Budget.EvaluateReviewCheckpoint());
                context.Budget.CompleteReview();
            });
        }

        scheduler.Schedule(20, ResearchPhase.ReviewStart, "review-over-budget", context =>
            Assert.Equal(
                ReviewCheckpointDisposition.ReviewBudgetExhausted,
                context.Budget.EvaluateReviewCheckpoint()));

        var result = scheduler.Run();

        Assert.Equal(new ScriptedResourceCounts(24, 12, 6), result.Counts);
    }

    [Fact]
    public void ActiveReviewSkipsCheckpointWithoutConsumingReviewBudget()
    {
        var scheduler = new ScriptedExperimentScheduler();
        ReviewCheckpointDisposition? first = null;
        ReviewCheckpointDisposition? skipped = null;
        ReviewCheckpointDisposition? second = null;
        scheduler.Schedule(0, ResearchPhase.ReviewStart, "review-0", context =>
            first = context.Budget.EvaluateReviewCheckpoint());
        scheduler.Schedule(4, ResearchPhase.ReviewStart, "review-4", context =>
            skipped = context.Budget.EvaluateReviewCheckpoint());
        scheduler.Schedule(5, ResearchPhase.ReviewProcessing, "review-0-complete", context =>
            context.Budget.CompleteReview());
        scheduler.Schedule(8, ResearchPhase.ReviewStart, "review-8", context =>
            second = context.Budget.EvaluateReviewCheckpoint());

        var result = scheduler.Run();

        Assert.Equal(ReviewCheckpointDisposition.Start, first);
        Assert.Equal(ReviewCheckpointDisposition.SkipActiveReview, skipped);
        Assert.Equal(ReviewCheckpointDisposition.Start, second);
        Assert.Equal(2u, result.Counts.Reviews);
    }

    [Fact]
    public void ApprovedDurationsAndExclusiveDeadlinesAreExact()
    {
        var scheduler = new ScriptedExperimentScheduler();

        Assert.Equal(13u, scheduler.ActorCompletionTick(12));
        Assert.Equal(15u, scheduler.ReviewCompletionTick(12));
        Assert.Equal(18u, scheduler.ReviewTimeoutTick(12));
        Assert.False(scheduler.IsReviewTimedOut(12, 17));
        Assert.True(scheduler.IsReviewTimedOut(12, 18));
        Assert.Equal(20u, scheduler.GuidanceExpiryTick(12));
        Assert.False(scheduler.IsGuidanceExpired(12, 19));
        Assert.True(scheduler.IsGuidanceExpired(12, 20));
        scheduler.ValidateWait(12, 16);
        Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ValidateWait(12, 17));
    }

    [Fact]
    public void DelayedReviewSkipsTwelveWithoutCatchUpAndStartsAtSixteen()
    {
        var scheduler = new ScriptedExperimentScheduler();
        var outcomes = new List<ReviewCheckpointDisposition>();
        scheduler.Schedule(8, ResearchPhase.ReviewStart, "review-8", context =>
            outcomes.Add(context.Budget.EvaluateReviewCheckpoint()));
        scheduler.Schedule(12, ResearchPhase.ReviewStart, "review-12", context =>
            outcomes.Add(context.Budget.EvaluateReviewCheckpoint()));
        scheduler.Schedule(13, ResearchPhase.ReviewProcessing, "review-8-complete", context =>
            context.Budget.CompleteReview());
        scheduler.Schedule(16, ResearchPhase.ReviewStart, "review-16", context =>
            outcomes.Add(context.Budget.EvaluateReviewCheckpoint()));

        var result = scheduler.Run();

        Assert.Equal(
            [
                ReviewCheckpointDisposition.Start,
                ReviewCheckpointDisposition.SkipActiveReview,
                ReviewCheckpointDisposition.Start
            ],
            outcomes);
        Assert.Equal(2u, result.Counts.Reviews);
    }

    [Fact]
    public void WorldUpdatesAndInterruptsPrecedeSameTickActorProcessing()
    {
        var scheduler = new ScriptedExperimentScheduler();
        scheduler.Schedule(8, ResearchPhase.ActorProcessing, "wait-result", _ => { });
        scheduler.Schedule(8, ResearchPhase.WorldDelivery, "shared-notification", _ => { });
        scheduler.Schedule(16, ResearchPhase.ActorProcessing, "obsolete-query", _ => { });
        scheduler.Schedule(16, ResearchPhase.ReviewProcessing, "guidance-accepted", _ => { });

        var result = scheduler.Run();

        Assert.Equal(
            ["shared-notification", "wait-result", "guidance-accepted", "obsolete-query"],
            result.Trace.Select(item => item.ActionId));
    }

    [Fact]
    public void PriorDiagnosticDeliveryPrecedesReviewAndIsRecordedOnce()
    {
        var scheduler = new ScriptedExperimentScheduler();
        scheduler.Schedule(11, ResearchPhase.ReviewProcessing, "guidance-arrives", _ => { });
        scheduler.Schedule(11, ResearchPhase.PriorDiagnosticResults, "diagnostic-completed", _ => { });

        var result = scheduler.Run();

        Assert.Equal(
            ["diagnostic-completed", "guidance-arrives"],
            result.Trace.Select(item => item.ActionId));
        Assert.Single(result.Trace, item => item.ActionId == "diagnostic-completed");
    }

    [Fact]
    public void ReviewTimeoutPrecedesSameTickLateResponse()
    {
        var scheduler = new ScriptedExperimentScheduler();
        scheduler.Schedule(14, ResearchPhase.ReviewProcessing, "late-review-result", _ => { });
        scheduler.ScheduleReviewTimeout(14, "review-timeout", _ => { });

        var result = scheduler.Run();

        Assert.Equal(
            ["review-timeout", "late-review-result"],
            result.Trace.Select(item => item.ActionId));
    }
}
