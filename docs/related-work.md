# Related work

This page situates the study among the works below; it is not an exhaustive
literature review. Descriptions distinguish each work's mechanism from the
incident-diagnostics comparison in this repository.

## AgileThinker

Yule Wen et al., **Real-Time Reasoning Agents in Evolving Environments**,
2025 preprint. [Paper](https://arxiv.org/abs/2511.04898) |
[Architecture](https://arxiv.org/html/2511.04898v1#S3).

AgileThinker runs a planning thread alongside a time-bounded reactive thread
that can consume partial planner reasoning while the environment evolves.
It is a close concurrency precedent; this study instead makes supervisor
updates explicit provisional memory transactions, with coordinator-controlled
commitment, interruption, stale-result handling, and execution authority.
The supplied ICLR 2026 venue claim was not verified in the primary material
consulted, so this page cites the preprint.

## SwiftSage

Bill Yuchen Lin et al., **SwiftSage: A Generative Agent with Fast and Slow
Thinking for Complex Interactive Tasks**, NeurIPS 2023.
[Paper](https://arxiv.org/abs/2305.17390).

SwiftSage combines a smaller imitation-trained action model with LLM
deliberation triggered by conditions such as stalled progress, invalid actions,
critical decisions, or exceptions; its planning supports within-task reflection.
Its documented coordination switches to a generated action buffer, whereas this
study compares waiting for supervision with continued action during review under
the same operational constraints.

## DPT-Agent

Shao Zhang et al., **Leveraging Dual Process Theory in Language Agent Framework
for Real-time Simultaneous Human-AI Collaboration**, ACL 2025.
[Paper](https://arxiv.org/abs/2502.11882) |
[Architecture](https://arxiv.org/html/2502.11882v5#S4).

DPT-Agent asynchronously infers beliefs about a human partner and updates
behavioral guidelines from interaction history; a code-as-policy component
translates them into behavior for an autonomously operating finite-state machine.
This is particularly close prior work for within-episode belief-guided action:
our study uses incident diagnostics and explicitly validated, versioned
provisional memory, stale-update rejection, and independent execution controls
rather than claiming that asynchronous belief updates are a new pattern.

## Slow and Fast AI

Grady Booch et al., **Thinking Fast and Slow in AI**, AAAI 2021
(preprint 2020). [Publication](https://doi.org/10.1609/aaai.v35i17.17765) |
[Author manuscript](https://arxiv.org/abs/2010.06002).

This conceptual precursor discusses introspection and governance for selecting,
switching, and combining fast and slow capabilities, including asynchronous
specialized components. Our study explores a narrower operational setting with
executable coordination contracts; governance of deliberation is not a new idea
introduced here.

M. Bergamaschi Ganapini et al., **Fast, slow, and metacognitive thinking in AI**,
*npj Artificial Intelligence* 1, article 27 (2025).
[Publication](https://www.nature.com/articles/s44387-025-00027-5).

SOFAI combines fast and slow solvers through a separate metacognitive module
and reports a constrained-navigation instantiation considering decision quality
and resource consumption. This study instead examines ongoing incident
supervision and explicit memory/execution boundaries; the SOFAI description here
is limited to its verified abstract, not unverified detailed scheduling rules.

## Reflexion

Noah Shinn et al., **Reflexion: language agents with verbal reinforcement
learning**, NeurIPS 2023.
[Paper](https://arxiv.org/abs/2303.11366) |
[Proceedings](https://proceedings.neurips.cc/paper_files/paper/2023/hash/1b44b878bb782e6954cd888628510e90-Abstract-Conference.html).

Reflexion uses actor, evaluator, and self-reflection components to retain verbal
feedback in episodic memory and improve subsequent trials without model-weight
updates. Our study concerns updates during the same evolving incident, potentially
while the actor is still working; an actor-only, compute-matched self-reflection
control is an [undecided future comparison](open-design-questions.md#compute-matched-actor-self-reflection),
not an existing experimental condition.

## AI control and trusted monitoring

Ryan Greenblatt et al., **AI Control: Improving Safety Despite Intentional
Subversion**, 2023 preprint; ICML 2024.
[Paper](https://arxiv.org/abs/2312.06942) |
[Proceedings](https://proceedings.mlr.press/v235/greenblatt24a.html).

AI control studies protocols that use trusted monitoring and limited oversight
to identify suspicious output from a capable, potentially subversive model.
It is relevant to separating useful proposals from acceptance authority, but this
study does not evaluate intentionally subversive actors or reproduce that threat
model; a slower supervisor is not automatically a trusted monitor.

## Talker-Reasoner

Konstantina Christakopoulou, Shibl Mourad, and Maja Mataric,
**Agents Thinking Fast and Slow: A Talker-Reasoner Architecture**,
2024 preprint. [Paper](https://arxiv.org/abs/2410.08328) |
[Architecture](https://arxiv.org/html/2410.08328v1#S3.SS2).

Talker-Reasoner separates fast conversational responses from slower reasoning,
tool use, planning, and belief formation, communicating primarily through memory
while acknowledging delayed beliefs and optional waiting. Here the fast actor
performs bounded diagnostic actions, while supervisor proposals pass through
explicit memory, interruption, and execution controls.

## Positioning

Fast/slow selection is well studied, and the cited work reports concurrent
reasoning and action in other environments. This study focuses on concurrency
combined with independently enforced execution authority and explicit stale-advice
handling in incident response, including versioned within-incident beliefs.

That is a bounded evaluation focus, not a claim to have invented concurrent
agents, shared memory, governance of deliberation, or a universally better
architecture. Current evidence consists of scripted feasibility and contract
validation; comparative model effectiveness remains to be measured.
