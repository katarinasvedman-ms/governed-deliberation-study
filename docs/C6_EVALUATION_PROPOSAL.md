# C6 evaluation rubric (frozen: C6-rubric-1)

| Field | Proposed value |
| --- | --- |
| Status | **Frozen 2026-09-24 as `C6-rubric-1`.** Authoritative for the ER-1 development, ambiguous, and recovery cases; not retuned after comparative/model runs; not yet implemented. |
| Rubric version | `C6-rubric-1` |
| Date | 2026-09-24 |
| Fills | The deferred C6 gap in [EXPERIMENT_SPEC.md §9](EXPERIMENT_SPEC.md#9-outcome-and-measurement-contract) and the case-rubric items in the handoff |
| Depends on | Approved `ER1-fixture-1` evidence and the candidate-3 report contract |
| Explicitly out of scope | Comparative benefit scoring, live-model thresholds, held-out scenario design |

## 0. Why this document exists, and its one danger

Every milestone so far is **mechanism**: the system provably behaves as specified,
but nothing yet judges whether a report is *supported*, *appropriate*, or
*uncertain in a justified way*. Until an evaluator exists, we cannot answer the
study's actual question, and we must not introduce live models, because we would
be left eyeballing transcripts.

**The rubric is the most bias-prone artifact in the project.** Whoever defines
"a supported diagnosis" and "an acceptable next step" implicitly defines what it
means for supervision to help. Two rules protect the study from that:

1. The rubric and evaluator are **frozen before any comparative or model-driven
   run**, and are never tuned after seeing which architecture or model "wins."
2. This document separates the mechanical mappings (which the evaluator applies
   deterministically) from a small set of **genuine judgment calls** in §7. Those
   are decisions for the research owner, not choices to be made silently in code.

**Frozen 2026-09-24 as `C6-rubric-1`.** The §4–§7 mappings are authoritative for
the ER-1 development, ambiguous, and recovery cases and are **not** retuned after
comparative or model-driven runs (§7-C's strict citation policy is the one
pre-registered exception, used only as a robustness check). Freezing the rubric
does not add numerical thresholds, held-out design, scoring weights, or authorize
live-model runs — those remain deferred.

## 1. What the evaluator judges — and what it must not

The evaluator classifies **one submitted report against the evidence that episode
actually observed and cited**. It is a per-episode, post-termination function of
the persisted records only.

It judges, as **separate recorded fields** (never collapsed into one score, per
§9):

| Field | Meaning |
| --- | --- |
| `diagnosisClass` | supported / supported-as-hypothesis / unsupported / justified-uncertainty (see §4). `wrong-domain` was dropped (2026-09-24, §7-D); a claim that contradicts its cited evidence is `unsupported` with a recorded `contradictsCitedEvidence` sub-fact. |
| `evidenceCoverage` | which required evidence for the case was observed and cited |
| `nextStepClass` | acceptable / premature / inappropriate for the observed evidence |
| `temporalRelevance` | whether the conclusion addresses the current observed incident, not only a historical state (matters in the recovery variant) |
| `uncertaintyConsistency` | whether the stated uncertainty is consistent with the cited evidence. Per §7-A this flags **only** `low` uncertainty on evidence that does not support the claim; `low` on a well-cited domain hypothesis is *not* flagged. |
| `neutralFacts` | recorded, non-scored trajectory observations (see §6.1): the citation facts, mechanism-naming-before-evidence, and handoff trajectory. |

It must **not**:

- Read the fixture's hidden cause, answer key, case label, variant name, or the
  evaluator-only causal explanation. It sees only persisted `observations`,
  `actions`, `reviews`, `memory-updates`, `beliefs`, `termination`, `run-closure`.
- Reward a report that names the hidden-true domain **without having observed and
  cited the evidence that supports it** (the anti-oracle rule, §5).
- Compare architectures, decide comparative benefit, or feed anything back into a
  running episode. It runs strictly after termination.
- Reuse the reproducibility machinery. Reproducibility checks that two runs are
  the same trace; evaluation checks whether one report is supported. They are
  orthogonal and stay separate modules.

## 2. Independence and inputs

- The evaluator is a distinct component that consumes an episode's persisted,
  already-validated artifact directory. It re-validates each root through the
  existing contract validators before scoring, and fails closed on any invalid
  record rather than scoring it.
- Ground-truth rubric data lives in a separate **evaluator-only** definition,
  hashed into the run manifest as its own `DefinitionRef` so provenance shows
  which rubric version produced a classification. It is never delivered to actor
  or supervisor inputs.
- The evaluator is deterministic given (report, observed+cited evidence, rubric
  version). No model call is involved in C6 scoring itself.

## 3. The ER-1 evidence, restated as the evaluator sees it

The evaluator reasons only about **what the report cited from observed history**.
For reference, the approved straightforward evidence and what each item can
support:

| Evidence actually observed and cited | Strongest domain it can support |
| --- | --- |
| E0 (incident/latency), E1 (local instances healthy, no deploy) | An active incident exists; no specific failure domain is yet supported. |
| E2 (failures correlate with authorization-service timeouts) | The dependency **path** as a hypothesis. Not the mechanism. |
| E2 + E3 (request-linked queue wait exceeding the caller timeout, from the on-demand dependency-log query) | Dependency-side **queue delay** as the supported failure domain. |
| E4 (cache warning, unchanged rate, no link to failures) | Nothing; a distractor. |

E3 is only obtainable by the dependency-log query at/after tick 12, with no
notification. "Observed E3" therefore means the episode actually issued that
governed query and the linked records entered its history.

## 4. Diagnosis classification (resolved 2026-09-24)

For the report's `hypothesis`, given the evidence observed and cited. Judged
against cited evidence only, symmetrically — never against the hidden cause:

| `hypothesis` reported | Cited evidence present | `diagnosisClass` |
| --- | --- | --- |
| `dependency-side-queue-delay` | E2 present and ≥1 E3-derived observation cited | supported |
| `dependency-side-queue-delay` | E3 observed but no E3-derived observation cited, or only E2 cited | **unsupported** (§7-C primary policy) |
| `dependency-side-queue-delay` | E3 not observed | **unsupported** (anti-oracle; matches hidden truth but not earned) |
| `dependency-path-issue` | E2 cited | supported-as-hypothesis. `low` uncertainty is acceptable (§7-A). |
| `dependency-path-issue` | E2 not cited | unsupported |
| `local-instance-issue` | local symptom cited, E2 not cited | supported-as-hypothesis (ambiguous variant; §7-D) |
| `local-instance-issue` | E2 cited, still asserts local | unsupported |
| `unresolved` | evidence insufficient for a domain | justified-uncertainty (acceptable, distinct from a supported diagnosis) |
| `no-currently-active-incident` | cited recovery evidence (recovery variant) | supported |
| `no-currently-active-incident` | active incident still observed | unsupported (`contradictsCitedEvidence` recorded) |

A `timeout` or `budget-exhausted` termination with no report is recorded as such
by the mechanism and is **not** an evaluator diagnosis class. Reaching the budget
and *choosing* to report justified uncertainty is `justified-uncertainty`; these
are scored distinctly so premature abstention is not equated with a supported
diagnosis (per §9).

## 5. The anti-oracle rule (non-negotiable)

If a report names a domain the evidence it observed and cited does not support,
it is `unsupported` **even if it matches the hidden world state**. Concretely:
reporting `dependency-side-queue-delay` without having observed and cited E3 is
`unsupported`. This is the single most important rule in the rubric — it prevents
the study from rewarding a lucky guess and calling it a diagnosis.

**The rule is symmetric.** Just as we do not *reward* a claim the cited evidence
fails to support merely because it matches hidden truth, we do not *punish* a
claim merely because it *mismatches* hidden truth when the cited evidence
reasonably supports it (this is what makes early `local-instance-issue` on a
cited local symptom `supported-as-hypothesis`, §7-D). The evaluator never reads
the hidden cause in either direction.

## 6. Next-step and other fields (resolved 2026-09-24)

- `nextStepClass` is judged against the observed evidence and the report's own
  diagnosis:
  - After E2, **both** dependency-directed steps (`further-dependency-diagnostics`
    and `investigate-dependency-queue`) are `acceptable` — they point where the
    evidence points (§7-B). A step pointing away from the cited evidence (e.g.
    `further-local-diagnostics` after E2) is `premature`/`inappropriate`.
  - `human-handoff` is `acceptable` at **any** evidence state (§7-E). Report
    quality is carried by `diagnosisClass` + `evidenceCoverage`, not by penalizing
    the safe exit.
  - `monitor` is judged against the **observed** incident state (§7-E):
    `acceptable` when observed evidence shows recovery / no active fault (recovery
    variant) or when paired with a supported diagnosis; `inappropriate` when an
    active incident is still observed and the report is `unresolved`
    (recommending inaction during an unresolved active incident).
- `temporalRelevance`: only exercised in the recovery variant; a report asserting
  an ongoing fault after cited recovery evidence is `stale`.
- `uncertaintyConsistency`: flags **only** `low` uncertainty on a claim the cited
  evidence does not support (§7-A). `low` uncertainty on a well-cited domain
  hypothesis (e.g. `dependency-path-issue` after E2) is *not* flagged.

### 6.1 Recorded neutral facts (observed, never scored)

To let us *see* trajectory differences without the scorer rewarding or punishing
behavioral choices — the discipline that keeps the rubric neutral toward the
supervisor's preferred moves — the evaluator records, per report:

- **Citation facts (§7-C):** did the episode observe E3; did the report cite any
  E3-derived observation; did it cite only E2.
- **Mechanism-naming (§7-B):** did the report recommend `investigate-dependency-queue`
  before any E3-derived observation was cited (premature narrowing) — recorded,
  not penalized.
- **Handoff trajectory (§7-E):** evidence state and diagnostic-attempt count at
  the point of a `human-handoff`, so reflexive early punting is visible.
- **`contradictsCitedEvidence` (§7-D):** whether the hypothesis actively
  contradicts the cited evidence (a more severe error than under-support).

These are observations, not penalties. Any comparative use of them is a later,
pre-registered analysis, never a live scoring dial.

## 7. Resolved judgment calls (2026-09-24)

These were worked through with the research owner one by one. Each shapes what
"supervision helps" means; each is now fixed **before** any comparative or
model-driven run and must not be retuned after seeing results.

- **§7-A — Uncertainty discipline on E2-only. Resolved: softened.** Domain
  support and uncertainty are separate fields. `low` uncertainty on a well-cited
  domain hypothesis (`dependency-path-issue` after E2) is acceptable and *not*
  flagged. `uncertaintyConsistency` flags only `low` uncertainty on a claim the
  cited evidence does not support. Rationale: the strict reading would penalize
  "being done at E2" and thereby quietly reward moving in the supervisor's
  preferred direction; strictness stays on evidence-support of the claim.
- **§7-B — `investigate-dependency-queue` before E3. Resolved: neutral.** Both
  dependency-directed next steps are `acceptable` after E2. Naming the mechanism
  before mechanism evidence is recorded as a neutral trajectory fact (§6.1), not
  penalized. Strictness stays on the *claim* (anti-oracle), not action granularity.
- **§7-C — Citation strictness. Resolved: (b) primary, (a) pre-registered.**
  Primary policy: `dependency-side-queue-delay` is `supported` only if the
  episode observed E3 **and** the report cites at least one E3-derived
  observation; citing only E2, or observing E3 but citing none of it, is
  `unsupported`. The three raw citation facts are recorded regardless (§6.1). The
  strict "must cite every E3 record" policy (a) is **pre-registered as a
  robustness/sensitivity check** — computed for comparison, never used as a dial
  turned after seeing which arm wins. Rationale: lenient possession reopens the
  oracle loophole *and* understates supervision; strict-only measures citation
  hygiene. **Freeze-time check:** confirm what the scripted actor actually cites;
  if it does not cite an E3-derived observation, its scripted "success" scores
  `unsupported`, which is acceptable (it is a plumbing demo, not a diagnostician).
- **§7-D — Ambiguous `local-instance-issue`. Resolved.** `supported-as-hypothesis`
  on a cited local symptom alone; `unsupported` once E2 is cited and the report
  still asserts local. The `wrong-domain` class is dropped entirely; a claim that
  contradicts its cited evidence is `unsupported` with a recorded
  `contradictsCitedEvidence` sub-fact. The "supervision helped it get further"
  signal lives in `evidenceCoverage` and the terminal domain reached, not in
  punishing a reasonable early hypothesis.
- **§7-E — `human-handoff` and `monitor`. Resolved.** `human-handoff` is
  `acceptable` at any evidence state, with a neutral handoff-trajectory fact
  recorded (§6.1). `monitor` is judged against observed incident state:
  `acceptable` on observed recovery / no active fault or with a supported
  diagnosis; `inappropriate` when an active incident is still observed and the
  report is `unresolved`.

### 7.1 Handoff semantics — a deliberate scope choice

In the current system, `human-handoff` is one allowed value of the report's
`nextStep` field. Submitting any report terminates the episode with
`kind = report`, and there is no separate "handoff" termination kind and no human
in the loop. So a handoff is an **exit, not a pause**: the episode ends as a
report whose recommended next action is "escalate to a person," carrying its own
hypothesis (possibly a supported diagnosis, possibly `unresolved`) and
uncertainty. An actor cannot hand off and keep gathering evidence; reflexive
early handoff means "give up immediately and terminate," already visible via
`unresolved` + zero coverage.

**In a real deployed system, handoff-to-a-human would naturally be a
suspend-and-wait.** We are deliberately not modelling that. This bounded study
ends at diagnosis + next-step, and a pause/resume-with-human path would be a
whole new mechanism (it needs a human simulator) — out of scope, later work.

## 8. What this still does not do

- No **comparative** judgment. Whether asynchronous supervision produced better
  reports than actor-only requires the competent-actor baseline and repeated runs;
  those come after this rubric is frozen, not here.
- No live-model thresholds, non-inferiority margins, sample sizes, or aggregate
  weighting. Those remain research-protocol decisions.
- No new scenarios. The rubric covers the approved straightforward and ambiguous
  ER-1 cases plus the recovery variant's temporal-relevance check only.
