# T0: Agent Framework feasibility evidence

**Architecture accepted for the scripted experiment on 2026-09-16.
NO-GO for the tested shared-superstep fan-out graph. T1 implementation remains
behind a separate contract-review gate.**

The user accepted isolated Agent Framework actor and supervisor invocations,
with one experiment coordinator owning scheduling, immutable snapshots,
decision generations, interruption, and dispatch eligibility. The tested shared
fan-out graph must not be used for the asynchronous treatment. All limitations
below remain in force; acceptance does not establish live-provider cancellation
or concurrency support. Only the [T1 contract proposal](T1_CONTRACT_PROPOSAL.md)
is authorized next, as documentation for review, not contract implementation.

Implemented only the authorized T0 gate from [COPILOT_HANDOFF.md](COPILOT_HANDOFF.md)
against [EXPERIMENT_SPEC.md v1.0](EXPERIMENT_SPEC.md), section 3.4.
This is executable, credential-free mechanism evidence, not an experiment
runner, diagnosis evaluator, live-agent result, or comparative speedup.

## Reproduce

From the repository root, with the SDK in `global.json`, Node.js, and the
repository's npm dependencies installed:

```powershell
pwsh .\scripts\test-t0-feasibility.ps1
```

On a fresh checkout, run `npm ci` first. The existing host build target builds
the Node verifier. The command restores NuGet dependencies as needed, builds
the existing integration-test project, and selects only the T0 tests. Package
restore is the only step that needs package-network access; the fixture has no
service credentials, model clients, HTTP clients, Copilot inference sessions,
Azure authentication, deployment, or operational writes.

After restore, the offline form is:

```powershell
pwsh .\scripts\test-t0-feasibility.ps1 -NoRestore
```

Each invocation writes to a new ignored `.artifacts\t0-feasibility\<run-id>`
directory. `t0.trx` contains the assertions' outcomes and actual causal traces;
`provenance.txt` identifies the source revision, dirty worktree, SDK, scope,
clock, and loaded workflow assembly; `source-sha256.txt` identifies the probe,
design inputs, and dependency declarations; `project.assets.json` captures the
resolved dependency graph. These are local evidence, not files to commit.
Any test failure makes the script fail. A two-minute runner hang timeout and
30-second fixture watchdogs are engineering safeguards, not research deadlines.

The cancellation-ignoring Query and Report cases each execute twice and assert
exact equality of their causal traces. Artifact directory IDs, plan IDs,
audit hashes, TRX timings, and measured barrier delays are not canonical
comparison fields.

## Arrangement actually exercised

All new C# code is test-only, under
`tests\GovernedAgent.IntegrationTests\T0`; no new application entry point or
test project was introduced.

`FrameworkProbe` uses actual `Executor<TInput, TOutput>`, `WorkflowBuilder`,
`WithOutputFrom`, and `InProcessExecution.RunAsync` APIs. Each scripted actor
turn and supervisor invocation gets its own executor, workflow run, immutable
input snapshot, and run lifetime. The adapter consumes actual
`WorkflowOutputEvent` values and asserts framework invocation/completion events,
not just a callback that pretends the framework ran. All runs are disposed.

One `GovernedProbeOwner` owns guidance acceptance, decision generations, report
submission, and dispatch. The framework executes the scripted invocations;
the test owner supplies cross-run ordering and authority. It is **not** a claim
that a single Agent Framework graph inherently provides asynchronous supervision.
There is no shared mutable conversation/session, and a replacement invocation
does not wait for the obsolete invocation to finish.

For each diagnostic, the owner constructs the existing canonical plan and trusted
identity, then calls `LocalDeterministicAgentWorkflow`. This uses the actual
`NodePlanVerifier`, canonicalizer, diagnostic-only `ToolRegistry`,
`DefaultDenyPolicyEvaluator`, approval/budget/kill-switch/audit controls, and
`GovernedToolGateway`. The existing `SimulatorGovernedToolExecutor` executes
only `query_metrics` for the original synthetic Payments service.

A test-only executor decorator adds a generation check **after** asynchronous
verification/policy and holds the same owner lock as guidance acceptance through
the actual simulator dispatch. A check only before starting verification would
be insufficient. Report submission uses that same boundary. The decorator adds
no permission and cannot bypass the existing gateway.

For the already-dispatched case, the real simulator read is sampled at dispatch;
an explicit signal delays delivery of its result. The lock is not held while
delivery waits. The accepted interrupt cannot erase that observation or cause
the operation to execute again.

## Observed evidence

Observed locally on September 16, 2026, from source revision
`2612294a32b39f611bf8b3b0aab84577fb79f7e7` plus the uncommitted T0 changes:
Windows, .NET SDK 10.0.303, test runtime .NET 10.0.12, Node v22.16.0,
workflow assembly 1.17.0.0. The T0 selection passed **10/10** tests.

The main Query trace recorded this order (the Report variant also passed):

| Sequence | Actual recorded event |
| --- | --- |
| 01 | Supervisor framework invocation starts with its captured snapshot. |
| 02-08 | External notification is observed; actor starts, returns Query, passes verification, dispatches, and retains the real simulator observation. |
| 09 | Diagnostic is complete while the supervisor result remains pending. |
| 10-15 | Next actor invocation starts; review returns; guidance increments generation 0 to 1, invalidates that actor, and requests cancellation. The call remains unsettled. |
| 16-22 | Replacement invocation receives the new snapshot/guidance, completes a governed diagnostic, and finishes reconsideration while the obsolete call is still pending. |
| 23-24 | Obsolete invocation finally returns with `cancellationIgnored=True`; its decision is suppressed. |
| 25-27 | A current Report marker is accepted as a positive control. |

Before the current Report, the assertions observe exactly two diagnostic
dispatches, two retained results, four intact audit records, and no submitted
report. Neither the obsolete Query nor the obsolete Report changes that state.
Captured snapshots do not acquire later observations or guidance.

Additional executable coverage:

| Test mechanism | Observation and specification mapping |
| --- | --- |
| Delayed review plus independent runs | Actor observes an external event and completes its diagnostic before review completion; section 3.4 / A19. |
| Cancellation-ignoring Query and Report | Reconsideration finishes before the obsolete call settles; late output cannot dispatch or report; A14. |
| Both decisions already returned, guidance accepted first | Query and Report suppressed with no gateway call; boundary-priority portion of A15, **not** the T2 same-tick scheduler. |
| Interrupt while real verification is paused | Verification later succeeds, but the dispatch decorator rejects `obsolete_decision`; zero actual tool executions. |
| Interrupt after dispatch | One completion, one retained observation, no duplicate execution, intact audit; A16. |
| Pending review with kill switch, missing verifier, or exhausted budget | Explicit respective failure reason, no diagnostic effect, no report. |
| Shared fan-out graph | Actor handler returns, but its downstream executor starts only after the held review returns. |

The shared-graph probe uses actual `AddFanOutEdge` and `AddEdge` execution.
One recorded run measured 1.990 ms from actor-handler return to downstream entry,
and 1.148 ms from review release to downstream entry. These values include a
test-controlled hold and local runtime overhead, vary by run, and are **not**
model latency, a performance threshold, or an experimental speedup. The causal
barrier assertion, not these magnitudes, determines the graph's suitability.

The existing `pwsh .\scripts\validate.ps1` also passed: 105 .NET tests
(74 integration, including these 10; 20 unit; 10 security; 1 conformance),
frontend/verifier lint and builds, seven Node tests, 17 verified Dafny obligations,
and the guarantee-report check. This preserves the original demo's recovery
expectations and bounded proof claims; it does not extend those proofs to T0.

## Dependency change

Added only `Microsoft.Agents.AI.Workflows` **1.17.0** to central package versions
and as a reference in the existing integration-test project. The inherited host
still references `Microsoft.Agents.AI.GitHub.Copilot` 1.17.0 and
`GitHub.Copilot.SDK` 1.0.9 unchanged. No application package references, deployment
settings, strict plan schemas, simulator defaults, or governance implementation
were modified.

This version matches the inherited Agent Framework dependency line rather than
upgrading the application. Comparing resolved graph reachability with and without
the Workflows root identified these added packages:

| Package | Resolved version |
| --- | --- |
| Microsoft.Agents.AI.Workflows | 1.17.0 |
| Microsoft.Agents.AI | 1.17.0 |
| Microsoft.Extensions.AI | 10.7.0 |
| Microsoft.Extensions.AI.Evaluation | 10.7.0 |
| Microsoft.Extensions.Caching.Abstractions | 10.0.9 |
| Microsoft.Extensions.Compliance.Abstractions | 10.5.0 |
| Microsoft.Extensions.ObjectPool | 10.0.6 |
| Microsoft.Extensions.VectorData.Abstractions | 10.7.0 |
| Microsoft.ML.Tokenizers | 2.0.0 |
| System.Numerics.Tensors | 10.0.9 |
| Google.Protobuf | 3.30.2 |

No resolved version shared with the inherited host changed. The package carries
broader functionality than this probe uses; no evaluation/model integration from
those transitive dependencies is instantiated. The exact resolved graph is
retained in each successful evidence directory, rather than claiming a new
repository-wide lockfile policy.

API choices were checked against the restored 1.17.0 assembly/XML documentation
and [versioned NuGet package](https://www.nuget.org/packages/Microsoft.Agents.AI.Workflows/1.17.0).
Microsoft's [workflow execution documentation](https://learn.microsoft.com/en-us/agent-framework/workflows/workflows)
describes the superstep barrier; its
[executor documentation](https://learn.microsoft.com/en-us/agent-framework/concepts/workflows/executors)
describes typed handlers and outputs. Those are rolling documentation pages;
the repository's pinned executable probes, not newer documentation alone,
establish the behavior reported here.

## Limitations and architecture review gate

- **Shared graph limitation:** the tested actor-chain/supervisor fan-out cannot
  advance the actor chain independently. Do not use it unchanged as the
  asynchronous experimental treatment.
- **Accepted scripted arrangement:** isolated per-invocation framework workflows, with
  one owner providing cross-run scheduling, snapshots, result delivery, and an
  atomic dispatch boundary. The tested arrangement avoids the shared barrier,
  but cross-run checkpointing, recovery, lifecycle accounting, and cancellation
  are adapter responsibilities, not demonstrated framework guarantees.
- **Cancellation scope:** a separate component cancellation token models a
  best-effort provider request. The scripted component deliberately ignores it;
  the framework run stays alive to return the late output. A separate runtime
  watchdog bounds test lifetime. This does not demonstrate that cancelling a
  framework run forcibly stops its handler, or that a real provider supports
  concurrent sessions, cooperative cancellation, or independent replacement.
- **Time:** numbered probe stages are explicitly advanced by the test driver,
  never by workflow supersteps. The fixed simulator/governance UTC time makes
  the fixture repeatable. The section 3 logical clock, durations, horizon,
  phase ordering, evolving E0-E4 world, and blocking comparison are not
  implemented. Stopwatch measurements are separate.
- **Scripted scope:** Focus and Report are test-only markers, not the finalized
  guidance/report contracts or evidence-supported diagnoses. Only one authored
  guidance cue is accepted; expiry, applicability, duplicates, wrong advice,
  budgets for repeated interruption, and complete actor readiness semantics
  remain later work. No serialization/rubric/protocol open item was filled in
  with an invented research choice.
- **Resource scope:** each test has finitely many invocations, explicitly
  released completions, watchdogs, and awaited cleanup. No production limit for
  indefinitely draining calls or live token/cost accounting is established.
  No model effectiveness, zero model cost estimate, or whole-agent proof follows.

**Stop/go disposition:** the user accepted the isolated-run arrangement for the
scripted experiment on 2026-09-16, retaining the ownership/lifecycle trade-offs
above. Stop after the T1 documentation proposal for contract review. Neither
these passing tests nor architecture acceptance authorize T1 contract code,
T2-T5, or live models. No commit or push is authorized.
