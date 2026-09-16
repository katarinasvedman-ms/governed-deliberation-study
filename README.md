# Governed Deliberation Study

**Fast action, ongoing reflection, fixed authority.**

An experimental research project investigating when a fast actor benefits from
a slower, ongoing supervisor under independently enforced execution controls.
This is a bounded study, not an agent platform or production service.

## Discussion pitch

Open [the research pitch](docs/pitch.html) for team, customer, and community
discussions. It is a self-contained HTML page with illustrative scenarios,
research questions, study boundaries, and a print/PDF option. It presents a
proposal, not experimental results. The previous governance-demo pitch remains
available in the source history.

## Purpose and intended outcome

Primary question:

> Under what conditions can a slower, ongoing supervisor improve a fast agent's
> behavior over time without sacrificing required responsiveness or weakening
> independently enforced execution constraints?

The fast actor responds to incident events. The slower supervisor reviews the
observed trajectory, evaluates progress, and proposes feedback or a change in
direction. In the asynchronous architecture, the actor may continue permitted
work while review is pending; it does not wait for every supervisory response.

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

### First evolving incident: proposed storyboard

This is a design sketch, not a frozen protocol or an implemented experiment.

| Moment | Observation or behavior to study |
| --- | --- |
| 1. Incident opens | Payments API errors rise. The actor receives initial evidence and can choose bounded diagnostic actions. |
| 2. Local investigation | Instance-focused diagnostics are plausible but may repeat without narrowing the cause. This is a possible behavior, not a forced model outcome. |
| 3. New evidence arrives | A dependency-related signal becomes available on the predefined external schedule, accessible to both components. |
| 4. Supervisory review | A supervisor may identify the ineffective pattern and propose a dependency-focused direction, bound to the observations it reviewed. |
| 5. Continue or wait | The asynchronous actor can continue permitted work during review; the blocking variant waits at its review checkpoint. The harness assesses guidance applicability before it influences later decisions. |
| 6. Observe the trajectory | Measure whether the investigation improves, how promptly it responds, what guidance it used, and what the additional work cost. |

Variations include recovery before guidance arrives, a wrong supervisory
interpretation, and a routine case needing no correction. Freshness and policy
checks cannot guarantee that every semantically wrong recommendation is caught;
record harmful guidance influence even when its resulting action was permitted.
No remediation may bypass the existing approval requirements.

Start with scripted actors and supervisors to exercise timing, guidance
application, and observation capture. These are mechanism checks, not evidence
of model effectiveness. Introduce real models only after the experiment can
record trustworthy outcomes.

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

- [Local demo guide](docs/DEMO_GUIDE.md)
- [Local operations runbook](docs/OPERATIONS_RUNBOOK.md)
- [Governed agent runtime architecture](docs/architecture/governed-agent-runtime.excalidraw)
- [Product Requirements Document](docs/PRD.md)
- [Functional Requirements Document](docs/FRD.md)
- [Threat Model](docs/THREAT_MODEL.md)
- [Verification Specification](docs/VERIFICATION_SPEC.md)
- [Credential-free hosted-agent release](docs/RELEASE_DEPLOYMENT.md)
- [ADR 0001: GitHub Copilot SDK inner loop](docs/adr/0001-copilot-sdk-inner-loop.md)