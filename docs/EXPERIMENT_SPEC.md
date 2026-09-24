# Experiment specification: ongoing supervision

| Field | Value |
| --- | --- |
| Design reference | v1.0 plus the consolidated working-memory contract baseline |
| Date | 2026-09-16 |
| Scope | One evolving synthetic incident, ending at diagnosis and a justified next step |
| Purpose | Remove research-design ambiguity before a later GitHub Copilot implementation handoff |
| Research charter | [README](../README.md) |

No experiment runner, model integration, or new simulator capability is
implemented by this document. The scripted defaults below are design parameters,
not measured model performance or evidence that supervision helps. Live-model
methodology and the open handoff items in section 11 remain separate.

The T3-only authorization of 2026-09-18 permits scripted runtime coordination
against this specification and the pushed T2 baseline. It does not authorize
the T4 runner/evaluator, T5 comparative execution, live models, deployment, or
new research semantics.

See [the process record](governance/process.md#specification-consolidation-record)
for approval history and implementation scope. The technical baseline and
[contract definitions](T1_CONTRACT_PROPOSAL.md) are unchanged.

## 1. Scope and design baseline

### Research scope

- Study a fast actor with a slower, ongoing supervisor, not primarily
  per-request routing to a larger model.
- Compare actor-only, blocking supervision, and asynchronous supervision.
  Retain a deterministic reference where practical.
- Study evolving trajectories and identify the conditions in which supervision
  helps, does nothing useful, or makes behavior worse.
- Keep execution authority outside both components and hold the controls
  constant across architectures.
- Fresh, valid, actionable supervisory guidance interrupts the actor's current
  decision and requires reconsideration. Request cancellation where supported,
  but suppress obsolete results whether cancellation succeeds or not.
- Prefer Agent Framework for coordination, with an independent experiment clock
  and feasibility evidence for parallel progress and interruption.
- End the first experiment at diagnosis and a justified next step. Do not
  execute production remediation, restarts, or rollback.
- Use synthetic data and prevent the supervisor from accessing privileged
  answers or future evidence.
- Prepare documentation now; authorize implementation separately.

### Accepted design baseline

The clock values, review policy, finite vocabularies, guidance expiry, and
failure handling in this document form the reviewed design baseline.
Acceptance of the design does not imply that the experiment is implemented or
that empirical outcomes are known.

The implementation handoff must reference this specification revision and
resolve the explicitly open items relevant to its scope.

## 2. Experimental unit and comparisons

One run is one incident episode under one architecture, case seed, and
configuration. An episode contains multiple observations, actor decisions,
diagnostic results, and optionally supervisory reviews.

| Architecture | Difference |
| --- | --- |
| Actor-only | No supervisory calls or guidance. |
| Blocking supervision | At the configured review checkpoints, an active review prevents new actor turns until that review completes or times out. An already-started actor turn is not retroactively cancelled solely because review starts. |
| Asynchronous supervision | The actor may start new permitted turns while review is active. New actionable guidance preempts its pending decision and triggers reconsideration. |

Use the same actor configuration in all three. Use the same supervisor
configuration, review checkpoints, and output-validation rules in the two
supervised variants. Blocking versus non-blocking scheduling is the treatment,
not a different supervisor prompt or access to more tools.

The actor and supervisor share the permitted observed history within a run.
Across runs, action-dependent observations can differ. Match initial state and
external event schedules, but never fabricate identical histories after
different diagnostic choices.

## 3. Clock and scheduling

Scripted runs use logical ticks to establish mechanics such as staleness
binding, obsolete-result suppression, and execution-control behavior. They
can compare schedules under declared duration assumptions, but cannot establish
the real-model responsiveness or cost advantage asked about in the second
research question. Live comparisons require measured inference/coordination
latencies and an independently scheduled environment; a tick-to-time conversion
alone is not empirical evidence. The live-model methodology remains undecided.
This distinction does not change any scripted duration or ordering rule below.

### 3.1 Initial scripted clock

Use a deterministic logical clock for mechanism acceptance examples. A tick is
an abstract unit, not a second or a claimed model-inference duration.

| Parameter | Scripted default |
| --- | --- |
| Episode interval | Tick 0 inclusive through tick 24 exclusive |
| Actor turn duration | 1 tick from input snapshot to returned decision |
| Diagnostic execution duration | 0 additional ticks in this fixture; sampled when the returned Query decision executes |
| Supervisor review duration | 3 ticks from captured snapshot to returned guidance |
| Review checkpoints | 0, 4, 8, 12, 16, 20, while the episode is active |
| Concurrent actor turns | At most 1 current, dispatch-eligible turn; cancelled or obsolete calls may still be draining and must remain accounted for |
| Concurrent reviews | At most 1 |
| Checkpoint while review is active | Record a skipped checkpoint; do not queue or immediately catch up |
| Review timeout | 6 ticks after review start, exclusive; a response arriving at the timeout tick is late |
| Guidance lifetime | Until review-start tick + 8, exclusive, unless invalidated sooner |
| Maximum actor turns | 24 started turns, including invalid outputs and Wait/Report decisions |
| Maximum diagnostic attempts | 12, including rejected or unavailable diagnostic requests |
| Maximum reviews | 6 started reviews |
| Maximum requested wait | 4 ticks |

These values exercise scheduling; they must not be reported as speedups from
real models. Varying the actor/review duration ratio is a later protocol choice.
Real-model timing requires its own declared wall-clock measurement and event
schedule. Do not convert a real model's latency into ticks opportunistically
after seeing its answer.

### 3.2 Order of operations within a tick

Apply the following order consistently in every architecture:

1. If the episode has ended or the tick is at least 24, reject further results
   from affecting the run. At the horizon, terminate an unfinished episode;
   do not accept a report completing exactly at tick 24.
2. Apply scheduled world changes and deliver shared notifications for this tick.
   Update history and any declared applicability epoch.
3. Append results from diagnostic operations dispatched earlier. Such operations
   cannot be assumed to stop or roll back when an interrupt arrives.
4. Expire guidance and time out active reviews whose deadlines have been reached.
   Process remaining due review completions, validate their outputs, and apply
   actionable interrupts before processing not-yet-dispatched actor decisions.
5. Process due actor-turn completions only if their decision generation remains
   current. Validate before diagnostic execution, waiting, or report submission.
   In the zero-duration fixture, diagnostic results are sampled now and appended
   immediately. Discard obsolete completions without executing their proposals.
6. If this is a review checkpoint, start a review if the episode is active,
   review budget remains, and no review is active. Capture the history after
   the earlier phases. A blocking review prevents a new actor turn this tick.
7. Start an actor turn if the episode is active, the actor is ready, its budget
   remains, and it is not blocked. Capture an immutable input snapshot.

If a valid report ends the episode in phase 5, no later phase starts more work.
World changes do not pause for the blocking architecture.

An input snapshot never gains information while inference is pending.
New evidence is available at the next input snapshot. An actionable guidance
interrupt invalidates the current decision rather than modifying its input,
and starts reconsideration from a new snapshot. Independent execution checks
still apply when a returned action executes.

For live execution, the coordinator must serialize guidance acceptance and action
dispatch with an explicit dispatch boundary. An interrupt accepted before that
boundary suppresses the pending action; one accepted afterward cannot undo it.
Do not equate observation of a telemetry event with atomic control of dispatch.

### 3.3 Readiness and waiting

The actor is initially ready and normally becomes ready after a diagnostic
result or a rejected decision. A Wait decision requests suspension until the
earliest of its bounded timer, a new shared notification, or a newly accepted
change in guidance. NoChange reviews do not wake a waiting actor.

Do not lose updates that arrive during the turn producing Wait: if an eligible
wake event occurred after that turn's input snapshot, do not park the actor on
completion. Being ready does not bypass blocking review.

For the scripted examples, a zero-duration diagnostic does not permit multiple
actor decisions in one tick: each subsequent actor turn still takes one tick.

### 3.4 Preferred framework mapping and feasibility gate

Use Microsoft Agent Framework as the preferred coordination layer, not as the
definition of experimental time. Workflow messages, executor activity, and
observability can represent research activity, but one framework step or
superstep is not one clock tick.

Keep three responsibilities separate:

- Experiment scheduler: external events, virtual time, deadlines, and stable
  same-tick ordering for scripted runs.
- Framework adapters: actor/review invocation, message delivery, cancellation,
  and integration with existing controlled execution.
- Research event record: run-local sequence, experimental and measured timestamps,
  evidence snapshot, review ID, actor-turn ID, decision generation, and causal links.

Use an independent monotonic elapsed-time source for later live measurements.
Do not advance simulated time by counting messages or freeze the world while
a review is pending.

Before selecting a workflow graph arrangement, demonstrate that:

1. An actor can complete a permitted diagnostic action while a delayed supervisor
   is still running; no implicit join/superstep barrier serializes their progress.
2. Accepted guidance can invalidate an in-flight actor decision, and a late
   completion cannot dispatch a tool or terminate the episode.
3. Reconsideration can proceed independently of a logically obsolete call where
   the runtime supports it. If cancellation or session serialization prevents
   this, expose the limitation and measured delay rather than claim preemption.
4. Already-dispatched diagnostics have one recorded completion, and interrupts
   cannot cause duplicate execution or erase the resulting observation.

The actor may require isolated invocation snapshots rather than concurrent
mutation of one conversation/session. The later implementer must document what
the pinned SDK actually supports. If the preferred graph imposes synchronization
that changes the treatment, propose an adapter arrangement for review instead
of silently changing the experiment.

References: [workflow concepts](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/)
and [workflow observability](https://learn.microsoft.com/en-us/agent-framework/workflows/observability).
These describe available primitives, not proof of the required execution behavior
in this repository's pinned version.

## 4. Evidence and world state

The approved [ER-1 amendment](#13-approved-amendment-realistic-diagnostic-retrieval)
and [fixture specification](ER1_FIXTURE_PROPOSAL.md) define authoritative
diagnostic retrieval, notification, variant, and recovery behavior. This is a
documentation baseline only; T2 implementation remains separately gated.

The approved development example is derived from the
[historical Payments evidence sequence](governance/research-history.md#draft-evidence-sequence-payments-investigation):

| ID | Available from | Delivery and content |
| --- | --- | --- |
| E0 | 0 | Shared notification: API error rate rises to 18%, p95 latency to 2.6 seconds; no cause established. |
| E1 | 4 | Straightforward shared update: three ready instances and the specified aggregate resource samples. The ambiguous variant instead pushes the approved `payments-api-03` CPU symptom and follow-ups. |
| E2 | 8 | Straightforward shared trace summary: sampled failures correlate with authorization-service timeouts across instances. In the ambiguous variant, the underlying payments logs become queryable without this notification. |
| E3 | 12 | On-demand authorization-service logs: linked request arrival and processing timestamps expose queue waits exceeding the caller timeout. Tick 12 is silent; availability alone does not deliver this result. |
| E4 | 16 | Shared log digest: a cache warning without a changed rate or demonstrated link to the failures. |

Normal successful queries return the currently observable fixed projection,
including legitimate empty log results. In particular,
`query_logs(authorization-service)` dispatched before tick 12 returns the
successful `AL0` empty projection; at or after tick 12 it returns the collected
E3 records. Neither response reveals a future schedule. Returning a query
decision at tick 12 is allowed even if the decision's input snapshot was taken
at tick 11: diagnostic retrieval is sampled at dispatch, not retroactively
inserted into the earlier snapshot.

An explicit unavailable result is reserved for a separately authored
telemetry/access failure. It is not the normal representation of an empty
window or evidence that has not yet been collected.

The supervisor has no independent diagnostic-tool access in this experiment. It
can suggest that the actor investigate a target, then review the resulting
shared observations in a later review.

### 4.1 Observation envelope

The harness, not the model, supplies observation ID, source, target,
world-availability tick, observation/delivery tick, history revision, and
applicability epoch. Content may include untrusted log text; an envelope does
not make embedded instructions authoritative.

A history revision advances when observations are appended. An applicability
epoch changes only on a predefined invalidating world event, such as recovery
or a change in the incident target. It is not a model-selected score.

New supporting evidence or an unrelated cache warning does not automatically
invalidate all previous guidance. Recovery does invalidate active-incident
guidance under the epoch rule. This is a bounded fixture mechanism,
not general semantic detection of every stale assumption.

Keep hidden world state, future events, case-category labels, expected answers,
and evaluator rationales outside all model inputs. The published worked case
is a development example; it is not a held-out benchmark.

## 5. Component contracts

These are semantic contracts, not a prescribed C# API or finalized JSON schema.
Use bounded values and observation references where possible so the experiment
does not require free-form generation from the fast component.

### 5.1 Actor input

Include the incident scope, current tick and remaining declared budgets,
permitted action/target catalogue, observed history, prior actor actions and
results, and currently applicable guidance. Include the history revision and
applicability epoch captured at turn start.

Do not include a model-visible label such as "needs supervision" or the
schedule of future diagnostic evidence.

### 5.2 Actor decisions

| Kind | Fields and meaning |
| --- | --- |
| Query | One registered diagnostic operation, one permitted target, and only its allowed arguments. |
| Wait | Positive wait duration no greater than four ticks; the harness may wake earlier under section 3.3. |
| Report | Diagnosis hypothesis, cited observation IDs, recommended next step, and explicit uncertainty category. |

Candidate diagnostic operations are the inherited `get_incident`,
`get_service_health`, `query_metrics`, and `query_logs`. The Payments service,
incident identifier, and approved authorization-service dependency must be
bound by trusted metadata. The inherited simulator does not yet implement the
full multi-target development scenario.

Candidate report hypotheses are local-instance issue, dependency-path issue,
dependency-side queue delay, no currently active incident, and unresolved.
Candidate next steps include further local diagnostics, further dependency
diagnostics, investigating the dependency queue, monitoring, or human handoff.
Exact serialized names and case-specific rubric mappings must be fixed in the
implementation handoff without changing these semantics.

The harness constructs trusted identity, timestamps, plan metadata, and action
digests. Models must not provide their own authorization or verification
attestations. Diagnostic queries still go through the applicable existing
verification/policy/gateway path; Wait and Report do not execute operational
tools.

### 5.3 Supervisor input and output

The supervisor receives the same permitted observed history and action history,
captured at review start, with the incident goal, remaining budgets, current
investigative direction, and previous review summaries. It does not receive
the actor's private reasoning trace or an evaluator answer.

Investigative direction means the most recent diagnostic
target/category, or unset before the first query. Derive it from recorded
actions, not from an inferred private intention.

| Output kind | Meaning |
| --- | --- |
| NoChange | No proposed change in investigative direction. Record the completed review; do not wake the actor or silently renew older guidance. |
| SuggestFocus | Recommend a permitted investigative target or diagnostic step, citing observed evidence. |
| RecommendHandoff | Recommend an uncertainty report or human handoff, citing the observed limitation. |

The supervisor may provide a concise structured rationale and uncertainty, but
these are assertions to evaluate, not authority. Never require private
chain-of-thought logging as research evidence.

The harness attaches review ID, captured history revision/epoch, review start,
completion and delivery ticks, expiry, and run identity. Cited observations
must belong to that review's captured view. Confidence cannot waive a guard.

## 6. Guidance lifecycle and application

1. Validate structure, scope, and cited observation membership.
2. Reject guidance from another run, timed-out/cancelled review, or ended episode.
3. Reject guidance whose lifetime has expired or whose captured applicability
   epoch no longer matches the observed current epoch.
4. A valid SuggestFocus or RecommendHandoff replaces the previously active
   actionable guidance. Keep the old item as superseded evidence in the log.
5. For a materially new actionable item, pause new dispatch and increment a
   harness-owned decision generation. Invalidate any pending actor decision,
   request cancellation, and wake a parked actor for reconsideration.
6. Start a fresh actor turn with current evidence and accepted guidance, subject
   to budgets and the selected architecture's blocking rules. A result from an
   older generation cannot query, wait, report, or mutate current actor state.
7. Require the reconsidering actor to reference the guidance and record whether
   it adopts, adapts, or rejects the suggested direction with bounded reasons.
   Do not require obedience or private chain-of-thought. Evaluate the resulting
   action independently.

Duplicate rule: the same recommendation kind, target/step, cited
observation set, and applicability epoch is not a new interrupt. Record its
refreshed provenance without restarting the actor or silently renewing the
actionable item's original expiry. A recommendation citing new evidence is
not a duplicate merely because its target is unchanged. NoChange, rejected,
expired, and duplicate outputs remain observable but do not preempt.

Logical invalidation does not depend on provider cancellation succeeding.
Track obsolete calls until they settle and account for their incurred usage.
All replacement turns count against the existing turn budget; do not create
unbounded retries. A separate cap on outstanding draining calls and its
resource-exhaustion behavior must be chosen before live-model implementation.

An already-dispatched diagnostic may finish. Incorporate its result into the
shared history once; do not claim it was undone. Guidance delivered before an
undispatched Query or Report has priority even if both complete on the same
logical tick. Repeated distinct interrupts may prevent progress; record this
as a potential supervisory failure, not a benefit.

Expiry prevents guidance from entering newly started turns; it does not erase
its historical influence. A turn already in flight can have consumed guidance
that expires before its output arrives. Record that relationship, and apply
normal execution validation and the current applicability-epoch check before
acting. If the epoch changed during the turn, discard its proposed Query or
Report as stale and make the actor ready to reconsider; do not execute it.

Freshness, schema, and policy checks cannot identify every semantically bad
recommendation. Wrong but well-formed in-scope advice can be accepted and can
harm the investigation. That is a research outcome to measure, not a failure
the implementation may conceal through a hidden oracle.

No guidance may change permissions, policy, model weights, tool registration,
or the experiment's budgets. A supervisor is not a safety override.

## 7. Worked scripted timeline

### 7.1 Fixture behavior

This deliberately simple fixture establishes that the scheduling mechanism
works. Its behavior is authored, not evidence of real-model effectiveness:

- The scripted actor prioritizes Report after observing E3, otherwise queries
  the dependency when accepted guidance suggests that focus. Without either,
  it performs local metrics queries when a shared update or wait timer prompts
  it, and uses bounded Wait decisions between updates.
- The scripted supervisor returns SuggestFocus(dependency) when its captured
  view contains E2 but not E3; otherwise it returns NoChange.
- Guidance cites E2, not the unretrieved E3. The actor is intentionally limited
  in this fixture; a real actor is allowed to interpret E2 without help.

### 7.2 Selected checkpoints

The table shows consequential points, not every Wait turn. All event ordering
and durations are governed by section 3. It is the straightforward variant
only; it does not define report times for the ambiguous or recovery variants.

| Tick | World / shared evidence | Actor-only | Blocking supervision | Asynchronous supervision |
| --- | --- | --- | --- | --- |
| 0 | E0 delivered | Starts local diagnostic turn. | Starts review; actor waits. | Starts review and local diagnostic turn. |
| 3 | No new external evidence | Continues under the scripted wait policy. | NoChange review completes; starts first local diagnostic turn. | NoChange review completes without waking a parked actor. |
| 4 | E1 delivered | Responds with a local diagnostic turn when ready. | Local query executes; next review starts and blocks new turns. | Next review starts; actor may respond to E1. |
| 8 | E2 delivered | Script remains locally focused. | Review starts with E2; actor waits after any due turn completion. | Review starts with E2; actor may continue local work. |
| 11 | No E3 notification; a dependency-log query dispatched now would return the successful `AL0` empty projection. | No supervisor to redirect this fixture. | Dependency-focus guidance arrives; starts dependency query turn. | Dependency-focus guidance arrives; wakes the waiting actor, which starts dependency query turn. |
| 12 | `AL12` is collected without a notification. | Script has not requested it. | Query executes, returns `AL12`, and E3 enters history. Review starts with E3 and blocks the report turn. | Query executes, returns `AL12`, and E3 enters history. Review starts, but actor also starts its Report turn. |
| 13 | No new external evidence | Continues scripted local investigation. | Report is still blocked by review. | Report completes; episode ends and pending review is cancelled. |
| 15 | No new external evidence | No supported report from this fixture. | NoChange review completes; actor starts Report turn. | Episode already ended. |
| 16 | E4 delivered to active episodes; epoch unchanged | Can respond, without receiving an evaluator hint. | Report completes before a new review can start; episode ends. | Episode already ended. |
| 24 | Exclusive horizon reached | Unfinished fixture run terminates as timeout. | Episode already ended. | Episode already ended. |

The tick-13 asynchronous and tick-16 blocking reports have been re-derived for
this straightforward scripted actor: guidance starts the dependency Query turn
at tick 11, the Query dispatches and returns `AL12` at tick 12, and the existing
review/actor phase ordering then yields those report completions. These times
are not predictions for AI models and do not apply to the ambiguous or recovery
variants. Do not use this example to claim a measured hybrid speedup. A separate
acceptance fixture must let the actor independently seek E3 and succeed without
guidance.

## 8. Failure and termination behavior

| Condition | Behavior |
| --- | --- |
| Invalid actor output or unknown/forbidden action | Record the rejected decision with reason; no operational effect. Consume its turn and any diagnostic-attempt budget, then allow reconsideration while budgets remain. |
| Successful diagnostic projection has no matching records | Return the currently observable typed result, including a legitimate empty collection. Do not invent values, retry silently, or reveal future evidence. |
| Authored telemetry/access failure | Return the explicit unavailable observation permitted by the existing contract and record the failure condition. Do not disguise it as an empty successful result or use it for normal pre-signal retrieval. |
| Current actor/model dependency fails | Record an infrastructure failure and terminate that run; do not substitute a different actor model or a success-shaped report. Cancellation/failure of an already-obsolete call is recorded against that call, not allowed to terminate or mutate the replacement turn. |
| Supervisor returns invalid guidance | Record rejection, close the review, release blocking wait, and retain only any older guidance that is still valid. |
| Supervisor errors or times out | Record the failure, close the review, and release blocking wait. Continue only under unchanged permitted diagnostic controls; mark the run as supervision-degraded. Never treat failure as a NoChange opinion. |
| New checkpoint during an active review | Skip and record it. Do not create an unbounded backlog. |
| Kill switch or required governance/verifier unavailable | Apply the inherited fail-closed boundary and terminate the experimental run with the control reason. No supervisor advice bypasses it. |
| Attempt would exceed turn, diagnostic, or review cap | Do not start the work; terminate the run as budget exhausted. Record which cap was reached. |
| Valid Report received before horizon | End the episode and evaluate separately; a valid structure does not imply a correct diagnosis. |
| Episode ends with work in flight | Cancel remaining work where supported. Ignore later results for behavior, retain their provenance, and account for incurred usage. |

Evaluation has two views: performance under the intended comparison conditions,
and robustness under faults. Report faulted runs separately and include their
counts; do not silently exclude them or pool degraded supervision with successful
supervision as though the treatment were identical.

## 9. Outcome and measurement contract

Do not collapse everything into a single success score in this version.

| Dimension | Recorded outcome |
| --- | --- |
| Termination | Report, timeout, budget exhausted, governance stop, or infrastructure failure. |
| Diagnostic support | Whether the report's hypothesis is supported by evidence actually observed and cited. |
| Required coverage | Whether the case's required diagnostic evidence was obtained; justified uncertainty is distinct from a fully supported diagnosis. |
| Next-step appropriateness | Whether the recommendation is one of the case rubric's acceptable directions. |
| Temporal relevance | Whether the conclusion and guidance still address the current observed incident, rather than only a historical state. |
| Trajectory | Diagnostic attempts, repeated same-target queries without new evidence, changes in direction, and progress milestones. Repetition is not automatically an error; the case rubric determines whether polling was reasonable. |
| Responsiveness | Evidence availability, observation time, next turn start, next action completion, and time to a predefined appropriate response. Keep discovery delay separate from interpretation/action delay. |
| Guidance influence | Review input/output IDs, delivery, acceptance/rejection/expiry, interrupts, reconsideration acknowledgments, and observable changes in later choices. |
| Interruption overhead | Cancel requests and outcomes, obsolete completions suppressed, replacement turns, discarded computation/usage, time to resumed action, and failure to progress under repeated interrupts. |
| Resources | Actor/review counts, diagnostic counts, synthetic durations, and separately real token/usage and wall-clock measurements when those exist. |
| Constraints | Rejected attempts and any unauthorized operational effect. First-phase runs do not establish production approval or remediation guarantees. |

For the development case, E2 supports a dependency-path hypothesis; E2 plus E3
or a predefined equivalent supports dependency-side queue delay. Neither
establishes the deepest cause of the queue. Do not reward an unsupported early
guess merely because it matches hidden world state.

The evaluator runs after episode termination and does not tell the actor whether
it is "getting warmer." Acceptable hypotheses, evidence requirements, alternative
next steps, and uncertainty cases must be defined before held-out evaluation.
Numerical response deadlines, non-inferiority margins, worthwhile-effect
thresholds, sample size, repeated-run seeds, and aggregate weighting remain
research-protocol decisions, not implementation choices.

For the ER-1 development, ambiguous, and recovery cases, the acceptable
hypotheses, evidence and citation requirements, next-step acceptability,
uncertainty consistency, and recorded neutral facts are now fixed in the frozen
rubric `C6-rubric-1` ([C6_EVALUATION_PROPOSAL.md](C6_EVALUATION_PROPOSAL.md)).
Held-out cases, broader evidence equivalence, scoring weights, aggregate
weighting, and the statistical decisions above remain deferred.

For live calls, record reported usage including cancelled work where available.
Missing usage is unknown, not zero. A token price table needs provider/model
version and date. Do not invent cost savings from the scripted fixture.

## 10. Acceptance examples for the later build

| ID | Setup | Expected observable result |
| --- | --- | --- |
| A01 | Straightforward primary scripted fixture | Asynchronous report at 13, blocking report at 16, actor-only timeout at 24 under section 7's re-derived authored behavior. No operational writes. These report times do not apply to the ambiguous or recovery variants. |
| A02 | Same events, competent scripted actor independently follows E2 | Actor-only can obtain E3 and report correctly. No hidden supervisor requirement in the evaluator. |
| A03 | `query_logs(authorization-service)` dispatches at 11, then a distinct query dispatches at 12 | The first returns successful `AL0` with an empty log collection and no evidence IDs. The second returns `AL12` with E3's stable, ordinal-sorted evidence IDs. Both attempts consume existing budgets. Neither earlier input contains E3, and tick 12 has no availability notification. |
| A04 | Review starts at 8; new evidence is observed at 9 | Review input remains its tick-8 snapshot. Referencing the tick-9 observation in its output is rejected. |
| A05 | Recovery notification at 10 increments applicability epoch; tick-8 guidance arrives at 11 | Reject guidance for epoch mismatch. `query_metrics(authorization-service)` returns `AM10R` from tick 10; at tick 12, `AM12R` and `AL12R` retain tick-10/current records alongside timestamped pre-recovery history under sampled epoch 1. Do not attribute recovery to either agent. |
| A06 | In-scope, fresh, but wrong supervisory advice | The harness may accept it; evaluator records whether it worsens later behavior. No oracle silently removes it. |
| A07 | Advice proposes a production write or an unknown target | Reject it as outside the first experiment's catalogue. No write-capable tool is invoked. |
| A08 | Review starts at 8, completes at 13 | Skip checkpoint 12 because review is still active. No queued catch-up review; next eligible checkpoint is 16. |
| A09 | Review starts at 8, response arrives at 14 | Timeout takes precedence at 14. Late response cannot influence behavior; record degraded supervision and release blocking wait. |
| A10 | Guidance from review started at 8 is still active at 16 | It expires before new turns at 16. Existing turn snapshots retain historical provenance; independent epoch/execution checks still apply. |
| A11 | Shared notification arrives while a turn producing Wait is in flight | Do not park the actor as if no update arrived. Blocking review may still defer its next turn. |
| A12 | Report finishes at 24, or guidance arrives after a run ended | Do not accept the report at the exclusive horizon or apply post-termination guidance. Record the late result without altering the outcome. |
| A13 | Malformed output, exhausted budget, unavailable verifier, or model failure | Observable failure reason and accounting; no silent retry, alternate model, fabricated observation, or successful completion. |
| A14 | A long actor turn is pending when fresh actionable guidance arrives | Invalidate its generation immediately, request cancellation, and start reconsideration when permitted. An old completion cannot dispatch a tool or submit a report, even if cancellation fails. |
| A15 | Guidance acceptance and an undispatched actor Query or Report occur at the same tick | Process the interrupt first; discard the obsolete decision. Record the deterministic ordering. |
| A16 | Diagnostic was dispatched before guidance arrived | Record its eventual result exactly once. The interruption affects future decisions, not a fictional rollback of the dispatched operation. |
| A17 | NoChange, duplicate, expired, or invalid guidance arrives | Record the review outcome without cancelling a useful actor turn. New evidence can make a same-target recommendation a distinct actionable interrupt. |
| A18 | Repeated distinct actionable guidance arrives before replacement turns finish | Record interruptions, discarded work, and lack of progress; enforce budgets without silently disabling interrupts to make the result favorable. |
| A19 | Framework review invocation is deliberately delayed | Actor processes an external event and completes a diagnostic before review completion; then the result can interrupt a subsequent pending actor decision. Record any framework serialization limitation. |
| A20 | The same unchanged diagnostic content is retrieved twice | The observations have distinct diagnostic IDs, observation IDs, observed ticks, and history revisions, but retain identical neutral ordinal-sorted evidence IDs and content. |
| A21 | Ambiguous development variant | E1 contains the approved `payments-api-03` CPU symptom and follow-ups; E2 is not pushed, but `PL8` is queryable from tick 8. No evaluator-only causal explanation or hidden variant label enters component input. |

These are mechanism acceptance examples. They do not replace empirical
comparisons on independently designed held-out scenarios.

## 11. Reuse boundaries and handoff readiness

Reuse the existing canonical plan/action contracts, verification boundary,
policy/gateway, bounded tool metadata, and audit concepts rather than building
a parallel authorization path. Preserve the existing deterministic demo as a
baseline instead of silently changing its behavior.

The current simulator is not the evolving multi-target environment specified
here, and existing evaluation JSON supplies observations rather than running
this experiment. Those are explicit implementation gaps, not completed features.
Existing formal claims do not automatically cover the specified scheduler or
guidance lifecycle.

Do not extend strict plan schemas merely to carry research metadata without
an explicit schema/versioning decision. Prefer separate run/observation/guidance
records linked to existing plan/action identities where needed.

Remaining handoff work:

- Define draining-call limits and confirm the framework parallelism/interruption
  feasibility arrangement before live integration.
- Fix serialized action/report/guidance names and case-specific rubric mappings.
- Translate the reviewed timeline and acceptance examples into implementation
  tasks without presenting their expected scripted outputs as empirical results.
- Write the research protocol's live-model methodology and statistical decisions
  before authorizing comparative claims; these need not block a separately
  authorized scripted mechanism build.
- Create a sequenced implementation brief with explicit acceptance boundaries,
  approved specification revision, and non-goals.

The later implementer may choose code organization and normal engineering
details. It must not invent research thresholds, optimize against hidden
evaluation cases, bypass controls, or turn the study into a platform.

## 12. Approved amendment: within-incident working memory

The [amendment history](governance/process.md#working-memory-amendment-history)
records the version, approval, and implementation scope. The
[contract reference](T1_CONTRACT_PROPOSAL.md) defines its serialized representation.

Supervision proposes changes to **shared working memory**, not merely transient
messages. Accepted changes may shape
several subsequent actor turns in the same incident. Interruption makes an
accepted actionable change effective promptly; it is not the memory itself.

Accepted C1-C3 retain uncalibrated low/medium/high uncertainty, allow empty
Report citations structurally while requiring observation citations for claims
and actionable operations, and accept the seven diagnostic operation/target
pairs. Citations establish provenance, not truth. Accepted C5 counts started
turns and recognizable current Query attempts; obsolete output creates no new
attempt. Accepted C9 derives recorded direction from the latest dispatched
diagnostic, separately from the recommended direction in memory.

### 12.1 Four distinct layers

| Layer | Meaning and permitted change |
| --- | --- |
| Immutable observations and action history | Actual delivered evidence, decisions, attempts, and outcomes. Append records; never edit a prior observation or make a belief into an observation. A historical action's later completion is a new linked record, not rewritten history. |
| Provisional incident beliefs | Evidence-linked hypotheses with explicit provenance and lifecycle state. They may be mistaken, contested, replaced, retracted, or invalidated. Structural acceptance means admissible shared context, **not factual truth**. |
| Recommended investigative direction | An advisory target, diagnostic step, uncertainty report, or handoff recommendation, with its own evidence/belief references and guidance lifetime. Distinct from both incident beliefs and the direction actually taken in recorded actions. |
| Versioned working-memory snapshots | Immutable, coordinator-issued views of belief states and recommended direction. Both actor and supervisor consume a particular revision alongside their captured observation/action history. No invocation reads a mutable shared session while running. |

Memory is scoped by run and incident, begins empty, and is closed at episode
termination. Its persisted records are research evidence, not a memory to load
into another incident. No cross-incident learning, generic memory service,
retrieval platform, model-weight update, reward optimization, or claim of RL
training is introduced.

The accepted first scope (M1) is supervisor-proposed semantic updates; the actor
consumes memory and can explicitly adopt, adapt, or reject advice in its
decisions, but does not independently commit beliefs. Actor diagnostics still
append actual observations through the existing governed path. Do not
automatically turn actor Reports or supervisor text into facts. The treatment
is supervision plus within-incident memory adaptation, not memory's isolated
benefit.

### 12.2 Independent revisions and snapshot capture

Keep four coordinates distinct:

- **History revision:** advances when observations are actually appended;
  action/event sequence records the associated decisions and execution history.
- **Memory revision:** advances once for a committed change to the shared belief
  or recommended-direction state, including a lifecycle change. An observation
  append alone does not manufacture a belief or increment memory revision.
- **Applicability epoch:** changes only on a predefined invalidating world
  event. It is not a model-selected confidence or relevance score.
- **Decision generation:** changes when the coordinator invalidates pending
  actor decisions. Not every memory revision requires such an invalidation.

Capture history revision, action/event cutoff, memory revision, epoch, and
generation together at invocation start. The complete memory snapshot is
immutable and identified by `(runId, memoryRevision)`. Later memory commits,
new observations, or guidance expiry cannot alter an in-flight input.

Attach the **consumed** memory revision to every actor invocation, returned
decision, validation/disposition, dispatch, and Report through trusted
invocation provenance. Also record the current revision when handling the
result. Never relabel an old result with the newest revision. Exposure to a
belief, an actor's declared reliance, and independently observed behavior
change are separate evidence; a snapshot reference alone does not prove
causal influence or successful correction.

### 12.3 Proposals from older revisions (M2)

Accepted M2 requires an all-or-nothing, exact-base rule:

1. Bind an update's base memory revision to the supervisor invocation's actual
   captured snapshot. A component echo of that revision cannot choose a newer
   base or another run.
2. At the acceptance boundary, recheck run, review eligibility/deadline,
   applicability epoch, and that the base equals the current memory revision.
3. If the base is older, record `memory-revision-conflict` and retain the
   proposal as rejected provenance. Do not overwrite, partially apply, merge,
   silently rebase, or selectively salvage its direction change.
4. A genuinely newer proposal requires a newly captured eligible review.
   Do not insert an extra review, automatic retry, or catch-up outside the
   existing checkpoints and budgets to repair the conflict.
5. Re-delivery of an already processed invocation/result is an idempotent
   duplicate delivery, not a second commit. A fresh proposal on an old base
   does not become eligible merely because part of its content is familiar.

New observations with an unchanged memory revision do not by themselves make
the base conflict. Citations must still belong to the review's older captured
history, and epoch/deadline checks still apply. The coordinator does not decide
whether newer evidence makes a claim semantically wrong through a hidden oracle.
Record both base/current revisions and relevant rejection conditions.

This conservative rule can reject otherwise useful advice, including a proposal
that touched a different belief. That cost must remain visible. Concurrent
merge/rebase policies are not part of this amendment.

### 12.4 Belief lifecycle and disagreement (M3)

Use bounded incident hypotheses and targets, not arbitrary memory keys or
instructions. Every new/replacement claim cites observations available to its
proposing review. Existing belief references may explain what is being corrected
but cannot substitute for observation provenance or create circular evidence.

| State / operation | Approved rule |
| --- | --- |
| Provisional | An accepted, evidence-linked candidate in the current epoch. Never synonymous with verified, true, or authorized. |
| Contested | Explicitly declared conflicting current claims remain visible together, marked contested. Neither is automatically promoted by recency, confidence, or majority. |
| Superseded | A current-base correction explicitly replaces a named belief with a new immutable claim and links the predecessor. Old snapshots retain the old claim; the new snapshot marks it superseded. |
| Retracted | A current-base proposal withdraws a named belief without replacing it. Retain the claim, reason, evidence, and retraction event; do not delete history. |
| Invalidated | A predefined epoch change makes the claim inapplicable to the current incident state. Retain it as historical, not as a currently applicable premise. Invalidation is not proof the original claim was false. |

Contradiction detection is not an oracle: the proposer explicitly identifies
conflicts, or a previously declared deterministic case rule may identify
applicability invalidation. Do not silently infer that every pair of different
failure hypotheses is mutually exclusive. Undeclared semantic disagreement
can remain undetected and is a limitation to report.

An explicit correction can resolve a declared conflict by superseding or
retracting a named contender. If a conflict loses its last current counterpart,
the remaining claim is still provisional, not thereby proven. Terminal beliefs
are never reactivated in place. A later reassertion is a new provisional claim
from a fresh eligible current-base review, with nonempty captured-observation
citations, concise rationale, and explicit lineage to the latest matching
terminal predecessor. The predecessor remains terminal and immutable.
Supersession lineage records a transaction that retires an eligible predecessor
while selecting a replacement; reassertion lineage links a new claim to a
predecessor that was already terminal in the base snapshot and does not modify
that predecessor.

A direction explicitly supported by a belief cannot quietly retain that premise
after it is retracted, superseded, invalidated, or made contested. A semantic
update must also clear or validly replace such a direction, or the transaction
is rejected. Coordinator-driven epoch invalidation clears dependent direction
as part of the same transition. This guards reference consistency, not truth.
It does not erase the direction the actor previously took in its action history.

### 12.5 Guidance expiry is not belief validity (accepted M4)

Retain the reviewed guidance lifetime, review-start tick + 8 exclusive, for an
active recommended direction. Expiry removes that active recommendation from
new inputs; a duplicate, NoChange, or a belief-only update cannot extend it.
A direction change and a belief change are distinct parts of a proposal.

A belief has no automatic eight-tick TTL in this candidate. It remains
provisional within its incident/epoch until superseded, retracted, invalidated,
or closed with the incident; explicit conflicts mark it contested. Therefore a
correction can continue shaping later turns after the associated direction has
expired. Its origin, age, evidence, and status remain visible. This persistence
is an **accepted working-memory design choice**, not a claim that it was already
implemented or present in the original v1.0 contract.
It can preserve a mistaken belief too; no new expiry threshold is invented.

Expiry of a direction creates a new memory revision with that recommendation
removed from the active slot, but **does not on its own increment decision
generation, wake a parked actor, or cancel an in-flight turn**. This preserves
the v1.0 distinction between expiry and a fresh actionable interrupt. An older
turn can retain its historical input; generation/epoch/operational checks still
apply. Consequently, memory-revision inequality alone is not a dispatch veto.
It is an exact-base veto on a proposed memory write.

Historical guidance and terminal beliefs remain identifiable in the evidence
record, clearly labeled and never smuggled back into the active recommendation
slot. All shared memory freezes at incident termination; draining results
cannot reopen it or change the frozen outcome.

### 12.6 Atomic actionable acceptance and interruption (accepted M5)

Classify every non-no-op supervisor change to the actor-visible
belief state or recommended direction as actionable. The coordinator computes
this classification from the validated transition; the supervisor cannot choose
`actionable: false` to avoid reconsideration. No-op/duplicate provenance alone
does not change memory or generation. Direction expiry alone is the explicitly
non-interrupting lifecycle transition described above.

For an actionable update, one coordinator critical section must:

1. Revalidate the proposal against current memory, history membership, epoch,
   review eligibility, scope, and consistency of all proposed operations.
2. Construct the complete successor memory revision without mutating any old
   snapshot, observation, action, belief claim, or proposal.
3. Record the accepted transition and atomically publish **both** the new
   memory revision and the incremented decision generation, invalidating
   pending old-generation decisions and recording the reconsideration trigger.
4. Leave no dispatch window in which a decision from before the actionable
   commit is still eligible while the new memory is already visible.

Failed validation changes neither memory nor generation. If the required
record/control boundary is unavailable, fail closed; do not publish half of
the transition or fabricate a successful commit.

After logical invalidation, request cancellation best-effort without holding
the dispatch lock across cancellation callbacks or inference. Reconsideration
uses the new snapshot, subject to the existing readiness, blocking, and budget
rules. Recheck eligibility after asynchronous verification/policy and at actual
dispatch. A late Query, Wait, or Report cannot act, even when cancellation fails.
Already-dispatched diagnostics may finish exactly once and retain their actual
observations.

Apply semantic review commits in the existing review-processing phase before
actor-result dispatch. Predefined epoch invalidation occurs in world-delivery;
if it changes actor-visible beliefs/direction, publish its memory
change and actor invalidation atomically there as well. Ordinary evidence
delivery does not become an extra supervisory interrupt. This is accepted
design, not an implemented T2/T3 change to section 3. The exact trigger
contracts and no-op classifier are the approved C7/M7 and C4/M6 rules.

### 12.7 Illustrative cases, not measured outcomes

**Correction across multiple turns.** A fresh review commits memory revision 2,
superseding a provisional local-instance belief with an E2-linked
dependency-path belief and recommending dependency investigation. An actor
turn pending on revision 1 is invalidated. The next Query, a later Wait, and
a subsequent diagnostic turn can all consume revision 2 while history revisions
advance separately. When the direction expires, revision 3 clears only that
recommendation; a later turn still sees the provisional dependency belief.
Record each turn's consumed revision and actual choice, rather than claiming
obedience or correctness from the memory update itself.

**Mistaken belief revised.** A review cites E0 and proposes that a local instance
is responsible. The claim is structurally admissible but wrong in this authored
case; the coordinator must not consult the answer key to reject it. After E2 is
observed, a fresh review on the current memory revision explicitly supersedes
that claim with a dependency-path hypothesis. The old claim and the turns it
influenced remain in historical snapshots. If the correction instead declares
a conflict without resolving it, show both claims as contested rather than
quietly choosing a winner. No new E3 access rule or evaluation score is implied.

**Older output cannot restore newer state.** An already accepted direction from
a tick-8 review expires at tick 16. A review that began at tick 12 on memory
revision 2 is deliberately delayed until 17, before its timeout at 18. Expiry
has published revision 3, with the direction inactive and beliefs retained.
Its base-2 memory proposal is rejected against revision 3, even though the
review itself is not timed out. A late replay of an earlier belief proposal
likewise cannot undo a later correction: its result was already processed or
its base is stale. Neither example requires two concurrently active reviews.

**Terminal belief reasserted without revival.** Belief A is retracted and
remains an immutable terminal record. A later scheduled review captures the
then-current memory revision and current-epoch observations, cites those
observations, explicitly names A as the latest structurally matching terminal
predecessor, and gives a concise rationale. If all ordinary scope, epoch,
evidence-membership, conflict and direction-dependency checks pass, the
coordinator creates belief B as a new provisional claim with reassertion
lineage A <- B, commits one actionable revision, and records the resulting
trigger and interrupt. It never changes A back to provisional.

**Non-effects remain distinct.** Re-delivery of the old result that created A
is duplicate delivery and cannot commit. A new proposal captured on a stale
base is rejected under exact-base validation. If an eligible current belief
already has the same structural claim key, a proposed reassertion aliases it
and creates no update or interrupt. If B is later retracted and a later eligible
review reasserts it as C, retain A <- B <- C, each lifecycle event, each
actionable trigger/interrupt, suppressed work, and all existing turn/review
budgets so oscillation and its costs remain measurable.

These cases are approved mechanism obligations, not new A01 timing claims.
The tick-13/tick-16 worked outcomes in section 7 remain v1.0 baseline examples;
their behavior under an approved memory amendment would need explicit mapping
and later validation, not an assumption of unchanged results.

### 12.8 Decision status and boundedness

The approval table and deferred evidence-equivalence decision are preserved in
[the working-memory decision record](governance/process.md#working-memory-decision-record).

Preserve the current episode, turn, diagnostic and review budgets; one eligible
actor turn and one active review; fixed authority; isolated invocations; and
the absence of live-provider claims. Memory operations do not execute tools.
There is no new memory daemon, embedding store, multi-incident cache, training
loop, automatic truth promotion, hidden scoring rule, or implementation here.

### 12.9 Interpretation of the memory-enabled comparison

WM-1 studies supervision and within-incident shared-memory adaptation as a
combined treatment. Under the accepted supervisor-only semantic-write scope,
the actor-only baseline has the same permitted observed history and actor
configuration, but no supervisor-generated shared belief updates. This is not
a comparison that isolates persistent memory from additional reasoning or
interrupt-driven coordination.

If the combined design improves a trajectory, attribute the result to that
design under the tested conditions, not independently to memory, interruption,
or reinforcement learning. Consumed-revision traces and declared belief use
explain exposure and behavior; they do not by themselves establish causation.
Separate memory-only, transient-feedback, or other ablation comparisons would
be needed to isolate mechanisms and are not added to the initial scope here.

Between blocking and asynchronous supervision, hold configurations, memory
rules, and the external event schedule constant. Timing-induced differences in
observations, accepted revisions, review counts, and costs remain part of the
architectural effect and must be reported. Do not claim that the two runs
consumed identical histories or differed only in model inference speed.

Report benefits and costs by predefined case category, including no-benefit
and harmful-supervision cases. Specifically retain counts and traces for
exact-base rejection after expiry-only revisions, incorrect beliefs persisting
after direction expiry, repeated retract/reassert oscillation, and repeated
meaningful updates causing interruption pressure. This interpretation does not
approve C6 mappings, add numerical thresholds, or authorize T2-T5.

### 12.10 Duplicate and no-op normalization (C4/M6: accepted)

Use deterministic structural equality, never an LLM or hidden answer key.
Distinguish three cases:

1. **Result redelivery:** the same bound invocation/result identity has already
   been processed. Record duplicate delivery; do not reevaluate against newer
   state, charge another attempt, create IDs, or commit again. Different bytes
   under an already settled identity are a conflicting delivery, not a new opinion.
2. **Repeated direction:** compare recommendation kind/target/operation/arguments,
   sorted observation citations, and epoch against previously committed directions
   in this run, including expired/cleared/superseded directions. Ignore fresh IDs,
   review time, rationale, uncertainty, and supporting-belief IDs for this key.
   A repeated direction is not allowed to renew or reactivate its predecessor.
3. **Transaction no-op:** after the normalization and full validation below,
   the effective belief/direction/conflict state is unchanged. Record provenance
   without a memory revision, generation increment, or interrupt.

Approved belief equality key: `(epoch, targetId, hypothesis, sorted unique
observationIds, uncertainty)`. Ignore generated IDs, proposal-local keys,
creation timestamps and proposing review. A changed uncertainty or observation
set is a structural state change, not evidence of a more correct belief; record
interrupt pressure from such changes rather than applying semantic deduplication.

An add equal to an eligible existing belief aliases that belief instead of
creating another. Equal adds within one transaction coalesce deterministically.
A replacement equal to its own target aliases that target and drops the
replacement; a replacement equal to another eligible belief can supersede the
target by that existing belief without inventing a new claim. A plain add that
exactly matches only a terminal belief is rejected because explicit reassertion
lineage is required.

A `reassert-belief` operation explicitly references the latest terminal
predecessor with the same run, current epoch, target and hypothesis, selected by
state-change revision then belief ID. The predecessor must already be terminal
in the captured base; a belief retracted or superseded earlier in the same
transaction cannot be used. The new claim supplies its own nonempty captured
observation citations and concise reason. It receives a new ID only after the
entire successor validates, remains provisional rather than true, and does not
inherit predecessor conflicts or direction dependencies.

Within-transaction equality is resolved before ID allocation. If a reassertion
matches an already eligible current belief, it aliases that belief; fresh IDs
or lineage intent alone cannot create an update. Equal proposed claims coalesce
only when their lineage is compatible. An alias that the same transaction then
retires is inconsistent. No conflict edges automatically transfer during
replacement or reassertion.

Normalize only after raw shape/scope/citation/reference validation and the
accepted exact-base check. Remove a repeated `set-direction` operation while
leaving the current active slot unchanged, including null if it already expired.
Retain genuine belief changes. Then compute and validate the **entire normalized
successor**, including conflict symmetry and all surviving direction dependencies.
If invalid, reject the whole transaction: no automatic clear, rebase, partial
belief commit, or selective salvage. Record the original output, normalization
decisions/aliases, duplicate direction identity, reassertion lineage and
validation failure. Reassertion does not weaken run, epoch, scope,
evidence-membership, exact-base, conflict, or direction-dependency checks.

Examples:

- Duplicate direction plus a correction to an unrelated belief: drop the
  direction write, commit the correction, retain the old direction's expiry.
- The duplicate is historical and the active slot is empty: commit a valid
  correction while leaving the slot empty, not reactivating the old direction.
- The current direction depends on the belief being superseded: dropping a
  duplicate direction write leaves a broken dependency, so reject everything.
  An explicitly proposed clear instead, or a genuinely distinct, valid direction,
  can form a valid whole transaction at a later eligible review.
- An add or self-replacement differing only in generated/local IDs is a no-op,
  not an interrupt. Proposer text ordering is not a tie-breaker for truth.
- Retracted A plus a later fresh current-base `reassert-belief` creates new
  provisional B with A <- B lineage and one ordinary actionable interrupt.
- Re-delivery of A's old creation result is duplicate delivery; a fresh
  reassertion proposal on a stale base is an exact-base rejection.
- A reassertion identical to an eligible current belief aliases it and creates
  no artificial change. Repeated valid retract/reassert cycles remain visible
  oscillation and consume the existing reviews, turns, cancellations and
  suppression costs; no extra review or silent retry is introduced.

### 12.11 Deterministic conflict transitions (M3: accepted)

Current conflict relationships are an undirected graph over eligible beliefs
(provisional or contested, in the current epoch). No self-links, dangling links,
or duplicate edges are allowed. A declaration adds both endpoint relationships.
A nonterminal belief is contested exactly when it has a current incident edge;
otherwise it is provisional.

Retraction, supersession, or invalidation removes that belief and all its edges
from the current graph in the successor snapshot. Historical claims/snapshots
remain unchanged. A replacement does **not** inherit predecessor edges; any
conflict with it must be declared explicitly. Validate resulting direction
dependencies after normalization and graph transitions; the proposal must clear
or consistently replace a direction whose premise became ineligible/contested.

Three-belief example: A conflicts with B, and B conflicts with C; A does not
conflict with C. All three are contested. Retracting B removes A-B and B-C:
A and C become provisional, B is historical/retracted. Neither A nor C became
true merely because the conflict disappeared. If B is instead replaced by D,
D starts without those edges unless explicitly declared; that is still not
evidence that D is correct. A direction supported by a newly contested A
must be cleared/replaced in the same proposal or the proposal is rejected.

### 12.12 Latest actionable reconsideration trigger (C7/M7: accepted)

Every actionable commit creates an immutable coordinator-owned trigger linked
to its update, committed memory revision, and new decision generation. Retain
the prior-trigger chain and all earlier events as provenance. A coordinator
pending-trigger pointer selects the **latest** required trigger.

At actor start, atomically capture the current memory snapshot and that pending
trigger. Starting a turn does not acknowledge it. A valid current actor decision
must reference the captured trigger in `memoryDisposition`; only acceptance of
that disposition clears the pending pointer, and only if it still identifies the
same trigger. Malformed, rejected, or obsolete output cannot clear a newer trigger.
An ordinary turn starting without a pending trigger uses null disposition.

Several accepted updates before restart therefore require acknowledgment only
of the latest trigger, not repeated callbacks for every intermediate state.
Earlier triggers remain linked in the input provenance. Expiry alone creates no
new trigger: the latest pending trigger is still acknowledged against the **current**
snapshot, with its associated direction explicitly expired/absent and any
surviving correction still provisional. Do not reinstall old advice to explain it.

A later actionable supersession/invalidation creates a newer trigger and replaces
the pending requirement. Its effect-state view shows which old beliefs/directions
are now terminal, contested, or still applicable. If only direction expiry removed
the triggering effect, a disposition can state `trigger-no-longer-applicable`
without pretending the direction is active. Every result retains the memory
revision it actually consumed; an old turn is never relabeled.

The complete trigger, disposition, event and terminal-record field definitions
are in the approved consolidated contract baseline. These lifecycle rules and
atomic invalidation are approved for T1 contract-level implementation.

## 13. Approved amendment: realistic diagnostic retrieval

| Field | Value |
| --- | --- |
| Amendment ID | ER-1 |
| Date | 2026-09-18 |
| Status | Approved authoritative baseline; T2-only implementation authorized 2026-09-18 |
| Scope | Development diagnostic retrieval, notification delivery, and a recovery variation |
| Implementation | T2 code and focused tests authorized; contracts and scenario data unchanged |

The authoritative [ER-1 fixture specification](ER1_FIXTURE_PROPOSAL.md)
supplies the approved synthetic payloads, neutral evidence IDs,
timing/provenance mappings, variant differences, recovery responses, and DTO
compatibility. The initial fixture approval established the documentation
baseline only; a later 2026-09-18 decision authorized T2 against that baseline.

### 13.1 Purpose and baseline interaction

Normal successful queries should return currently observable records, not a
fixture-authored signal that relevant evidence will appear later. Absence of a
particular record is not an access failure or a promise of future evidence.
The model receives measurements and provenance, not evaluator interpretations.

This amendment replaces the superseded pre-signal behavior in section 4 and
A03. The straightforward development case remains, with a separately specified
ambiguous-investigation variant. Exact evidence equivalence and outcome rubrics
remain deferred.

### 13.2 Query-to-evidence table

Times below refer to diagnostic dispatch under the existing sampling rule,
not the start of the actor's reasoning. Later evidence does not modify an
in-flight input snapshot. The seven operation/target pairs remain unchanged.

| Operation | Target | Initially observable | Later observable |
| --- | --- | --- | --- |
| `get_incident` | `INC-1042` | From tick 0: incident identity, affected service, opening time, and current status. | Status changes and their timestamps; no inferred cause or future event schedule. |
| `get_service_health` | `payments-api` | Current instance readiness and available restart history. | Updated readiness and restart records, as defined by the fixture. |
| `query_metrics` | `payments-api` | Available error-rate, latency, CPU, and memory samples, including pre-incident comparison windows. | Additional timestamped samples as they become observable, without an explanation of which measurements matter. |
| `query_logs` | `payments-api` | Existing application records for the defined query window; no timeout records before they become observable. | From tick 8: request-linked authorization timeout records. From tick 16: cache-warning records and comparable earlier-window records, with their time windows distinguished. |
| `get_service_health` | `authorization-service` | Current readiness and available health history. | Updated health observations, without interpreting readiness as evidence of normal request latency. |
| `query_metrics` | `authorization-service` | Currently collected dependency latency and request-count samples. In the recovery variation, the tick-10 sample is returned from tick 10. | From tick 12: delayed pre-recovery samples are added while the tick-10 sample is retained; metrics contain no queue-wait decomposition. |
| `query_logs` | `authorization-service` | Currently collected request-processing records; no future queue-delay records. | From tick 12: E3, linked arrival, processing-start, and processing-end records exposing the queue delay. |

The canonical retrieval route for E3 is
`query_logs(authorization-service)`. General dependency latency is not assigned
the same evidence identity as the request-linked queue-delay records.
This route does not settle all possible equivalent evidence or scoring rules.

The fixture specification fixes query windows, initial and subsequent records,
numerical values, collection timing, stable evidence identities, and exact
operation/target projections. The E0-E4 aliases remain narrative labels, not
complete diagnostic payloads or model-visible evidence IDs.

### 13.3 Successful empty results and actual unavailability

A successful log query may return an empty collection for its defined window
if the fixture establishes that no matching records have been collected.
Do not fabricate normal measurements or use empty success to conceal an
access/telemetry failure.

The existing `unavailable` branch is reserved for an explicitly modeled
diagnostic-access or telemetry-availability condition, not the normal absence
of a matching record. The current wire vocabulary still supports only
`reason: "not-yet-available"` for that explicit branch; ER-1 adds no failure
codes and does not authorize labeling an unrelated failure with that reason.

Available telemetry freshness or completeness information can legitimately aid
reasoning. The requirement is no privileged hints from the scenario author,
not a guarantee that every kind of missing evidence is indistinguishable.
Do not disclose the next scheduled collection time or future fixture contents.

### 13.4 Notification delivery and variant separation

| Tick | Straightforward variant | Ambiguous-investigation variant |
| --- | --- | --- |
| 0 | Push the incident alert: errors and latency rise. | Same initial alert. |
| 4 | Push the approved three-ready-instance and aggregate resource measurements. | Push the approved `payments-api-03` CPU/memory symptom; CPU is 87% at tick 4 and has the specified tick-8/12/16 follow-ups. |
| 8 | Push the timeout summary; underlying records are also queryable. | Timeout records become retrievable through `query_logs(payments-api)`; no timeout-summary notification. |
| 12 | Dependency metrics and E3 logs become observable on request. No notification announces this availability. | Same retrieval boundary, also without a notification. |
| 16 | Deliver a cache-warning digest with records and comparable historical counts. | Same presentation rule; no description of the signal as a distraction or explanation of its relevance. |
| 24 | Existing exclusive episode horizon. | Same horizon. |

Record evidence-content choices separately from notification-policy choices.
This pair of variants explores different conditions; it does not isolate the
causal effect of ambiguity versus notification delivery. Do not add new
experimental arms or claim a controlled ablation on that basis.

The ambiguous symptom's evaluator-only explanation is fixed in the fixture:
the local CPU rise is a consequence of timeout handling and retry/log work,
not a model-visible causal label. The actor may diagnose either case effectively
without supervision.

### 13.5 Evidence presentation and provenance

| Model-visible data | Evaluator/development interpretation only |
| --- | --- |
| Readiness states, restart timestamps, and resource samples over specified windows | Whether these weaken or support a local-fault explanation |
| Request identifiers and timeout records | Whether dependency investigation is an appropriate next step |
| Arrival, processing-start, and completion timestamps | Whether queue delay is sufficiently supported as the failure domain |
| Warning records and counts across comparable windows | Whether pursuing the cache is justified |
| Observations and declared remaining budgets | Future schedule, hidden cause, scenario category, acceptable-answer rubric |

Queries must expose the data needed for the intended comparison. For example,
an unchanged warning rate requires accessible earlier and current observations,
not a sentence saying "the rate is unchanged and unrelated to this incident."

Distinguish underlying event time, collection/availability time, dispatch-time
sampling, and delivery to observed history. Tick 12 is an observability boundary,
not necessarily the onset of the underlying queue delay. Historical records
retain their original event times even when delivered later.

Evidence IDs identify the actual returned content and are neutral,
ordinal-sorted sets. Repeated retrieval creates a new observation record but
retains unchanged evidence identity and is not independent corroboration.
Metric and log DTO arrays remain chronological.

Under approved Q-ER1-1, fixed query windows and per-record collection times are
harness-owned fixture metadata under the existing contracts. Both components
receive the projection semantics and synthetic source-time convention; returned
records retain actual source timestamps; aggregate `availableTick` is not every
record's collection time; and future schedules, hidden variant labels, and
evaluator interpretations remain outside component input. Actor-selected query
arguments remain `{}` and no closed DTO fields are added.

### 13.6 External-recovery variation

The recovery variation has externally caused recovery at tick 10, after E2
becomes observable and before the first possible E3 retrieval. It does not
revise every episode's schedule.

- Deliver timestamped recovery observations and advance the applicability epoch.
  Do not attribute external recovery to an actor or supervisor.
- Record observed recovery separately from administrative incident closure.
- Authorization metrics return `AM10R` from tick 10. At tick 12, `AM12R` adds
  delayed pre-recovery samples while retaining the tick-10 sample.
- E3 becomes observable at tick 12 as evidence about pre-recovery requests.
  Its timestamps must not present those requests as ongoing queue delay.
- Retain old input snapshots and historical records unchanged. Apply existing
  epoch rules to pending guidance; recovery does not erase the earlier incident.
- The cache digest may still arrive at tick 16 with its historical comparison
  windows; it must not imply a new causal relationship.

Historical records retrieved after recovery keep their source timestamps while
the observation envelope carries the epoch sampled at dispatch. A result
sampled before recovery and delivered later retains its old content and epoch;
a current post-recovery retrieval is not delayed delivery of that old sample.

### 13.7 Query cost and acceptance

Retain the existing 12-diagnostic-attempt budget, one-tick actor turns, zero
additional diagnostic execution ticks in the scripted default, and exclusive
tick-24 horizon. Successful queries, including polling that yields no new signal,
consume the existing attempt and turn budgets. No new polling penalty or
automatic early-stop policy is introduced. Record repeated retrieval and assess
its usefulness in context.

Acceptance examples A01-A05 and A20-A21 demonstrate:

- A query before tick 12 returns ordinary currently observable records, not E3
  or a scheduled-unavailability hint. At or after tick 12, the prescribed
  dependency-log query can return E3.
- Tick 12 produces no model-visible availability notification.
- Repeated records preserve evidence identity while observation IDs change.
- The ambiguous variant does not push E2, and relevant records are obtainable
  through the existing diagnostic catalogue.
- Model-visible payloads contain raw observations and provenance, not the
  interpretation column or hidden answer.
- Recovery changes current observations consistently while preserving historical
  fault evidence, original timestamps, and frozen in-flight snapshots.
- Polling consumes existing budgets; a competent actor can succeed without help.

These are mechanism requirements, not new success thresholds. The authorized
T2 implementation may encode focused deterministic acceptance tests for them.
Section 7 re-derives the straightforward report times; those times do not apply
to the ambiguous or recovery variants.

### 13.8 Change history and remaining boundary

ER-1 was proposed on 2026-09-18 to replace scheduled pre-signal unavailability
with realistic retrieval. The exact fixture, ambiguous CPU symptom, tick-10
recovery treatment, neutral evidence IDs, timing distinctions, and Q-ER1-1
existing-contract approach were approved later that day. Sections 4, 7, 8, 10,
and the implementation handoff were reconciled to this baseline.

Evidence equivalence beyond the stated E3 retrieval route, complete case rubrics,
scoring weights, and held-out design remain deferred. A later 2026-09-18
decision authorized T2 implementation and focused tests against
`ER1-fixture-1`; it did not authorize serialized contract/schema changes,
scenario-data changes, T3-T5, live models, deployment, or changes to the
current three-architecture comparison.
