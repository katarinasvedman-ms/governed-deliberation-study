using System.Text.Json;
using GovernedAgent.Research;

namespace GovernedAgent.UnitTests;

public sealed class ResearchContractTests
{
    private const string RunId = "8f816ca2-0d21-4eb3-9a41-ec1af43c4181";

    [Fact]
    public void ApprovedReassertionExampleDeserializes()
    {
        const string json = """
            {
              "schemaVersion": "1.0-candidate.3",
              "recordType": "supervisor-output",
              "kind": "propose-memory-update",
              "baseMemoryRevision": 3,
              "observationIds": ["observation-3"],
              "rationale": "Current dependency evidence again supports the retracted hypothesis.",
              "uncertainty": "medium",
              "operations": [
                {
                  "kind": "reassert-belief",
                  "predecessorBeliefId": "belief-1",
                  "key": "reasserted-dependency-path",
                  "claim": {
                    "hypothesis": "dependency-path-issue",
                    "targetId": "authorization-service",
                    "observationIds": ["observation-3"],
                    "uncertainty": "medium"
                  },
                  "reason": "Reassert after retraction using the current captured evidence."
                }
              ]
            }
            """;

        var root = ResearchContractSerializer.DeserializeRoot(json);
        var proposal = Assert.IsType<ProposeMemoryUpdateSupervisorOutput>(root);
        var operation = Assert.IsType<ReassertBeliefOperation>(
            Assert.Single(proposal.Operations));

        Assert.Equal("belief-1", operation.PredecessorBeliefId);
        Assert.Contains(
            "\"schemaVersion\":\"1.0-candidate.3\"",
            ResearchContractSerializer.Serialize(proposal),
            StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(InvalidActorExamples))]
    public void InvalidActorExamplesAreRejected(string json, string expectedCode)
    {
        var exception = Assert.Throws<ResearchContractException>(
            () => ResearchContractSerializer.DeserializeRoot(json));

        Assert.Equal(expectedCode, exception.Code);
    }

    public static TheoryData<string, string> InvalidActorExamples =>
        new()
        {
            {
                """
                {
                """,
                "invalid-json"
            },
            {
                """
                {
                  "schemaVersion": "1.0",
                  "recordType": "actor-decision",
                  "kind": "wait",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "ticks": 2
                }
                """,
                "unsupported-schema"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "wait",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "ticks": 0
                }
                """,
                "out-of-range"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "query",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "operation": "get_incident",
                  "targetId": "payments-api",
                  "arguments": {}
                }
                """,
                "out-of-scope-target"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "wait",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "ticks": 2,
                  "modelSuppliedAuthorization": true
                }
                """,
                "invalid-shape"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "wait",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "ticks": 2,
                  "ticks": 3
                }
                """,
                "duplicate-property"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "report",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "hypothesis": 1,
                  "observationIds": [],
                  "nextStep": "human-handoff",
                  "uncertainty": "high"
                }
                """,
                "invalid-shape"
            },
            {
                """
                {
                  "schemaVersion": "1.0-candidate.3",
                  "recordType": "actor-decision",
                  "kind": "report",
                  "memoryDisposition": null,
                  "usedBeliefIds": [],
                  "hypothesis": "Dependency-Path-Issue",
                  "observationIds": [],
                  "nextStep": "human-handoff",
                  "uncertainty": "high"
                }
                """,
                "invalid-shape"
            }
        };

    [Fact]
    public void FreshCurrentBaseReviewMayReassertRetractedBelief()
    {
        var context = Context(RetractedBelief());
        var result = MemoryProposalValidator.Validate(ReassertionProposal(3), context);

        Assert.True(result.IsValid);
        Assert.False(result.IsNoOp);
        var created = Assert.Single(result.CreatedBeliefs);
        Assert.Equal("belief-1", created.ReassertedFromBeliefId);
        Assert.Equal(
            new PlannedBeliefLink("belief-1", "proposed:reasserted-dependency-path"),
            Assert.Single(result.Reassertions));
    }

    [Fact]
    public void FreshProposalOnStaleBaseIsRejectedBeforeNormalizationCanAuthorizeIt()
    {
        var context = Context(RetractedBelief()) with
        {
            CapturedMemoryRevision = 2,
            CurrentMemoryRevision = 3
        };

        var result = MemoryProposalValidator.Validate(ReassertionProposal(2), context);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "memory-revision-conflict");
        Assert.Empty(result.Normalization);
        Assert.Empty(result.CreatedBeliefs);
    }

    [Fact]
    public void IdenticalEligibleBeliefAliasesWithoutArtificialChange()
    {
        var eligible = EligibleBelief("belief-2");
        var context = Context(RetractedBelief(), eligible);
        var result = MemoryProposalValidator.Validate(ReassertionProposal(3), context);

        Assert.True(result.IsValid);
        Assert.True(result.IsNoOp);
        Assert.Empty(result.CreatedBeliefs);
        Assert.Empty(result.Reassertions);
        Assert.Contains(
            result.Normalization,
            note =>
                note.Rule == "alias-existing-claim" &&
                note.ExistingBeliefId == "belief-2");
    }

    [Fact]
    public void PlainAddCannotRestoreExactTerminalClaim()
    {
        var proposal = new ProposeMemoryUpdateSupervisorOutput(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            3,
            ["observation-3"],
            "Restore the prior claim.",
            Uncertainty.Medium,
            [
                new AddBeliefOperation(
                    "restored",
                    DependencyClaim())
            ]);

        var result = MemoryProposalValidator.Validate(
            proposal,
            Context(RetractedBelief()));

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "terminal-belief-lineage-required");
    }

    [Fact]
    public void NonlatestReassertionPredecessorIsRejected()
    {
        var older = RetractedBelief();
        var newer = RetractedBelief("belief-2", stateRevision: 3);
        var proposal = ReassertionProposal(3);

        var result = MemoryProposalValidator.Validate(
            proposal,
            Context(older, newer));

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "invalid-reassertion-lineage");
    }

    [Fact]
    public void WholeSuccessorRejectsRetainedDirectionWithRetiredSupport()
    {
        var eligible = EligibleBelief("belief-1");
        var direction = new Direction(
            ResearchContractVersions.SchemaVersion,
            "direction",
            RunId,
            "direction-1",
            "memory-update-1",
            "review-1",
            8,
            16,
            0,
            new FocusDirectionChoice(new TargetFocus(TargetId.AuthorizationService)),
            ["observation-3"],
            ["belief-1"]);
        var proposal = new ProposeMemoryUpdateSupervisorOutput(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            3,
            ["observation-3"],
            "Replace the supported belief.",
            Uncertainty.Medium,
            [
                new ReplaceBeliefOperation(
                    "belief-1",
                    "replacement",
                    new Claim(
                        Hypothesis.DependencySideQueueDelay,
                        TargetId.AuthorizationService,
                        ["observation-3"],
                        Uncertainty.Medium))
            ]);
        var context = Context(eligible) with
        {
            ActiveDirection = direction,
            HistoricalDirections = [direction]
        };

        var result = MemoryProposalValidator.Validate(proposal, context);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "invalid-direction-dependency");
    }

    [Fact]
    public void RedeclaringExistingConflictIsNoOp()
    {
        var left = EligibleBelief("belief-1") with
        {
            State = new BeliefStateEntry(
                "belief-1",
                BeliefState.Contested,
                "memory-update-2",
                ["belief-2"],
                null)
        };
        var right = EligibleBelief("belief-2") with
        {
            State = new BeliefStateEntry(
                "belief-2",
                BeliefState.Contested,
                "memory-update-2",
                ["belief-1"],
                null)
        };
        var proposal = new ProposeMemoryUpdateSupervisorOutput(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            3,
            ["observation-3"],
            "Retain the declared disagreement.",
            Uncertainty.Medium,
            [
                new DeclareConflictOperation(
                    [
                        new ExistingBeliefRef("belief-1"),
                        new ExistingBeliefRef("belief-2")
                    ],
                    ["observation-3"],
                    "Both claims remain explicitly contested.")
            ]);

        var result = MemoryProposalValidator.Validate(proposal, Context(left, right));

        Assert.True(result.IsValid);
        Assert.True(result.IsNoOp);
        Assert.Empty(result.AddedConflicts);
        Assert.Contains(
            result.Normalization,
            note => note.Rule == "drop-existing-conflict");
    }

    [Fact]
    public void ReassertionEventHasClosedMeasuredPayload()
    {
        const string json = """
            {
              "schemaVersion": "1.0-candidate.3",
              "recordType": "research-event",
              "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
              "sequence": 19,
              "tick": 12,
              "phase": "review-processing",
              "eventType": "belief.reasserted",
              "causedBySequences": [17, 18],
              "currentGeneration": 3,
              "currentMemoryRevision": 4,
              "historyRevision": 3,
              "applicabilityEpoch": 0,
              "data": {
                "memoryUpdateId": "memory-update-4",
                "predecessorBeliefId": "belief-1",
                "beliefId": "belief-2",
                "triggerId": "trigger-3"
              }
            }
            """;

        var record = Assert.IsType<ResearchEvent>(
            ResearchContractSerializer.DeserializeRoot(json));

        Assert.Equal("belief.reasserted", record.EventType);
    }

    [Fact]
    public void InvocationResultRetainsConsumedRevisionAndTypedOutput()
    {
        const string json = """
            {
              "schemaVersion": "1.0-candidate.3",
              "recordType": "invocation-result",
              "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
              "resultId": "result-10",
              "consumer": "actor",
              "actorTurnId": "actor-7",
              "reviewId": null,
              "snapshotId": "snapshot-8",
              "consumedMemoryRevision": 2,
              "capturedGeneration": 2,
              "capturedApplicabilityEpoch": 0,
              "settledTick": 12,
              "settlement": "returned",
              "parseStatus": "well-formed",
              "output": {
                "schemaVersion": "1.0-candidate.3",
                "recordType": "actor-decision",
                "kind": "wait",
                "memoryDisposition": null,
                "usedBeliefIds": ["belief-2"],
                "ticks": 2
              },
              "rawOutputSha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
              "redactedOutput": "{}",
              "reason": null
            }
            """;

        var result = Assert.IsType<InvocationResult>(
            ResearchContractSerializer.DeserializeRoot(json));

        Assert.Equal(2u, result.ConsumedMemoryRevision);
        Assert.Equal("actor-decision", result.Output?.GetProperty("recordType").GetString());
    }

    [Fact]
    public void MemoryUpdateRequiresAtomicReassertionTrigger()
    {
        var update = new MemoryUpdate(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            RunId,
            "memory-update-4",
            new SupervisorUpdateOrigin("review-4", "result-9", "snapshot-8"),
            3,
            3,
            4,
            "committed",
            true,
            2,
            3,
            "trigger-3",
            ReassertionProposal(3),
            [new NormalizationNote(0, "unchanged", null, null, null)],
            [new KeyBinding("reasserted-dependency-path", "belief-2")],
            new MemoryEffects(
                ["belief-2"],
                [],
                [new BeliefLink("belief-1", "belief-2")],
                [],
                [],
                [],
                [],
                null,
                null,
                null),
            []);

        Assert.Empty(ResearchContractValidator.Validate(update));
    }

    [Fact]
    public void ManifestPinsApprovedVersionClockAndEngineeringLimits()
    {
        var manifest = Manifest();

        Assert.Empty(ResearchContractValidator.Validate(manifest));

        var invalid = manifest with
        {
            EngineeringLimits = manifest.EngineeringLimits with
            {
                MaximumOperationsPerUpdate = 17
            }
        };
        Assert.Contains(
            ResearchContractValidator.Validate(invalid),
            issue => issue.Code == "engineering-limit-exceeded");
    }

    [Fact]
    public void ResearchEventLedgerRequiresContiguousSameRunSequence()
    {
        var started = Event(1, "run.started", """{}""");
        var reasserted = Event(
            2,
            "belief.reasserted",
            """
            {
              "memoryUpdateId": "memory-update-4",
              "predecessorBeliefId": "belief-1",
              "beliefId": "belief-2",
              "triggerId": "trigger-3"
            }
            """,
            [1],
            generation: 3,
            memoryRevision: 4);

        Assert.Empty(ResearchEventSequenceValidator.Validate([started, reasserted]));

        var gap = reasserted with { Sequence = 3 };
        Assert.Contains(
            ResearchEventSequenceValidator.Validate([started, gap]),
            issue => issue.Code == "invalid-reference");
    }

    [Fact]
    public void ExplicitNullCollectionsAreRejectedAsContractErrors()
    {
        const string json = """
            {
              "schemaVersion": "1.0-candidate.3",
              "recordType": "actor-decision",
              "kind": "wait",
              "memoryDisposition": null,
              "usedBeliefIds": null,
              "ticks": 2
            }
            """;

        var exception = Assert.Throws<ResearchContractException>(
            () => ResearchContractSerializer.DeserializeRoot(json));

        Assert.Equal("invalid-shape", exception.Code);
    }

    [Fact]
    public void SnapshotRecursivelyRejectsMalformedEmbeddedRootsAndHistory()
    {
        var malformedMemory = new WorkingMemorySnapshot(
            "unsupported",
            "observation",
            RunId,
            0,
            999,
            0,
            [],
            null);
        var invalidDecision = JsonDocument.Parse(
            """{"schemaVersion":"1.0-candidate.3","recordType":"actor-decision","anything":"not-an-actor-decision"}""")
            .RootElement.Clone();
        var snapshot = MinimalSnapshot() with
        {
            WorkingMemory = malformedMemory,
            ActionHistory =
            [
                new ActionHistoryItem(
                    "actor-1",
                    "snapshot-1",
                    "result-1",
                    0,
                    invalidDecision,
                    "invented",
                    null,
                    null,
                    [])
            ]
        };

        var issues = ResearchContractValidator.Validate(snapshot);

        Assert.Contains(issues, issue => issue.Code == "unsupported-schema");
        Assert.Contains(issues, issue => issue.Code == "unknown-record-type");
        Assert.Contains(issues, issue => issue.Code == "inconsistent-update");
        Assert.Contains(issues, issue => issue.Code == "invalid-shape");
        Assert.Contains(issues, issue => issue.Code == "unknown-enum");
    }

    [Fact]
    public void SnapshotRejectsCrossRunFutureReconsiderationBinding()
    {
        var trigger = new ReconsiderationTrigger(
            ResearchContractVersions.SchemaVersion,
            "reconsideration-trigger",
            "18af8fd5-d85e-4146-9f7c-b5eef350f992",
            "trigger-1",
            "memory-update-1",
            99,
            99,
            0,
            null);
        var snapshot = MinimalSnapshot() with
        {
            DecisionGeneration = 99,
            Reconsideration = new ReconsiderationRequirement(trigger, [], [])
        };

        Assert.Contains(
            ResearchContractValidator.Validate(snapshot),
            issue => issue.Code == "trigger-mismatch");
    }

    [Fact]
    public void SnapshotPermitsTerminalHistoricalBeliefFromPriorEpoch()
    {
        var historical = EligibleBelief("belief-1") with
        {
            Belief = EligibleBelief("belief-1").Belief with
            {
                ApplicabilityEpoch = 0
            },
            State = new BeliefStateEntry(
                "belief-1",
                BeliefState.Invalidated,
                "memory-update-2",
                [],
                null)
        };
        var snapshot = MinimalSnapshot() with
        {
            ApplicabilityEpoch = 1,
            MemoryRevision = 1,
            WorkingMemory = new WorkingMemorySnapshot(
                ResearchContractVersions.SchemaVersion,
                "working-memory-snapshot",
                RunId,
                1,
                0,
                1,
                [historical.State],
                null),
            Beliefs = [historical.Belief]
        };

        Assert.DoesNotContain(
            ResearchContractValidator.Validate(snapshot),
            issue => issue.Code == "epoch-mismatch");
    }

    [Fact]
    public void EqualProposalDefinitionsAlwaysChooseOrdinalSmallestKey()
    {
        var operations = new MemoryOperation[]
        {
            new AddBeliefOperation("z", DependencyClaim()),
            new AddBeliefOperation("a", DependencyClaim())
        };
        var proposal = Proposal(operations);

        var forward = MemoryProposalValidator.Validate(proposal, Context());
        var reverse = MemoryProposalValidator.Validate(
            Proposal(operations.Reverse().ToArray()),
            Context());

        Assert.Equal("a", Assert.Single(forward.CreatedBeliefs).Key);
        Assert.Equal("a", Assert.Single(reverse.CreatedBeliefs).Key);
        Assert.All(
            forward.KeyBindings,
            binding => Assert.Equal("proposed:a", binding.Reference));
        Assert.All(
            reverse.KeyBindings,
            binding => Assert.Equal("proposed:a", binding.Reference));
    }

    [Fact]
    public void ForwardProposedReferencesResolveBeforeSuccessorValidation()
    {
        var existing = EligibleBelief("belief-1");
        var addedClaim = new Claim(
            Hypothesis.DependencySideQueueDelay,
            TargetId.AuthorizationService,
            ["observation-3"],
            Uncertainty.Medium);
        var proposal = Proposal(
            new DeclareConflictOperation(
                [new ProposedBeliefRef("a"), new ExistingBeliefRef("belief-1")],
                ["observation-3"],
                "Keep both explanations contested."),
            new AddBeliefOperation("a", addedClaim));

        var result = MemoryProposalValidator.Validate(proposal, Context(existing));

        Assert.True(result.IsValid, string.Join("; ", result.Issues));
        Assert.Single(result.AddedConflicts);
    }

    [Fact]
    public void DuplicateDirectionCannotHideInvalidRawSupportingReference()
    {
        var direction = DirectionFixture(epoch: 0);
        var operation = SetDirection(new ExistingBeliefRef("belief-999"));
        var result = MemoryProposalValidator.Validate(
            Proposal(operation),
            Context() with { HistoricalDirections = [direction] });

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "invalid-direction-dependency");
    }

    [Fact]
    public void DirectionEqualityIncludesRunAndEpoch()
    {
        var historical = DirectionFixture(epoch: 0);
        var context = Context() with
        {
            ApplicabilityEpoch = 1,
            HistoricalDirections = [historical]
        };

        var result = MemoryProposalValidator.Validate(
            Proposal(SetDirection()),
            context);

        Assert.True(result.IsValid, string.Join("; ", result.Issues));
        Assert.True(result.DirectionChanged);
        Assert.DoesNotContain(
            result.Normalization,
            note => note.Rule == "drop-repeated-direction");
    }

    [Fact]
    public void MemoryUpdateOriginControlsGenerationAndActionability()
    {
        var expiry = CommittedUpdate(
            new DirectionExpiryUpdateOrigin("direction-1"),
            actionable: false,
            previousGeneration: 2,
            newGeneration: 9,
            triggerId: null,
            effects: EmptyEffects() with
            {
                DirectionBeforeId = "direction-1",
                DirectionEndState = DirectionState.Expired
            });
        var supervisor = CommittedUpdate(
            new SupervisorUpdateOrigin("review-1", "result-1", "snapshot-1"),
            actionable: false,
            previousGeneration: 2,
            newGeneration: 2,
            triggerId: null,
            effects: EmptyEffects());

        Assert.Contains(
            ResearchContractValidator.Validate(expiry),
            issue => issue.Code == "inconsistent-update");
        Assert.Contains(
            ResearchContractValidator.Validate(supervisor),
            issue => issue.Code == "inconsistent-update");

        var validExpiry = expiry with { NewGeneration = 2 };
        Assert.Empty(ResearchContractValidator.Validate(validExpiry));
    }

    [Fact]
    public void RepeatedDirectionRequiresNoOpUnlessAnotherEffectChangesMemory()
    {
        var repeatedProposal = Proposal(SetDirection());
        var repeatedNote = new NormalizationNote(
            0,
            "drop-repeated-direction",
            null,
            null,
            "direction-1");
        var committedRepeat = new MemoryUpdate(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            RunId,
            "memory-update-4",
            new SupervisorUpdateOrigin("review-4", "result-9", "snapshot-8"),
            3,
            3,
            4,
            "committed",
            true,
            2,
            3,
            "trigger-3",
            repeatedProposal,
            [repeatedNote],
            [],
            EmptyEffects() with
            {
                DirectionBeforeId = "direction-1",
                DirectionAfterId = "direction-1"
            },
            []);

        Assert.Contains(
            ResearchContractValidator.Validate(committedRepeat),
            issue => issue.Code == "inconsistent-update");

        var noOp = committedRepeat with
        {
            CommittedMemoryRevision = null,
            Outcome = "no-op",
            Actionable = false,
            NewGeneration = 2,
            TriggerId = null,
            Effects = null
        };
        Assert.Empty(ResearchContractValidator.Validate(noOp));

        var genuineSet = committedRepeat with
        {
            Effects = EmptyEffects() with
            {
                DirectionAfterId = "direction-2"
            }
        };
        var genuineReplacement = committedRepeat with
        {
            Effects = EmptyEffects() with
            {
                DirectionBeforeId = "direction-1",
                DirectionAfterId = "direction-2"
            }
        };
        var genuineClear = committedRepeat with
        {
            Proposal = Proposal(
                new ClearDirectionOperation(
                    ["observation-3"],
                    "Current evidence no longer supports a direction.")),
            Effects = EmptyEffects() with
            {
                DirectionBeforeId = "direction-1",
                DirectionEndState = DirectionState.Cleared
            }
        };
        var beliefCorrection = committedRepeat with
        {
            Proposal = Proposal(
                new AddBeliefOperation(
                    "corrected-belief",
                    DependencyClaim())),
            Effects = EmptyEffects() with
            {
                CreatedBeliefIds = ["belief-2"],
                DirectionBeforeId = "direction-1",
                DirectionAfterId = "direction-1"
            }
        };

        Assert.Empty(ResearchContractValidator.Validate(genuineSet));
        Assert.Empty(ResearchContractValidator.Validate(genuineReplacement));
        Assert.Empty(ResearchContractValidator.Validate(genuineClear));
        Assert.Empty(ResearchContractValidator.Validate(beliefCorrection));
    }

    [Fact]
    public void EventPayloadValuesAreStrictlyTyped()
    {
        var malformed = Event(
            1,
            "actor.started",
            """
            {
              "actorTurnId": 123,
              "snapshotId": false,
              "consumedMemoryRevision": "fake",
              "requiredTriggerId": {}
            }
            """,
            tick: 1,
            phase: ResearchPhase.ActorStart);

        var issues = ResearchContractValidator.Validate(malformed);

        Assert.True(issues.Count >= 4, string.Join("; ", issues));
        Assert.All(issues, issue => Assert.Contains(issue.Code, new[] { "wrong-type", "invalid-identifier" }));
    }

    [Fact]
    public void EventPayloadUsesEvidenceTokensAndOperationalBindingText()
    {
        var evidence = Event(
            1,
            "evidence.available",
            """{"evidenceIds":["E1"],"targetId":"INC-1042","availableTick":0}""");
        var dispatched = Event(
            2,
            "diagnostic.dispatched",
            """
            {
              "diagnosticId":"diagnostic-1",
              "actorTurnId":"actor-1",
              "resultId":"result-1",
              "consumedMemoryRevision":0,
              "binding":{
                "planId":"82a87acf-c08e-463f-b826-b588005f6705",
                "stepId":"read incident",
                "planDigest":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "actionDigest":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "requestId":null,
                "sessionId":"session/INC-1042"
              }
            }
            """,
            tick: 0,
            phase: ResearchPhase.ActorProcessing);

        Assert.Empty(ResearchContractValidator.Validate(evidence));
        Assert.Empty(ResearchContractValidator.Validate(dispatched));
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                [evidence],
                new HashSet<string>(["E1"], StringComparer.Ordinal)));
        Assert.Contains(
            ResearchEventSequenceValidator.Validate(
                [evidence],
                new HashSet<string>(["E2"], StringComparer.Ordinal)),
            issue => issue.Code == "invalid-reference");
    }

    [Fact]
    public void StoredSupervisorProposalIsRecursiveAndCannotCommitNoChange()
    {
        var noChange = new NoChangeSupervisorOutput(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            0,
            [],
            null,
            null);
        var noChangeCommit = CommittedUpdate(
            new SupervisorUpdateOrigin("review-1", "result-1", "snapshot-1"),
            actionable: true,
            previousGeneration: 0,
            newGeneration: 1,
            triggerId: "trigger-1",
            effects: EmptyEffects()) with
        {
            Proposal = noChange
        };
        var malformed = noChangeCommit with
        {
            Proposal = new ProposeMemoryUpdateSupervisorOutput(
                "unsupported",
                "observation",
                0,
                [],
                null,
                null,
                [])
        };

        Assert.Contains(
            ResearchContractValidator.Validate(noChangeCommit),
            issue => issue.Code == "inconsistent-update");
        var malformedIssues = ResearchContractValidator.Validate(malformed);
        Assert.Contains(malformedIssues, issue => issue.Code == "unsupported-schema");
        Assert.Contains(malformedIssues, issue => issue.Code == "unknown-record-type");
        Assert.Contains(
            malformedIssues,
            issue => issue.Code == "engineering-limit-exceeded");
    }

    [Fact]
    public void SnapshotEnforcesCaptureTimeTriggerAndActiveDirectionApplicability()
    {
        var active = DirectionFixture(epoch: 0) with
        {
            ReviewStartTick = 0,
            ExpiresAtTick = 8
        };
        var baseSnapshot = MinimalSnapshot() with
        {
            Tick = 7,
            MemoryRevision = 1,
            WorkingMemory = new WorkingMemorySnapshot(
                ResearchContractVersions.SchemaVersion,
                "working-memory-snapshot",
                RunId,
                1,
                0,
                0,
                [],
                active)
        };

        Assert.Empty(ResearchContractValidator.Validate(baseSnapshot));
        Assert.Contains(
            ResearchContractValidator.Validate(baseSnapshot with { Tick = 8 }),
            issue => issue.Code == "inconsistent-update");
        Assert.Empty(ResearchContractValidator.Validate(active));
        Assert.Contains(
            ResearchContractValidator.Validate(
                active with { ExpiresAtTick = 100 }),
            issue => issue.Code == "out-of-range");

        var trigger = new ReconsiderationTrigger(
            ResearchContractVersions.SchemaVersion,
            "reconsideration-trigger",
            RunId,
            "trigger-1",
            "memory-update-1",
            1,
            1,
            99,
            null);
        var triggerSnapshot = baseSnapshot with
        {
            Tick = 9,
            WorkingMemory = baseSnapshot.WorkingMemory with
            {
                RecommendedDirection = null
            },
            DecisionGeneration = 1,
            Reconsideration = new ReconsiderationRequirement(trigger, [], [])
        };
        Assert.Contains(
            ResearchContractValidator.Validate(triggerSnapshot),
            issue => issue.Code == "trigger-mismatch");
        Assert.Empty(
            ResearchContractValidator.Validate(
                triggerSnapshot with
                {
                    Reconsideration = new ReconsiderationRequirement(
                        trigger with { CreatedTick = 9 },
                        [],
                        [])
                }));
    }

    [Fact]
    public void InvocationResultUsesClosedConsumerAndSettlementVocabularies()
    {
        var invalid = Invocation(
            consumer: "invented",
            actorTurnId: null,
            parseStatus: "invented",
            output: JsonDocument.Parse("""{"anything":true}""").RootElement.Clone());

        var issues = ResearchContractValidator.Validate(invalid);

        Assert.Contains(issues, issue => issue.Code == "unknown-enum");

        Assert.Empty(
            ResearchContractValidator.Validate(
                Invocation(
                    consumer: "actor",
                    actorTurnId: "actor-1",
                    parseStatus: "invalid",
                    output: null,
                    reason: ContextReason("obsolete-generation"))));
    }

    [Fact]
    public void LedgerRejectsBackwardTicksAndStateChangesAfterTermination()
    {
        var started = Event(1, "run.started", """{}""", tick: 10);
        var backward = Event(2, "observation.recorded", """{"observationId":"observation-1"}""", tick: 2);
        Assert.Contains(
            ResearchEventSequenceValidator.Validate([started, backward]),
            issue => issue.Message.Contains("ticks", StringComparison.Ordinal));

        var terminated = Event(
            2,
            "episode.terminated",
            """{}""",
            tick: 10,
            phase: ResearchPhase.ActorProcessing);
        var commit = Event(
            3,
            "memory.committed",
            """
            {
              "memoryUpdateId":"memory-update-1",
              "previousMemoryRevision":0,
              "newMemoryRevision":1,
              "previousGeneration":0,
              "newGeneration":1,
              "actionable":true,
              "triggerId":"trigger-1"
            }
            """,
            tick: 10,
            phase: ResearchPhase.Drain);
        Assert.Contains(
            ResearchEventSequenceValidator.Validate([started, terminated, commit]),
            issue => issue.Code == "episode-ended");
    }

    [Fact]
    public void LedgerPermitsSettlementAccountingDuringDrain()
    {
        var events = new[]
        {
            Event(1, "run.started", """{}"""),
            Event(
                2,
                "episode.terminated",
                """{}""",
                tick: 10,
                phase: ResearchPhase.ActorProcessing),
            Event(
                3,
                "actor.cancelled",
                """{"actorTurnId":"actor-1","resultId":"result-1","consumedMemoryRevision":0}""",
                tick: 10,
                phase: ResearchPhase.Drain),
            Event(4, "run.closed", """{}""", tick: null, phase: ResearchPhase.Drain)
        };

        Assert.Empty(ResearchEventSequenceValidator.Validate(events));
    }

    [Fact]
    public void DrainSettlementsPreserveFrozenCoordinatesAndTerminationCounts()
    {
        var prefix = new[]
        {
            Event(1, "run.started", """{}"""),
            Event(
                2,
                "report.submitted",
                """{"actorTurnId":"actor-1","resultId":"result-1","consumedMemoryRevision":0}""",
                tick: 12,
                phase: ResearchPhase.ActorProcessing),
            Event(
                3,
                "episode.terminated",
                """{}""",
                tick: 12,
                phase: ResearchPhase.ActorProcessing)
        };
        var drain = Event(
            4,
            "result.suppressed",
            """
            {
              "invocation":{"consumer":"actor","actorTurnId":"actor-2"},
              "resultId":"result-2",
              "consumedMemoryRevision":0,
              "reason":{"domain":"context","code":"episode-ended","message":null}
            }
            """,
            tick: 12,
            phase: ResearchPhase.Drain);
        var events = prefix.Append(drain).ToArray();

        Assert.Empty(ResearchEventSequenceValidator.Validate(events));
        Assert.Empty(
            ResearchEventSequenceValidator.Validate(
                events,
                TerminationFixture() with
                {
                    Counts = Counts(
                        reportsSubmitted: 1,
                        actorResultsSuppressed: 0)
                }));

        var changed = drain with
        {
            CurrentGeneration = 99,
            CurrentMemoryRevision = 99,
            HistoryRevision = 99,
            ApplicabilityEpoch = 99
        };
        Assert.Contains(
            ResearchEventSequenceValidator.Validate(prefix.Append(changed).ToArray()),
            issue => issue.Message.Contains("frozen", StringComparison.Ordinal));
    }

    [Fact]
    public void TerminationValidatesDerivableActionableAndReassertionCounters()
    {
        var events = new[]
        {
            Event(1, "run.started", """{}"""),
            Event(
                2,
                "memory.committed",
                """
                {
                  "memoryUpdateId":"memory-update-1",
                  "previousMemoryRevision":0,
                  "newMemoryRevision":1,
                  "previousGeneration":0,
                  "newGeneration":1,
                  "actionable":true,
                  "triggerId":"trigger-1"
                }
                """,
                generation: 1,
                memoryRevision: 1,
                tick: 12,
                phase: ResearchPhase.ReviewProcessing),
            Event(
                3,
                "belief.reasserted",
                """{"memoryUpdateId":"memory-update-1","predecessorBeliefId":"belief-1","beliefId":"belief-2","triggerId":"trigger-1"}""",
                generation: 1,
                memoryRevision: 1,
                tick: 12,
                phase: ResearchPhase.ReviewProcessing),
            Event(
                4,
                "belief.reasserted",
                """{"memoryUpdateId":"memory-update-1","predecessorBeliefId":"belief-3","beliefId":"belief-4","triggerId":"trigger-1"}""",
                generation: 1,
                memoryRevision: 1,
                tick: 12,
                phase: ResearchPhase.ReviewProcessing),
            Event(
                5,
                "report.submitted",
                """{"actorTurnId":"actor-1","resultId":"result-1","consumedMemoryRevision":1}""",
                generation: 1,
                memoryRevision: 1,
                tick: 12,
                phase: ResearchPhase.ActorProcessing),
            Event(
                6,
                "episode.terminated",
                """{}""",
                generation: 1,
                memoryRevision: 1,
                tick: 12,
                phase: ResearchPhase.ActorProcessing)
        };
        var termination = TerminationFixture() with
        {
            FinalGeneration = 1,
            FinalMemoryRevision = 1,
            Counts = Counts(
                reportsSubmitted: 1,
                memoryUpdatesCommitted: 1,
                beliefReassertionsCommitted: 2,
                reassertionInterrupts: 1,
                actionableCommits: 1)
        };

        Assert.Empty(ResearchEventSequenceValidator.Validate(events, termination));
        Assert.Contains(
            ResearchEventSequenceValidator.Validate(
                events,
                termination with
                {
                    Counts = Counts(
                        reportsSubmitted: 1,
                        memoryUpdatesCommitted: 1,
                        beliefReassertionsCommitted: 2,
                        reassertionInterrupts: 99,
                        actionableCommits: 99)
                }),
            issue => issue.Message.Contains("actionableCommits", StringComparison.Ordinal));
    }

    [Fact]
    public void ReportTerminationEnforcesPhaseAcknowledgmentCountAndLedgerAccounting()
    {
        var invalid = TerminationFixture() with
        {
            Tick = 12,
            Phase = ResearchPhase.Setup,
            FinalPendingTriggerId = "trigger-1",
            Counts = Counts(reportsSubmitted: 2)
        };
        Assert.Contains(
            ResearchContractValidator.Validate(invalid),
            issue => issue.Code is "invalid-shape" or "out-of-range");

        var events = new[]
        {
            Event(1, "run.started", """{}"""),
            Event(
                2,
                "report.submitted",
                """{"actorTurnId":"actor-1","resultId":"result-1","consumedMemoryRevision":0}""",
                tick: 12,
                phase: ResearchPhase.ActorProcessing),
            Event(
                3,
                "episode.terminated",
                """{}""",
                tick: 12,
                phase: ResearchPhase.ActorProcessing)
        };
        var valid = TerminationFixture();
        Assert.Empty(ResearchEventSequenceValidator.Validate(events, valid));

        Assert.Contains(
            ResearchEventSequenceValidator.Validate(
                events,
                valid with { Counts = Counts(reportsSubmitted: 0) }),
            issue => issue.Message.Contains("reportsSubmitted", StringComparison.Ordinal));
    }

    private static ProposeMemoryUpdateSupervisorOutput ReassertionProposal(uint revision) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            revision,
            ["observation-3"],
            "Current dependency evidence again supports the retracted hypothesis.",
            Uncertainty.Medium,
            [
                new ReassertBeliefOperation(
                    "belief-1",
                    "reasserted-dependency-path",
                    DependencyClaim(),
                    "Reassert after retraction using the current captured evidence.")
            ]);

    private static ProposeMemoryUpdateSupervisorOutput Proposal(
        params MemoryOperation[] operations) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "supervisor-output",
            3,
            ["observation-3"],
            "Validate the proposed update.",
            Uncertainty.Medium,
            operations);

    private static SetDirectionOperation SetDirection(params BeliefRef[] support) =>
        new(
            new FocusDirectionChoice(
                new TargetFocus(TargetId.AuthorizationService)),
            ["observation-3"],
            support);

    private static Direction DirectionFixture(uint epoch) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "direction",
            RunId,
            "direction-1",
            "memory-update-1",
            "review-1",
            4,
            12,
            epoch,
            new FocusDirectionChoice(
                new TargetFocus(TargetId.AuthorizationService)),
            ["observation-3"],
            []);

    private static InputSnapshot MinimalSnapshot() =>
        new(
            ResearchContractVersions.SchemaVersion,
            "input-snapshot",
            RunId,
            "snapshot-1",
            "actor",
            "actor-1",
            null,
            0,
            0,
            null,
            0,
            0,
            0,
            new InputScope("INC-1042", "payments-api", "Diagnose the incident."),
            new RemainingBudgets(24, 12, 6),
            [],
            [],
            [],
            [],
            new WorkingMemorySnapshot(
                ResearchContractVersions.SchemaVersion,
                "working-memory-snapshot",
                RunId,
                0,
                null,
                0,
                [],
                null),
            [],
            null,
            null);

    private static InvocationResult Invocation(
        string consumer,
        string? actorTurnId,
        string parseStatus,
        JsonElement? output,
        Reason? reason = null) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "invocation-result",
            RunId,
            "result-1",
            consumer,
            actorTurnId,
            null,
            "snapshot-1",
            0,
            0,
            0,
            1,
            "returned",
            parseStatus,
            output,
            new string('a', 64),
            "{}",
            reason);

    private static Reason ContextReason(string code) =>
        new(ReasonDomain.Context, code, null);

    private static MemoryEffects EmptyEffects() =>
        new([], [], [], [], [], [], [], null, null, null);

    private static MemoryUpdate CommittedUpdate(
        UpdateOrigin origin,
        bool actionable,
        uint previousGeneration,
        uint newGeneration,
        string? triggerId,
        MemoryEffects effects) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "memory-update",
            RunId,
            "memory-update-1",
            origin,
            0,
            0,
            1,
            "committed",
            actionable,
            previousGeneration,
            newGeneration,
            triggerId,
            origin is SupervisorUpdateOrigin ? Proposal() : null,
            [],
            [],
            effects,
            []);

    private static Termination TerminationFixture() =>
        new(
            ResearchContractVersions.SchemaVersion,
            "termination",
            RunId,
            12,
            ResearchPhase.ActorProcessing,
            "report",
            "result-1",
            null,
            0,
            0,
            0,
            0,
            null,
            false,
            Counts(reportsSubmitted: 1),
            [],
            []);

    private static ResourceCounts Counts(
        uint reportsSubmitted = 0,
        uint actorResultsSuppressed = 0,
        uint memoryUpdatesCommitted = 0,
        uint beliefReassertionsCommitted = 0,
        uint reassertionInterrupts = 0,
        uint actionableCommits = 0) =>
        new(
            0,
            0,
            0,
            0,
            0,
            actorResultsSuppressed,
            0,
            0,
            0,
            0,
            0,
            0,
            reportsSubmitted,
            memoryUpdatesCommitted,
            0,
            0,
            0,
            0,
            beliefReassertionsCommitted,
            reassertionInterrupts,
            actionableCommits,
            0);

    private static Claim DependencyClaim() =>
        new(
            Hypothesis.DependencyPathIssue,
            TargetId.AuthorizationService,
            ["observation-3"],
            Uncertainty.Medium);

    private static MemoryBeliefFixture RetractedBelief(
        string beliefId = "belief-1",
        uint stateRevision = 2) =>
        new(
            new Belief(
                ResearchContractVersions.SchemaVersion,
                "belief",
                RunId,
                beliefId,
                $"memory-update-{stateRevision - 1}",
                stateRevision - 1,
                0,
                DependencyClaim(),
                null),
            new BeliefStateEntry(
                beliefId,
                BeliefState.Retracted,
                $"memory-update-{stateRevision}",
                [],
                null),
            stateRevision);

    private static MemoryBeliefFixture EligibleBelief(string beliefId) =>
        new(
            new Belief(
                ResearchContractVersions.SchemaVersion,
                "belief",
                RunId,
                beliefId,
                "memory-update-2",
                2,
                0,
                DependencyClaim(),
                beliefId == "belief-1" ? null : "belief-1"),
            new BeliefStateEntry(
                beliefId,
                BeliefState.Provisional,
                "memory-update-2",
                [],
                null),
            2);

    private static MemoryProposalContext Context(
        params MemoryBeliefFixture[] beliefs) =>
        new(
            RunId,
            3,
            3,
            0,
            new HashSet<string>(["observation-3"], StringComparer.Ordinal),
            beliefs,
            null,
            []);

    private static RunManifest Manifest()
    {
        var hash = new string('a', 64);
        var definition = new DefinitionRef("fixture", hash);
        return new RunManifest(
            ResearchContractVersions.SchemaVersion,
            "run-manifest",
            RunId,
            new StudySpecificationRef("1.0", "WM-1", "3"),
            ResearchContractVersions.ContractBaseline,
            "asynchronous-supervision",
            "isolated-invocations-single-coordinator",
            "supervision-plus-within-incident-memory-adaptation",
            "scripted",
            "synthetic",
            new string('b', 40),
            true,
            [],
            definition,
            definition,
            definition,
            definition,
            definition,
            definition,
            definition,
            null,
            null,
            new ScriptedClock(
                "logical-ticks",
                0,
                24,
                1,
                0,
                3,
                [0, 4, 8, 12, 16, 20],
                6,
                8,
                4),
            new ResearchLimits(24, 12, 6, 1, 1),
            new EngineeringLimits(65_536, 16, 2_048, 64, 1_048_576, 131_072),
            new RuntimeVersions("10.0", "1.17.0", "unavailable"),
            "unavailable-scripted");
    }

    private static ResearchEvent Event(
        uint sequence,
        string eventType,
        string data,
        IReadOnlyList<uint>? causes = null,
        uint generation = 0,
        uint memoryRevision = 0,
        uint? tick = 0,
        ResearchPhase phase = ResearchPhase.Setup) =>
        new(
            ResearchContractVersions.SchemaVersion,
            "research-event",
            RunId,
            sequence,
            tick,
            phase,
            eventType,
            causes ?? [],
            generation,
            memoryRevision,
            0,
            0,
            JsonDocument.Parse(data).RootElement.Clone());
}
