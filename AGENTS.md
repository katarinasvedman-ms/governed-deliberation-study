# Governed Deliberation Study Engineering Guide

## Current preparation phase

The reviewed design baseline is [EXPERIMENT_SPEC.md](docs/EXPERIMENT_SPEC.md)
v1.0 plus WM-1. On 2026-09-16, the user approved
`T1-WM-1-candidate-3` / `1.0-candidate.3` as the first scripted-build contract
baseline, including C4/M6, C7/M7, M3, C8/M8 and the listed engineering limits.
Previously accepted decisions remain accepted; C6 remains deferred.

Use [COPILOT_HANDOFF.md](docs/COPILOT_HANDOFF.md) to scope later authorized
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

## Research scope

This repository is a bounded research study derived from
`agentic-harness-validation`, not a general-purpose agent platform. The README
defines the research questions, source baseline, comparison strategies, and
stopping point. Inherited product and deployment documents are reference
material, not the research roadmap.

- The primary study is ongoing supervision over evolving trajectories, not selective per-request routing or isolated fixed-snapshot decisions.
- The first experiment ends at diagnosis and a justified next step using bounded diagnostic actions; do not introduce production remediation or count model-declared completion as success.
- Preserve the same execution controls across actor-only, blocking-supervision, and asynchronous-supervision architectures; retain a deterministic reference where practical.
- Keep the actor configuration constant, and match supervisor configuration and review triggers across supervised variants.
- Match external event schedules and declare the clock model; record action-dependent evidence differences and the snapshot each review actually consumed.
- Keep one workflow owner; models propose decisions but do not grant execution authority.
- Actionable supervisory interrupts invalidate pending actor decisions and require reconsideration; cancellation is best effort, but obsolete-result suppression at dispatch is mandatory. Already-dispatched tools may finish.
- Prefer Agent Framework coordination with an independent experiment clock; require parallel-progress and interruption feasibility evidence, not an assumption that workflow steps equal ticks.
- Supervisory guidance is fallible: bind it to evidence, define applicability/expiry, and record when it changes later behavior. It must not automatically change policy, permissions, or model weights.
- The actor's immediate constraints remain enforceable while review is pending or unavailable; retrospective supervision is not the sole safety boundary.
- Keep confidence and model assertions separate from trusted metadata, policy, and approval.
- Record actual runtime observations; fixture assertions are not empirical model results.
- Evaluate trajectories and responsiveness by predefined scenario category, including wrong/late guidance and cases where supervision adds no value.
- Separate development and held-out evaluation, and freeze configurations before comparative evaluation.
- Report negative findings, failures, costs, variability, and limitations without tuning on held-out results.
- Keep scripted fault injection separate from naturally observed model behavior.
- Use synthetic research data and enforce provider/data-sharing constraints before model calls.
- Do not build platform features or make deployment a prerequisite for the study.

## Inherited integration guidance

This project was built with the microsoft-foundry skill. Before working on or answering questions about foundry agents, read the microsoft-foundry skill first.

## Engineering constraints

- Keep the local demo runnable without Azure resources.
- Treat model output, Copilot permission prompts, and completion signals as untrusted input.
- Route every side effect through the governed gateway; tool handlers must not call operational systems directly.
- Fail closed when policy, verification, approval, audit, or trusted metadata is unavailable or indeterminate.
- Keep built-in shell, filesystem, unrestricted URL, and unapproved MCP capabilities disabled.
- Preserve matching canonicalization at the pre-tool hook and gateway.
- Do not claim whole-agent formal verification. Proof claims apply only to the bounded deterministic plan model.
- Never run Azure authentication commands for the user or commit environment values and credentials.
- Before any `azd` command, read the Microsoft Foundry `azd-guidance` skill and set `AZURE_DEV_USER_AGENT=microsoft_foundry_skill` for that command only.

## Validation

Run the repository validation command before committing:

```powershell
pwsh .\scripts\validate.ps1
```
