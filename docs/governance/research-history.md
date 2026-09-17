# Research framing and process history

Historical README preserved from commit `30417d9` before the outward-facing rewrite.
This captures the development narrative, original evidence sequence, and approval context;
it is not the current status page. The [process record](process.md) and linked technical
specifications retain the implementation boundaries. Wording below is preserved apart from relocated links.

---

# Governed Deliberation Study

**Fast action, ongoing reflection, fixed authority.**

An experimental research project investigating when a fast actor benefits from
a slower, ongoing supervisor under independently enforced execution controls.
This is a bounded study, not an agent platform or production service.

![A luminous running figure and a seated reflective figure exchange flowing data through arrows in both directions, illustrating fast action and slower supervision.](../assets/fast-actor-slow-supervisor.png)

*Fast action. Ongoing reflection. Connected through feedback.*

## Discussion pitch

Open [the research pitch](../pitch.html) for team, customer, and community
discussions. It is an HTML page with a local illustration asset, illustrative scenarios,
research questions, study boundaries, and a print/PDF option. It presents a
proposal, not experimental results. The previous governance-demo pitch remains
available in the source history.
Keep `docs/assets` alongside `docs/pitch.html` when copying or hosting the pitch.

## Experiment preparation

The design baseline and bounded T1 contract baseline are approved. The
separately authorized **T0 framework feasibility probe** is implemented; the
experiment scheduler and runner are not built.
The [T0 isolated-invocation architecture](../T0_FEASIBILITY.md) was accepted
for the scripted experiment on 2026-09-16, without extending its live-provider
claims. The [T1 contract baseline](../T1_CONTRACT_PROPOSAL.md) preserves the
`T1-WM-1-candidate-3` / `1.0-candidate.3` identifiers and is approved for the
first scripted build. T1-only contracts, serialization, validation,
research-event records and focused fixtures are authorized and implemented in
`src/GovernedAgent.Research`; T2-T5 remain unauthorized.

The [WM-1 working-memory amendment](../EXPERIMENT_SPEC.md#12-approved-amendment-within-incident-working-memory)
and the [approved T1 contract baseline](../T1_CONTRACT_PROPOSAL.md)
distinguish immutable history, provisional beliefs, recommended direction, and
versioned within-incident memory. The previously accepted decisions plus
C4/M6, C7/M7, M3 and C8/M8, including the listed engineering limits, are
approved. C6 evidence equivalence and case rubrics remain deferred. **Stop
before T2: no scheduler, runtime coordinator, full runner or live calls are
authorized.**

Read [the reviewed experiment specification, v1.0](../EXPERIMENT_SPEC.md) for
clock ordering, actor/supervisor contracts, guidance handling, worked timelines,
and acceptance examples. The design is accepted; implementation remains a
separately authorized step. Open live-model and handoff items are listed in the
specification. The worked timeline uses scripted components and is not a
model-performance result.

The [Copilot handoff](COPILOT_HANDOFF.md) sequences the future scripted build
from framework feasibility through acceptance coverage. It includes reuse
locations, stop/go gates, and a bounded first-task assignment. Live-model
experiments remain outside that first build.

## Purpose and intended outcome

Primary question:

> Under what conditions can a slower, ongoing supervisor improve a fast agent's
> behavior over time without sacrificing required responsiveness or weakening
> independently enforced execution constraints?

The fast actor responds to incident events. The slower supervisor reviews the
observed trajectory, evaluates progress, and proposes feedback or a change in
direction. In the asynchronous architecture, the actor may continue permitted
work while review is pending; it does not wait for every supervisory response.
Fresh, valid, actionable feedback interrupts its pending decision and requires
reconsideration. Request cancellation where possible and suppress obsolete
results even when cancellation fails. Already-dispatched diagnostics may
finish; interruption is not rollback and guidance is not automatic authority.

The clarified design intent is that supervision also proposes updates to shared
working memory, so a correction can influence multiple turns within one incident.
The coordinator validates and versions those updates; structural acceptance
does not make a belief true. WM-1 records accepted design choices and clearly
separates the remaining normalization, conflict, reconsideration, and wire-format
proposals rather than silently changing v1.0.
Neither component can rewrite observations, grant permissions, or change model
weights. This is not cross-incident learning, a generic memory platform, or RL
training.

Agent Framework is the preferred coordination layer, with an independent
experiment clock. The specification requires evidence that the selected
runtime arrangement supports parallel progress and reliable interruption rather
than silently serializing actor and supervisor.

The outcome is a reproducible comparison, a small reference example, and a
research report explaining where the approach helps, where it fails, and what
coordination controls it requires. Negative findings are valid outcomes.
The goal is to identify suitable conditions, not to demonstrate a universal
advantage or assume that the most complex or regulated workloads benefit most.

Working hypothesis, not a demonstrated result:

> In some evolving incidents, slower supervision can identify ineffective
> patterns and improve subsequent fast actions. Asynchronous cooperation may
> preserve responsiveness better than blocking review, provided useful guidance
> arrives in time and is applied appropriately. Supervision may also add cost,
> delay, or mistakes without improving the outcome.

### Architecture: memory and interruption

![The fast actor and slow supervisor read versioned shared working memory. The supervisor proposes updates; one coordinator commits accepted changes and interrupts pending actor decisions. A governed gateway controls diagnostics, whose observations return to shared history.](../architecture/actor-supervisor-shared-memory.png)

**Memory makes a correction persist; interruption makes it effective promptly.**
The diagram illustrates the approved within-incident working-memory design.
T1 implements its contracts, not the T2/T3 runtime behavior. Observations
remain immutable, beliefs remain provisional, and neither memory nor
supervisory confidence grants execution authority.

[Editable Excalidraw source](../architecture/actor-supervisor-shared-memory.excalidraw)
| [Scalable SVG](../architecture/actor-supervisor-shared-memory.svg)

### What this comparison can tell us

The first memory-enabled study investigates **ongoing supervision and
within-incident shared-memory adaptation as a combined design**. Under WM-1's
accepted writer scope, the supervisor proposes semantic belief/direction
updates; the actor reads them but does not independently commit beliefs.
The actor-only baseline retains its observed history and reasoning capability,
but has no supervisor producing shared belief updates.

Consequently, an improvement over actor-only would support the combined design
in the conditions studied. It would not isolate the benefit of persistent memory,
extra reasoning, or interruption individually, and it would not demonstrate
reinforcement-learning training. Isolating those effects would require additional
controlled comparisons, outside the initial scope.

Blocking and asynchronous supervision use the same actor configuration,
supervisor configuration, memory rules, and external event schedule. Their
comparison examines the consequences of waiting versus continuing and being
interrupted. Different timing can lead to different observations, memory states,
and review counts; record those differences and their resource costs rather than
claim identical histories or a pure inference-speed comparison.

Evaluate by incident category, including cases where the actor needs no help.
Keep the costs of conservative coordination visible: exact-base rejection may
discard useful advice after a direction-expiry revision, persistent beliefs may
retain mistakes, and frequent meaningful memory updates may repeatedly interrupt
useful work. These are outcomes to measure, not problems to hide by adjusting
the rules until supervision wins.

### Scope decision: supervision, not just routing

On 2026-09-16, the study was refocused from selective per-request deliberation
to cooperation over evolving incident trajectories. A fixed-snapshot,
single-next-step comparison remains an optional preliminary probe. Selective
routing is a secondary architectural alternative, not the primary experiment.

Supervisory feedback may update a working plan or diagnostic direction. It does
not imply online model training, automatic policy changes, or additional
permissions. One workflow owns execution. Guidance from a slower model is still
fallible input, not trusted authorization.

Immediate execution limits must hold before each action; retrospective review
cannot prevent an already completed irreversible action. This study does not
claim hard real-time guarantees or applicability to autonomous vehicle control.

The fast/slow analogy motivates the study; it is not a claim that the system
reproduces human cognition. Jev is an optional model candidate, not a required
dependency or the subject of a predetermined endorsement.

## Research questions

| Question | Evidence sought |
| --- | --- |
| RQ1: In which incident conditions does ongoing supervision improve the actor's trajectory? | Goal progress, appropriate outcomes, unnecessary or repeated actions, and persistence in mistakes, reported by predefined scenario category. |
| RQ2: What is the responsiveness and resource trade-off of asynchronous versus blocking review? | Event-to-action latency, deadlines met, supervisory lag, total actor/supervisor inference cost, and approval demand. |
| RQ3: When does feedback change behavior beneficially, and when does it mislead? | Guidance delivered, applied, rejected, or superseded; time to useful correction; stale or incorrect guidance applied; subsequent outcomes. |
| RQ4: Do execution constraints remain enforced across decision strategies? | Actual execution attempts, gateway decisions, approval handling, and observed side effects under identical controls. |

RQ4 concerns the scenarios and controls exercised by this study. It does not
establish universal safety, model correctness, or whole-system formal verification.

## Study design

Compare three primary architectures within one synthetic incident-response domain:

1. **Fast actor alone:** baseline responsiveness and behavior without supervision.
2. **Blocking supervision:** at defined review checkpoints, the actor waits for
   the supervisor before proceeding.
3. **Asynchronous supervision:** the supervisor reviews the evolving history
   while the actor continues work allowed by the independent controls; applicable
   guidance can influence later decisions.

Retain a deterministic reference where practical to establish whether models
are needed. Hold the actor configuration constant across the primary comparisons,
and use the same supervisor configuration and review-trigger policy in the two
supervised variants. The protocol must specify review checkpoints, pending-review
handling, and guidance application boundaries before evaluation.

Keep execution controls, tool capabilities, initial conditions, approval rules,
and outcome criteria identical. Use matched external event schedules and seeds
under a declared clock model. Do not give a slower run extra time by silently
pausing the environment, or give the supervisor hidden ground truth or future
observations. Within a run, both components can access the same permitted
observation history; every review records the snapshot actually consumed.
Action-dependent observations may diverge across runs and must be recorded,
not falsely presented as identical evidence.

Categorize cases before evaluation by reasoning demand, consequences, evidence
sufficiency/change rate, and opportunity for useful correction. Include routine
cases where the actor already performs well, repeated ineffective diagnostics,
ambiguous evidence, changing state, delayed or incorrect guidance, prohibited
requests, and unavailable supervision. Regulation alone is not a proxy for
reasoning difficulty. Report results by category before aggregating under
explicit workload mixes.

Measure trajectories, not merely the plausibility of supervisory text:

- Progress toward a predefined incident goal and appropriate final outcomes.
- Unnecessary/repeated actions and time spent pursuing an ineffective direction.
- Event-to-action latency and the proportion meeting predefined response deadlines.
- Time from relevant evidence becoming available to useful corrective behavior.
- Guidance lifecycle: issued, delivered, applied, rejected, superseded, and expired.
- Stale or incorrect guidance applied, resulting decision errors, and prohibited side effects.
- Total actor and supervisor usage, inference cost, coordination overhead, and approval demand.

Use predefined simulator/domain criteria with acceptable outcomes and rationale
withheld from the models. A policy-permitted action may still be a poor decision.
Trace an apparent improvement from guidance to subsequent behavior, and use
matched no-supervisor runs before attributing improvement to supervision.
Report model/runtime latency separately from approval waiting time, and scripted
timing experiments separately from live inference timings.

### First experiment boundary: diagnosis and direction

Confirmed on 2026-09-16: the first experiment ends at an evidence-supported
diagnosis and a justified next step, not production remediation or executed
recovery. The actor takes bounded diagnostic actions over an evolving episode.
The supervisor evaluates the trajectory and may suggest a different direction.

Operational writes, restarts, and rollback execution are outside this first
experiment. Retain the inherited controls, but expose only the diagnostic
capabilities required by the experiment. This phase cannot establish production
approval enforcement or remediation effectiveness from its own runs; those
remain separate inherited evidence or later research.

The following episode contract summarizes the design. The reviewed
[experiment specification](../EXPERIMENT_SPEC.md) defines the scheduling and
behavioral baseline; serialized schemas are still to be finalized.

| Element | Proposed definition |
| --- | --- |
| Actor input | Current incident observations, diagnostic results accumulated so far, available actions, and applicable supervisory guidance. |
| Diagnostic actions | Read incident context, inspect service health, query metrics, or inspect logs for declared targets. Additional dependency targets require explicit simulator support. |
| Supervisory input | The same permitted observed history, captured at a recorded review boundary; no future events, hidden diagnosis, or scoring rationale. |
| Supervisory output | A proposed investigative focus or next diagnostic step, referencing the evidence it used. Guidance is advisory and cannot execute tools or grant permissions itself. |
| Actor report | A proposed diagnosis, supporting observation identifiers, and a justified next step; explicit uncertainty or human handoff is permitted where evidence is insufficient. |
| Episode termination | The actor submits its report or a predefined time, action, or cost budget is exhausted. A declaration of completion is not evidence of success. |
| Independent evaluation | Score the submitted report and observed trajectory against the predefined case rubric after termination, without feeding hidden answers back into the run. |

Proposed outcome rubric:

- Accept an evidence-supported diagnosis and an appropriate next step; a case
  may have several acceptable next steps.
- Accept justified uncertainty or handoff in cases whose available evidence
  cannot support a specific diagnosis; do not reward guessing hidden truth.
- Count unsupported conclusions, irrelevant repeated actions, missed response
  deadlines, and inappropriate guidance influence as distinct outcome measures.
- Record malformed outputs, timeouts, and budget exhaustion rather than
  discarding those runs.
- Judge feedback by subsequent actor behavior and outcomes, not by agreement
  with the supervisor or the persuasiveness of its explanation.

The experiment specification defines the accepted scripted timing, action
budgets, review cadence, and guidance application rules. Live response deadlines,
inference-cost budgets, exact serialized names, and numerical scoring thresholds
remain handoff/protocol items. No efficiency-first or quality-first aggregate
objective has been selected; first identify where supervision helps.

### First evolving incident: proposed storyboard

This is a design sketch, not a frozen protocol or an implemented experiment.

| Moment | Observation or behavior to study |
| --- | --- |
| 1. Incident opens | Payments API errors rise. The actor receives initial evidence and can choose bounded diagnostic actions. |
| 2. Local investigation | Instance-focused diagnostics are plausible but may repeat without narrowing the cause. This is a possible behavior, not a forced model outcome. |
| 3. New evidence arrives | A dependency-related signal becomes available on the predefined external schedule, accessible to both components. |
| 4. Supervisory review | A supervisor may identify the ineffective pattern and propose a dependency-focused direction, bound to the observations it reviewed. |
| 5. Continue or wait | The asynchronous actor can continue permitted work during review; the blocking variant waits at its review checkpoint. The harness assesses guidance applicability before it influences later decisions. |
| 6. Report and evaluate | The actor reports its diagnosis and justified next step, or reaches a budget limit. Score the investigation, responsiveness, guidance influence, and cost without executing remediation. |

Variations include recovery before guidance arrives, a wrong supervisory
interpretation, and a routine case needing no correction. Freshness and policy
checks cannot guarantee that every semantically wrong recommendation is caught;
record harmful guidance influence even when its resulting action was permitted.
Production remediation is not performed in this first experiment.

Start with scripted actors and supervisors to exercise timing, guidance
application, and observation capture. These are mechanism checks, not evidence
of model effectiveness. Introduce real models only after the experiment can
record trustworthy outcomes.

### Draft evidence sequence: Payments investigation

This published worked example is for protocol development, not the held-out
evaluation set. All values below are synthetic. The target is a supported
failure-domain diagnosis, not proof of the deepest underlying root cause.

Use logical ticks to describe event ordering for the initial scripted mechanism
exercise. The proposed spacing below is not measured inference latency and
does not imply seconds or milliseconds. Review duration, actor duration,
deadlines, and the mapping for later live-model runs remain to be specified.

| Available from | Evidence | Delivery | What it supports, not a model-visible answer key |
| --- | --- | --- | --- |
| Tick 0 | E0: Payments API error rate is 18%, up from below 1%; p95 latency is 2.6 seconds, previously 0.3 seconds. | Shared incident notification. | There is an active incident; no specific cause is yet established. |
| Tick 4 | E1: All three API instances are ready, CPU is 30-45%, memory is stable, and no application deployment occurred in the previous hour. | Shared monitoring update; details available through service-health and metrics queries. | Weakens an isolated unhealthy-instance or recent-deployment explanation; does not rule out an application defect. |
| Tick 8 | E2: Eight of ten sampled failing requests contain an outbound authorization-service span reaching its configured two-second timeout. The pattern occurs across all API instances; other downstream spans are unchanged. | Shared trace-summary update, with observation identifiers available for citation. | Prioritizes the authorization dependency path, but does not yet distinguish dependency processing delay from the network or caller behavior. |
| Tick 12 | E3: Authorization-service diagnostics show linked requests arriving, with p95 queue wait of 2.3 seconds before approximately 0.1 seconds of processing. Request identifiers link this delay to failed API traces. | Actor must request the relevant dependency metrics/logs; results enter the shared observed history. Availability itself is not a notification that reveals the answer. | Corroborates dependency-side queue delay as the supported failure domain. It does not establish why that queue developed. |
| Tick 16 | E4: A local cache warning appears in a shared log digest. Its rate is unchanged from before the incident, and sampled warning identifiers do not match the failed requests. | Shared monitoring/log update if the episode is still running. | A plausible distraction, not affirmative evidence that cache behavior caused this incident. |
| Tick 24 | Proposed episode time limit, if no report has been submitted. | Harness termination condition, not a model observation about the diagnosis. | Record timeout/budget exhaustion separately from successful diagnosis. This limit is provisional. |

Delivery rules:

- Shared notifications become visible at their scheduled tick, whether or not
  the actor is waiting for review. No component sees a future event.
- An on-demand diagnostic returns only evidence available at its recorded query
  snapshot. Before tick 12, dependency queries may report that the diagnostic
  evidence is not yet available; they must not reveal E3 early.
- Query results join the observation history accessible to both components.
  The supervisor has no separate diagnostic-tool access in this first draft.
  A review already in flight retains its captured snapshot; newly arriving
  evidence does not silently rewrite its input.
- Log both when evidence becomes available in the environment and when it is
  actually observed. Distinguish delay in discovering evidence from delay in
  interpreting or acting on evidence already seen.
- Case labels, hidden world state, the schedule of future evidence, and the
  interpretation column above are evaluator/development material, not prompt
  inputs. The actor may know its declared remaining time/action budget.

Proposed evaluator expectations:

| Evidence actually observed when reporting | Appropriate conclusions and direction |
| --- | --- |
| E0 only, or E0 plus E1 | Keep the diagnosis uncertain; choose relevant diagnostics. A confident specific root-cause claim is unsupported even if it guesses the hidden cause. |
| E2 but not E3 | Report the dependency path as a hypothesis and seek dependency-side evidence. Do not assert queue delay as established. |
| E2 and E3 | Identify authorization-service queue delay as the supported failure domain and propose a justified next step, such as investigating its queue/workers or handing the evidence to the owning team. Do not execute a production change. |
| Approaching the budget limit without sufficient evidence | A report may explain the observed limitation and justified uncertainty/handoff. Score evidence coverage, investigative choices, and budget use separately so premature abstention is not treated as equivalent to a well-supported diagnosis. Runs reaching the limit without a report remain timeouts. |

Neither architecture is required to get stuck. A fast actor that interprets E2,
collects E3, and reports appropriately without help is a valid null result for
the value of supervision. For an apparent correction, record the actor's prior
direction, the guidance and cited evidence, and the later action; compare matched
actor-only runs before attributing the change to the supervisor.

World variants should include an actual local-instance fault, insufficient
evidence to distinguish the failure domain, and recovery before advice arrives.
Recovery variants must replace subsequent active-fault diagnostics with fresh
recovery observations, while retaining older observations as historical.
Delayed or deliberately incorrect supervisory guidance is a separate scripted
fault-injection dimension, not a replacement for naturally observed model errors.
Held-out variants must vary signals and presentation without exposing labels
that reveal the expected diagnosis or whether supervision should help.

Before the comparative evaluation:

- Define metrics, acceptable quality/response thresholds, useful-effect criteria, run budgets, and the repeated-run procedure.
- Specify the clock, event schedule, review cadence, guidance expiry, and response-deadline semantics.
- Separate development scenarios from held-out evaluation scenarios.
- Freeze model/configuration versions, supervision rules, scenario categories, and outcome criteria.
- Capture actual runtime observations rather than supplying expected observations.
- Report variability, failed runs, limitations, and negative results.
- Use synthetic data; model calls remain subject to provider, data-sharing, and budget constraints.

## Current status and provenance

**Status: research framing established; comparative experiments not implemented.**

Derived from
[agentic-harness-validation](https://github.com/katarinasvedman-ms/agentic-harness-validation)
at commit
[`ebe882a7b9cb30544292c03c166f466e46a5eee1`](https://github.com/katarinasvedman-ms/agentic-harness-validation/commit/ebe882a7b9cb30544292c03c166f466e46a5eee1).
The source history is retained. Uncommitted changes in the source working
directory are not part of this baseline. The original repository remains the
governance reference implementation.

The inherited foundation contains canonical plans, deterministic policy, a
governed gateway, exact approval, bounded plan verification, an incident
simulator, and audit/evaluation scaffolding. It is not yet a live fast/slow
model experiment:

- The local workflow consumes a supplied plan; model integration is a separate spike.
- The React console uses mock data, and the BFF includes a represented workflow attestation.
- The JSON evaluation fixtures supply observations; they are not comparative model-run results.
- Formal claims remain limited to the bounded model and assumptions in the inherited verification documentation.

## Research plan and stopping point

| Phase | Exit condition |
| --- | --- |
| 1. Protocol | Questions, metrics, scenarios, success criteria, and budget agreed before comparative results. |
| 2. Experimental foundation | Recorded baseline and one evolving scenario with scripted timing, guidance, and attributable runtime observations. |
| 3. Comparison architectures | Actor-only, blocking supervision, and asynchronous supervision run under the same execution controls; add a deterministic reference where practical. |
| 4. Development and freeze | Development completed; held-out evaluation procedure and configurations frozen. |
| 5. Experiments and analysis | Repeated comparisons answer each research question with limitations and uncertainty. |
| 6. Findings | Reproducible example, research report, and concise presentation suitable for review and community discussion. |

Stop when these research outcomes are delivered. Do not expand the project into
generic connectors, multi-tenancy, a model marketplace, production deployment,
or a comprehensive dashboard. Do not add complexity merely to make the hybrid
strategy appear to win.

Inherited product roadmaps, deployment scaffolding, and customer pitch material
are historical context, not the scope of this study. Sharing results requires
appropriate review and must not expose internal or customer material.

## Local development

Prerequisites:

- .NET SDK 10.0.303 or a compatible .NET 10 feature band.
- Node.js 22 or newer.
- PowerShell 7.
- Dafny 4.11.0 for formal-verification work.

```powershell
npm install
dotnet tool restore
pwsh .\scripts\validate.ps1
```

The inherited foundation builds without Azure resources. Cloud deployment is
not a milestone of this research study. The inherited deployment workflow is
manual; it is not required for the experiment.

The local Copilot integration spike can be invoked with:

```powershell
dotnet run --project .\src\GovernedAgent.Host -- --copilot-spike
```

At the inherited baseline, ADR 0001 records a Windows SDK/CLI timestamp
wire-format blocker. This study has not re-established live model compatibility.
Do not weaken or bypass the protocol check.

## Repository structure

- `src/GovernedAgent.Core` - canonical plans, actions, approvals, decisions, and audit contracts.
- `src/GovernedAgent.Governance` - policy, approval, gateway, budgets, and audit controls.
- `src/GovernedAgent.Simulator` - deterministic incident and service-state simulator.
- `src/GovernedAgent.Host` - Microsoft Agent Framework and Copilot inner-loop host.
- `src/GovernedAgent.Console` - React governance console.
- `src/GovernedAgent.Console.Bff` - authenticated console backend.
- `src/plan-verifier` - deterministic TypeScript validator and formal model boundary.
- `tests` - unit, integration, conformance, security, and verifier test suites.

## Inherited reference documentation

These documents describe the original governance demo. Use the research scope
above for this repository's goals; inherited product and deployment requirements
are not research commitments.

- [Local demo guide](../archive/DEMO_GUIDE.md)
- [Local operations runbook](../archive/OPERATIONS_RUNBOOK.md)
- [Governed agent runtime architecture](../architecture/governed-agent-runtime.excalidraw)
- [Product Requirements Document](../archive/PRD.md)
- [Functional Requirements Document](../archive/FRD.md)
- [Threat Model](../THREAT_MODEL.md)
- [Verification Specification](../VERIFICATION_SPEC.md)
- [Credential-free hosted-agent release](../archive/RELEASE_DEPLOYMENT.md)
- [ADR 0001: GitHub Copilot SDK inner loop](../adr/0001-copilot-sdk-inner-loop.md)