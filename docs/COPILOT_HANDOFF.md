# GitHub Copilot handoff: scripted supervision experiment

| Field | Value |
| --- | --- |
| Status | T1 contract baseline approved and T1-only implementation authorized; stop before T2 |
| Date | 2026-09-16 |
| Design baseline | [EXPERIMENT_SPEC.md v1.0](EXPERIMENT_SPEC.md) |
| Starting repository revision | `2612294a32b39f611bf8b3b0aab84577fb79f7e7` |
| First-build scope | Credential-free, scripted mechanism experiment; diagnosis and direction only |
| Intended implementer | GitHub Copilot, after explicit task authorization |

## 1. Goal and limits

Build the minimum reproducible mechanism needed to compare:

1. Fast actor alone.
2. Actor with blocking supervisory review.
3. Actor with interrupt-driven asynchronous supervisory review.

The first build must demonstrate scheduling, evidence isolation, controlled
diagnostics, feedback interruption, and trustworthy observation capture.
It must not claim that AI supervision improves outcomes: all component behavior
in this build is scripted.

One episode ends with a diagnostic report and justified next step, or an
explicit termination reason. No production remediation is executed.

### Out of scope

- Live model/API calls, Jev integration, Copilot SDK inference sessions, or Azure
  authentication and deployment.
- New UI, dashboard, general agent platform, generic connector framework, or
  customer-data ingestion.
- Retraining, learned routing policies, supervisor-controlled permissions, or
  dynamically registered tools.
- Held-out model evaluation, empirical speed/cost claims, or new whole-system
  formal-verification claims.
- Unrelated cleanup of the inherited demo, deployment scaffold, or presentations.

Existing package restore and normal local development dependencies are distinct
from runtime service calls. Once dependencies are present, the scripted
experiment must require no credentials or model service.

## 2. Authority and document precedence

Read [AGENTS.md](../AGENTS.md), [the charter](../README.md), and the complete
[experiment specification](EXPERIMENT_SPEC.md) before implementing.

The reviewed specification defines experimental semantics. This brief orders
implementation work; it does not override the specification or authorize
changes to its clock, interruption policy, or expected behavior.

The repository remains in documentation-only preparation until an explicit
implementation request names a task or task range below. Starting one task
does not authorize all later tasks or live integration.

If a contradiction or underspecified behavior affects acceptance, report it
with the relevant section and a minimal example. Do not silently choose the
interpretation that makes the asynchronous architecture look better.

### Accepted architecture and current contract gate (2026-09-16)

The user accepted the [T0 isolated-invocation architecture](T0_FEASIBILITY.md)
for the scripted experiment. Agent Framework runs isolated actor and supervisor
invocations. One experiment coordinator owns scheduling, immutable snapshots,
decision generations, interruption, and dispatch eligibility. The tested shared
fan-out graph must not be used for the asynchronous treatment.

All documented T0 limitations remain: this does not establish live-provider
cancellation, concurrency, or session support. Cross-run coordination remains
the experiment coordinator's responsibility.

The [T1 contract baseline](T1_CONTRACT_PROPOSAL.md) is approved as
`T1-WM-1-candidate-3` / `1.0-candidate.3`; preserve those serialized literals
for traceability. T1-only contracts, serialization, validation, research events
and focused fixtures are authorized. T0/T1 acceptance is not authorization for
T2-T5, live models, deployment, or changes to governance behavior.

### Working-memory consolidation and remaining review gates

The user clarified that the supervisor proposes shared working-memory changes
that shape subsequent turns in the same incident, not only transient guidance.
Review [EXPERIMENT_SPEC.md section 12](EXPERIMENT_SPEC.md#12-approved-amendment-within-incident-working-memory)
and the [approved T1 contract baseline](T1_CONTRACT_PROPOSAL.md).
Use the preserved candidate identifier as the complete approved contract
baseline, not an original
schema plus amendment prose. Previously accepted decisions plus C4/M6, C7/M7,
M3 and C8/M8, including the listed engineering limits, are accepted.

Keep immutable evidence/action history distinct from provisional beliefs,
recommended direction, and versioned memory snapshots. One coordinator validates
and versions updates; acceptance cannot establish truth, rewrite observations,
grant permissions, or change model weights. The proposal explicitly addresses
stale bases, conflicts/retractions, belief validity versus direction expiry,
atomic memory acceptance/actor invalidation, and consumed-revision tracing.
C6 evidence equivalence/rubrics remain deferred. Scope is within one incident,
with no cross-incident learning, generic memory platform, or RL training claim.

## 3. Reuse map

Inspect these existing surfaces before adding alternatives. Their presence is
not evidence that the new experiment is already implemented.

| Existing surface | Reuse or extension intent |
| --- | --- |
| [Core contracts](../src/GovernedAgent.Core/Contracts/PlanContracts.cs) and [canonicalization](../src/GovernedAgent.Governance/ActionCanonicalizer.cs) | Retain canonical operational proposals and action binding. Research observations, guidance, and decision generations should be separate linked records. |
| [Governed gateway](../src/GovernedAgent.Governance/GovernedToolGateway.cs), [policy](../src/GovernedAgent.Governance/PolicyEvaluator.cs), and [registry](../src/GovernedAgent.Governance/ToolRegistry.cs) | Keep one controlled execution boundary. Expose only the experiment's registered diagnostic subset; do not introduce a bypass for scripted actors. |
| [Workflow](../src/GovernedAgent.Host/Workflow/LocalDeterministicAgentWorkflow.cs) and [verification adapter](../src/GovernedAgent.Host/Verification/NodePlanVerifier.cs) | Reuse verification/gateway integration where compatible. Preserve inherited recovery-oriented completion behavior; diagnosis completion belongs to the experiment. |
| [Simulator](../src/GovernedAgent.Simulator/IncidentSimulator.cs) and [tool executor](../src/GovernedAgent.Simulator/SimulatorGovernedToolExecutor.cs) | Follow existing deterministic state and tool patterns. The evolving dependency case needs explicit support; do not replace the original fixed incident behavior globally. |
| [Telemetry event shape](../src/GovernedAgent.Host/Observability/SemanticTelemetryEvent.cs) and [redactor](../src/GovernedAgent.Host/Observability/TelemetryRedactor.cs) | Reuse correlation/redaction conventions. Framework telemetry alone is not the ordered research record. |
| [Existing evaluator](../src/GovernedAgent.Host/Observability/Evaluation/SecurityEvaluator.cs) | Keep its original role. It consumes supplied observations and is not a runner or trajectory evaluator. |
| [Integration tests](../tests/GovernedAgent.IntegrationTests/AgentWorkflowTests.cs) and [gateway tests](../tests/GovernedAgent.IntegrationTests/GovernedGatewayTests.cs) | Preserve baseline behavior and use existing test conventions for new acceptance coverage. |
| [Package versions](../Directory.Packages.props) and [host references](../src/GovernedAgent.Host/GovernedAgent.Host.csproj) | Inspect actual pinned dependencies before choosing workflow APIs. Do not assume the Copilot integration package proves the required workflow scheduling capabilities. |
| [Validation entry point](../scripts/validate.ps1) | Preserve the existing validation path; add focused coverage to the existing ecosystem rather than installing a new test stack. |

Any new source module, test project, or command must be justified by this bounded
experiment. Keep code organization conventional and small; do not turn the reuse
map into a requirement to refactor every inherited component.

## 4. Work sequence and review gates

Implement sequentially in small reviewable changes. Do not launch the dependent
tasks in parallel. Each task must leave the inherited demo usable.

| Task | Depends on | Deliverable | Gate |
| --- | --- | --- | --- |
| T0: Framework feasibility | Explicit authorization | Executable credential-free evidence of parallel progress and interruption using the chosen framework arrangement | Stop for architecture review before building the experiment |
| T1: Contracts and event record | T0 accepted; contract mapping resolved | Bounded research records and validated component contracts | No hidden research choices or plan-schema changes |
| T2: Clock and evolving evidence | T1 | Deterministic scheduler and case-specific observation delivery | Stable ordering, no future evidence, baseline preserved |
| T3: Supervision coordination | T2 | Actor-only, blocking, and interrupt-driven asynchronous coordination | Guidance can preempt without granting authority |
| T4: Scripted run and evaluator | T3 | One bounded entry point producing actual observations and case-rubric results | No prefilled success records or model-performance claims |
| T5: Acceptance and handoff evidence | T4 | Traceable coverage of A01-A19 and documented local execution | Stop before live-model work |

### T0. Establish framework feasibility first

**Build:** a narrow .NET fixture with scripted actor and supervisor work,
explicit completion control, and no model client. Prefer Microsoft Agent
Framework; identify the exact package/API version used.

Use controlled completion signals rather than fragile wall-clock sleeps.
The fixture must exercise the actual selected coordination arrangement, not
replace that arrangement with an unrelated mock and claim framework success.

**Show:**

- Actor work completes while a supervisor remains pending.
- New actionable guidance invalidates an in-flight actor decision.
- A deliberately late actor result cannot dispatch a diagnostic or submit a report,
  even if its cancellation request was ignored.
- An already-dispatched operation completes once and retains its observation.
- There is no shared mutable session corruption or unintended join barrier.

**Acceptance:** specification section 3.4 and the mechanisms in A14-A16/A19.
Record the causal event sequence, pinned dependencies, any limitations, and
which parts are real framework execution versus scripted component behavior.

**Stop/go:** if the framework imposes serialization, do not quietly label the
result asynchronous. Present an alternative adapter arrangement and its
trade-offs for approval. A package addition or upgrade must be explicit and
compatible with the inherited application; no speculative broad upgrades.

### T1. Define bounded contracts and a research event record

**Current gate:** approved on 2026-09-16 for T1 only. Implement the complete
`T1-WM-1-candidate-3` / `1.0-candidate.3` baseline, including C4/M6, C7/M7,
M3, C8/M8 and the listed engineering limits. C6 remains deferred. Stop after
contract-level implementation and focused fixtures; do not begin T2.

**Build:** actor Query/Wait/Report decisions, supervisor NoChange and
memory-update outputs, immutable observation snapshots, belief/direction memory,
reconsideration triggers, consumed-revision provenance, research events, and
run-level configuration/termination records.

Keep trusted run identity, timestamps, applicability epochs, history revisions,
decision generations, and budgets harness-owned.

Minimum event coverage:

- Evidence available and evidence observed.
- Actor turn started/completed/rejected/invalidated.
- Diagnostic requested/dispatched/completed/rejected.
- Review started/completed/timed out/failed and checkpoint skipped.
- Guidance accepted/rejected/superseded/expired/duplicate.
- Cancellation requested/completed/unsupported and obsolete result suppressed.
- Reconsideration started and guidance disposition recorded.
- Report submitted and episode terminated.

Use a run-local monotonic sequence for total ordering. Record logical tick,
phase, causal identifiers, and immutable snapshot references. Real elapsed-time
and usage fields must be absent or explicitly unavailable in scripted-only
records, not fictional zero-cost model measurements.

**Acceptance:** validated contracts, rejection of unknown values or out-of-scope
targets, isolation of private evaluator data, and reproducible event order.
Research records must not be smuggled into strict plan schemas.

**T1 stop gate:** serialized names, permitted operation/target pairs and report
fields are fixed by the approved baseline. Cross-record constraints may use
explicit fixtures. Case-rubric mappings remain C6-deferred. Do not implement a
runner or proceed to T2 without separate authorization.

### T2. Implement deterministic time and evolving evidence

**Build:** a small discrete-event scheduler implementing specification section 3,
including its exclusive horizon and same-tick ordering. Advance to scheduled
events rather than using wall-clock sleeps or framework-step counts as time.

Add the development evidence sequence E0-E4 with immutable IDs, delivery and
availability times, on-demand dependency retrieval, and explicit unavailable
results before the relevant evidence exists.

A recovery variant must update later diagnostics consistently while retaining
historical observations. Both components see only permitted observed history;
no supervisor-specific retrieval or hidden answer access.

**Acceptance:** A03-A05, A08-A12, and the scheduler portions of A15-A16.
Repeat the same scripted run inputs to produce equivalent logical event records.
If random run IDs are used, separate them from the canonical comparison payload.

**Preservation:** extend through an isolated fixture or clearly scoped simulator
mode. Do not change the original demo's incident, health, metrics, or outcome
expectations by default.

### T3. Wire supervision and interrupts

**Build:** one workflow owner, the three comparison modes, review checkpoints,
snapshot capture, guidance validation, epoch/expiry checks, and duplicate rules.

On actionable guidance, invalidate the pending decision generation before a
not-yet-dispatched action can cross the execution boundary. Cancel where
supported, record failures to cancel, and start reconsideration under the
specified readiness/blocking rules.

Old completions and cancellation failures must not overwrite or terminate the
replacement turn. Already-dispatched diagnostics may finish exactly once.
Reconsideration must acknowledge the guidance but need not obey it.

**Acceptance:** A06-A11 and A14-A18. Include a fresh but wrong recommendation
that passes structural controls; the harness must not use the answer key to
filter it. NoChange and duplicate outputs remain observable without preempting
the actor.

**Resource boundary:** scripted completions have finite scheduled lifetimes.
Enforce existing episode and turn caps. Live calls that may drain indefinitely
remain excluded until their concurrent-draining cap and failure policy are
explicitly decided.

### T4. Add a bounded entry point and independent evaluator

**Build:** one documented local command or entry point that runs the scripted
case across the three architectures. Use the same actor configuration, events,
and controls; hold supervisor configuration constant between supervised modes.

Every diagnostic attempt must traverse the applicable existing verifier,
registry, policy, and gateway path. Scripted behavior does not justify bypassing
controls or fabricating a verification attestation.

Write artifacts under a bounded ignored directory such as
`.artifacts\deliberation-study\<run-id>` using existing repository conventions.
Keep output destinations outside model control.

Produce:

- Run manifest: specification version, source revision, fixture/configuration
  identifiers, architecture, seed if relevant, and scripted execution mode.
- Ordered event record with observations and guidance provenance.
- Independently evaluated termination, evidence support, next-step
  appropriateness, trajectory, and resource measures.
- A concise comparison that labels all tick values as scripted.

The evaluator must consume actual recorded observations and outputs, not
dataset fields asserting that the expected action already occurred. Run it
after termination; do not feed scores into the actor or supervisor.

**Acceptance:** A01-A02 and A12-A13, plus successful traceability from an action
to its snapshot, any interrupt, and the final outcome. The competent-actor
fixture must succeed without supervision.

### T5. Complete acceptance coverage and document limits

**Build:** executable coverage of all A01-A19, including timing ties,
cancel-ignored cases, late guidance, budget exhaustion, and end-of-run cleanup.
Keep tests deterministic and use the existing testing ecosystem.

The A01 tick-13/tick-16 outcomes are authored fixture expectations, not a
benchmark victory. If the worked example leaves a scripted Wait or trigger
ambiguous, resolve that documentation ambiguity rather than hardcoding the
desired report times.

Document the implemented command, output shape, package/API choices, and
coverage-to-specification mapping. State unsupported behaviors and remaining
live-model work. Do not advertise stronger formal guarantees than the inherited
bounded proof establishes.

**Acceptance:** complete mapping below, existing validation preserved, no
operational writes, and reproducible results from the documented command.

## 5. Acceptance coverage map

These are coverage obligations, not claims that tests already exist.

| Specification cases | Primary work | What must be observable |
| --- | --- | --- |
| A01-A02 | T4/T5 | Authored baseline timeline and independent actor success without required supervision |
| A03-A05 | T2/T5 | Evidence visibility, captured snapshots, and recovery-based invalidation |
| A06-A07 | T3/T5 | Wrong-but-valid advice remains measurable; out-of-scope advice cannot execute |
| A08-A11 | T2/T3/T5 | Review backlog policy, timeout, expiry, and no lost wake events |
| A12-A13 | T2/T4/T5 | Exclusive horizon, explicit failures, and budget accounting |
| A14-A16 | T0/T3/T5 | Preemption, same-tick priority, and already-dispatched operation handling |
| A17-A18 | T3/T5 | Non-interrupting duplicate/NoChange outputs and bounded repeated interruption |
| A19 | T0/T5 | Actual framework parallel progress and later interruption |

Framework proof-of-feasibility and deterministic scheduler acceptance are
different evidence. Completing one does not imply the other.

## 6. Validation and review expectations

Use the smallest existing test selection covering each change during
development. Run the repository's required validation before committing:

```powershell
pwsh .\scripts\validate.ps1
```

Restore pinned dependencies when needed through the existing tooling. Do not
add a new test framework, alter proof claims, weaken validation, or update
unrelated package versions merely to make the new experiment pass.

Each task's review should identify:

- Which specification sections and acceptance IDs it addresses.
- Source changes and reused components.
- Reproducible commands and actual observed outputs.
- Scripted assumptions, framework limitations, and unresolved decisions.
- Confirmation of no model calls, no operational writes, and no altered default
  behavior in the inherited demo.

Do not commit generated local run output or credentials. Do not change
deployment settings, publish findings, or push changes without the applicable
authorization.

## 7. Deferred research protocol

The following belong to a separate research protocol before live comparative
experiments, not to implementation guesswork:

- Provider/model selection, data-sharing constraints, and monetary budgets.
- Wall-clock event schedules, hardware/runtime conditions, and inference latency
  measurement distinct from scripted ticks.
- Limits for concurrent obsolete calls still draining after cancellation.
- Scenario categories, independent held-out cases, sample sizes, and repeated
  run/randomization methodology.
- Numerical responsiveness thresholds, worthwhile-effect criteria, uncertainty
  reporting, and any aggregate workload weighting.
- Accounting for errors, cancelled work, missing usage, and model-version drift.

Do not create a nominally "frozen" protocol by filling these gaps with convenient
defaults during the scripted build.

## 8. Suggested first task assignment

The following is a reusable assignment for when implementation is explicitly
authorized. Its presence here is not authorization to execute it now.

> Implement T0 only from docs/COPILOT_HANDOFF.md against
> docs/EXPERIMENT_SPEC.md v1.0. Use credential-free scripted components to prove
> Agent Framework parallel progress and interrupt handling, including a
> cancellation-ignoring actor and suppression of its obsolete result. Keep the
> experimental clock independent of framework steps. Preserve the original
> demo, gateway controls, and pinned dependencies unless a narrowly justified
> dependency change is required and explicitly documented. Do not call live
> models, deploy services, implement the full experiment, or invent research
> thresholds. Return the reproducible feasibility evidence, limitations, and
> a stop/go recommendation before proceeding to T1.
