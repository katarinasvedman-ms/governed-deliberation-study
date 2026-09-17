# Development process and decision record

This page holds agent-facing approvals and scope boundaries; it is not the research overview.
The following process record is preserved from the contributor guidance. The repository now
contains the committed feasibility and contract implementation at `30417d9`; the scope stop
below remains in force and is not changed by the documentation restructure.

## Current preparation phase

The reviewed design baseline is [EXPERIMENT_SPEC.md](../EXPERIMENT_SPEC.md)
v1.0 plus WM-1. On 2026-09-16, the user approved
`T1-WM-1-candidate-3` / `1.0-candidate.3` as the first scripted-build contract
baseline, including C4/M6, C7/M7, M3, C8/M8 and the listed engineering limits.
Previously accepted decisions remain accepted; C6 remains deferred.

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

T1-only implementation is authorized: bounded research contracts,
serialization, validation, research-event records, working-memory revisions,
reconsideration triggers, reassertion lineage, consumed-revision provenance,
and focused contract fixtures. Keep this metadata separate from operational
plan schemas. **Stop before T2.** The scheduler, runtime coordinator, full
runner, T2-T5, live calls, deployment, and governance behavior changes are not
authorized.


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
T2-T5, live calls, deployment and full experiment execution remain unauthorized.

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
