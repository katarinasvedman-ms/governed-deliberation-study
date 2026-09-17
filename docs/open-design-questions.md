# Open design questions

These are possible follow-up studies, not additional experimental conditions,
implementation commitments, or results. The current design and contracts remain
unchanged.

## Compute-matched actor self-reflection

Consider an actor-only self-reflection baseline with a matched token budget.
It could help distinguish the contribution of a separate supervisor from the
benefit of extra reasoning. The prompting, timing, budget matching, and evaluation
procedure are not yet decided. This baseline is not implemented or part of the
current three-condition comparison; [Reflexion](related-work.md#reflexion) is
relevant prior work, not a ready-made compute-matched protocol.

## Calibrated fast layer

A possible later extension is to compare a natively calibrated fast decider
such as TypeSafe Jev (early access) with a selected LLM-based fast actor.
TypeSafe describes Jev as calibrated; calibration and usefulness as an
escalation signal would need evaluation on the actual workload.

This is **not yet decided or implemented, and there are no results**. The
repository currently has scripted actors, not a live LLM fast layer. Models,
access, budgets, and measurement methods have not been selected for this
comparison. Confidence-triggered escalation is a separate question from ongoing
supervision and must not silently replace the present study.

Existing product references:
[TypeSafe introduction](https://typesafe.ai/blog/introducing-system-one-models-and-jev)
and [typed decision interface](https://docs.typesafe.ai/introduction.md).

## Live timing and inference

Logical ticks specify ordering and assumed scripted durations. Empirical
comparisons require measured inference and coordination latency, a declared
external-event schedule, resource accounting, and a reproducible relationship
between elapsed time and environmental change. A tick-to-time mapping alone
does not establish real-model speed or cost.

See the [experiment specification](EXPERIMENT_SPEC.md) and the
[remaining protocol work](governance/COPILOT_HANDOFF.md#7-deferred-research-protocol)
for the existing unresolved methodological questions. No numerical thresholds
or new scenarios are selected here.
