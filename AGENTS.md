# Governed Deliberation Study Engineering Guide

## Research scope

This repository is a bounded research study derived from
`agentic-harness-validation`, not a general-purpose agent platform. The README
defines the research questions, source baseline, comparison strategies, and
stopping point. Inherited product and deployment documents are reference
material, not the research roadmap.

- Preserve the same execution controls across deterministic, fast-only, reasoner-only, and hybrid strategies.
- Keep one workflow owner; models propose decisions but do not grant execution authority.
- Keep confidence and model assertions separate from trusted metadata, policy, and approval.
- Record actual runtime observations; fixture assertions are not empirical model results.
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
