# Approved T1 contract baseline

| Field | Value |
| --- | --- |
| Status | Approved baseline for the first scripted T1 build; T2-T5 remain paused |
| Date | 2026-09-16 |
| Candidate ID | T1-WM-1-candidate-3 |
| Approved wire version | `1.0-candidate.3`; candidate literal preserved for traceability |
| Design source | [Experiment specification, including decision status in section 12](EXPERIMENT_SPEC.md#12-approved-amendment-within-incident-working-memory) |
| Architecture | Accepted isolated Agent Framework invocations; one experiment coordinator |
| Supersedes | `T1-WM-1-candidate-2`, the earlier unapproved T1 draft, and its separate WM-1 contract addendum |

**This is the single authoritative T1 contract baseline.** Its complete
shapes below replace the earlier candidate shapes; readers must not compose an
old schema with amendment prose. Approval authorizes only the bounded T1
contracts, serialization, validation, research-event record, and focused
contract fixtures. It does not authorize T2 scheduling, T3 coordination,
live calls, deployment, or operational-plan schema changes.

## 1. Accepted decisions

| Decision | Status and meaning |
| --- | --- |
| C1 | Accepted: low/medium/high uncertainty is uncalibrated assertion, not correctness or authority. The disposition vocabulary below extends the earlier reasons to belief corrections. |
| C2 | Accepted: Reports may have empty citations structurally. Claims and actionable memory operations require observation citations. Provenance is not truth; support is evaluated separately. |
| C3 | Accepted: the seven operation/target pairs in section 3. |
| C5 | Accepted: count started actor turns and recognizable current Query attempts; obsolete outputs create no new attempts. |
| C9 | Accepted: recorded investigative direction is the latest dispatched diagnostic, separate from recommended direction. |
| M1 | Accepted: supervisor-only semantic memory writes in the first study. The treatment is supervision plus within-incident memory adaptation, not memory's isolated benefit. |
| M2 | Accepted: exact-base whole-update rejection without merge, rebase, or extra-review retry. |
| M4 | Accepted: persistent within-incident beliefs and separately expiring direction. |
| M5 | Accepted: atomic memory publication and decision-generation invalidation; best-effort cancellation, mandatory obsolete suppression. |
| C4 + M6 | Accepted: deterministic equality, normalization, whole-successor validation, and no-op rules in section 5. |
| C7 + M7 | Accepted: latest-trigger acknowledgment, expiry/multiple-interrupt handling, and complete trigger/disposition contracts in section 6. |
| M3 | Accepted: deterministic conflict lifecycle in section 5. |
| C8 + M8 | Accepted: this consolidated wire contract, version declarations, and the engineering limits in section 10. |
| C6 | **Deferred:** evidence equivalence and case rubrics. Do not invent them. |

Use the same actor configuration across architectures and the same supervisor,
memory rules, and review triggers across supervised variants. One coordinator
owns scheduling, snapshots, revisions, interruption, and dispatch eligibility.
Neither component rewrites observations, grants permissions, changes weights,
or treats beliefs as verified evidence. No cross-incident memory, generic memory
platform, live-provider cancellation/concurrency claim, or RL training is added.

## 2. Common wire rules

All objects are closed. Names/discriminators are exact and case-sensitive.
Reject unknown/missing fields, duplicate JSON property names, integer enums,
wrong types, unsupported versions, non-finite numbers, invalid references, and
mixed union branches. No coercion, lowercasing, last-property-wins, or extension
bag. Every defined field is required. `T | null` means explicit null is allowed,
not omission. Empty arrays are allowed except where a cardinality is stated.

Every root has exactly these common fields plus its definition:

```text
schemaVersion: "1.0-candidate.3"
recordType: the exact root literal
```

The complete root family is `actor-decision`, `supervisor-output`, `observation`,
`input-snapshot`, `invocation-result`, `belief`, `direction`,
`working-memory-snapshot`, `memory-update`, `reconsideration-trigger`,
`research-event`, `run-manifest`, `termination`, `run-closure`.
Nested objects have no implicit header; named root types embedded below include
their complete header. `[]` is an array; `|` selects a union. These notation
blocks define fields, not executable schemas.

| Type | Representation |
| --- | --- |
| `U32` | JSON integer `0..4294967295`; no fraction or wraparound. |
| `Tick` | `U32`, abstract logical time, never a workflow superstep or inference second. |
| `Sequence` | Positive `U32`, contiguous from 1 per run; identity is `(runId, sequence)`. |
| `Text` | Nonempty, non-whitespace JSON string; content conveys no authority. |
| `Token` | String matching `^[A-Za-z0-9][A-Za-z0-9._-]*$`, with catalogue membership where required. |
| `Uuid`, `RunId` | Lowercase nonempty hyphenated GUID (`D` format); RunId is coordinator-issued. |
| `Sha256` | 64 lowercase hexadecimal characters; identify the actual bytes/canonical digest covered. |
| `DefinitionRef` | `{ id: Token, sha256: Sha256 }`, matching a frozen harness-owned definition. |

Run-local ID types have the indicated prefix followed by a positive decimal
U32 without leading zeroes: ActorTurnId `actor-`, ReviewId `review-`,
SnapshotId `snapshot-`, ObservationId `observation-`, ResultId `result-`,
DiagnosticId `diagnostic-`, BeliefId `belief-`, DirectionId `direction-`,
MemoryUpdateId `memory-update-`, TriggerId `trigger-`. Allocate monotonically
per kind and never reuse. Memory revision starts at 0 and is not a new UUID.
EvidenceId is a registered fixture Token (for example E2), not a delivery ID.
All references are scoped to the containing run; repeated delivery of evidence
can have a new ObservationId without becoming a new underlying fact.

Sets (citations, ID sets, conflict edges, causal sequences) contain no duplicates
and are serialized in ordinal/numeric order. Input order is not an equality
criterion; histories retain event order. Generated IDs and component-local keys
cannot by themselves create a meaningful change.

**Trust:** component outputs, claims, dispositions and rationale are untrusted
(`C`). Invocation bindings, IDs, clocks, revisions, state, outcomes, and
validation records are coordinator/adapter-owned (`H`). Copying C content into
an H envelope does not make it true. A component's echoed base revision must
match its captured input; it cannot choose its run, generation, current epoch,
permissions, plan binding or consumed revision.

Wire-version acceptance is C8/M8. This preserved candidate literal identifies
the approved first scripted-build contract baseline, not a production release
or experiment v1.1. Do not accept the old draft's `"1.0"` as an alias. Existing
operational plan/verification versions stay unchanged.

## 3. Decisions, catalogue, and semantic operations

### 3.1 Vocabularies

```text
Hypothesis = "local-instance-issue" | "dependency-path-issue"
  | "dependency-side-queue-delay" | "no-currently-active-incident" | "unresolved"
NextStep = "further-local-diagnostics" | "further-dependency-diagnostics"
  | "investigate-dependency-queue" | "monitor" | "human-handoff"
Uncertainty = "low" | "medium" | "high"
Stance = "adopt" | "adapt" | "reject"
DispositionReason = "follow-suggested-direction" | "narrow-suggested-step"
  | "combine-with-observed-evidence" | "retain-current-direction"
  | "evidence-insufficient" | "evidence-conflicts" | "already-addressed"
  | "adopt-belief-correction" | "adapt-belief-correction"
  | "retain-current-belief" | "trigger-no-longer-applicable"
TargetId = "INC-1042" | "payments-api" | "authorization-service"
Operation = "get_incident" | "get_service_health" | "query_metrics" | "query_logs"
```

The four added disposition reasons cover corrections and inapplicable triggering
state. These are actor assertions, not correctness labels or a scoring matrix.
An actor may reject advice; neither stance nor uncertainty bypasses a guard.

| Operation | Allowed target | Trusted capability / constructed argument |
| --- | --- | --- |
| `get_incident` | `INC-1042` | `incident.read`; `incidentId` bound to the incident |
| `get_service_health` | `payments-api`, `authorization-service` | `service.health.read`; `serviceId` bound to target |
| `query_metrics` | `payments-api`, `authorization-service` | `telemetry.metrics.read`; `serviceId` bound to target |
| `query_logs` | `payments-api`, `authorization-service` | `telemetry.logs.read`; `serviceId` bound to target |

These are seven pairs, not their unrestricted Cartesian product. Actor Query
arguments are exactly `{}`; the coordinator constructs the existing resource
argument. Registry effect/approval are `read`/`none`; all other trusted resource,
identity, classification, version, and validity metadata comes from the existing
controls and approved case binding. No write/shell/file/URL/MCP capability is
added. The current simulator does not implement the dependency case; accepting
its logical catalogue entry does not implement it or permit a silent fallback.

### 3.2 Actor output (root `actor-decision`, C)

```text
MemoryDisposition = {
  triggerId: TriggerId, memoryUpdateId: MemoryUpdateId,
  stance: Stance, reason: DispositionReason
}
ActorDecision =
  { kind: "query", memoryDisposition: MemoryDisposition | null,
    usedBeliefIds: BeliefId[], operation: Operation, targetId: TargetId, arguments: {} }
  | { kind: "wait", memoryDisposition: MemoryDisposition | null,
      usedBeliefIds: BeliefId[], ticks: U32 }
  | { kind: "report", memoryDisposition: MemoryDisposition | null,
      usedBeliefIds: BeliefId[], hypothesis: Hypothesis,
      observationIds: ObservationId[], nextStep: NextStep, uncertainty: Uncertainty }
```

Wait ticks are 1 through 4. Report citations may be empty; every supplied
citation must be in the input's observed history. Belief IDs cannot replace
observation citations. `usedBeliefIds` may be empty and expresses claimed use,
not proof of causation; supplied IDs must belong to the input. They cannot
promote terminal or contested beliefs into facts.

MemoryDisposition follows section 6: required and exactly bound to the input's
latest pending trigger, otherwise null. No component `consumedMemoryRevision`,
`decisionGeneration`, authority, or verification field is permitted.

A started turn counts even if its output is invalid. A current eligible result
that is valid JSON with one exact `kind: "query"` is also a diagnostic attempt
before its remaining Query fields are validated. Malformed JSON, duplicate
properties, or unknown discriminators count only the started turn. An obsolete
result creates no new attempt. Contract acceptance of Report is not factual
support or success; evaluation occurs separately after termination.

### 3.3 Supervisor output (root `supervisor-output`, C)

```text
SupervisorOutput =
  { kind: "no-change", baseMemoryRevision: U32,
    observationIds: ObservationId[], rationale: Text | null,
    uncertainty: Uncertainty | null }
  | { kind: "propose-memory-update", baseMemoryRevision: U32,
      observationIds: ObservationId[], rationale: Text | null,
      uncertainty: Uncertainty | null, operations: MemoryOperation[] }

Claim = {
  hypothesis: Hypothesis, targetId: TargetId,
  observationIds: ObservationId[], uncertainty: Uncertainty
}
BeliefRef = { kind: "existing", beliefId: BeliefId }
  | { kind: "proposed", key: Token }
Focus = { kind: "target", targetId: TargetId }
  | { kind: "diagnostic", operation: Operation, targetId: TargetId, arguments: {} }
DirectionChoice = { kind: "focus", focus: Focus }
  | { kind: "handoff", recommendation: "uncertainty-report" | "human-handoff" }
MemoryOperation =
  { kind: "add-belief", key: Token, claim: Claim }
  | { kind: "reassert-belief", predecessorBeliefId: BeliefId,
      key: Token, claim: Claim, reason: Text }
  | { kind: "replace-belief", beliefId: BeliefId, key: Token, claim: Claim }
  | { kind: "retract-belief", beliefId: BeliefId,
      observationIds: ObservationId[], reason: Text }
  | { kind: "declare-conflict", beliefs: BeliefRef[],
      observationIds: ObservationId[], reason: Text }
  | { kind: "set-direction", recommendation: DirectionChoice,
      observationIds: ObservationId[], supportingBeliefs: BeliefRef[] }
  | { kind: "clear-direction", observationIds: ObservationId[], reason: Text }
```

Memory operations form a nonempty transaction. Each claim/operation requires
nonempty observation citations from the review's captured history, also present
in its overall citation set. Add/replace citations are in Claim. NoChange may
have empty citations but no operations. It is not an error fallback, memory
write, renewal or wake event. An old-base NoChange is merely an opinion about
its recorded view; it cannot perform a write. An actionable proposal must pass
the accepted exact-base check even if some fields appear to be duplicates.

All keys are proposal-local, unique when defining a claim, and resolved only
within that transaction. Existing references must be in the captured memory.
No belief supplies evidence for another belief: Claim cites observations.
`reassert-belief` requires a predecessor that was already terminal in the base
snapshot, has the same run, epoch, target and hypothesis, and is the latest such
terminal belief by state-change revision then BeliefId. The explicit reference
makes that deterministic selection reviewable; a predecessor made terminal by
the same transaction is not eligible. Its nonempty reason is the concise
operation-specific rationale. Reassertion never edits or reactivates the
predecessor.
Conflict declarations have at least two distinct references and describe all
unordered pairs among them. Different hypotheses are not automatically conflicts.
An operation/target pair must be in the catalogue for diagnostic Focus.

Supervisor output has no standalone `suggest-focus`, `recommend-handoff` or
`guidance` root. Their meaning is fully represented by set-direction. There
is one memory transaction and one authority boundary, not two commit paths.

## 4. Immutable data and memory roots

### 4.1 Observation (root `observation`, H with untrusted content)

```text
Observation = {
  runId: RunId, observationId: ObservationId, evidenceIds: EvidenceId[],
  sourceKind: "shared-notification" | "diagnostic", sourceId: Token,
  targetId: TargetId, diagnosticId: DiagnosticId | null,
  availableTick: Tick, observedTick: Tick | null, historyRevision: U32 | null,
  applicabilityEpoch: U32, visibility: "shared-history" | "post-termination",
  content: ObservationContent
}
ObservationContent =
  { kind: "notification", text: Text }
  | { kind: "incident", value: IncidentSnapshot }
  | { kind: "service-health", value: ServiceHealthSnapshot }
  | { kind: "metrics", values: MetricSample[] }
  | { kind: "logs", values: LogEntry[] }
  | { kind: "unavailable", reason: "not-yet-available" }
```

Reuse the five existing simulator DTOs with ContractJson names, types, and enums;
source timestamps are synthetic evidence times, not measured inference latency.
Notification source requires notification content and null diagnostic ID.
Diagnostic source requires the actual operation/diagnostic ID and corresponding
DTO or unavailable branch. An unavailable answer has empty evidence IDs, no
future tick/content, and is not a fabricated empty success.

Shared observations receive unique consecutive history revisions starting at 1;
observedTick is non-null and at least availableTick. Post-termination records
have null observedTick/historyRevision and never enter active history/evaluation.
Epoch identifies the sampled content, not a relabeling at delayed delivery.
Each diagnostic has one accepted completion/observation. Later status/completion
records append to immutable action history; no component edits earlier records.

### 4.2 Belief (root `belief`, H provenance with C claim)

```text
Belief = {
  runId: RunId, beliefId: BeliefId, createdByUpdateId: MemoryUpdateId,
  createdAtMemoryRevision: U32, applicabilityEpoch: U32, claim: Claim,
  reassertedFromBeliefId: BeliefId | null
}
BeliefState = "provisional" | "contested" | "superseded" | "retracted" | "invalidated"
BeliefStateEntry = {
  beliefId: BeliefId, state: BeliefState, stateChangedByUpdateId: MemoryUpdateId,
  conflictingBeliefIds: BeliefId[], supersededByBeliefId: BeliefId | null
}
```

The immutable claim never acquires a true/verified state. Lifecycle and
supersession live in successor snapshots/updates, allowing replacement by an
already existing eligible claim without rewriting that claim's origin.
Only superseded entries have non-null supersededByBeliefId. Ordinary additions
and replacements have null reassertedFromBeliefId. A reassertion is a newly
created provisional claim whose non-null predecessor was terminal before the
transaction. Supersession lineage records a transition that retires an eligible
predecessor in this transaction; reassertion lineage records descent from an
already terminal historical belief and causes no new transition on that record.

### 4.3 Direction and working memory (H; recommendation remains advisory)

```text
Direction = {                         // root "direction"
  runId: RunId, directionId: DirectionId, createdByUpdateId: MemoryUpdateId,
  reviewId: ReviewId, reviewStartTick: Tick, expiresAtTick: Tick,
  applicabilityEpoch: U32, recommendation: DirectionChoice,
  observationIds: ObservationId[], supportingBeliefIds: BeliefId[]
}
WorkingMemorySnapshot = {             // root "working-memory-snapshot"
  runId: RunId, memoryRevision: U32, previousMemoryRevision: U32 | null,
  applicabilityEpoch: U32, beliefStates: BeliefStateEntry[],
  recommendedDirection: Direction | null
}
```

**One active representation:** `workingMemory.recommendedDirection` is the sole
active advisory slot. There is no input `guidance`, no guidance root, no
`requiredGuidanceId`, and no separate active-direction cache in these contracts.
Archived Direction records are not active merely because they exist.
Direction expiry is reviewStartTick + 8, exclusive; preserve the original
expiry on repetition. Terminal direction status is derived from recorded
transitions: expired, superseded, cleared, invalidated, or closed.

Memory 0 is empty, epoch 0, with null predecessor/direction. A committed change
advances memory once; each revision is immutable. Beliefs persist within the
incident/epoch until explicitly superseded/retracted/invalidated. Terminal
entries remain clearly historical. Direction expiry clears the slot in a new
revision without incrementing generation, waking, or cancelling solely for expiry.
Ordinary observations/history growth do not create beliefs or memory revisions.
Epoch changes publish an epoch-consistent memory revision, including when empty;
invalidation of actor-visible beliefs/direction is actionable, while an empty
epoch-only bookkeeping change still relies on the existing epoch dispatch guard.

## 5. Equality, normalization and conflict lifecycle — APPROVED

These are the approved C4/M6 and M3 rules for the first scripted T1 build.

### 5.1 Equality keys

**ClaimKey** = `(run, epoch, targetId, hypothesis, sorted observationIds,
uncertainty)`. Ignore IDs, local keys, proposing review and timestamps.
**ReassertionSubjectKey** = `(run, epoch, targetId, hypothesis)`. It is used
only to validate/select explicit reassertion lineage; it does not replace
ClaimKey equality or deduplicate differing evidence/uncertainty.
**DirectionKey** = `(run, epoch, exact DirectionChoice, sorted observationIds)`.
Ignore IDs, provenance time, rationale, uncertainty and supporting-belief IDs.
Arguments are the exact closed catalogue arguments; there is no natural-language
equivalence or LLM deduplication.

New observation IDs or changed uncertainty can change ClaimKey even if a human
would call the text similar. That may cause real interrupt pressure, not proven
new knowledge. No confidence threshold or semantic similarity cutoff is added.

### 5.2 Deterministic normalization order

1. Detect settled invocation/result redelivery before semantic processing.
   Same identity/same recorded bytes yields a duplicate-delivery event only.
   Different bytes under that identity yield conflicting-result-delivery and
   no second result settlement or state mutation.
2. For a fresh result, validate raw JSON, shape, scope, citations, references,
   unique defined keys, and review/run/epoch eligibility. No-op normalization
   cannot hide malformed or unauthorized input. Require captured base =
   echoed base = current memory before normalizing a memory write.
3. Resolve claim equality against the base state and within the transaction.
   Compare candidate keys ordinally; when multiple equal new definitions occur,
   the smallest local key is the representative. An add matching an eligible
   existing claim aliases it. Self-equal replacement drops its replacement
   effect and aliases the target. Replacement matching another eligible claim
   aliases that claim and still supersedes its different target. Exact matching
   terminal claims require `reassert-belief`; a plain add is rejected with
   terminal-belief-lineage-required. Validate its explicit predecessor as above.
   If an eligible current ClaimKey already exists, including one defined earlier
   in this transaction, reassertion aliases that eligible belief and cannot
   create a new claim, update or interrupt. Coalesced equal proposed claims must
   have compatible null lineage or the same reassertion predecessor; otherwise
   reject inconsistent-update. If multiple historical matches exist, terminal
   records never outrank an eligible current match.
4. Resolve proposal-local references through those aliases. No multiple
   retirement operations on one target, self/cyclic supersession, or retiring
   a replacement destination is allowed. New IDs are assigned only to actual
   new claims after full validation, in representative-key order.
5. A set-direction matching **any previously committed DirectionKey in this
   run/epoch** is dropped. Reference the first matching DirectionId in its
   normalization note. Leave the current slot exactly unchanged, including
   null if it expired or a different recommendation if later superseded.
   Do not rewrite its support references, renew expiry, or reactivate it.
6. Compute the complete successor: retire/supersede claims, remove their incident
   conflict edges, add explicitly declared eligible edges, derive contested/
   provisional state, and apply any remaining explicit direction set/clear.
7. Validate the **whole resulting state**: symmetric eligible conflict links,
   no dangling/self links, and any surviving direction references only current
   provisional beliefs in the current epoch. At most one raw direction
   set/clear is permitted. If inconsistent, reject the whole update; do not
   automatically clear a direction or salvage the belief changes.
8. If valid, compare effective state: eligible ClaimKeys and their lifecycle,
   explicit conflict relationships, and active direction including its identity/
   original expiry. A validated reassertion is an effective new provisional
   belief because its immutable lineage is part of the successor, even when its
   ClaimKey equals a terminal predecessor. Ignore no-op keys and fresh IDs alone.
   Unchanged state is no-op; otherwise commit once and publish its atomic
   generation/trigger transition. Clearing an already empty direction or
   declaring existing edges alone is no-op, not a new interrupt.

A failed raw operation is never erased to make a transaction pass. A normalized
alias to a claim retired by the same transaction is inconsistent and rejected.
An equal add cannot bypass contested status or change an existing claim's origin.
Retraction of a terminal belief is invalid, not a mechanism for clearing a
pending trigger. A retract-then-reassert sequence in one transaction is invalid:
reassertion lineage must refer to a predecessor already terminal in the base.
All scope, epoch, evidence-membership, exact-base, conflict and direction-
dependency checks apply before the whole successor can commit.

### 5.3 Terminal-belief reassertion

| Input situation | Required outcome |
| --- | --- |
| Belief A is retracted. A later eligible review captures the current base and observations, then proposes `reassert-belief` with A as the validated latest terminal predecessor, the same target/hypothesis, and a concise reason. | Create a new provisional belief with a new BeliefId and `reassertedFromBeliefId = A`; leave A retracted. Commit once and interrupt once if the whole successor is valid. |
| The already processed result that originally created A is delivered again after A was retracted. | Record duplicate delivery only. Do not normalize, allocate a belief, commit, or interrupt. |
| A fresh result proposes reassertion from an older captured base. | Reject under exact-base validation with memory-revision-conflict before normalization; lineage cannot rescue stale authority. |
| A currently eligible belief has the same ClaimKey as the proposed reassertion. | Alias the current belief under the existing rules. Fresh key/ID intent and historical lineage do not create an update or interrupt. |
| A reassertion would leave a surviving direction dependent on an ineligible or contested belief, or cites observations outside the captured review history. | Reject the whole transaction with the existing dependency or citation reason; do not partially publish the reassertion. |
| A is retracted, reasserted as B, B is later retracted, and a later current-base review validly reasserts it as C. | Preserve A <- B <- C reassertion lineage, all terminal transitions, commits, triggers, cancellations/suppressions, and consumed budgets. The oscillation is observable and receives no automatic retry or extra review. |

Reassertion does not promote a claim to truth, infer semantic equivalence with
an LLM, or copy predecessor conflicts/direction dependencies. The proposal's
Claim citations must be nonempty members of the captured review history and
its reason is bounded by the existing component-text limit. Within-transaction
equality is resolved before ID allocation; whole-successor validation then
checks the newly created provisional belief and every surviving dependency as
one atomic candidate state.

### 5.4 Duplicate direction with a real correction

| Input situation | Normalization and outcome |
| --- | --- |
| Direction D remains active and does not depend on corrected belief A. | Drop repeated set-direction(D), retain replacement(A), validate all dependencies, commit the correction while keeping D's ID/expiry. |
| D previously expired; active slot null. | Drop repeated set-direction(D); a valid correction can commit with the slot still null. |
| Active D depends on A, which is being superseded. | Drop the duplicate direction write; retained D now has an invalid dependency. Reject the entire transaction. No correction commit, renewal, or implicit clear. |
| Same situation, but proposal explicitly clears D instead of repeating it. | Keep explicit clear and correction; commit only if the whole normalized state is valid. |
| New supporting-belief IDs but identical DirectionKey. | Still repeated; those IDs alone cannot renew direction. A genuinely different, permitted, evidence-linked direction must be proposed if replacement is needed. |
| Same result redelivered after a newer correction. | Duplicate delivery only; do not run normalization or overwrite newer state. |

Rejecting a normalized dependency failure leaves memory, generation, trigger and
expiry untouched. Preserve raw result, the ordered normalization notes and all
validation findings. A new attempt needs the next eligible fresh review; no
extra retry is added. Repeated direction equality does not decide whether an
entire transaction is a no-op.

### 5.5 Conflict graph

Eligible beliefs are provisional/contested in the current epoch. Current edges
are symmetric, without self-links, duplicates or terminal endpoints. Store
conflictingBeliefIds on both endpoints in sorted order; contested means degree
greater than zero. Retraction/supersession/invalidation removes the old belief's
current edges on both endpoints in the successor. Historical snapshots/claims
are unchanged. Replacement beliefs inherit **no** edges automatically.

Three-belief example: edges A-B and B-C make A, B and C contested. Retract B:
remove both edges; A and C are provisional, B retracted. Neither survivor became
true. Replace B by D instead: A and C again lose those edges; D starts provisional
unless a conflict with D is explicitly declared. A new A-D declaration makes
only A and D contested. A direction dependent on A must then be explicitly
cleared or validly replaced in that same transaction, otherwise reject it.
No private truth oracle or automatic conflict transfer repairs the proposal.

## 6. Reconsideration contracts — C7/M7 APPROVED

### 6.1 Immutable trigger and snapshot requirement

```text
ReconsiderationTrigger = {            // root "reconsideration-trigger", H
  runId: RunId, triggerId: TriggerId, memoryUpdateId: MemoryUpdateId,
  committedMemoryRevision: U32, decisionGeneration: U32,
  createdTick: Tick, previousTriggerId: TriggerId | null
}
DirectionState = "active" | "expired" | "superseded" | "cleared" | "invalidated" | "closed"
TriggerEffectView =
  { kind: "belief", beliefId: BeliefId, state: BeliefState }
  | { kind: "direction", directionId: DirectionId, state: DirectionState }
ReconsiderationRequirement = {
  trigger: ReconsiderationTrigger, earlierTriggerIds: TriggerId[],
  effectsAtCapture: TriggerEffectView[]
}
```

Every actionable commit creates one H trigger; previousTriggerId links the
previous actionable trigger, acknowledged or not. The coordinator's pending
pointer selects the latest trigger requiring acknowledgment. At start, capture
that pointer and the **current** memory/history/epoch/generation atomically.
`earlierTriggerIds` is the ordered previous-trigger chain (oldest first);
`effectsAtCapture` covers every belief/direction ID changed by the trigger's
MemoryEffects, with its status in the captured current state. It contains no
old recommendation presented as active advice.

DirectionState comes from the active slot or its first recorded terminal
transition. The expiry clock must have been processed before new capture.
Belief status comes from the captured memory entries. A removed direction
record remains archived with its actual expiry and lineage, not resurrected
inside the active slot. All statuses are mechanical lifecycle data, not truth.

### 6.2 Complete pending-trigger rule

- On an actionable commit, publish memory, incremented generation, immutable
  trigger and pending-pointer replacement as one control transition.
- If several updates arrive before restart, capture only the **latest** required
  trigger; retain earlier triggers as provenance. Do not queue one new turn or
  forced acknowledgment per old trigger.
- Starting an actor does not clear the requirement. A structurally/contextually
  valid current decision must return MemoryDisposition whose triggerId and
  memoryUpdateId exactly match its captured requirement.
- Record that disposition and clear the pending pointer only if it still names
  the same trigger. An obsolete, invalid, missing or wrongly bound disposition
  never clears a newer pending trigger. A later valid turn may acknowledge the
  still-pending trigger; each started turn still counts.
- If a new actionable update arrives after start, the old generation becomes
  obsolete and the new trigger takes precedence. Invalidation/acknowledgment
  cannot race through separate authority paths.
- Expiry-only memory revisions create no new trigger. A surviving belief
  correction can remain provisional after its associated direction expires;
  acknowledge the existing pending trigger against current memory, not old advice.
- If only direction expiry removed all active triggering effects, still bind
  the pending acknowledgment, permitting `trigger-no-longer-applicable`.
  A later actionable supersession/invalidation instead creates a newer trigger.
- With no pending trigger at capture, `reconsideration` and memoryDisposition
  must both be null. Ordinary turns need not repeatedly acknowledge an old
  interrupt. `consumedMemoryRevision` is always recorded regardless.

Acknowledgment is of reconsideration, not obedience or evidence of correctness.
The actor may adopt, adapt or reject. An accepted current disposition can be
recorded before the governed diagnostic outcome; a later interrupt must still
suppress its not-yet-dispatched action and cannot be cleared by the older result.
Operational verification/policy never becomes an acknowledgment bypass.

## 7. Complete input, result and memory-update roots

### 7.1 Input snapshot (H, immutable)

```text
InputSnapshot = {                    // root "input-snapshot"
  runId: RunId, snapshotId: SnapshotId, consumer: "actor" | "supervisor",
  actorTurnId: ActorTurnId | null, reviewId: ReviewId | null,
  tick: Tick, historyRevision: U32, actionHistoryThroughSequence: Sequence | null,
  applicabilityEpoch: U32, decisionGeneration: U32, memoryRevision: U32,
  scope: { incidentId: "INC-1042", serviceId: "payments-api", goal: Text },
  remainingBudgets: { actorTurns: U32, diagnosticAttempts: U32, reviews: U32 },
  allowedQueries: { operation: Operation, targetId: TargetId, arguments: {} }[],
  observations: Observation[], actionHistory: ActionHistoryItem[],
  reviewHistory: ReviewHistoryItem[], workingMemory: WorkingMemorySnapshot,
  beliefs: Belief[], reconsideration: ReconsiderationRequirement | null,
  investigativeDirection: { operation: Operation, targetId: TargetId } | null
}
ActionHistoryItem = {
  actorTurnId: ActorTurnId, snapshotId: SnapshotId, resultId: ResultId,
  consumedMemoryRevision: U32, decision: ActorDecision | null,
  disposition: "applied" | "rejected" | "suppressed", reason: Reason | null,
  diagnosticId: DiagnosticId | null, observationIds: ObservationId[]
}
ReviewHistoryItem = {
  reviewId: ReviewId, snapshotId: SnapshotId, resultId: ResultId | null,
  status: "completed" | "timed-out" | "failed" | "cancelled",
  output: SupervisorOutput | null, memoryUpdateId: MemoryUpdateId | null,
  reason: Reason | null
}
```

Exactly the consumer's invocation ID is non-null. Supervisor reconsideration
is always null. Budgets are captured after reserving that invocation's start.
Observations contain all shared revisions 1 through captured historyRevision;
no unapproved truncation. History includes only recorded outcomes up to the
cutoff. Pending work is not a fabricated completion; late review settlement
does not reopen a timed-out review or clear degraded status.

Working memory matches run/revision/epoch. Beliefs materialize exactly the IDs
in its state entries, with matching immutable claims. The same projection
rules apply to both consumers; later observations and memory updates never
mutate captured inputs. Supervisors do not receive private actor reasoning.
No case category, expected answer, future evidence/schedule, fault script or
rubric is in this input. Recorded investigativeDirection is derived from the
latest actual dispatch, not the recommendation or an inferred private intent.

### 7.2 Invocation result (H envelope, C output)

```text
InvocationResult = {                 // root "invocation-result"
  runId: RunId, resultId: ResultId, consumer: "actor" | "supervisor",
  actorTurnId: ActorTurnId | null, reviewId: ReviewId | null,
  snapshotId: SnapshotId, consumedMemoryRevision: U32,
  capturedGeneration: U32, capturedApplicabilityEpoch: U32,
  settledTick: Tick | null, settlement: "returned" | "failed" | "cancelled",
  parseStatus: "well-formed" | "invalid" | "not-attempted",
  output: ActorDecision | SupervisorOutput | null,
  rawOutputSha256: Sha256 | null, redactedOutput: Text | null,
  reason: Reason | null
}
```

Bind metadata from the physical invocation, never its returned text. One
settlement per invocation. Returned bytes have a digest; well-formed output
has the corresponding complete root. Invalid output has null typed output and
a reason. Non-return settlements have null output/digest and not-attempted parse.
An obsolete return can remain unparsed with its digest retained; parsing for
forensics must not affect behavior. settledTick may be null only in drain with
no experiment-clock coordinate. Use the existing redactor, not raw secret/prompt
logging. Unknown usage is not zero.

A current returned supervisor output that fails structural validation records
review.rejected with the result's reason. Its ReviewHistoryItem has completed
status, null output/memoryUpdateId, and that reason; completion denotes physical
settlement, not acceptance. Do not fabricate a typed proposal or MemoryUpdate
from malformed bytes. Well-formed memory proposals instead receive the section
7.3 outcome, including rejection. Neither path schedules an extra review retry.

### 7.3 Memory update outcome and provenance (H)

```text
UpdateOrigin =
  { kind: "supervisor", reviewId: ReviewId, resultId: ResultId, snapshotId: SnapshotId }
  | { kind: "direction-expiry", directionId: DirectionId }
  | { kind: "epoch-invalidation", causedBySequence: Sequence }
NormalizationNote = {
  operationIndex: U32,
  rule: "unchanged" | "alias-existing-claim" | "coalesce-proposed-claim"
    | "drop-self-replacement" | "drop-repeated-direction"
    | "drop-existing-conflict" | "drop-empty-clear",
  existingBeliefId: BeliefId | null, proposedKey: Token | null,
  duplicateDirectionId: DirectionId | null
}
ConflictEdge = { left: BeliefId, right: BeliefId }
MemoryEffects = {
  createdBeliefIds: BeliefId[],
  supersessions: { fromBeliefId: BeliefId, toBeliefId: BeliefId }[],
  reassertions: { fromBeliefId: BeliefId, toBeliefId: BeliefId }[],
  retractedBeliefIds: BeliefId[], invalidatedBeliefIds: BeliefId[],
  addedConflicts: ConflictEdge[], removedConflicts: ConflictEdge[],
  directionBeforeId: DirectionId | null, directionAfterId: DirectionId | null,
  directionEndState: DirectionState | null
}
MemoryUpdate = {                     // root "memory-update"
  runId: RunId, memoryUpdateId: MemoryUpdateId, origin: UpdateOrigin,
  baseMemoryRevision: U32, observedCurrentMemoryRevision: U32,
  committedMemoryRevision: U32 | null,
  outcome: "committed" | "rejected" | "no-op", actionable: boolean,
  previousGeneration: U32, newGeneration: U32, triggerId: TriggerId | null,
  proposal: SupervisorOutput | null, normalization: NormalizationNote[],
  keyBindings: { key: Token, beliefId: BeliefId }[],
  effects: MemoryEffects | null, errors: Reason[]
}
```

Notes are in original zero-based operation order; notes identify aliases/dropped
direction and retain the full original proposal/result. A repeated direction
note has only duplicateDirectionId non-null; an existing-claim alias has only
existingBeliefId non-null; a coalesced proposed claim has only proposedKey
non-null; other notes have null reference fields except self-replacement,
which identifies its target in existingBeliefId. The unchanged rule means the
raw operation is retained, not that the transaction is a no-op. A conflict
declaration with both existing and new edges is retained; set union adds only
the new edges, and MemoryEffects records only actual edge changes. Use
drop-existing-conflict only when the entire declaration adds no edge.

Committed updates have base = observed current, committed = base + 1, effects
non-null and errors empty. A meaningful supervisor change is actionable:
newGeneration = previousGeneration + 1 and triggerId non-null. Expiry-only commits
are nonactionable with equal generations and null trigger. Epoch commits follow
section 4.3. System origins have null proposal and empty normalization/keyBindings.
Rejected/no-op records have null committed revision/effects/trigger, false
actionable and unchanged generation. Rejection has errors; no-op has none.
Rejected transactions allocate no new Belief/Direction/Trigger records and have
empty keyBindings. Successful/no-op aliases can bind keys to existing IDs.

DirectionEndState is non-null only when the old direction leaves the active
slot; it identifies the first terminal transition (not active). Unchanged slots
have equal IDs and null end state. Conflict edges use ordinal left < right,
are unique, and apply symmetrically. StateChangedByUpdateId changes also on
conflict-degree transitions, without rewriting claims. Reassertion effects link
an already terminal base-state predecessor to a newly created belief; they are
not supersessions and do not produce another state change for the predecessor.
All created records, memory revision, generation, trigger and its required event
batch are published atomically; cross-references within that batch resolve as a
unit.

## 8. Research events and reasons

```text
ResearchEvent = {                    // root "research-event", H
  runId: RunId, sequence: Sequence, tick: Tick | null, phase: Phase,
  eventType: EventType, causedBySequences: Sequence[],
  currentGeneration: U32, currentMemoryRevision: U32,
  historyRevision: U32, applicabilityEpoch: U32, data: event-specific object
}
Phase = "setup" | "end-check" | "world-delivery" | "prior-diagnostic-results"
  | "review-processing" | "actor-processing" | "review-start" | "actor-start" | "drain"
InvocationRef = { consumer: "actor", actorTurnId: ActorTurnId }
  | { consumer: "supervisor", reviewId: ReviewId }
OperationalBinding = {
  planId: Uuid, stepId: Text, planDigest: Sha256, actionDigest: Sha256,
  requestId: Uuid | null, sessionId: Text
}
```

Sequence is the total order. Causes reference earlier same-run events; independent
setup/world/checkpoint events may have no cause. Null tick is restricted to
setup/drain without a clock coordinate. During episodes, preserve v1.0 phases
1-7; immediate diagnostic completion stays in actor-processing. Review/memory
acceptance precedes not-yet-dispatched actor output at the same tick. Framework
events are neither an experimental clock nor an atomic dispatch boundary.

Each following row defines all required fields of the closed event `data`;
listed IDs/types are exact. Empty `{}` refers to that run's unique root record.
This is the complete EventType catalogue, including memory payloads.

| EventType | Data fields |
| --- | --- |
| `run.started` | `{}` |
| `evidence.available` | `evidenceIds: EvidenceId[], targetId: TargetId, availableTick: Tick` |
| `observation.recorded` | `observationId: ObservationId` |
| `epoch.changed` | `previousEpoch: U32, newEpoch: U32, sourceId: Token` |
| `actor.started` | `actorTurnId: ActorTurnId, snapshotId: SnapshotId, consumedMemoryRevision: U32, requiredTriggerId: TriggerId or null` |
| `actor.completed`, `actor.failed`, `actor.cancelled` | `actorTurnId: ActorTurnId, resultId: ResultId, consumedMemoryRevision: U32` |
| `actor.rejected` | `actorTurnId: ActorTurnId, resultId: ResultId, reason: Reason` |
| `actor.invalidated` | `actorTurnId: ActorTurnId, snapshotId: SnapshotId, triggerId: TriggerId, previousGeneration: U32, newGeneration: U32` |
| `reconsideration.required` | `triggerId: TriggerId, previousPendingTriggerId: TriggerId or null` |
| `actor.memory-disposition` | `actorTurnId: ActorTurnId, resultId: ResultId, consumedMemoryRevision: U32, disposition: MemoryDisposition` |
| `reconsideration.acknowledged` | `triggerId: TriggerId, actorTurnId: ActorTurnId, resultId: ResultId` |
| `actor.wait-started` | `actorTurnId: ActorTurnId, resultId: ResultId, untilTick: Tick` |
| `actor.wait-ended` | `actorTurnId: ActorTurnId, reason: Reason` |
| `diagnostic.requested` | `diagnosticId: DiagnosticId, actorTurnId: ActorTurnId, resultId: ResultId` |
| `diagnostic.dispatched` | `diagnosticId: DiagnosticId, actorTurnId: ActorTurnId, resultId: ResultId, consumedMemoryRevision: U32, binding: OperationalBinding` |
| `diagnostic.completed` | `diagnosticId: DiagnosticId, observationId: ObservationId, auditRecordIds: Uuid[]` |
| `diagnostic.rejected`, `diagnostic.failed` | `diagnosticId: DiagnosticId, reason: Reason, binding: OperationalBinding or null, auditRecordIds: Uuid[]` |
| `diagnostic.duplicate-delivery` | `diagnosticId: DiagnosticId, originalCompletionSequence: Sequence` |
| `review.started` | `reviewId: ReviewId, snapshotId: SnapshotId, consumedMemoryRevision: U32, timeoutAtTick: Tick` |
| `review.completed`, `review.failed`, `review.cancelled` | `reviewId: ReviewId, resultId: ResultId, consumedMemoryRevision: U32` |
| `review.rejected` | `reviewId: ReviewId, resultId: ResultId, reason: Reason` (structurally invalid current output; no typed memory proposal) |
| `review.timed-out` | `reviewId: ReviewId, timeoutAtTick: Tick` |
| `review.checkpoint-skipped` | `checkpointTick: Tick, activeReviewId: ReviewId` |
| `review.no-change` | `reviewId: ReviewId, resultId: ResultId` |
| `memory.update-proposed` | `reviewId: ReviewId, resultId: ResultId, baseMemoryRevision: U32` |
| `memory.update-rejected`, `memory.update-no-op` | `memoryUpdateId: MemoryUpdateId` (full normalization/errors in the immutable record) |
| `memory.committed` | `memoryUpdateId: MemoryUpdateId, previousMemoryRevision: U32, newMemoryRevision: U32, previousGeneration: U32, newGeneration: U32, actionable: boolean, triggerId: TriggerId or null` |
| `belief.state-changed` | `memoryUpdateId: MemoryUpdateId, beliefId: BeliefId, previousState: BeliefState or null, newState: BeliefState` (null only for creation) |
| `belief.reasserted` | `memoryUpdateId: MemoryUpdateId, predecessorBeliefId: BeliefId, beliefId: BeliefId, triggerId: TriggerId` |
| `direction.ended` | `memoryUpdateId: MemoryUpdateId, directionId: DirectionId, state: DirectionState` (terminal only) |
| `cancellation.requested` | `invocation: InvocationRef, reason: Reason` |
| `cancellation.unsupported`, `cancellation.failed` | `invocation: InvocationRef, reason: Reason` |
| `cancellation.confirmed`, `cancellation.ignored` | `invocation: InvocationRef, resultId: ResultId` |
| `result.suppressed` | `invocation: InvocationRef, resultId: ResultId, consumedMemoryRevision: U32, reason: Reason` |
| `result.duplicate-delivery`, `result.conflicting-delivery` | `invocation: InvocationRef, originalResultId: ResultId, deliveredOutputSha256: Sha256` |
| `report.submitted` | `actorTurnId: ActorTurnId, resultId: ResultId, consumedMemoryRevision: U32` |
| `episode.terminated`, `run.closed` | `{}` |

Memory/lifecycle/invalidation events describe one committed control transition;
they are not independently published permission changes. Reconsideration-start
is represented by actor.started with requiredTriggerId, not a second charged
invocation. Acknowledgment never depends on a fabricated active guidance record.

Cancellation confirmation requires actual cancelled settlement, not successful
request submission. Ignored denotes non-cancelled settlement after a request
that was neither unsupported nor failed, not provider intent. Old-call failure
does not fail the replacement. Already-dispatched operations are retained once.
After termination, only drain/accounting/provenance events are allowed; no
memory commit, history mutation, new actor/review, acknowledgment or report.

```text
Reason = {
  domain: "contract" | "context" | "governance" | "infrastructure",
  code: Text, message: Text | null
}
```

H reasons use these closed study codes, with the explicit inherited exception:

| Domain | Codes |
| --- | --- |
| `contract` | `invalid-json`, `duplicate-property`, `unsupported-schema`, `unknown-record-type`, `unknown-field`, `missing-field`, `wrong-type`, `unknown-enum`, `invalid-identifier`, `invalid-reference`, `invalid-shape`, `out-of-range`, `missing-memory-disposition`, `engineering-limit-exceeded` |
| `context` | `out-of-scope-operation`, `out-of-scope-target`, `observation-not-in-snapshot`, `belief-not-in-snapshot`, `trigger-mismatch`, `run-mismatch`, `obsolete-generation`, `epoch-mismatch`, `review-timed-out`, `review-cancelled`, `episode-ended`, `duplicate-result`, `conflicting-result-delivery`, `horizon-reached`, `actor-turn-budget-exhausted`, `diagnostic-budget-exhausted`, `review-budget-exhausted`, `memory-base-mismatch`, `memory-revision-conflict`, `invalid-direction-dependency`, `invalid-conflict`, `inconsistent-update`, `terminal-belief-lineage-required`, `invalid-reassertion-lineage`, `memory-update-interrupt`, `epoch-changed`, `wait-timer`, `shared-notification`, `wake-already-observed` |
| `governance` | Exact existing gateway/verifier code from a trusted adapter, retaining its underscore spelling; never a component-selected authorization claim. |
| `infrastructure` | `actor-invocation-failed`, `supervisor-invocation-failed`, `invocation-cancelled`, `cancellation-unsupported`, `cancellation-request-failed`, `diagnostic-execution-failed`, `record-unavailable`, `input-limit-exceeded`, `record-limit-exceeded`, `drain-incomplete` |

Messages are redacted diagnostic context. Preserve all detected findings in
MemoryUpdate.errors; behavior precedence follows episode/review/epoch eligibility,
raw validation, exact base, then normalized-state validation. No answer-key
filter, hidden retry or successful fallback is allowed.

## 9. Run manifest, termination and closure

```text
RunManifest = {                      // root "run-manifest", H
  runId: RunId,
  studySpecification: { baseline: "1.0", amendment: "WM-1", candidate: "3" },
  contractCandidate: "T1-WM-1-candidate-3",
  architecture: "actor-only" | "blocking-supervision" | "asynchronous-supervision",
  coordination: "isolated-invocations-single-coordinator",
  treatment: "supervision-plus-within-incident-memory-adaptation",
  executionMode: "scripted", dataMode: "synthetic",
  sourceRevision: Text, sourceDirty: boolean,
  sourceFiles: { path: Text, sha256: Sha256 }[],
  actorConfig: DefinitionRef, supervisorConfig: DefinitionRef | null,
  caseDefinition: DefinitionRef, externalSchedule: DefinitionRef,
  catalogueConfig: DefinitionRef, governanceConfig: DefinitionRef,
  memoryRules: DefinitionRef, seed: U32 | null, faultInjectionConfig: DefinitionRef | null,
  clock: ScriptedClock, limits: ResearchLimits, engineeringLimits: EngineeringLimits,
  runtimeVersions: { dotnetSdk: Text, agentFrameworkWorkflows: Text, node: Text },
  modelMeasurements: "unavailable-scripted"
}
ScriptedClock = {
  kind: "logical-ticks", startTick: 0, endTickExclusive: 24,
  actorTurnTicks: 1, diagnosticTicks: 0, reviewTicks: 3,
  reviewCheckpoints: [0, 4, 8, 12, 16, 20],
  reviewTimeoutTicks: 6, guidanceLifetimeTicks: 8, maximumWaitTicks: 4
}
ResearchLimits = {
  actorTurns: 24, diagnosticAttempts: 12, reviews: 6,
  dispatchEligibleActorTurns: 1, activeReviews: 1
}
ResourceCounts = {
  actorTurnsStarted: U32, reviewsStarted: U32, diagnosticAttempts: U32,
  diagnosticsDispatched: U32, diagnosticsCompleted: U32,
  actorResultsSuppressed: U32, reviewResultsSuppressed: U32,
  cancellationRequests: U32, cancellationsConfirmed: U32,
  cancellationsUnsupported: U32, cancellationRequestsFailed: U32,
  cancellationsIgnored: U32, reportsSubmitted: U32,
  memoryUpdatesCommitted: U32, memoryUpdatesRejected: U32, memoryUpdatesNoOp: U32,
  memoryRevisionConflicts: U32, repeatedDirectionsNormalized: U32,
  beliefReassertionsCommitted: U32, reassertionInterrupts: U32,
  actionableCommits: U32, reconsiderationAcknowledgments: U32
}
Termination = {                      // root "termination", H
  runId: RunId, tick: Tick, phase: Phase,
  kind: "report" | "timeout" | "budget-exhausted" | "governance-stop" | "infrastructure-failure",
  reportResultId: ResultId | null, reason: Reason | null,
  finalHistoryRevision: U32, finalApplicabilityEpoch: U32,
  finalGeneration: U32, finalMemoryRevision: U32, finalPendingTriggerId: TriggerId | null,
  supervisionDegraded: boolean, counts: ResourceCounts,
  pendingInvocations: InvocationRef[], pendingDiagnosticIds: DiagnosticId[]
}
RunClosure = {                       // root "run-closure", H
  runId: RunId, status: "complete" | "incomplete", finalMemoryRevision: U32,
  counts: ResourceCounts, unsettledInvocations: InvocationRef[],
  unsettledDiagnosticIds: DiagnosticId[], reason: Reason | null,
  modelMeasurements: "unavailable-scripted"
}
```

SourceRevision is an actual 40-hex commit, not a claim it contains dirty work.
SourceFiles identify relevant changed/new bytes with repository-relative paths,
no traversal. All definitions are frozen, hash-bound and harness-owned;
future schedules/faults/rubrics never enter component input. Treatment names the
study comparison, including its actor-only baseline; supervisorConfig is null
only for actor-only. Runtime versions are actual observations, not model output.

MemoryRules identifies the approved equality/trigger/lifecycle choices;
manifest version fields explicitly preserve the candidate identifier rather
than silently minting a new wire version. No live protocol or provider
usage/pricing is implied.

Counts derive from actual events, not fixture expectations. Repeated result
delivery creates no new turn, attempt, commit or settlement. Memory commits
include expiry/system commits; actionableCommits counts only generation-changing
commits. A rejected update with base-conflict error increments both corresponding
counters. Structurally invalid supervisor outputs are observable through
review.rejected, not counted as typed rejected memory updates.
RepeatedDirectionsNormalized counts normalization notes, including
when the whole transaction subsequently fails, not fictional successful updates.
BeliefReassertionsCommitted counts newly created beliefs with validated
reassertion lineage. ReassertionInterrupts counts actionable commits containing
at least one such creation, so one transaction with several reassertions counts
one interrupt while each lineage edge remains observable. Its trigger,
actor.invalidated, cancellation, suppression, replacement turn, and budget
events retain their ordinary costs rather than being collapsed into this count.
ReconsiderationAcknowledgments counts accepted pointer-clearing acknowledgments,
not actor starts or self-declared use. Report count is at most one.

Report termination requires a valid current Report before tick 24 in
actor-processing, with reportResultId non-null and reason null. A pending
required trigger must have been acknowledged. Timeout is tick 24/end-check
with horizon-reached. Other termination kinds have explicit reasons and null
reportResultId. Required governance/verifier failure is governance-stop; current
actor failure is infrastructure-failure. Invalid current decisions consume their
applicable budget, not a fabricated Report. Supervisor errors/timeouts mark
degraded supervision; actor-only has false degraded status.

Termination freezes history, memory, generation and pending-trigger provenance
before draining. Closure has the same finalMemoryRevision; cumulative settlement/
cancellation counts may increase, but no late result changes the diagnosis,
memory or termination counts. Complete closure requires empty unsettled arrays
and null reason. Incomplete closure retains pending IDs and an explicit reason.
No silent cleanup retry or success-shaped missing record. If recording fails,
fail closed and report incomplete evidence rather than fabricating continuity.

## 10. Approved engineering limits

These protect serialization/resource handling, not research effect sizes,
confidence thresholds, inference budgets or outcome scores. The values below
are approved for the first scripted T1 build under C8/M8.

```text
EngineeringLimits = {
  maximumComponentOutputBytes: U32,
  maximumOperationsPerUpdate: U32,
  maximumComponentTextBytes: U32,
  maximumCitationsPerField: U32,
  maximumInputSnapshotBytes: U32,
  maximumEventBytes: U32
}
```

| Limit | Approved value | Defined measurement / failure |
| --- | --- | --- |
| Component output | 65,536 bytes | UTF-8 received bytes before parsing; reject oversize, never truncate into valid JSON. |
| Operations/update | 16 | Raw operation-array count before normalization; duplicate operations cannot evade this bound. |
| Component text field | 2,048 bytes | UTF-8 rationale/reason string content, excluding JSON escaping; not a source-data truth threshold. |
| Citations/field | 64 | Raw array count before set canonicalization; duplicate citations still fail validation. |
| Materialized input | 1,048,576 bytes | Serialized complete immutable input; stop explicitly rather than dropping old beliefs/evidence to fit. |
| Research event | 131,072 bytes | Serialized event root; stop explicitly rather than silently losing causality. |

The manifest records these approved positive values when a run is eventually
authorized; missing or different limits are not runtime defaults. Component excess
is an invalid output with applicable existing accounting. Oversize trusted
input/event is an infrastructure/recording failure, not an alternative actor or
abbreviated history. Record such faults separately in later evaluation. No new
cap on indefinitely draining live calls or statistical sample size is chosen.

## 11. Complete JSON examples

Examples are authored **wire-valid candidate records**, not measured outcomes.
Where referenced earlier records/definitions are not reproduced, assume the
same-run, hash-bound fixture records described in the accompanying text.
Illustrative configuration hashes are not attestations from an executed run.
Numbered examples are independent fixture branches unless explicitly linked;
reused display IDs do not combine those branches into one valid run ledger.
All examples use this consolidated version; there are no amendment-only field
fragments masquerading as full valid roots.

### 11.1 Supervisor correction plus repeated direction

Assume base memory 2 contains provisional belief-1 and an active direction
whose DirectionKey matches the set-direction below. If that existing direction
does not depend on belief-1, normalize out the repeated direction and commit
the correction. If it depends on belief-1, reject the **whole** normalized
transaction; the proposed replacement support does not renew that direction.

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "supervisor-output",
  "kind": "propose-memory-update",
  "baseMemoryRevision": 2,
  "observationIds": ["observation-3"],
  "rationale": "Revise the local-instance hypothesis using the observed dependency timeouts.",
  "uncertainty": "medium",
  "operations": [
    {
      "kind": "replace-belief",
      "beliefId": "belief-1",
      "key": "correction",
      "claim": {
        "hypothesis": "dependency-path-issue",
        "targetId": "authorization-service",
        "observationIds": ["observation-3"],
        "uncertainty": "medium"
      }
    },
    {
      "kind": "set-direction",
      "recommendation": {
        "kind": "focus",
        "focus": { "kind": "target", "targetId": "authorization-service" }
      },
      "observationIds": ["observation-3"],
      "supportingBeliefs": [{ "kind": "proposed", "key": "correction" }]
    }
  ]
}
```

A NoChange on a captured view is complete and non-mutating:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "supervisor-output",
  "kind": "no-change",
  "baseMemoryRevision": 3,
  "observationIds": [],
  "rationale": null,
  "uncertainty": null
}
```

### 11.2 Latest trigger, expiry, and subsequent turns

Assume actionable update-1 created trigger-1; a second update before restart
created trigger-2 at memory 2/generation 2. Its direction subsequently expired,
publishing memory 3 without another trigger. Both roots below are complete.
The actor captures current memory 3 with pending trigger-2, not active old advice.

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "reconsideration-trigger",
  "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
  "triggerId": "trigger-2",
  "memoryUpdateId": "memory-update-2",
  "committedMemoryRevision": 2,
  "decisionGeneration": 2,
  "createdTick": 11,
  "previousTriggerId": "trigger-1"
}
```

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "working-memory-snapshot",
  "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
  "memoryRevision": 3,
  "previousMemoryRevision": 2,
  "applicabilityEpoch": 0,
  "beliefStates": [
    {
      "beliefId": "belief-1",
      "state": "superseded",
      "stateChangedByUpdateId": "memory-update-2",
      "conflictingBeliefIds": [],
      "supersededByBeliefId": "belief-2"
    },
    {
      "beliefId": "belief-2",
      "state": "provisional",
      "stateChangedByUpdateId": "memory-update-2",
      "conflictingBeliefIds": [],
      "supersededByBeliefId": null
    }
  ],
  "recommendedDirection": null
}
```

The reconsidering actor may use the surviving correction:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "actor-decision",
  "kind": "query",
  "memoryDisposition": {
    "triggerId": "trigger-2",
    "memoryUpdateId": "memory-update-2",
    "stance": "adopt",
    "reason": "adopt-belief-correction"
  },
  "usedBeliefIds": ["belief-2"],
  "operation": "query_metrics",
  "targetId": "authorization-service",
  "arguments": {}
}
```

Once that current disposition is accepted, an ordinary later turn need not
acknowledge trigger-2 again. It still records its consumed revision through
the H input/result binding, even when it returns this complete Wait:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "actor-decision",
  "kind": "wait",
  "memoryDisposition": null,
  "usedBeliefIds": ["belief-2"],
  "ticks": 2
}
```

If a later actionable invalidation creates trigger-3 before restart, the actor
must acknowledge trigger-3 instead; trigger-2 becomes earlier provenance.
If only expiry removed all actionable effects, a bound adapt/reject disposition
can use trigger-no-longer-applicable without restoring a direction.

### 11.3 Normalization rejection and exact-base rejection events

This complete MemoryUpdate records the inconsistent-dependent-direction branch
of 11.1. The hypothetical original output is retained by its result record;
proposal below repeats that exact candidate root, not a success substitute.

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "memory-update",
  "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
  "memoryUpdateId": "memory-update-4",
  "origin": {
    "kind": "supervisor",
    "reviewId": "review-3",
    "resultId": "result-9",
    "snapshotId": "snapshot-8"
  },
  "baseMemoryRevision": 2,
  "observedCurrentMemoryRevision": 2,
  "committedMemoryRevision": null,
  "outcome": "rejected",
  "actionable": false,
  "previousGeneration": 2,
  "newGeneration": 2,
  "triggerId": null,
  "proposal": {
    "schemaVersion": "1.0-candidate.3",
    "recordType": "supervisor-output",
    "kind": "propose-memory-update",
    "baseMemoryRevision": 2,
    "observationIds": ["observation-3"],
    "rationale": "Revise the local-instance hypothesis using the observed dependency timeouts.",
    "uncertainty": "medium",
    "operations": [
      {
        "kind": "replace-belief",
        "beliefId": "belief-1",
        "key": "correction",
        "claim": {
          "hypothesis": "dependency-path-issue",
          "targetId": "authorization-service",
          "observationIds": ["observation-3"],
          "uncertainty": "medium"
        }
      },
      {
        "kind": "set-direction",
        "recommendation": {
          "kind": "focus",
          "focus": { "kind": "target", "targetId": "authorization-service" }
        },
        "observationIds": ["observation-3"],
        "supportingBeliefs": [{ "kind": "proposed", "key": "correction" }]
      }
    ]
  },
  "normalization": [
    {
      "operationIndex": 0,
      "rule": "unchanged",
      "existingBeliefId": null,
      "proposedKey": null,
      "duplicateDirectionId": null
    },
    {
      "operationIndex": 1,
      "rule": "drop-repeated-direction",
      "existingBeliefId": null,
      "proposedKey": null,
      "duplicateDirectionId": "direction-1"
    }
  ],
  "keyBindings": [],
  "effects": null,
  "errors": [
    {
      "domain": "context",
      "code": "invalid-direction-dependency",
      "message": "The retained direction depends on the belief proposed for supersession."
    }
  ]
}
```

Separately, a review started at tick 12 on memory 2 may return at 17, before
timeout 18 but after expiry committed memory 3 at 16. Its outcome record has
base 2/current 3, null committed revision, empty normalization/effects/bindings,
and memory-revision-conflict. Rejection is recorded without a retry or mutation:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "research-event",
  "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
  "sequence": 51,
  "tick": 17,
  "phase": "review-processing",
  "eventType": "memory.update-rejected",
  "causedBySequences": [50],
  "currentGeneration": 2,
  "currentMemoryRevision": 3,
  "historyRevision": 4,
  "applicabilityEpoch": 0,
  "data": { "memoryUpdateId": "memory-update-5" }
}
```

### 11.4 Fresh terminal-belief reassertion

Assume belief-1 has ClaimKey `(run, epoch 0, authorization-service,
dependency-path-issue, [observation-3], medium)` and was retracted in memory 3.
It is the latest terminal belief with that target/hypothesis. Review-4 captured
memory 3 and observation-3. This is a valid proposal shape; acceptance still
depends on the ordinary current review, epoch, exact-base, scope, citation and
whole-successor checks:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "supervisor-output",
  "kind": "propose-memory-update",
  "baseMemoryRevision": 3,
  "observationIds": ["observation-3"],
  "rationale": "Current dependency evidence again supports the retracted hypothesis.",
  "uncertainty": "medium",
  "operations": [
    {
      "kind": "reassert-belief",
      "predecessorBeliefId": "belief-1",
      "key": "reasserted-dependency-path",
      "claim": {
        "hypothesis": "dependency-path-issue",
        "targetId": "authorization-service",
        "observationIds": ["observation-3"],
        "uncertainty": "medium"
      },
      "reason": "Reassert after retraction using the current captured evidence."
    }
  ]
}
```

A valid commit creates belief-2 with `reassertedFromBeliefId: belief-1`, records
`effects.reassertions = [{ fromBeliefId: belief-1, toBeliefId: belief-2 }]`,
publishes `belief.state-changed` for belief-2 and `belief.reasserted`, increments
both reassertion counters as applicable, and creates the ordinary actionable
trigger/interrupt. Belief-1 remains retracted. If an eligible current belief
already has the same ClaimKey, normalize the operation to that belief instead:
no belief-2, commit, trigger or interrupt.

The same bytes delivered again under the settled review/result identity produce
only result.duplicate-delivery. The same proposal from a newly settled result
whose captured base is memory 2 is rejected with memory-revision-conflict before
normalization. A plain add matching the terminal ClaimKey is rejected with
terminal-belief-lineage-required; an incorrect/nonlatest predecessor is rejected
with invalid-reassertion-lineage. None receives a retry or partial commit.

### 11.5 Obsolete Report cannot clear a newer trigger

Assume this otherwise well-formed actor output was captured on memory 1/
generation 1 with trigger-1 required. An actionable update published memory 2/
generation 2 and trigger-2 before it returned. Suppress it: no report, no new
diagnostic attempt, and no acknowledgment of trigger-2.

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "actor-decision",
  "kind": "report",
  "memoryDisposition": {
    "triggerId": "trigger-1",
    "memoryUpdateId": "memory-update-1",
    "stance": "reject",
    "reason": "evidence-insufficient"
  },
  "usedBeliefIds": [],
  "hypothesis": "unresolved",
  "observationIds": [],
  "nextStep": "human-handoff",
  "uncertainty": "high"
}
```

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "research-event",
  "runId": "8f816ca2-0d21-4eb3-9a41-ec1af43c4181",
  "sequence": 48,
  "tick": 12,
  "phase": "actor-processing",
  "eventType": "result.suppressed",
  "causedBySequences": [41, 46],
  "currentGeneration": 2,
  "currentMemoryRevision": 2,
  "historyRevision": 3,
  "applicabilityEpoch": 0,
  "data": {
    "invocation": { "consumer": "actor", "actorTurnId": "actor-7" },
    "resultId": "result-10",
    "consumedMemoryRevision": 1,
    "reason": { "domain": "context", "code": "obsolete-generation", "message": null }
  }
}
```

### 11.6 Complete manifest and terminal accounting example

This separate illustrative actor-only long-turn fault fixture holds actor-1
past the horizon. E0/E1/E2/E4 deliver four observations; E3 is never queried.
It illustrates complete record fields, not a frozen fault protocol or a measured
run. Hashes identify assumed example definitions. The engineering numbers are
the approved T1 limits from section 10.

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "run-manifest",
  "runId": "0c333ffc-9c27-4ba1-bc2e-c682a080327a",
  "studySpecification": { "baseline": "1.0", "amendment": "WM-1", "candidate": "2" },
  "contractCandidate": "T1-WM-1-candidate-3",
  "architecture": "actor-only",
  "coordination": "isolated-invocations-single-coordinator",
  "treatment": "supervision-plus-within-incident-memory-adaptation",
  "executionMode": "scripted",
  "dataMode": "synthetic",
  "sourceRevision": "2612294a32b39f611bf8b3b0aab84577fb79f7e7",
  "sourceDirty": false,
  "sourceFiles": [],
  "actorConfig": { "id": "example-actor", "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa" },
  "supervisorConfig": null,
  "caseDefinition": { "id": "example-case", "sha256": "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" },
  "externalSchedule": { "id": "example-schedule", "sha256": "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc" },
  "catalogueConfig": { "id": "seven-pair-catalogue", "sha256": "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd" },
  "governanceConfig": { "id": "existing-controls", "sha256": "eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee" },
  "memoryRules": { "id": "candidate-3-memory-rules", "sha256": "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff" },
  "seed": null,
  "faultInjectionConfig": { "id": "example-delayed-actor", "sha256": "1111111111111111111111111111111111111111111111111111111111111111" },
  "clock": {
    "kind": "logical-ticks", "startTick": 0, "endTickExclusive": 24,
    "actorTurnTicks": 1, "diagnosticTicks": 0, "reviewTicks": 3,
    "reviewCheckpoints": [0, 4, 8, 12, 16, 20],
    "reviewTimeoutTicks": 6, "guidanceLifetimeTicks": 8, "maximumWaitTicks": 4
  },
  "limits": {
    "actorTurns": 24, "diagnosticAttempts": 12, "reviews": 6,
    "dispatchEligibleActorTurns": 1, "activeReviews": 1
  },
  "engineeringLimits": {
    "maximumComponentOutputBytes": 65536, "maximumOperationsPerUpdate": 16,
    "maximumComponentTextBytes": 2048, "maximumCitationsPerField": 64,
    "maximumInputSnapshotBytes": 1048576, "maximumEventBytes": 131072
  },
  "runtimeVersions": { "dotnetSdk": "10.0.303", "agentFrameworkWorkflows": "1.17.0", "node": "22.16.0" },
  "modelMeasurements": "unavailable-scripted"
}
```

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "termination",
  "runId": "0c333ffc-9c27-4ba1-bc2e-c682a080327a",
  "tick": 24, "phase": "end-check", "kind": "timeout",
  "reportResultId": null,
  "reason": { "domain": "context", "code": "horizon-reached", "message": null },
  "finalHistoryRevision": 4, "finalApplicabilityEpoch": 0,
  "finalGeneration": 0, "finalMemoryRevision": 0, "finalPendingTriggerId": null,
  "supervisionDegraded": false,
  "counts": {
    "actorTurnsStarted": 1, "reviewsStarted": 0, "diagnosticAttempts": 0,
    "diagnosticsDispatched": 0, "diagnosticsCompleted": 0,
    "actorResultsSuppressed": 0, "reviewResultsSuppressed": 0,
    "cancellationRequests": 0, "cancellationsConfirmed": 0,
    "cancellationsUnsupported": 0, "cancellationRequestsFailed": 0,
    "cancellationsIgnored": 0, "reportsSubmitted": 0,
    "memoryUpdatesCommitted": 0, "memoryUpdatesRejected": 0, "memoryUpdatesNoOp": 0,
    "memoryRevisionConflicts": 0, "repeatedDirectionsNormalized": 0,
    "beliefReassertionsCommitted": 0, "reassertionInterrupts": 0,
    "actionableCommits": 0, "reconsiderationAcknowledgments": 0
  },
  "pendingInvocations": [{ "consumer": "actor", "actorTurnId": "actor-1" }],
  "pendingDiagnosticIds": []
}
```

After the requested cancellation is ignored and that call settles, the complete
closure records the late suppression without rewriting termination or memory:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "run-closure",
  "runId": "0c333ffc-9c27-4ba1-bc2e-c682a080327a",
  "status": "complete", "finalMemoryRevision": 0,
  "counts": {
    "actorTurnsStarted": 1, "reviewsStarted": 0, "diagnosticAttempts": 0,
    "diagnosticsDispatched": 0, "diagnosticsCompleted": 0,
    "actorResultsSuppressed": 1, "reviewResultsSuppressed": 0,
    "cancellationRequests": 1, "cancellationsConfirmed": 0,
    "cancellationsUnsupported": 0, "cancellationRequestsFailed": 0,
    "cancellationsIgnored": 1, "reportsSubmitted": 0,
    "memoryUpdatesCommitted": 0, "memoryUpdatesRejected": 0, "memoryUpdatesNoOp": 0,
    "memoryRevisionConflicts": 0, "repeatedDirectionsNormalized": 0,
    "beliefReassertionsCommitted": 0, "reassertionInterrupts": 0,
    "actionableCommits": 0, "reconsiderationAcknowledgments": 0
  },
  "unsettledInvocations": [], "unsettledDiagnosticIds": [],
  "reason": null, "modelMeasurements": "unavailable-scripted"
}
```

### 11.7 Complete invalid objects and mutation cases

Invalid: old guidance representation plus component-chosen trusted revision:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "actor-decision",
  "kind": "query",
  "guidanceDisposition": null,
  "memoryDisposition": null,
  "usedBeliefIds": [],
  "consumedMemoryRevision": 9,
  "operation": "query_metrics",
  "targetId": "payments-api",
  "arguments": {}
}
```

Invalid: uncited memory claim, even if the hypothesis happens to match a hidden
answer. The complete object does not get normalized into a successful opinion:

```json
{
  "schemaVersion": "1.0-candidate.3",
  "recordType": "supervisor-output",
  "kind": "propose-memory-update",
  "baseMemoryRevision": 3,
  "observationIds": [],
  "rationale": null,
  "uncertainty": "low",
  "operations": [
    {
      "kind": "add-belief",
      "key": "unsupported",
      "claim": {
        "hypothesis": "dependency-side-queue-delay",
        "targetId": "authorization-service",
        "observationIds": [],
        "uncertainty": "low"
      }
    }
  ]
}
```

| Further invalid/state case | Required non-effect |
| --- | --- |
| memoryDisposition references trigger-1 when the captured requirement is trigger-2 | Reject trigger-mismatch; cannot clear trigger-2. |
| Base echo 3 while invocation actually consumed 2 | Reject memory-base-mismatch; no authority from echo. |
| Real base 2 while current memory 3 after expiry | Reject memory-revision-conflict; do not silently optimize away the expiry-only conflict. |
| Fresh local ID for exactly equal eligible claim | Alias/no-op, not a new claim or interrupt. |
| Plain add exactly matches only a terminal belief | Reject terminal-belief-lineage-required; fresh ID intent is not lineage. |
| reassert-belief names a nonterminal, nonmatching, wrong-epoch, or nonlatest predecessor | Reject invalid-reassertion-lineage; no partial creation or predecessor mutation. |
| reassert-belief matches an already eligible current ClaimKey | Alias/no-op under existing equality; no artificial update or interrupt. |
| Retraction removes B but successor still contains an A-B current edge | Invalid conflict successor; no partial commit. |
| Replacement silently copies all predecessor conflict links | Invalid transition unless those edges were explicitly declared. |
| NoChange also contains operations | Wrong union shape; no memory write. |
| Query uses get_incident with payments-api, a write tool, or nonempty arguments | Reject scope/shape; no governed dispatch. |
| Wait ticks 0, 5 or string `"2"` | Reject range/type; no fabricated wait. |
| Report cites a belief ID or unseen observation | Reject reference; an empty observation array alone is allowed. |
| Old wire `"1.0"`, numeric enum, duplicate property names | Reject, not an alias/default/last-property-wins parse. |
| Report at tick 24 or a late cancellation-ignoring result | Retain provenance; no report or action. |

## 12. Reuse, traceability, and remaining review

| Contract concern | Spec / repository mapping |
| --- | --- |
| Catalogue, canonical Query, fixed authority | Spec 5/11; reuse `GovernedAgent.Core/Contracts/PlanContracts.cs`, `GovernanceContracts.cs`, `GovernedAgent.Governance/ToolRegistry.cs`, `ActionCanonicalizer.cs`, `GovernedToolGateway.cs`, and policy/approval/audit controls. Research metadata stays outside strict plan schemas. |
| Actual verification/binding | Reuse Host `Verification/NodePlanVerifier.cs`, `PlanVerificationContracts.cs`, and workflow integration. Never invent attestation/digests or confuse study-spec version with verifier-spec version. |
| Observations and diagnostics | Spec 4; reuse `GovernedAgent.Simulator/SimulatorContracts.cs` and `SimulatorGovernedToolExecutor.cs`. Source DTO contents/timestamps are not trusted instructions. Dependency/recovery evidence still requires its separately authorized case work. |
| Recorded direction and history | Accepted C9; derive from dispatch, retain actual observations, and keep it separate from the sole recommended-direction slot. |
| Memory, normalization, triggers | Spec 12; this candidate integrates WM-1. T0 supplies isolated-invocation/dispatch-boundary prior art, not a memory implementation or whole-agent proof. |
| Clock, budgets, suppression | Spec 3, 6, 8; A08-A19. Preserve horizon, same-tick ordering, pending-review progress, best-effort cancellation and exact-once diagnostic observation. No T2 scheduler is implemented. |
| Event redaction and correlation | Reuse Host `Observability/SemanticTelemetryEvent.cs` and `TelemetryRedactor.cs`; research sequence is distinct from telemetry timing. |
| Termination/evaluation | Spec 8/9; do not change `AgentWorkflowStatus`, original recovery completion evaluator, or existing security evaluator into a diagnostic truth oracle. |
| Serialization | Reuse camelCase/kebab-case and explicit-null conventions in Core `Serialization/ContractJson.cs`, with separate candidate-version/union validation later. Do not alter shared operational options today. |

Paths in the mapping are under `src` in their named projects. The reviewed E2
and E2+E3 support relationships remain design context, **not** a new equivalence
rule or full rubric. C6 is deferred. No thresholds, scoring weights, sample
sizes, live-call draining caps or model-cost claims are assigned.

Keep these effects measurable, not optimized away:

- Expiry-only memory revisions can reject useful old-base proposals.
- Persistent beliefs can preserve mistakes after direction expiry.
- Repeated retract/reassert cycles can create observable belief oscillation,
  actionable interrupts, cancelled/suppressed work, and budget consumption.
- Repeated meaningful structural updates can interrupt useful work and prevent
  progress; record no-ops, rejected normalization, trigger chains and discarded calls.
- Blocking/asynchronous timing can change observations, accepted updates,
  review counts and costs despite matched configurations/external schedules.

These comparisons study supervision **plus** memory adaptation; consumed-revision
provenance and actor claims do not isolate memory's causal benefit. No memory-only
ablation, generic memory service or training loop is added.

**Approval recorded, 2026-09-16:** `T1-WM-1-candidate-3` /
`1.0-candidate.3`, including C4/M6, C7/M7, M3, C8/M8 and the listed
engineering limits, is the contract baseline for the first scripted build.
Previously accepted choices remain accepted and C6 remains deferred. T1
implementation is authorized only for bounded contracts, serialization,
validation, research events and focused fixtures. **Stop before T2.** No live
models, deployment, evaluator thresholds or governance behavior changes are
authorized.
