# Straightforward ER-1 scripted smoke-run proposal

| Field | Proposed value |
| --- | --- |
| Status | Narrow scripted smoke implementation authorized; stop for review before remaining T4/T5 |
| Date | 2026-09-18 |
| Fixture | Approved `ER1-fixture-1`, straightforward development variant only |
| Architectures | Actor-only, blocking supervision, asynchronous supervision |
| Execution mode | Scripted logical-clock mechanism run |
| Episodes | Two repetitions per architecture; six episodes total |
| Evaluation | Contract, ledger, termination, and reproducibility checks only |

## 1. Purpose and boundary

The smallest subsequent end-to-end milestone is one execution of the approved
straightforward fixture across the three existing coordination architectures,
followed by one exact repetition of each architecture. It demonstrates that the
T1 contracts, T2 scheduler/evidence fixture, and reviewed T3 coordinator can
produce complete runtime records together.

This proposal does not add a dashboard, scenario, model call, score, outcome
rubric, deployment path, or new coordination layer. Its six-episode scripted
smoke implementation was authorized on 2026-09-18 as a separate review
checkpoint. Scripted tick differences are mechanism observations, not evidence
that supervision improves model performance.

## 2. Fixed inputs

All six episodes use the same harness-owned definitions:

- `ER1-fixture-1` with `Er1FixtureOptions.Straightforward`.
- The section-7 scripted actor: report after observing E3; otherwise follow an
  accepted dependency focus; otherwise perform the existing local diagnostic
  and bounded-Wait behavior.
- The section-7 scripted supervisor: propose the dependency focus when its
  immutable snapshot contains E2 but not E3; otherwise return `NoChange`.
- The approved external evidence schedule, including E0 at tick 0, E1 at tick
  4, E2 at tick 8, silent E3 collection at tick 12, and E4 at tick 16 for
  episodes still active.
- The approved logical clock, phase ordering, checkpoints, exclusive tick-24
  horizon, 24 actor turns, 12 recognizable Query attempts, and 6 reviews.
- The same seven-operation catalogue, verifier, canonicalizer, default-deny
  policy, approval/budget controls, governed gateway, audit chain, and
  read-only ER-1 simulator route.
- The same actor definition in all three architectures and the same supervisor
  definition in both supervised architectures. Architecture selection changes
  scheduling only.

The authored section-7 behavior currently yields an actor-only timeout and
supervised reports at their mechanically derived ticks. Those values are
acceptance expectations outside the component inputs. The runner must derive
termination from actual coordinator events rather than inserting an expected
termination record.

## 3. Minimal execution shape

The narrowest implementation should be one dedicated integration-smoke test
invoked directly through the existing test project:

```powershell
dotnet test .\tests\GovernedAgent.IntegrationTests\GovernedAgent.IntegrationTests.csproj `
  --filter FullyQualifiedName~Er1StraightforwardSmokeRun `
  --logger "console;verbosity=normal"
```

The test should instantiate, rather than replace:

1. `ScriptedExperimentScheduler`.
2. `Er1EvidenceFixture` and `Er1FixtureIncidentSimulator`.
3. `IsolatedAgentFrameworkRuntime`.
4. `CoordinatorMemoryStore` through `ScriptedSupervisionCoordinator`.
5. The existing governed diagnostic adapter using the verifier, policy, gateway,
   audit chain, and fixture simulator.

Each architecture runs once to create its primary records and once more from
fresh state for reproducibility. Diagnostic initiation remains separate from
scheduled result processing, so delayed delivery cannot block world ticks,
reviews, interruptions, or the horizon.

This test and artifact production are the authorized narrow smoke checkpoint.
They do not authorize the remaining T4 evaluator work or T5.

## 4. Required runtime records

Each episode should persist actual validated records under a bounded ignored
directory such as:

```text
.artifacts\deliberation-study\smoke-er1\<run-set-id>\<architecture>\<repetition>\
```

The episode directory should contain:

| File | Actual source |
| --- | --- |
| `run-manifest.json` | Harness-built candidate-3 `RunManifest` with actual source revision, dirty-state flag, runtime versions, and hashed definitions |
| `events.jsonl` | Ordered `ResearchEvent` records emitted by the coordinator |
| `observations.jsonl` | Actual shared observations, including query-linked evidence IDs and source payloads |
| `invocation-results.jsonl` | Actor and supervisor settlements actually received |
| `actions.jsonl` | Applied, rejected, and suppressed actor action-history records |
| `reviews.jsonl` | Actual review-history records |
| `memory-updates.jsonl` | Accepted, rejected, and no-op memory updates |
| `beliefs.jsonl` | Materialized immutable belief records |
| `termination.json` | Coordinator-issued validated terminal record |
| `run-closure.json` | Validated bounded-drain closure |
| `timeline.txt` | Readable projection derived from the records above |

The files must be written from authoritative coordinator/contract objects after
validation. Expected diagnoses, report ticks, variant labels, evaluator
rationales, and fixture answer fields must not be copied into observed records.

## 5. Readable timeline

`timeline.txt` should be a derived, non-authoritative view with one row per
consequential event:

```text
tick | phase | event | actor/review/diagnostic | history | memory | generation | summary
```

It should include, when present:

- Evidence delivery and diagnostic observations.
- Actor and review snapshot capture.
- Query request, governed dispatch, and delivery.
- Memory proposal, revision publication, and reconsideration trigger.
- Actor invalidation and cancellation outcome.
- Obsolete-result suppression.
- Report or horizon termination and bounded drain.

The summary column may display identifiers and neutral payload aliases already
present in authoritative records. It must not infer correctness, causality,
importance, or model quality.

## 6. Repeated-run reproducibility

The second repetition of each architecture should be compared with the first
using the already approved distinction between run identity and a canonical
logical trace. The comparison is mechanical, not C6 evidence equivalence.

Compare:

- Ordered tick, phase, and event-type sequence.
- History revision, applicability epoch, memory revision, and decision
  generation at each event.
- Actor, review, diagnostic, observation, memory-update, belief, direction, and
  trigger identifiers after replacing only run-scoped identities with their
  encounter-order placeholders.
- Query operation/target, evidence IDs, observation content, memory effects,
  action dispositions, resource counts, termination kind/tick, and closure
  status.

Exclude only values that are deliberately fresh operational identities:
`runId`, plan/request/correlation/audit UUIDs, encounter-normalized operational
record identifiers, and artifact path. The scripted run uses the fixed approved
clock and contains no measured wall-clock fields; all record timestamps are
compared exactly. Preserve and compare source revision, source dirty-state,
plan/action/audit hashes, and all semantic payloads.

For the smoke checkpoint only, the governed adapter receives an
encounter-ordered deterministic UUID source for plan, request, and audit record
identities. The default adapter continues to use fresh UUIDs. This seam changes
only opaque identity values, remains unique within each run, and is identified
by the run manifest's governance configuration. Actual plan, action, and audit
hashes remain unmodified and are compared exactly across repetitions.

Write `reproducibility.json` at the run-set root with one result per
architecture, the exact excluded-field list, and the first mismatching path if
a comparison fails. Do not label differing records equivalent or successful.

## 7. Smoke-run checks

The smoke command should fail unless:

- Every root record passes `ResearchContractValidator`.
- Each event ledger passes `ResearchEventSequenceValidator`, including its
  terminal-record validation.
- Every diagnostic crosses the existing governed boundary.
- Observations are linked to actual dispatch/completion records and stable
  evidence identities.
- Accepted memory updates, interrupts, cancellation outcomes, obsolete
  suppression, and terminal records agree with their event ledger.
- All episodes terminate and close within the approved horizon and cleanup
  boundary.
- Both repetitions produce the same canonical logical trace for their
  architecture.

These are mechanism and provenance checks. They do not score diagnosis quality,
evidence sufficiency, next-step appropriateness, or comparative benefit.

## 8. Remaining decisions

No additional ER-1 payload, schedule, budget, catalogue, or coordination
decision blocks this mechanism smoke run. The approved straightforward fixture
and section-7 scripted components are sufficient.

One deferred evaluator decision prevents this smoke run from being called the
complete T4 evaluator milestone: C6 has not defined the case-specific evidence
support and next-step rubric. Therefore this proposal records actual reports
and terminations but does not classify them as correct, supported, better, or
worse.

The separate A02 competent-actor fixture is also not part of this smoke run.
Adding it would be an additional actor fixture/acceptance case rather than the
single straightforward incident requested here.
