# Governed Deliberation Study

**Fast decisions, slow reasoning, fixed authority.**

An experimental research project investigating when fast structured judgments
and slower reasoning should cooperate under independently enforced execution
controls. This is a bounded study, not an agent platform or production service.

## Purpose and intended outcome

Investigate whether selective deliberation improves incident-response outcomes,
latency, and cost while preserving the same execution constraints across
decision strategies.

The outcome is a reproducible comparison, a small reference example, and a
research report explaining where the approach helps, where it fails, and what
coordination controls it requires. Negative findings are valid outcomes.

Working hypothesis, not a demonstrated result:

> Selective deliberation can offer a useful trade-off compared with always-fast
> or always-slow approaches on some workloads, but only when escalation and
> evidence handling are designed explicitly.

The fast/slow analogy motivates the study; it is not a claim that the system
reproduces human cognition. Jev is an optional model candidate, not a required
dependency or the subject of a predetermined endorsement.

## Research questions

| Question | Evidence sought |
| --- | --- |
| RQ1: Does selective deliberation offer a useful trade-off compared with simpler approaches? | Task outcomes, unnecessary actions, end-to-end latency, inference cost, and approval demand. |
| RQ2: When should the fast path escalate? | Confidence-only routing compared with routing that also considers consequences, ambiguity, and evidence freshness. |
| RQ3: What happens when evidence changes during deliberation or approval? | Invalidation of stale proposals, appropriate reconsideration, and resulting workflow outcomes. |
| RQ4: Do execution constraints remain enforced across decision strategies? | Actual execution attempts, gateway decisions, approval handling, and observed side effects under identical controls. |

RQ4 concerns the scenarios and controls exercised by this study. It does not
establish universal safety, model correctness, or whole-system formal verification.

## Study design

Compare four strategies within one synthetic incident-response domain:

1. **Deterministic rules:** establish whether AI is needed for the scenarios.
2. **Fast-only:** bounded model judgments without slower deliberation.
3. **Reasoner-only:** deliberation for every case.
4. **Selective hybrid:** fast judgments with explicit escalation.

Keep the execution controls, tool capabilities, initial evidence, approval
rules, and outcome criteria identical across strategies. Further evidence
gathering must use the same permitted tools, with its cost and latency recorded.
Within the hybrid, compare confidence-only routing with risk-and-evidence-aware
routing. One workflow owns execution; cooperating models do not receive
independent authority to perform side effects.

Scenario families include familiar incidents, missing or contradictory evidence,
cases where no remediation is appropriate, changing state or evidence, prohibited
requests, and unavailable dependencies. Scripted bad predictions exercise the
harness separately; do not report them as observed model errors.

For each run, record outcome correctness, unnecessary actions, prohibited side
effects, escalation and approval demand, timing, inference usage and cost,
evidence versions, model/configuration identity, and decision provenance.
Use predefined simulator/domain criteria rather than model opinion alone.
Report model/runtime latency separately from approval waiting time.

Before the comparative evaluation:

- Define metrics, useful-effect criteria, run budgets, and the repeated-run procedure.
- Separate development scenarios from held-out evaluation scenarios.
- Freeze routing rules, model/configuration versions, and outcome criteria.
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
| 2. Experimental foundation | Recorded baseline and one scenario with attributable runtime observations. |
| 3. Decision strategies | All four strategies run through the same experimental interface and execution controls. |
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
- [Standalone solution pitch](docs/pitch.html)