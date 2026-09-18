using System.Text.Json;
using GovernedAgent.Research;

namespace GovernedAgent.IntegrationTests.T3;

public sealed class ScriptedSupervisionCoordinatorTests
{
    private static readonly string Schema = ResearchContractVersions.SchemaVersion;

    [Fact]
    public async Task AsynchronousActorProgressesWhileReviewRemainsPending()
    {
        var reviewRelease = Signal<SupervisorOutput>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);

        var review = coordinator.StartReview(
            4,
            async (_, token) => await reviewRelease.Task.WaitAsync(token));
        Assert.NotNull(review);
        await review.Started;

        var actor = coordinator.StartActor(
            4,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1)));
        Assert.NotNull(actor);
        await actor.Started;
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 5);

        Assert.Single(coordinator.ActionHistory);
        Assert.Empty(coordinator.ReviewHistory);

        reviewRelease.SetResult(NoChange(review.Snapshot.MemoryRevision));
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);
        Assert.Single(coordinator.ReviewHistory);
    }

    [Fact]
    public async Task ActionableReviewInvalidatesDrainingActorAndReplacementCompletes()
    {
        var actorRelease = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);

        var oldActor = coordinator.StartActor(
            1,
            async (_, _) => await actorRelease.Task);
        Assert.NotNull(oldActor);
        await oldActor.Started;

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        var replacement = coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(1, Disposition(snapshot))));
        Assert.NotNull(replacement);
        await coordinator.ProcessActorResultAsync(replacement.ActorTurnId, 8);

        actorRelease.SetResult(QueryMetrics());
        await coordinator.ProcessActorResultAsync(oldActor.ActorTurnId, 9);

        Assert.Contains(
            coordinator.Events,
            item => item.EventType == "actor.invalidated");
        Assert.Contains(
            coordinator.Events,
            item => item.EventType == "cancellation.ignored");
        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == oldActor.ActorTurnId &&
                item.Disposition == "suppressed");
        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == replacement.ActorTurnId &&
                item.Disposition == "applied");
        Assert.Null(coordinator.PendingTriggerId);
    }

    [Fact]
    public async Task InterruptDuringVerificationSuppressesBeforeDispatch()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true,
            CompletionTick = 8
        };
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            dispatcher);
        ObserveInitial(coordinator);

        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        var processing = coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2).AsTask();
        await dispatcher.VerificationStarted.Task;

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencySideQueueDelay)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        var replacement = coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(1, Disposition(snapshot))));
        Assert.NotNull(replacement);
        await coordinator.ProcessActorResultAsync(replacement.ActorTurnId, 8);
        dispatcher.ReleaseAuthorization.SetResult(true);
        await processing;
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            8,
            ResearchPhase.ActorProcessing);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            8,
            ResearchPhase.ActorProcessing);

        Assert.Equal(0, dispatcher.DispatchCount);
        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "result.suppressed" &&
                item.Data.GetProperty("invocation").GetProperty("consumer").GetString() == "actor");
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task ActionableUpdateWakesWaitWithoutWaitingForTimer()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(4)));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2);

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "actor.wait-ended" &&
                item.Data.GetProperty("reason").GetProperty("code").GetString() ==
                    "memory-update-interrupt");
        Assert.NotNull(coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(1, Disposition(snapshot)))));
    }

    [Fact]
    public async Task AlreadyDispatchedDiagnosticCompletesExactlyOnceAfterInterrupt()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseAfterAuthorization = true,
            CompletionTick = 8
        };
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            dispatcher);
        ObserveInitial(coordinator);

        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        var processing = coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2).AsTask();
        await processing;
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);
        await dispatcher.Dispatched.Task;

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.LocalInstanceIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        dispatcher.ReleaseCompletion.SetResult(true);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            8,
            ResearchPhase.PriorDiagnosticResults);

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.completed");
        Assert.Equal(2, coordinator.Observations.Count);
        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == actor.ActorTurnId &&
                item.Disposition == "applied");
    }

    [Fact]
    public async Task WrongButValidAdvicePersistsAndDirectionExpiresWithoutInterrupt()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.LocalInstanceIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        var generation = coordinator.DecisionGeneration;
        Assert.Equal(Hypothesis.LocalInstanceIssue, Assert.Single(coordinator.Beliefs).Claim.Hypothesis);
        Assert.NotNull(coordinator.WorkingMemory.RecommendedDirection);

        coordinator.ExpireDirection(12);

        Assert.Equal(generation, coordinator.DecisionGeneration);
        Assert.Null(coordinator.WorkingMemory.RecommendedDirection);
        Assert.Equal(BeliefState.Provisional, Assert.Single(coordinator.WorkingMemory.BeliefStates).State);
        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "memory.committed" &&
                !item.Data.GetProperty("actionable").GetBoolean());
    }

    [Fact]
    public async Task RepeatedAdviceNormalizesToNoOpAndDoesNotInterruptAgain()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);

        var first = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(first);
        await coordinator.ProcessReviewResultAsync(first.ReviewId, 7);
        var generation = coordinator.DecisionGeneration;

        var second = coordinator.StartReview(
            8,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(second);
        await coordinator.ProcessReviewResultAsync(second.ReviewId, 11);
        var updateCount = coordinator.MemoryUpdates.Count;
        await coordinator.ProcessReviewResultAsync(second.ReviewId, 11);

        Assert.Equal(generation, coordinator.DecisionGeneration);
        Assert.Equal(updateCount, coordinator.MemoryUpdates.Count);
        Assert.Equal("no-op", coordinator.MemoryUpdates[^1].Outcome);
        Assert.Contains(
            coordinator.MemoryUpdates[^1].Normalization,
            note => note.Rule == "drop-repeated-direction");
        Assert.Contains(
            coordinator.Events,
            item => item.EventType == "result.duplicate-delivery");
    }

    [Fact]
    public async Task BlockingReviewCompletionFailureAndTimeoutReleaseActor()
    {
        await using var completed = Create(
            SupervisionArchitecture.BlockingSupervision,
            new ControlledDispatcher());
        ObserveInitial(completed);
        var review = completed.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                NoChange(snapshot.MemoryRevision)));
        Assert.NotNull(review);
        Assert.Null(completed.StartActor(
            4,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
        await completed.ProcessReviewResultAsync(review.ReviewId, 7);
        Assert.NotNull(completed.StartActor(
            7,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));

        await using var failed = Create(
            SupervisionArchitecture.BlockingSupervision,
            new ControlledDispatcher());
        ObserveInitial(failed);
        var failedReview = failed.StartReview(
            4,
            (_, _) => throw new InvalidOperationException("scripted failure"));
        Assert.NotNull(failedReview);
        await failed.ProcessReviewResultAsync(failedReview.ReviewId, 7);
        Assert.True(failed.SupervisionDegraded);
        Assert.NotNull(failed.StartActor(
            7,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));

        var timeoutRelease = Signal<SupervisorOutput>();
        await using var timedOut = Create(
            SupervisionArchitecture.BlockingSupervision,
            new ControlledDispatcher());
        ObserveInitial(timedOut);
        var timedReview = timedOut.StartReview(
            4,
            async (_, _) => await timeoutRelease.Task);
        Assert.NotNull(timedReview);
        timedOut.ProcessReviewTimeout(timedReview.ReviewId, 10);
        Assert.True(timedOut.SupervisionDegraded);
        Assert.NotNull(timedOut.StartActor(
            10,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
        timeoutRelease.SetResult(NoChange(timedReview.Snapshot.MemoryRevision));
        await timedOut.ProcessReviewResultAsync(timedReview.ReviewId, 11);
        Assert.Contains(
            timedOut.Events,
            item => item.EventType == "result.suppressed");
    }

    [Fact]
    public async Task LateActorResultAfterTerminationOnlyAddsDrainProvenance()
    {
        var actorRelease = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, _) => await actorRelease.Task);
        Assert.NotNull(actor);
        await actor.Started;

        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The exclusive horizon was reached."));
        var historyAtTermination = coordinator.Observations.Count;
        actorRelease.SetResult(Wait(1));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 24);

        Assert.Equal(historyAtTermination, coordinator.Observations.Count);
        Assert.All(
            coordinator.Events.SkipWhile(item => item.EventType != "episode.terminated").Skip(1),
            item => Assert.Equal(ResearchPhase.Drain, item.Phase));
        var closure = coordinator.Close();
        Assert.Equal("complete", closure.Status);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination!));
    }

    [Fact]
    public async Task ActorMustAcknowledgeLatestCapturedTriggerExactly()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        var actor = coordinator.StartActor(
            7,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1)));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 8);

        Assert.NotNull(coordinator.PendingTriggerId);
        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == actor.ActorTurnId &&
                item.Disposition == "rejected" &&
                item.Reason?.Code == "missing-memory-disposition");
    }

    [Fact]
    public async Task FrozenActorSnapshotRejectsObservationDeliveredAfterStart()
    {
        var actorRelease = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, _) => await actorRelease.Task);
        Assert.NotNull(actor);
        await actor.Started;

        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        coordinator.RecordExternalObservation(Assert.Single(fixture.GetNotifications(4)), 4);
        actorRelease.SetResult(
            new ReportActorDecision(
                Schema,
                "actor-decision",
                null,
                [],
                Hypothesis.Unresolved,
                ["observation-2"],
                NextStep.HumanHandoff,
                Uncertainty.High));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 5);

        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == actor.ActorTurnId &&
                item.Disposition == "rejected" &&
                item.Reason?.Code == "observation-not-in-snapshot");
        Assert.Null(coordinator.Termination);
    }

    [Fact]
    public async Task RecoveryEpochSuppressesOlderReviewWithoutMemoryMutation()
    {
        var reviewRelease = Signal<SupervisorOutput>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var review = coordinator.StartReview(
            8,
            async (snapshot, _) =>
            {
                await reviewRelease.Task;
                return Proposal(snapshot, Hypothesis.DependencyPathIssue);
            });
        Assert.NotNull(review);
        await review.Started;

        coordinator.ChangeEpoch(1, "external-recovery", 10);
        var updateCount = coordinator.MemoryUpdates.Count;
        reviewRelease.SetResult(NoChange(review.Snapshot.MemoryRevision));
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 11);

        Assert.Equal(updateCount, coordinator.MemoryUpdates.Count);
        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "result.suppressed" &&
                item.Data.GetProperty("invocation").GetProperty("consumer").GetString() ==
                    "supervisor");
    }

    [Fact]
    public async Task UnsupportedCancellationIsRecordedWithoutClaimingIgnoredRequest()
    {
        var actorRelease = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, _) => await actorRelease.Task,
            InvocationCancellationCapability.Unsupported);
        Assert.NotNull(actor);
        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        actorRelease.SetResult(Wait(1));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 8);

        Assert.Contains(
            coordinator.Events,
            item => item.EventType == "cancellation.unsupported");
        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "cancellation.ignored");
    }

    [Fact]
    public async Task RecognizableInvalidQueryConsumesDiagnosticAttempt()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(
                new QueryActorDecision(
                    Schema,
                    "actor-decision",
                    null,
                    [],
                    ResearchOperation.GetIncident,
                    TargetId.PaymentsApi,
                    new Dictionary<string, JsonElement>())));
        Assert.NotNull(actor);

        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2);

        Assert.Contains(
            coordinator.ActionHistory,
            item =>
                item.ActorTurnId == actor.ActorTurnId &&
                item.Disposition == "rejected" &&
                item.Reason?.Code == "out-of-scope-target");
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.requested");
        Assert.Equal(11u, coordinator.StartActor(
            2,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1)))!
            .Snapshot.RemainingBudgets.DiagnosticAttempts);
    }

    [Fact]
    public async Task RepeatedInterruptReplacesPendingTriggerAndStaleReviewCannotCommit()
    {
        var draining = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);

        var firstReview = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(firstReview);
        await coordinator.ProcessReviewResultAsync(firstReview.ReviewId, 7);
        var firstTrigger = coordinator.PendingTriggerId;

        var actor = coordinator.StartActor(
            7,
            async (_, _) => await draining.Task);
        Assert.NotNull(actor);
        var secondReview = coordinator.StartReview(
            8,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencySideQueueDelay)));
        Assert.NotNull(secondReview);
        await coordinator.ProcessReviewResultAsync(secondReview.ReviewId, 11);
        var secondTrigger = coordinator.PendingTriggerId;

        Assert.NotEqual(firstTrigger, secondTrigger);
        var stale = coordinator.StartReview(
            11,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.LocalInstanceIssue)));
        Assert.NotNull(stale);
        coordinator.ExpireDirection(12);
        draining.SetResult(Wait(1, Disposition(actor.Snapshot)));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 12);
        Assert.Equal(secondTrigger, coordinator.PendingTriggerId);

        await coordinator.ProcessReviewResultAsync(stale.ReviewId, 14);

        Assert.Equal("rejected", coordinator.MemoryUpdates[^1].Outcome);
        Assert.Contains(
            coordinator.MemoryUpdates[^1].Errors,
            reason => reason.Code == "memory-revision-conflict");
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task DiagnosticTraversesExistingVerifierPolicyAndGatewayBoundary()
    {
        var dispatcher = new GovernedWorkflowDiagnosticDispatcher();
        await using var coordinator = new ScriptedSupervisionCoordinator(
            Guid.NewGuid().ToString("D"),
            SupervisionArchitecture.ActorOnly,
            new ScriptedExperimentScheduler(),
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);

        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2);
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2);

        Assert.Equal(1, dispatcher.DispatchCount);
        Assert.True(dispatcher.AuditRecordCount > 0);
        var dispatch = Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.dispatched");
        Assert.Equal(
            64,
            dispatch.Data.GetProperty("binding").GetProperty("planDigest").GetString()!.Length);
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.completed");
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "result.duplicate-delivery");
    }

    [Fact]
    public async Task DispatchedDiagnosticSettlesAsPostTerminationProvenance()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseAfterAuthorization = true,
            CompletionTick = 24
        };
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        var processing = coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2).AsTask();
        await processing;
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);
        await dispatcher.Dispatched.Task;

        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The exclusive horizon was reached."));
        dispatcher.ReleaseCompletion.SetResult(true);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            24,
            ResearchPhase.Drain);

        Assert.Single(coordinator.Observations);
        var late = Assert.Single(coordinator.PostTerminationObservations);
        Assert.Equal("post-termination", late.Visibility);
        Assert.Null(late.ObservedTick);
        Assert.Null(late.HistoryRevision);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task AliasedBeliefReferenceMaterializesToExistingBelief()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var first = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(first);
        await coordinator.ProcessReviewResultAsync(first.ReviewId, 7);

        var second = coordinator.StartReview(
            8,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Alias an identical claim and use it in a new direction.",
                        Uncertainty.Medium,
                        [
                            new AddBeliefOperation(
                                "alias",
                                new Claim(
                                    Hypothesis.DependencyPathIssue,
                                    TargetId.AuthorizationService,
                                    [observationId],
                                    Uncertainty.Medium)),
                            new SetDirectionOperation(
                                new FocusDirectionChoice(
                                    new DiagnosticFocus(
                                        ResearchOperation.QueryMetrics,
                                        TargetId.AuthorizationService,
                                        new Dictionary<string, JsonElement>())),
                                [observationId],
                                [
                                    new ExistingBeliefRef("belief-1"),
                                    new ProposedBeliefRef("alias")
                                ])
                        ]));
            });
        Assert.NotNull(second);
        await coordinator.ProcessReviewResultAsync(second.ReviewId, 11);

        Assert.Single(coordinator.Beliefs);
        Assert.Equal(
            ["belief-1"],
            coordinator.WorkingMemory.RecommendedDirection!.SupportingBeliefIds);
        Assert.Contains(
            coordinator.MemoryUpdates[^1].KeyBindings,
            item => item.Key == "alias" && item.BeliefId == "belief-1");
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task CoalescedNewDirectionSupportsMaterializeAsCanonicalSet()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var review = coordinator.StartReview(
            4,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                var claim = new Claim(
                    Hypothesis.DependencyPathIssue,
                    TargetId.AuthorizationService,
                    [observationId],
                    Uncertainty.Medium);
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Coalesce equal new definitions used by one direction.",
                        Uncertainty.Medium,
                        [
                            new AddBeliefOperation("a", claim),
                            new AddBeliefOperation("b", claim),
                            new SetDirectionOperation(
                                new FocusDirectionChoice(
                                    new DiagnosticFocus(
                                        ResearchOperation.QueryLogs,
                                        TargetId.AuthorizationService,
                                        new Dictionary<string, JsonElement>())),
                                [observationId],
                                [
                                    new ProposedBeliefRef("a"),
                                    new ProposedBeliefRef("b")
                                ])
                        ]));
            });
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        Assert.Single(coordinator.Beliefs);
        Assert.Equal(
            ["belief-1"],
            coordinator.WorkingMemory.RecommendedDirection!.SupportingBeliefIds);
        Assert.Equal(
            2,
            coordinator.MemoryUpdates[^1].KeyBindings.Count(
                item => item.BeliefId == "belief-1"));
        ResearchContractValidator.ValidateAndThrow(coordinator.MemoryUpdates[^1]);
    }

    [Fact]
    public async Task PublishedSnapshotsAndProposalDataAreDeeplyDetached()
    {
        var operations = new List<MemoryOperation>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var review = coordinator.StartReview(
            4,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                operations.Add(
                    new AddBeliefOperation(
                        "candidate",
                        new Claim(
                            Hypothesis.DependencyPathIssue,
                            TargetId.AuthorizationService,
                            [observationId],
                            Uncertainty.Medium)));
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Mutable component-owned proposal data.",
                        Uncertainty.Medium,
                        operations));
            });
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);
        operations.Clear();

        var actor = coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(1, Disposition(snapshot))));
        Assert.NotNull(actor);
        var publishedStates =
            Assert.IsAssignableFrom<IList<BeliefStateEntry>>(
                actor.Snapshot.WorkingMemory.BeliefStates);
        Assert.Throws<NotSupportedException>(() => publishedStates.Clear());

        Assert.Single(coordinator.Beliefs);
        Assert.Single(coordinator.WorkingMemory.BeliefStates);
        Assert.Single(
            Assert.IsType<ProposeMemoryUpdateSupervisorOutput>(
                coordinator.MemoryUpdates[^1].Proposal)
            .Operations);
    }

    [Fact]
    public async Task EpochStaleActorCompletionRestoresCurrentReadiness()
    {
        var release = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var stale = coordinator.StartActor(
            9,
            async (_, _) => await release.Task);
        Assert.NotNull(stale);
        coordinator.ChangeEpoch(1, "external-recovery", 10);
        release.SetResult(Wait(1));
        await coordinator.ProcessActorResultAsync(stale.ActorTurnId, 10);

        Assert.NotNull(coordinator.StartActor(
            10,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
    }

    [Fact]
    public async Task ObsoleteDiagnosticRejectionDoesNotWakeReplacementWait()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true,
            CompletionTick = 9
        };
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            dispatcher);
        ObserveInitial(coordinator);
        var old = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(old);
        var oldProcessing =
            coordinator.ProcessActorResultAsync(old.ActorTurnId, 2).AsTask();
        await dispatcher.VerificationStarted.Task;

        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencySideQueueDelay)));
        Assert.NotNull(review);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);
        var replacement = coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(4, Disposition(snapshot))));
        Assert.NotNull(replacement);
        await coordinator.ProcessActorResultAsync(replacement.ActorTurnId, 8);

        dispatcher.ReleaseAuthorization.SetResult(true);
        await oldProcessing;
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            9,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            9,
            ResearchPhase.PriorDiagnosticResults);

        Assert.Null(coordinator.StartActor(
            9,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
        Assert.DoesNotContain(
            coordinator.Events,
            item =>
                item.EventType == "actor.wait-ended" &&
                item.Tick == 9);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task ObservationDuringWaitProducingTurnPreventsParking()
    {
        var release = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            3,
            async (_, _) => await release.Task);
        Assert.NotNull(actor);
        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        coordinator.RecordExternalObservation(
            Assert.Single(fixture.GetNotifications(4)),
            4);
        release.SetResult(Wait(4));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 4);

        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "actor.wait-ended" &&
                item.Data.GetProperty("reason").GetProperty("code").GetString() ==
                    "wake-already-observed");
        Assert.DoesNotContain(
            coordinator.Events,
            item =>
                item.EventType == "actor.wait-started" &&
                item.Data.GetProperty("actorTurnId").GetString() ==
                    actor.ActorTurnId);
        Assert.NotNull(coordinator.StartActor(
            4,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
    }

    [Theory]
    [InlineData(true, false, "kill_switch_active")]
    [InlineData(false, true, "verifier_unavailable")]
    public async Task RequiredGovernanceFailureTerminatesThroughActualBoundary(
        bool killSwitchActive,
        bool verifierUnavailable,
        string reasonCode)
    {
        var dispatcher = new GovernedWorkflowDiagnosticDispatcher
        {
            KillSwitchActive = killSwitchActive,
            VerifierUnavailable = verifierUnavailable
        };
        await using var coordinator = new ScriptedSupervisionCoordinator(
            Guid.NewGuid().ToString("D"),
            SupervisionArchitecture.ActorOnly,
            new ScriptedExperimentScheduler(),
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);

        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 2);
        var authorization = await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            2,
            ResearchPhase.ActorProcessing);

        Assert.Equal(
            DiagnosticAuthorizationProcessingOutcome
                .DiagnosticSettledWithoutAuthorization,
            authorization);
        Assert.Equal("governance-stop", coordinator.Termination!.Kind);
        Assert.Equal(reasonCode, coordinator.Termination.Reason!.Code);
        Assert.Null(coordinator.StartActor(
            2,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task TerminationCancelsPendingReviewAndFreezesTimeoutCallback()
    {
        var release = Signal<SupervisorOutput>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var review = coordinator.StartReview(
            12,
            async (_, token) => await release.Task.WaitAsync(token));
        Assert.NotNull(review);
        var actor = coordinator.StartActor(
            12,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                new ReportActorDecision(
                    Schema,
                    "actor-decision",
                    null,
                    [],
                    Hypothesis.Unresolved,
                    [Assert.Single(snapshot.Observations).ObservationId],
                    NextStep.HumanHandoff,
                    Uncertainty.High)));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 13);

        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "cancellation.requested" &&
                item.Data.GetProperty("invocation").GetProperty("consumer").GetString() ==
                    "supervisor");
        var eventCount = coordinator.Events.Count;
        var reviewHistoryCount = coordinator.ReviewHistory.Count;
        coordinator.ProcessReviewTimeout(review.ReviewId, 18);
        Assert.Equal(eventCount, coordinator.Events.Count);
        Assert.Equal(reviewHistoryCount, coordinator.ReviewHistory.Count);
        Assert.False(coordinator.SupervisionDegraded);

        await coordinator.ProcessReviewResultAsync(review.ReviewId, 18);
        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "review.timed-out");
        Assert.All(
            coordinator.Events.SkipWhile(item => item.EventType != "episode.terminated").Skip(1),
            item => Assert.Equal(ResearchPhase.Drain, item.Phase));
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination!));
    }

    [Fact]
    public async Task WaitNearExclusiveHorizonRemainsPendingWithoutFabricatedWake()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            22,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(4)));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 23);
        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The exclusive horizon was reached."));

        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "actor.wait-ended");
        Assert.Equal("timeout", coordinator.Termination!.Kind);
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task AcknowledgedTriggerRemainsInLaterActionableLineage()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var first = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(first);
        await coordinator.ProcessReviewResultAsync(first.ReviewId, 7);
        var firstTrigger = coordinator.PendingTriggerId;
        var acknowledgingActor = coordinator.StartActor(
            7,
            (snapshot, _) => ValueTask.FromResult<ActorDecision>(
                Wait(1, Disposition(snapshot))));
        Assert.NotNull(acknowledgingActor);
        await coordinator.ProcessActorResultAsync(acknowledgingActor.ActorTurnId, 8);
        Assert.Null(coordinator.PendingTriggerId);

        var second = coordinator.StartReview(
            8,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencySideQueueDelay)));
        Assert.NotNull(second);
        await coordinator.ProcessReviewResultAsync(second.ReviewId, 11);
        var nextActor = coordinator.StartActor(
            11,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1)));
        Assert.NotNull(nextActor);

        Assert.Equal(
            firstTrigger,
            nextActor.Snapshot.Reconsideration!.Trigger.PreviousTriggerId);
        Assert.Equal(
            [firstTrigger!],
            nextActor.Snapshot.Reconsideration.EarlierTriggerIds);
    }

    [Fact]
    public async Task ConflictOnlyTriggerIncludesBothChangedBeliefEndpoints()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var first = coordinator.StartReview(
            4,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Create two provisional beliefs.",
                        Uncertainty.Medium,
                        [
                            new AddBeliefOperation(
                                "left",
                                new Claim(
                                    Hypothesis.DependencyPathIssue,
                                    TargetId.AuthorizationService,
                                    [observationId],
                                    Uncertainty.Medium)),
                            new AddBeliefOperation(
                                "right",
                                new Claim(
                                    Hypothesis.LocalInstanceIssue,
                                    TargetId.AuthorizationService,
                                    [observationId],
                                    Uncertainty.Medium))
                        ]));
            });
        Assert.NotNull(first);
        await coordinator.ProcessReviewResultAsync(first.ReviewId, 7);

        var conflict = coordinator.StartReview(
            8,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Declare the two beliefs in conflict.",
                        Uncertainty.Medium,
                        [
                            new AddBeliefOperation(
                                "left-alias",
                                new Claim(
                                    Hypothesis.DependencyPathIssue,
                                    TargetId.AuthorizationService,
                                    [observationId],
                                    Uncertainty.Medium)),
                            new AddBeliefOperation(
                                "right-alias",
                                new Claim(
                                    Hypothesis.LocalInstanceIssue,
                                    TargetId.AuthorizationService,
                                    [observationId],
                                    Uncertainty.Medium)),
                            new DeclareConflictOperation(
                                [
                                    new ProposedBeliefRef("left-alias"),
                                    new ProposedBeliefRef("right-alias")
                                ],
                                [observationId],
                                "The claims conflict.")
                        ]));
            });
        Assert.NotNull(conflict);
        await coordinator.ProcessReviewResultAsync(conflict.ReviewId, 11);
        var actor = coordinator.StartActor(
            11,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1)));
        Assert.NotNull(actor);

        var effects = actor.Snapshot.Reconsideration!.EffectsAtCapture
            .OfType<BeliefTriggerEffectView>()
            .ToDictionary(item => item.BeliefId, item => item.State);
        Assert.Equal(BeliefState.Contested, effects["belief-1"]);
        Assert.Equal(BeliefState.Contested, effects["belief-2"]);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task ConflictEffectsCanonicalizeAfterBeliefNineAndTenAllocation()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var seed = coordinator.StartReview(
            4,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Create eight distinct beliefs before the identifier boundary.",
                        Uncertainty.Medium,
                        Enumerable.Range(0, 8)
                            .Select(index => (MemoryOperation)new AddBeliefOperation(
                                $"seed-{index:D2}",
                                IndexedClaim(index, observationId)))
                            .ToArray()));
            });
        Assert.NotNull(seed);
        await coordinator.ProcessReviewResultAsync(seed.ReviewId, 7);
        Assert.Equal(8, coordinator.Beliefs.Count);

        var conflict = coordinator.StartReview(
            8,
            (snapshot, _) =>
            {
                var observationId = Assert.Single(snapshot.Observations).ObservationId;
                return ValueTask.FromResult<SupervisorOutput>(
                    new ProposeMemoryUpdateSupervisorOutput(
                        Schema,
                        "supervisor-output",
                        snapshot.MemoryRevision,
                        [observationId],
                        "Create two beliefs and declare their conflict.",
                        Uncertainty.Medium,
                        [
                            new AddBeliefOperation("a", IndexedClaim(8, observationId)),
                            new AddBeliefOperation("b", IndexedClaim(9, observationId)),
                            new DeclareConflictOperation(
                                [
                                    new ProposedBeliefRef("a"),
                                    new ProposedBeliefRef("b")
                                ],
                                [observationId],
                                "The claims conflict.")
                        ]));
            });
        Assert.NotNull(conflict);
        await coordinator.ProcessReviewResultAsync(conflict.ReviewId, 11);

        var edge = Assert.Single(
            coordinator.MemoryUpdates[^1].Effects!.AddedConflicts);
        Assert.Equal("belief-10", edge.Left);
        Assert.Equal("belief-9", edge.Right);
        ResearchContractValidator.ValidateAndThrow(coordinator.MemoryUpdates[^1]);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task EpochChangeReleasesPendingAuthorizationReadinessOwner()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true,
            CompletionTick = 10
        };
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            8,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 9);
        await dispatcher.VerificationStarted.Task;

        coordinator.ChangeEpoch(1, "external-recovery", 10);
        dispatcher.ReleaseAuthorization.SetResult(true);
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            10,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            10,
            ResearchPhase.PriorDiagnosticResults);

        Assert.Equal(0, dispatcher.DispatchCount);
        Assert.NotNull(coordinator.StartActor(
            10,
            (_, _) => ValueTask.FromResult<ActorDecision>(Wait(1))));
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task ThrowingCancellationCallbackRecordsFailedInterruption()
    {
        var registered = Signal<bool>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, token) =>
            {
                token.Register(
                    () => throw new InvalidOperationException(
                        "scripted cancellation callback failure"));
                registered.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Wait(1);
            });
        Assert.NotNull(actor);
        await registered.Task;
        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                Proposal(snapshot, Hypothesis.DependencyPathIssue)));
        Assert.NotNull(review);

        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 7);

        Assert.Contains(
            coordinator.Events,
            item =>
                item.EventType == "cancellation.failed" &&
                item.Data.GetProperty("invocation").GetProperty("consumer").GetString() ==
                    "actor" &&
                item.Data.GetProperty("reason").GetProperty("code").GetString() ==
                    "cancellation-request-failed");
        Assert.Contains(
            coordinator.Events,
            item => item.EventType == "result.suppressed");
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task TerminationContinuesAfterMultipleThrowingCancellationCallbacks()
    {
        var actorRegistered = Signal<bool>();
        var reviewRegistered = Signal<bool>();
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, token) =>
            {
                token.Register(
                    () => throw new InvalidOperationException("actor cancellation failure"));
                actorRegistered.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return Wait(1);
            });
        var review = coordinator.StartReview(
            4,
            async (_, token) =>
            {
                token.Register(
                    () => throw new InvalidOperationException("review cancellation failure"));
                reviewRegistered.SetResult(true);
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                return NoChange(0);
            });
        Assert.NotNull(actor);
        Assert.NotNull(review);
        await Task.WhenAll(actorRegistered.Task, reviewRegistered.Task);

        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The scripted episode ended."));
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 24);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 24);

        Assert.Equal(
            2,
            coordinator.Events.Count(item => item.EventType == "cancellation.failed"));
        Assert.Equal("timeout", coordinator.Termination!.Kind);
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task TerminalRecordsExposeImmutablePendingCollections()
    {
        var actorRelease = Signal<ActorDecision>();
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            1,
            async (_, _) => await actorRelease.Task,
            InvocationCancellationCapability.Unsupported);
        Assert.NotNull(actor);
        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The scripted episode ended."));

        var termination = coordinator.Termination!;
        var pending =
            Assert.IsAssignableFrom<IList<InvocationRef>>(
                termination.PendingInvocations);
        Assert.Throws<NotSupportedException>(() => pending.Clear());
        Assert.Single(coordinator.Termination!.PendingInvocations);
        ResearchContractValidator.ValidateAndThrow(coordinator.Termination!);

        var closure = coordinator.Close();
        Assert.Equal("incomplete", closure.Status);
        var unsettled =
            Assert.IsAssignableFrom<IList<InvocationRef>>(
                closure.UnsettledInvocations);
        Assert.Throws<NotSupportedException>(() => unsettled.Clear());
        Assert.Single(coordinator.Close().UnsettledInvocations);
        ResearchContractValidator.ValidateAndThrow(coordinator.Close());
    }

    [Fact]
    public async Task SchedulerAdvancesWhileDispatchedDiagnosticRemainsPending()
    {
        var scheduler = new ScriptedExperimentScheduler();
        var dispatcher = new ControlledDispatcher
        {
            PauseAfterAuthorization = true,
            CompletionTick = 5
        };
        await using var coordinator = new ScriptedSupervisionCoordinator(
            Guid.NewGuid().ToString("D"),
            SupervisionArchitecture.AsynchronousSupervision,
            scheduler,
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);
        ObserveInitial(coordinator);
        CoordinatorActorStart? actor = null;
        CoordinatorReviewStart? review = null;
        scheduler.Schedule(
            0,
            ResearchPhase.ReviewStart,
            "start-review",
            _ =>
            {
                review = coordinator.StartReview(
                    0,
                    (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                        Proposal(snapshot, Hypothesis.DependencyPathIssue)));
                Assert.NotNull(review);
            });
        scheduler.Schedule(
            0,
            ResearchPhase.ActorStart,
            "start-actor",
            _ =>
            {
                actor = coordinator.StartActor(
                    0,
                    (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
                Assert.NotNull(actor);
            });
        scheduler.ScheduleAsync(
            1,
            ResearchPhase.ActorProcessing,
            "process-query",
            async (_, token) =>
                await coordinator.ProcessActorResultAsync(
                    actor!.ActorTurnId,
                    1,
                    token));
        scheduler.ScheduleAsync(
            1,
            ResearchPhase.ActorProcessing,
            "authorize-query",
            async (_, token) =>
                await coordinator.ProcessDiagnosticAuthorizationAsync(
                    "diagnostic-1",
                    1,
                    ResearchPhase.ActorProcessing,
                    token));
        scheduler.ScheduleAsync(
            3,
            ResearchPhase.ReviewProcessing,
            "accept-interrupt",
            async (_, token) =>
                await coordinator.ProcessReviewResultAsync(
                    review!.ReviewId,
                    3,
                    token));
        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        scheduler.Schedule(
            4,
            ResearchPhase.WorldDelivery,
            "deliver-notification",
            _ => coordinator.RecordExternalObservation(
                Assert.Single(fixture.GetNotifications(4)),
                4));
        scheduler.ScheduleAsync(
            5,
            ResearchPhase.PriorDiagnosticResults,
            "deliver-diagnostic",
            async (_, token) =>
            {
                dispatcher.ReleaseCompletion.SetResult(true);
                await coordinator.ProcessDiagnosticResultAsync(
                    "diagnostic-1",
                    5,
                    ResearchPhase.PriorDiagnosticResults,
                    token);
            });
        scheduler.Schedule(
            24,
            ResearchPhase.EndCheck,
            "terminate-horizon",
            _ => coordinator.Terminate(
                24,
                ResearchPhase.EndCheck,
                "timeout",
                new Reason(
                    ReasonDomain.Context,
                    "horizon-reached",
                    "The exclusive horizon was reached.")));

        var result = await scheduler.RunAsync();

        Assert.True(result.HorizonReached);
        Assert.Equal(
            [
                "start-review",
                "start-actor",
                "process-query",
                "authorize-query",
                "accept-interrupt",
                "deliver-notification",
                "deliver-diagnostic",
                "terminate-horizon"
            ],
            result.Trace.Select(item => item.ActionId));
        Assert.Equal(3, coordinator.Observations.Count);
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.completed");
        Assert.Equal("timeout", coordinator.Termination!.Kind);
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task DelayedAuthorizationUsesSchedulerDispatchCoordinateAndSample()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true
        };
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            0,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 1);
        await dispatcher.VerificationStarted.Task;

        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        coordinator.RecordExternalObservation(
            Assert.Single(fixture.GetNotifications(4)),
            4);
        dispatcher.ReleaseAuthorization.SetResult(true);
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            5,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            5,
            ResearchPhase.PriorDiagnosticResults);

        var dispatch = Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.dispatched");
        Assert.Equal(5u, dispatch.Tick);
        Assert.Equal(ResearchPhase.PriorDiagnosticResults, dispatch.Phase);
        var observation = coordinator.Observations[^1];
        var expected = fixture.SampleDiagnostic(
            ResearchOperation.QueryMetrics,
            TargetId.PaymentsApi,
            5);
        Assert.Equal(
            JsonSerializer.Serialize(expected.Content),
            JsonSerializer.Serialize(observation.Content));
        Assert.Equal(expected.EvidenceIds, observation.EvidenceIds);
        Assert.Equal(
            [0u, 4u, 5u, 5u, 5u],
            coordinator.Events
                .Where(item => item.EventType is
                    "observation.recorded" or
                    "diagnostic.dispatched" or
                    "diagnostic.completed")
                .Select(item => item.Tick!.Value));
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task DuplicateDiagnosticDeliveryKeepsPriorResultsPhaseBeforeReview()
    {
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            new ControlledDispatcher());
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            0,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 1);
        await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            1,
            ResearchPhase.ActorProcessing);
        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                NoChange(snapshot.MemoryRevision)));
        Assert.NotNull(review);

        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            7,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            7,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        var tickSeven = coordinator.Events
            .Where(item => item.Tick == 7)
            .Select(item => (item.EventType, item.Phase))
            .ToArray();
        Assert.Contains(
            ("diagnostic.completed", ResearchPhase.PriorDiagnosticResults),
            tickSeven);
        Assert.Contains(
            ("diagnostic.duplicate-delivery", ResearchPhase.PriorDiagnosticResults),
            tickSeven);
        Assert.Contains(
            ("review.completed", ResearchPhase.ReviewProcessing),
            tickSeven);
        Assert.Equal(
            [
                ResearchPhase.PriorDiagnosticResults,
                ResearchPhase.PriorDiagnosticResults,
                ResearchPhase.PriorDiagnosticResults,
                ResearchPhase.ReviewProcessing,
                ResearchPhase.ReviewProcessing
            ],
            tickSeven.Select(item => item.Phase));
        Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.completed");
        Assert.Single(
            coordinator.Observations,
            item => item.DiagnosticId == "diagnostic-1");
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task HorizonReleasesPendingAuthorizationWithoutDispatch()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true
        };
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            0,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 1);
        await dispatcher.VerificationStarted.Task;

        coordinator.Terminate(
            24,
            ResearchPhase.EndCheck,
            "timeout",
            new Reason(
                ReasonDomain.Context,
                "horizon-reached",
                "The exclusive horizon was reached."));
        var authorization = await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            24,
            ResearchPhase.Drain);
        dispatcher.ReleaseAuthorization.SetResult(true);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            24,
            ResearchPhase.Drain);

        Assert.Equal(
            DiagnosticAuthorizationProcessingOutcome.EpisodeTerminated,
            authorization);
        Assert.Equal(0, dispatcher.DispatchCount);
        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "diagnostic.dispatched");
        Assert.Empty(
            coordinator.PostTerminationObservations);
        Assert.Equal("timeout", coordinator.Termination!.Kind);
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task VerifierFailureReleasesScheduledAuthorizationBeforeResult()
    {
        var scheduler = new ScriptedExperimentScheduler();
        var dispatcher = new GovernedWorkflowDiagnosticDispatcher
        {
            VerifierUnavailable = true
        };
        await using var coordinator = new ScriptedSupervisionCoordinator(
            Guid.NewGuid().ToString("D"),
            SupervisionArchitecture.ActorOnly,
            scheduler,
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);
        ObserveInitial(coordinator);
        CoordinatorActorStart? actor = null;
        DiagnosticAuthorizationProcessingOutcome? authorization = null;
        scheduler.Schedule(
            0,
            ResearchPhase.ActorStart,
            "start-actor",
            _ =>
            {
                actor = coordinator.StartActor(
                    0,
                    (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
                Assert.NotNull(actor);
            });
        scheduler.ScheduleAsync(
            1,
            ResearchPhase.ActorProcessing,
            "process-query",
            async (_, token) =>
                await coordinator.ProcessActorResultAsync(
                    actor!.ActorTurnId,
                    1,
                    token));
        scheduler.ScheduleAsync(
            2,
            ResearchPhase.PriorDiagnosticResults,
            "process-authorization",
            async (_, token) =>
                authorization =
                    await coordinator.ProcessDiagnosticAuthorizationAsync(
                        "diagnostic-1",
                        2,
                        ResearchPhase.PriorDiagnosticResults,
                        token));
        scheduler.ScheduleAsync(
            2,
            ResearchPhase.PriorDiagnosticResults,
            "process-failure",
            async (_, token) =>
                await coordinator.ProcessDiagnosticResultAsync(
                    "diagnostic-1",
                    2,
                    ResearchPhase.PriorDiagnosticResults,
                    token));
        scheduler.Schedule(
            24,
            ResearchPhase.EndCheck,
            "horizon-observed",
            _ => { });

        var run = await scheduler.RunAsync();

        Assert.Equal(
            DiagnosticAuthorizationProcessingOutcome
                .DiagnosticSettledWithoutAuthorization,
            authorization);
        Assert.Equal("governance-stop", coordinator.Termination!.Kind);
        Assert.Equal("verifier_unavailable", coordinator.Termination.Reason!.Code);
        Assert.Equal("horizon-observed", run.Trace[^1].ActionId);
        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "diagnostic.dispatched");
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    [Fact]
    public async Task StaleDiagnosticSuppressionRetainsPriorResultsPhase()
    {
        var dispatcher = new ControlledDispatcher
        {
            PauseBeforeAuthorization = true
        };
        await using var coordinator = Create(
            SupervisionArchitecture.AsynchronousSupervision,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            0,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 1);
        await dispatcher.VerificationStarted.Task;
        var review = coordinator.StartReview(
            4,
            (snapshot, _) => ValueTask.FromResult<SupervisorOutput>(
                NoChange(snapshot.MemoryRevision)));
        Assert.NotNull(review);
        coordinator.ChangeEpoch(1, "external-recovery", 5);
        dispatcher.ReleaseAuthorization.SetResult(true);
        var authorization = await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            7,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            7,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessReviewResultAsync(review.ReviewId, 7);

        Assert.Equal(
            DiagnosticAuthorizationProcessingOutcome.Rejected,
            authorization);
        var rejected = Assert.Single(
            coordinator.Events,
            item => item.EventType == "diagnostic.rejected");
        var suppressed = Assert.Single(
            coordinator.Events,
            item =>
                item.EventType == "result.suppressed" &&
                item.Data.GetProperty("invocation").GetProperty("consumer").GetString() ==
                    "actor");
        var reviewCompleted = Assert.Single(
            coordinator.Events,
            item => item.EventType == "review.completed");
        Assert.Equal(ResearchPhase.PriorDiagnosticResults, rejected.Phase);
        Assert.Equal(ResearchPhase.PriorDiagnosticResults, suppressed.Phase);
        Assert.Equal(ResearchPhase.ReviewProcessing, reviewCompleted.Phase);
        Assert.Empty(ResearchEventSequenceValidator.Validate(coordinator.Events));
    }

    [Fact]
    public async Task ThrownDiagnosticFailureReleasesAuthorizationWaiter()
    {
        var dispatcher = new ControlledDispatcher
        {
            ThrowBeforeAuthorization = true
        };
        await using var coordinator = Create(
            SupervisionArchitecture.ActorOnly,
            dispatcher);
        ObserveInitial(coordinator);
        var actor = coordinator.StartActor(
            0,
            (_, _) => ValueTask.FromResult<ActorDecision>(QueryMetrics()));
        Assert.NotNull(actor);
        await coordinator.ProcessActorResultAsync(actor.ActorTurnId, 1);

        var authorization = await coordinator.ProcessDiagnosticAuthorizationAsync(
            "diagnostic-1",
            2,
            ResearchPhase.PriorDiagnosticResults);
        await coordinator.ProcessDiagnosticResultAsync(
            "diagnostic-1",
            2,
            ResearchPhase.PriorDiagnosticResults);

        Assert.Equal(
            DiagnosticAuthorizationProcessingOutcome
                .DiagnosticSettledWithoutAuthorization,
            authorization);
        Assert.Equal("infrastructure-failure", coordinator.Termination!.Kind);
        Assert.DoesNotContain(
            coordinator.Events,
            item => item.EventType == "diagnostic.dispatched");
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                coordinator.Events,
                coordinator.Termination));
    }

    private static ScriptedSupervisionCoordinator Create(
        SupervisionArchitecture architecture,
        ControlledDispatcher dispatcher) =>
        new(
            Guid.NewGuid().ToString("D"),
            architecture,
            new ScriptedExperimentScheduler(),
            new IsolatedAgentFrameworkRuntime(),
            dispatcher);

    private static void ObserveInitial(ScriptedSupervisionCoordinator coordinator)
    {
        var fixture = new Er1EvidenceFixture(Er1FixtureOptions.Straightforward);
        coordinator.RecordExternalObservation(Assert.Single(fixture.GetNotifications(0)), 0);
    }

    private static QueryActorDecision QueryMetrics(
        MemoryDisposition? disposition = null) =>
        new(
            Schema,
            "actor-decision",
            disposition,
            [],
            ResearchOperation.QueryMetrics,
            TargetId.PaymentsApi,
            new Dictionary<string, JsonElement>());

    private static WaitActorDecision Wait(
        uint ticks,
        MemoryDisposition? disposition = null) =>
        new(
            Schema,
            "actor-decision",
            disposition,
            [],
            ticks);

    private static MemoryDisposition Disposition(InputSnapshot snapshot)
    {
        Assert.NotNull(snapshot.Reconsideration);
        var trigger = snapshot.Reconsideration.Trigger;
        return new MemoryDisposition(
            trigger.TriggerId,
            trigger.MemoryUpdateId,
            Stance.Adapt,
            DispositionReason.CombineWithObservedEvidence);
    }

    private static NoChangeSupervisorOutput NoChange(uint revision) =>
        new(
            Schema,
            "supervisor-output",
            revision,
            [],
            "No actionable change.",
            Uncertainty.Medium);

    private static ProposeMemoryUpdateSupervisorOutput Proposal(
        InputSnapshot snapshot,
        Hypothesis hypothesis)
    {
        var observationId = Assert.Single(snapshot.Observations).ObservationId;
        return new ProposeMemoryUpdateSupervisorOutput(
            Schema,
            "supervisor-output",
            snapshot.MemoryRevision,
            [observationId],
            "Scripted, structurally valid supervisory advice.",
            Uncertainty.Medium,
            [
                new AddBeliefOperation(
                    "candidate",
                    new Claim(
                        hypothesis,
                        TargetId.AuthorizationService,
                        [observationId],
                        Uncertainty.Medium)),
                new SetDirectionOperation(
                    new FocusDirectionChoice(
                        new DiagnosticFocus(
                            ResearchOperation.QueryLogs,
                            TargetId.AuthorizationService,
                            new Dictionary<string, JsonElement>())),
                    [observationId],
                    [new ProposedBeliefRef("candidate")])
            ]);
    }

    private static Claim IndexedClaim(int index, string observationId)
    {
        var hypotheses = new[]
        {
            Hypothesis.LocalInstanceIssue,
            Hypothesis.DependencyPathIssue,
            Hypothesis.DependencySideQueueDelay,
            Hypothesis.NoCurrentlyActiveIncident,
            Hypothesis.Unresolved
        };
        var uncertainties = new[]
        {
            Uncertainty.Low,
            Uncertainty.Medium,
            Uncertainty.High
        };
        return new Claim(
            hypotheses[index % hypotheses.Length],
            TargetId.AuthorizationService,
            [observationId],
            uncertainties[index / hypotheses.Length]);
    }

    private static TaskCompletionSource<T> Signal<T>() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private sealed class ControlledDispatcher : IGovernedDiagnosticDispatcher
    {
        private readonly Er1EvidenceFixture _fixture =
            new(Er1FixtureOptions.Straightforward);

        public bool PauseBeforeAuthorization { get; init; }
        public bool PauseAfterAuthorization { get; init; }
        public bool ThrowBeforeAuthorization { get; init; }
        public uint? CompletionTick { get; init; }
        public int DispatchCount { get; private set; }
        public TaskCompletionSource<bool> VerificationStarted { get; } = Signal<bool>();
        public TaskCompletionSource<bool> ReleaseAuthorization { get; } = Signal<bool>();
        public TaskCompletionSource<bool> Dispatched { get; } = Signal<bool>();
        public TaskCompletionSource<bool> ReleaseCompletion { get; } = Signal<bool>();

        public async ValueTask<GovernedDiagnosticResult> DispatchAsync(
            GovernedDiagnosticRequest request,
            Func<
                GovernedDiagnosticBinding,
                CancellationToken,
                ValueTask<GovernedDiagnosticAuthorization>> authorizeDispatch,
            CancellationToken cancellationToken)
        {
            VerificationStarted.TrySetResult(true);
            if (ThrowBeforeAuthorization)
            {
                throw new InvalidOperationException(
                    "Scripted diagnostic failure before authorization.");
            }

            if (PauseBeforeAuthorization)
            {
                await ReleaseAuthorization.Task.WaitAsync(cancellationToken);
            }

            var binding = new GovernedDiagnosticBinding(
                Guid.NewGuid(),
                request.DiagnosticId,
                new string('a', 64),
                new string('b', 64),
                Guid.NewGuid(),
                "t3-scripted-session");
            var authorization = await authorizeDispatch(binding, cancellationToken);
            if (!authorization.Authorized)
            {
                return new GovernedDiagnosticResult(
                    "rejected",
                    CompletionTick ?? authorization.DispatchTick,
                    null,
                    binding,
                    [],
                    new Reason(
                        ReasonDomain.Context,
                        "obsolete-generation",
                        "The actor generation changed before dispatch."));
            }

            DispatchCount++;
            Dispatched.TrySetResult(true);
            var sample = _fixture.SampleDiagnostic(
                request.Decision.Operation,
                request.Decision.TargetId,
                authorization.DispatchTick);
            if (PauseAfterAuthorization)
            {
                await ReleaseCompletion.Task.WaitAsync(cancellationToken);
            }

            return new GovernedDiagnosticResult(
                "completed",
                CompletionTick ?? authorization.DispatchTick,
                sample,
                binding,
                [Guid.NewGuid()],
                null);
        }
    }
}
