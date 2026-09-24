# Development process and decision record

This page holds agent-facing approvals and scope boundaries; it is not the research overview.
The following process record is preserved from the contributor guidance. The repository now
contains the committed feasibility and contract implementation at `30417d9` and the approved
ER-1 documentation checkpoint at `bec4423`, with the pushed T2 baseline at
`3105d19`, reviewed T3 coordination at `948181f`, and the published scripted
smoke checkpoint at `9578dd0`.

## Current preparation phase

The reviewed design baseline is [EXPERIMENT_SPEC.md](../EXPERIMENT_SPEC.md)
v1.0 plus WM-1. On 2026-09-16, the user approved
`T1-WM-1-candidate-3` / `1.0-candidate.3` as the first scripted-build contract
baseline, including C4/M6, C7/M7, M3, C8/M8 and the listed engineering limits.
Previously accepted decisions remain accepted; C6 remains deferred.

On 2026-09-18, the user approved ER-1 and
[its synthetic fixture specification](../ER1_FIXTURE_PROPOSAL.md): successful
diagnostics return current records or legitimate empty projections; tick-12
dependency evidence availability remains silent; the ambiguous CPU-symptom and
tick-10 recovery variants are fixed; and Q-ER1-1 keeps query-window and
collection metadata harness-owned under the existing contracts with its stated
safeguards. Its initial approval was documentation-only.

Later on 2026-09-18, the user authorized T2 implementation only against
`ER1-fixture-1` and the reconciled experiment specification. The authorization
includes the deterministic scheduler, evolving evidence fixture, focused tests,
and required validation. T2 was committed and pushed as `3105d19`.

After independent T2 review found no significant conformance issue, the user
authorized T3 runtime supervision coordination only. That authorization covers
the one coordinator for actor-only, blocking, and asynchronous supervision;
isolated Agent Framework invocations; coordinator-owned memory and
reconsideration; cancellation and obsolete-result suppression; governed
diagnostic dispatch; bounded drain; and focused scripted integration tests. It
does not authorize T4/T5, a full runner/evaluator, live models, deployment, new
scenarios, or research-design additions. Independent review confirmed the T3
conformance corrections on 2026-09-18 and authorized publishing the reviewed
T3 checkpoint to `main`. Stop before T4.

Later on 2026-09-18, the user authorized only the six-episode straightforward
ER-1 scripted smoke checkpoint described in
[SCRIPTED_SMOKE_RUN_PROPOSAL.md](../SCRIPTED_SMOKE_RUN_PROPOSAL.md). It may
persist validated mechanism records and compare repeated logical traces, using
the approved deterministic opaque-ID test seam. It does not authorize the C6
rubric, remaining T4 evaluator work, T5, live models, or deployment. Independent
review approved the implementation, which was committed and pushed as
`9578dd0`; the remaining evaluator and acceptance work stays separately gated.

Use [COPILOT_HANDOFF.md](COPILOT_HANDOFF.md) to scope later authorized
implementation tasks. Start with its framework feasibility gate; do not execute
all tasks merely because the handoff exists or replace scripted components
with live model calls.

On 2026-09-16, the user accepted the T0 isolated-invocation architecture for
the scripted experiment: Agent Framework runs isolated actor/supervisor
invocations; one experiment coordinator owns scheduling, immutable snapshots,
decision generations, interruption, and dispatch eligibility. Do not use the
tested shared fan-out graph for the asynchronous treatment. This acceptance
does not establish live-provider cancellation or concurrency support.

The earlier T1-only gate authorized bounded research contracts,
serialization, validation, research-event records, working-memory revisions,
reconsideration triggers, reassertion lineage, consumed-revision provenance,
and focused contract fixtures. That historical stop-before-T2 gate was
superseded by the later T2 and T3 authorizations above. Keep research metadata
separate from operational plan schemas; the full runner, independent evaluator,
T4-T5, live calls, deployment, and governance behavior changes remain
unauthorized.


## Implementation tasks and gates

The [implementation handoff](COPILOT_HANDOFF.md) preserves the task definitions, dependencies,
authorization history, stop/go gates, reuse map, and acceptance mapping.

The [historical research narrative](research-history.md) preserves the former README,
including the routing-to-supervision refocus and original episode development material.

Technical rules and wire identifiers remain in the [experiment specification](../EXPERIMENT_SPEC.md)
and [contract reference](../T1_CONTRACT_PROPOSAL.md). Moving process text does not change either.

## Contract decision record

Preserved from [the contract reference, section 1](../T1_CONTRACT_PROPOSAL.md#1-accepted-decisions).
Section numbers and "below" in this table refer to that document.


| Decision | Status and meaning |
| --- | --- |
| C1 | Accepted: low/medium/high uncertainty is uncalibrated assertion, not correctness or authority. The disposition vocabulary below extends the earlier reasons to belief corrections. |
| C2 | Accepted: Reports may have empty citations structurally. Claims and actionable memory operations require observation citations. Provenance is not truth; support is evaluated separately. |
| C3 | Accepted: the seven operation/target pairs in section 3. |
| C5 | Accepted: count started actor turns and recognizable current Query attempts; obsolete outputs create no new attempts. |
| C9 | Accepted: recorded investigative direction is the latest dispatched diagnostic, separate from recommended direction. |
| M1 | Accepted: supervisor-only semantic memory writes in the first study. The treatment is supervision plus within-incident memory adaptation, not memory's isolated benefit. |
| M2 | Accepted: exact-base whole-update rejection without merge, rebase, or extra-review retry. |
| M4 | Accepted: persistent within-incident beliefs and separately expiring direction. |
| M5 | Accepted: atomic memory publication and decision-generation invalidation; best-effort cancellation, mandatory obsolete suppression. |
| C4 + M6 | Accepted: deterministic equality, normalization, whole-successor validation, and no-op rules in section 5. |
| C7 + M7 | Accepted: latest-trigger acknowledgment, expiry/multiple-interrupt handling, and complete trigger/disposition contracts in section 6. |
| M3 | Accepted: deterministic conflict lifecycle in section 5. |
| C8 + M8 | Accepted: this consolidated wire contract, version declarations, and the engineering limits in section 10. |
| C6 | **Deferred:** evidence equivalence and case rubrics. Do not invent them. |



## Working-memory decision record

Preserved from [the experiment specification, section 12.8](../EXPERIMENT_SPEC.md#128-decision-status-and-boundedness).
Section numbers in this table refer to that document.


| ID | Status and scope |
| --- | --- |
| M1 | Accepted: supervisor-only semantic writes and combined-treatment interpretation. |
| M2 | Accepted: exact-base whole-update rejection without merge, rebase, or extra-review retry. |
| M3 | Accepted: deterministic conflict lifecycle in section 12.11. |
| M4 | Accepted: within-incident belief persistence and separately expiring direction. |
| M5 | Accepted: atomic publication and decision invalidation; cancellation best-effort, obsolete suppression mandatory. |
| M6 | With C4, accepted: equality, normalization, no-ops and rejection in section 12.10. |
| M7 | With C7, accepted: latest actionable trigger and acknowledgment in section 12.12. |
| M8 | With C8, accepted: the consolidated wire candidate, version declarations, and listed engineering limits. |

C1-C5, C7-C9 and M1-M8 are accepted as described above. C6 remains
deferred; do not invent evidence equivalence or case rubrics.


## Specification consolidation record

**Design consolidation and approval, 2026-09-16:** [section 12](../EXPERIMENT_SPEC.md#12-approved-amendment-within-incident-working-memory)
and the [approved contract baseline](../T1_CONTRACT_PROPOSAL.md) integrate WM-1
with sections 1-11. `T1-WM-1-candidate-3` / `1.0-candidate.3` is approved for
the first scripted T1 build with its candidate literals preserved. T1 contracts,
serialization, validation, research events and focused fixtures are authorized.
At that decision point, T2-T5, live calls, deployment and full experiment
execution were unauthorized; the later T2-only authorization is recorded above.

**Evidence-retrieval consolidation and approval, 2026-09-18:**
[ER-1](../EXPERIMENT_SPEC.md#13-approved-amendment-realistic-diagnostic-retrieval)
and the [approved fixture](../ER1_FIXTURE_PROPOSAL.md) replace normal scheduled
pre-signal unavailability with current observable records or legitimate empty
results. Explicit unavailability remains available only for authored
telemetry/access failures. The seven-pair catalogue, budgets, silent tick-12
availability, and deferred C6/evaluation decisions are unchanged.

## Working-memory amendment history

Preserved from the experiment specification's amendment header:

| Field | Value |
| --- | --- |
| Amendment ID | WM-1 |
| Status | Complete contract baseline approved; T1-only implementation authorized |
| Date | 2026-09-16 |
| Baseline retained | Reviewed v1.0, sections 1-11 above |
| Contract counterpart | [Approved T1 contract baseline with preserved candidate ID](../T1_CONTRACT_PROPOSAL.md) |
| Contract candidate revision | `T1-WM-1-candidate-3` / `1.0-candidate.3`; approved literals preserved |
| Accepted | C1-C5 and C7-C9; M1-M8; including C4/M6, C7/M7, M3, C8/M8 and listed engineering limits |
| Authorized implementation | T1 bounded contracts, serialization, validation, research-event record and focused fixtures only |
| Deferred | C6 evidence equivalence and case rubrics |

The status table above records the user's final T1 contract approval. Accepted
choices are requirements of the consolidated design. This approval authorizes
T1 only and is not permission to implement the T2 scheduler, T3 coordinator,
T4-T5, live models, deployment, or a full experiment runner.

## C6 evaluation rubric freeze (2026-09-24)

The C6 case-classification rubric for the ER-1 development, ambiguous, and
recovery cases was worked through with the research owner (judgment calls §7-A
through §7-E) and **frozen as `C6-rubric-1`** in
[C6_EVALUATION_PROPOSAL.md](../C6_EVALUATION_PROPOSAL.md). It is authoritative and
not retuned after comparative or model-driven runs; §7-C's strict citation policy
is the one pre-registered robustness check.

This resolves only the case-classification rubric named in the earlier
"Deferred: C6 evidence equivalence and case rubrics" note. Still deferred: broader
evidence equivalence beyond the E3 retrieval route, scoring weights, aggregate
weighting, held-out scenario design, and live-model timing/evaluation methodology.

Authorized next: implement the deterministic evaluator and its focused tests over
the existing smoke-run artifacts. It classifies reports and records neutral facts
only; it makes no comparative claim and does not add scoring weights. The
competent-actor baseline and any comparative run remain separately gated.

**Implementation status, 2026-09-24:** the deterministic evaluator and focused
tests are implemented against `C6-rubric-1` and await independent review. The
authorization remains stopped before the competent-actor baseline, comparative
execution, remaining T4/T5 acceptance work, live models, or deployment.
