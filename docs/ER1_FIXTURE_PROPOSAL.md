# ER-1 fixture specification

| Field | Value |
| --- | --- |
| Fixture revision | `ER1-fixture-1` |
| Date | 2026-09-18 |
| Status | Approved synthetic fixture specification |
| Applies to | [EXPERIMENT_SPEC.md section 13](EXPERIMENT_SPEC.md#13-approved-amendment-realistic-diagnostic-retrieval) |
| Implementation effect | Authoritative baseline for the T2-only implementation authorized on 2026-09-18 |

## 1. Requirement boundary

The following are **existing approved requirements**, not new decisions:

- The actor has exactly the seven operation/target pairs in the T1 catalogue,
  with actor arguments exactly `{}` and coordinator-constructed resource arguments.
- Diagnostic execution is sampled when a current Query decision dispatches.
  The scripted default adds zero diagnostic ticks.
- Observations use the approved closed `Observation` contract and the existing
  `IncidentSnapshot`, `ServiceHealthSnapshot`, `MetricSample`, and `LogEntry` DTOs.
- The harness owns observation IDs, diagnostic IDs, history revisions,
  availability/observation ticks, and applicability epochs.
- Repeated retrieval creates a new observation ID. Repeated underlying records
  retain their evidence IDs and are not independent corroboration.
- The episode remains tick 0 inclusive through tick 24 exclusive, with the
  existing 24-turn, 12-diagnostic-attempt, and 6-review limits.
- E3's canonical route is `query_logs` on `authorization-service`. General
  dependency latency metrics do not identify request-linked queue delay.
- Evidence equivalence, scoring weights, held-out design, case rubrics, and
  live-model methodology remain deferred.

The values below are approved synthetic fixture requirements. They are not
empirical observations or claims about a production system. Their initial
approval was documentation-only; a later 2026-09-18 decision authorized T2
implementation against this exact fixture revision.

## 2. Time, projection, and delivery rules

### 2.1 Synthetic timestamp anchor

Use `2026-09-18T08:00:00Z` as the tick-0 source-time anchor.
For this fixture only, source records authored at tick `n` use an `08:nn`
timestamp unless a more precise timestamp is listed. This is a deterministic
timestamp convention, not a claim that one scheduler tick represents one real
minute or one model-inference minute.

### 2.2 Catalogue-bound observation windows

The actor selects no window. Each allowed pair has this fixed projection,
bound by the case catalogue:

| Operation/target | Fixed projection |
| --- | --- |
| `get_incident(INC-1042)` | Current incident snapshot at dispatch. |
| `get_service_health(payments-api)` | Current aggregate and three current instance snapshots at dispatch. Restart counts summarize earlier restarts. |
| `query_metrics(payments-api)` | Collected samples in the authored interval `2026-09-18T07:55:00Z` through the dispatch tick, capped before `08:24:00Z`. |
| `query_logs(payments-api)` | Collected entries with source timestamps in `2026-09-18T07:52:00Z` through the dispatch tick, capped before `08:24:00Z`. |
| `get_service_health(authorization-service)` | Current aggregate and two current instance snapshots at dispatch. |
| `query_metrics(authorization-service)` | Collected samples in `2026-09-18T07:55:00Z` through the dispatch tick, capped before `08:24:00Z`. |
| `query_logs(authorization-service)` | Collected entries with source timestamps in `2026-09-18T07:52:00Z` through the dispatch tick, capped before `08:24:00Z`. |

Results contain records whose `collectionTick` is no later than dispatch.
Records are ordered by DTO timestamp and then by evidence ID. A legitimate
empty log result is `{"kind":"logs","values":[]}` and has no evidence IDs.
It says only that the fixed projection has no collected matching entries.

### 2.3 Event, collection, availability, dispatch, and delivery

`source time` is the DTO timestamp or `updatedAt`. `collectionTick` below is
fixture metadata: the first tick at which the telemetry record has been
collected into the relevant projection. `availableTick` in an observation is:

- the state version's first availability tick for current-state DTOs;
- the greatest collection tick among returned records for a nonempty query,
  as aggregate content-availability metadata rather than a claim that every
  returned record was collected at that tick; or
- the dispatch tick for a newly sampled successful empty query.

`observedTick` is when the accepted diagnostic result or notification is
appended to shared history. Therefore:

- **Event-to-collection delay:** a source event at tick 7 with
  `collectionTick: 12` cannot be returned at tick 11.
- **Availability-to-query/dispatch delay:** evidence collected and available
  at tick 8 but first queried at tick 10 is sampled at tick 10. Its observation
  has `availableTick: 8`, and the linked `diagnostic.dispatched` event records
  dispatch at tick 10. This is discovery/retrieval delay, not delivery delay.
- **Dispatch/sample-to-observed-history delivery delay:** a diagnostic sampled
  at tick 8 but completed and appended at tick 10 retains its tick-8 content,
  availability metadata, and sampled applicability epoch. Its linked dispatch
  event is at tick 8 and its observation has `observedTick: 10`.
- A diagnostic sampled and completed in the zero-duration default normally has
  `observedTick` equal to dispatch, even when its evidence became available earlier.
- A delayed diagnostic implementation would preserve the availability and
  sampled epoch from dispatch and append it later; it must not resample at delivery.

No response contains a future collection tick, future record, hidden cause,
case label, evaluator interpretation, or next-availability hint.

## 3. Evidence registry

The evidence ID identifies the exact DTO record or exact state snapshot listed
here. An observation returning multiple records lists all corresponding IDs.
Notifications have their own evidence IDs because a summary is not the same
content as its underlying diagnostic records. Every model-visible
`evidenceIds` array is an ordinal-sorted set. DTO record arrays retain their
separate ordering rule: metric and log records remain chronological.

| Evidence ID | Source time | Record collection tick or state availability | DTO or use |
| --- | --- | --- | --- |
| `ev-inc-1042-v1` | `08:00:00Z` | tick 0 | Incident snapshot `I0`. |
| `ev-pay-health-v1` | `07:59:30Z` | tick 0 | Payments health snapshot `PH0`. |
| `ev-auth-health-v1` | `07:59:30Z` | tick 0 | Authorization health snapshot `AH0`. |
| `ev-pay-err-0755` | `07:55:00Z` | tick 0 | Payments earlier error-rate sample. |
| `ev-pay-p95-0755` | `07:55:00Z` | tick 0 | Payments earlier p95 sample. |
| `ev-pay-err-00` | `08:00:00Z` | tick 0 | Payments incident error-rate sample. |
| `ev-pay-p95-00` | `08:00:00Z` | tick 0 | Payments incident p95 sample. |
| `ev-pay-cpu-00` | `08:00:00Z` | tick 0 | Payments aggregate CPU sample. |
| `ev-pay-mem-00` | `08:00:00Z` | tick 0 | Payments aggregate memory sample. |
| `ev-pay-cpu-04` | `08:04:00Z` | tick 4 | Straightforward aggregate CPU sample. |
| `ev-pay-mem-04` | `08:04:00Z` | tick 4 | Straightforward aggregate memory sample. |
| `ev-pay-cpu-p03-04` | `08:04:00Z` | tick 4 | Ambiguous instance CPU sample. |
| `ev-pay-mem-p03-04` | `08:04:00Z` | tick 4 | Ambiguous instance memory sample. |
| `ev-pay-cpu-p03-08` | `08:08:00Z` | tick 8 | Ambiguous instance CPU follow-up. |
| `ev-pay-cpu-p03-12` | `08:12:00Z` | tick 12 | Ambiguous instance CPU follow-up. |
| `ev-pay-cpu-p03-16` | `08:16:00Z` | tick 16 | Ambiguous instance CPU follow-up. |
| `ev-pay-log-a17-1` | `08:06:02.500Z` | tick 8 | Payments log record for `req-a17`. |
| `ev-pay-log-b09-1` | `08:07:02.400Z` | tick 8 | Payments log record for `req-b09`. |
| `ev-auth-lat-0755` | `07:55:00Z` | tick 0 | Authorization earlier p95 latency. |
| `ev-auth-count-0755` | `07:55:00Z` | tick 0 | Authorization earlier request count. |
| `ev-auth-lat-0806` | `08:06:00Z` | tick 12 | Delayed-collected latency sample. |
| `ev-auth-lat-0807` | `08:07:00Z` | tick 12 | Delayed-collected latency sample. |
| `ev-auth-count-0807` | `08:07:00Z` | tick 12 | Delayed-collected request-count sample. |
| `ev-auth-log-a17-1` | `08:06:00.500Z` | tick 12 | Authorization log record for `req-a17`. |
| `ev-auth-log-a17-2` | `08:06:03.050Z` | tick 12 | Authorization log record for `req-a17`. |
| `ev-auth-log-a17-3` | `08:06:03.130Z` | tick 12 | Authorization log record for `req-a17`. |
| `ev-auth-log-b09-1` | `08:07:00.400Z` | tick 12 | Authorization log record for `req-b09`. |
| `ev-auth-log-b09-2` | `08:07:03.200Z` | tick 12 | Authorization log record for `req-b09`. |
| `ev-auth-log-b09-3` | `08:07:03.275Z` | tick 12 | Authorization log record for `req-b09`. |
| `ev-pay-log-0758` | `07:58:00Z` | tick 16 | Earlier comparison log record, delayed collection. |
| `ev-pay-log-0814` | `08:14:00Z` | tick 16 | Incident-window comparison log record. |
| `ev-notification-10-v1` | `08:10:00Z` | tick 10 | Tick-10 notification, recovery variation only. |
| `ev-inc-1042-v2` | `08:10:00Z` | tick 10 | Current incident snapshot after external recovery. |
| `ev-pay-err-0810` | `08:10:00Z` | tick 10 | Tick-10 payments error-rate sample. |
| `ev-pay-p95-0810` | `08:10:00Z` | tick 10 | Tick-10 payments p95 sample. |
| `ev-auth-lat-0810` | `08:10:00Z` | tick 10 | Tick-10 authorization p95 sample. |
| `ev-auth-log-c17-1` | `08:11:00.200Z` | tick 12 | Authorization log record for `req-c17`. |
| `ev-auth-log-c17-2` | `08:11:00.260Z` | tick 12 | Authorization log record for `req-c17`. |
| `ev-auth-log-c17-3` | `08:11:00.335Z` | tick 12 | Authorization log record for `req-c17`. |

The earlier cache warning is intentionally withheld from the diagnostic
projection until tick 16, when the comparison digest is collected. This is
delayed collection of an earlier record, not a future event or a query hint.

Exact payload-to-evidence sets are:

| Payload | Evidence IDs |
| --- | --- |
| `I0` | `ev-inc-1042-v1` |
| `I1` | `ev-inc-1042-v2` |
| `PH0` | `ev-pay-health-v1` |
| `AH0` | `ev-auth-health-v1` |
| `PM0` | `ev-pay-cpu-00`, `ev-pay-err-00`, `ev-pay-err-0755`, `ev-pay-mem-00`, `ev-pay-p95-00`, `ev-pay-p95-0755` |
| `PM4` append | `ev-pay-cpu-04`, `ev-pay-mem-04` |
| `PM4A` append | `ev-pay-cpu-p03-04`, `ev-pay-mem-p03-04` |
| `PM8A` append | `ev-pay-cpu-p03-08` |
| `PM12A` append | `ev-pay-cpu-p03-12` |
| `PM16A` append | `ev-pay-cpu-p03-16` |
| Tick-10 payments append | `ev-pay-err-0810`, `ev-pay-p95-0810` |
| `PL0` | none |
| `PL8` | `ev-pay-log-a17-1`, `ev-pay-log-b09-1` |
| `PL16` append | `ev-pay-log-0758`, `ev-pay-log-0814` |
| `AM0` | `ev-auth-count-0755`, `ev-auth-lat-0755` |
| `AM10R` append | `ev-auth-lat-0810` |
| `AM12` append | `ev-auth-count-0807`, `ev-auth-lat-0806`, `ev-auth-lat-0807` |
| `AL0` | none |
| `AL12` | `ev-auth-log-a17-1`, `ev-auth-log-a17-2`, `ev-auth-log-a17-3`, `ev-auth-log-b09-1`, `ev-auth-log-b09-2`, `ev-auth-log-b09-3` |
| `AL12R` append | `ev-auth-log-c17-1`, `ev-auth-log-c17-2`, `ev-auth-log-c17-3` |

## 4. Exact DTO payloads

These are the approved exact `value`/`values` payloads under the observation
content discriminators. Numbers are synthetic.

### 4.1 Current-state payloads

`I0`:

```json
{"kind":"incident","value":{"incidentId":"INC-1042","serviceId":"payments-api","title":"Payments API elevated error rate","status":"open","severity":1,"version":1,"updatedAt":"2026-09-18T08:00:00+00:00"}}
```

`I1`, recovery variation from tick 10:

```json
{"kind":"incident","value":{"incidentId":"INC-1042","serviceId":"payments-api","title":"Payments API elevated error rate","status":"mitigating","severity":1,"version":2,"updatedAt":"2026-09-18T08:10:00+00:00"}}
```

`PH0`, all variants and ticks:

```json
{"kind":"service-health","value":{"serviceId":"payments-api","health":"healthy","version":1,"instances":[{"instanceId":"payments-api-01","health":"healthy","restartCount":0,"updatedAt":"2026-09-18T07:59:30+00:00"},{"instanceId":"payments-api-02","health":"healthy","restartCount":0,"updatedAt":"2026-09-18T07:59:30+00:00"},{"instanceId":"payments-api-03","health":"healthy","restartCount":0,"updatedAt":"2026-09-18T07:59:30+00:00"}]}}
```

`AH0`, all variants and ticks:

```json
{"kind":"service-health","value":{"serviceId":"authorization-service","health":"healthy","version":1,"instances":[{"instanceId":"authorization-service-01","health":"healthy","restartCount":0,"updatedAt":"2026-09-18T07:59:30+00:00"},{"instanceId":"authorization-service-02","health":"healthy","restartCount":0,"updatedAt":"2026-09-18T07:59:30+00:00"}]}}
```

Healthy readiness is not evidence of normal request latency or absence of queueing.

### 4.2 Payments metrics

`PM0`, returned at ticks 0-3:

```json
{"kind":"metrics","values":[{"name":"http.server.error_rate","value":0.011,"unit":"ratio","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":220,"unit":"ms","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"process.cpu.utilization","value":0.47,"unit":"ratio","timestamp":"2026-09-18T08:00:00+00:00"},{"name":"http.server.error_rate","value":0.18,"unit":"ratio","timestamp":"2026-09-18T08:00:00+00:00"},{"name":"process.memory.utilization","value":0.58,"unit":"ratio","timestamp":"2026-09-18T08:00:00+00:00"},{"name":"http.server.p95_latency","value":2600,"unit":"ms","timestamp":"2026-09-18T08:00:00+00:00"}]}
```

In the straightforward variant, `PM4` from tick 4 appends:

```json
[{"name":"process.cpu.utilization","value":0.49,"unit":"ratio","timestamp":"2026-09-18T08:04:00+00:00"},{"name":"process.memory.utilization","value":0.60,"unit":"ratio","timestamp":"2026-09-18T08:04:00+00:00"}]
```

No later non-recovery payments samples are added before the horizon. Thus later
straightforward retrieval returns the exact `PM0` list plus the two `PM4` items.

In the ambiguous variant, `PM4A` from tick 4 instead appends:

```json
[{"name":"process.cpu.utilization.payments-api-03","value":0.87,"unit":"ratio","timestamp":"2026-09-18T08:04:00+00:00"},{"name":"process.memory.utilization.payments-api-03","value":0.64,"unit":"ratio","timestamp":"2026-09-18T08:04:00+00:00"}]
```

`PM8A`, `PM12A`, and `PM16A` cumulatively append, at their named ticks:

```json
[{"name":"process.cpu.utilization.payments-api-03","value":0.79,"unit":"ratio","timestamp":"2026-09-18T08:08:00+00:00"},{"name":"process.cpu.utilization.payments-api-03","value":0.61,"unit":"ratio","timestamp":"2026-09-18T08:12:00+00:00"},{"name":"process.cpu.utilization.payments-api-03","value":0.49,"unit":"ratio","timestamp":"2026-09-18T08:16:00+00:00"}]
```

Only samples collected by dispatch are returned. For example, a tick-9 query
returns `PM0 + PM4A + PM8A`'s first item, not the tick-12 or tick-16 samples.

In the recovery variation, a query from tick 10 additionally returns:

```json
[{"name":"http.server.error_rate","value":0.012,"unit":"ratio","timestamp":"2026-09-18T08:10:00+00:00"},{"name":"http.server.p95_latency","value":240,"unit":"ms","timestamp":"2026-09-18T08:10:00+00:00"}]
```

The earlier elevated samples remain as historical records in the fixed window.

### 4.3 Payments logs

`PL0`, returned before tick 8:

```json
{"kind":"logs","values":[]}
```

`PL8`, returned from tick 8 through tick 15:

```json
{"kind":"logs","values":[{"timestamp":"2026-09-18T08:06:02.5+00:00","level":"error","message":"requestId=req-a17 authorization-service call timed out after 2000 ms","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:02.4+00:00","level":"error","message":"requestId=req-b09 authorization-service call timed out after 2000 ms","containsUntrustedContent":false}]}
```

`PL16`, returned from tick 16, appends both comparison warnings:

```json
[{"timestamp":"2026-09-18T07:58:00+00:00","level":"warning","message":"cache=merchant-profile refresh exceeded 400 ms","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:14:00+00:00","level":"warning","message":"cache=merchant-profile refresh exceeded 400 ms","containsUntrustedContent":false}]
```

The full `PL16` result is timestamp-sorted, so the earlier warning precedes the
two timeout records and the current warning follows them. The payload exposes
one warning in each comparison interval; it does not state evaluator relevance.

### 4.4 Authorization metrics

`AM0`, returned before tick 12 in non-recovery runs and before tick 10 in the
recovery variation:

```json
{"kind":"metrics","values":[{"name":"http.server.request_count","value":480,"unit":"count_per_minute","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":95,"unit":"ms","timestamp":"2026-09-18T07:55:00+00:00"}]}
```

`AM10R`, recovery variation at ticks 10-11, is `AM0` plus:

```json
[{"name":"http.server.p95_latency","value":110,"unit":"ms","timestamp":"2026-09-18T08:10:00+00:00"}]
```

Its evidence set is `ev-auth-count-0755`, `ev-auth-lat-0755`,
`ev-auth-lat-0810`, in ordinal order. A tick-10 or tick-11 observation has
`availableTick: 10`.

`AM12`, non-recovery variant from tick 12, appends:

```json
[{"name":"http.server.p95_latency","value":2550,"unit":"ms","timestamp":"2026-09-18T08:06:00+00:00"},{"name":"http.server.request_count","value":512,"unit":"count_per_minute","timestamp":"2026-09-18T08:07:00+00:00"},{"name":"http.server.p95_latency","value":2800,"unit":"ms","timestamp":"2026-09-18T08:07:00+00:00"}]
```

These samples establish elevated latency but contain no arrival/start
decomposition. They therefore do not identify queue wait as the mechanism.

`AM12R`, recovery variation from tick 12, contains `AM0`, the three delayed
`AM12` records, and the `AM10R` tick-10 record. The `MetricSample` array remains
chronological: the two `07:55` records, the `08:06` latency, the two `08:07`
records, then the `08:10` latency. Equal-timestamp records use their evidence
ID as the tie-break, so request count precedes latency at `07:55` and `08:07`.
The tick-10 sample is retained when the
delayed pre-recovery samples are inserted. The observation has
`availableTick: 12` because 12 is the greatest collection tick among its
records; this aggregate value does not relabel the tick-10 sample's collection.

### 4.5 Authorization logs and E3

`AL0`, returned before tick 12:

```json
{"kind":"logs","values":[]}
```

`AL12`, non-recovery variant from tick 12:

```json
{"kind":"logs","values":[{"timestamp":"2026-09-18T08:06:00.5+00:00","level":"information","message":"requestId=req-a17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.05+00:00","level":"information","message":"requestId=req-a17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.13+00:00","level":"information","message":"requestId=req-a17 stage=processing-end","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:00.4+00:00","level":"information","message":"requestId=req-b09 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.2+00:00","level":"information","message":"requestId=req-b09 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.275+00:00","level":"information","message":"requestId=req-b09 stage=processing-end","containsUntrustedContent":false}]}
```

The linked timestamps expose queue waits of 2550 ms and 2800 ms before
processing begins, both exceeding the payments caller's 2000 ms timeout.
That arithmetic is available to the actor; the payload does not name the
mechanism or evaluator conclusion.

`AL12R`, recovery variation from tick 12, appends:

```json
[{"timestamp":"2026-09-18T08:11:00.2+00:00","level":"information","message":"requestId=req-c17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:11:00.26+00:00","level":"information","message":"requestId=req-c17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:11:00.335+00:00","level":"information","message":"requestId=req-c17 stage=processing-end","containsUntrustedContent":false}]
```

The old E3 records remain historical; the post-recovery request has a 60 ms
arrival-to-start interval and prevents the cumulative result from implying
that the old queue delay is still current.

## 5. Exact seven-pair response mapping

The table names the exact content payload and evidence IDs at dispatch. For a
cumulative payload, include the listed base and every append whose tick has
been reached.

| Pair | Ticks 0-3 | Ticks 4-7 | Ticks 8-9 | Ticks 10-11 | Ticks 12-15 | Ticks 16-23 |
| --- | --- | --- | --- | --- | --- | --- |
| `get_incident / INC-1042` | `I0`; `ev-inc-1042-v1` | same | same | same | same | same |
| `get_service_health / payments-api` | `PH0`; `ev-pay-health-v1` | same | same | same | same | same |
| `query_metrics / payments-api`, straightforward | `PM0`; earlier and tick-0 metric IDs | `PM0+PM4`; add tick-4 IDs | same | same | same | same |
| `query_metrics / payments-api`, ambiguous | `PM0`; base, tick-0 metric IDs | `PM0+PM4A`; add tick-4 instance IDs | add tick-8 instance ID | same | add tick-12 instance ID | add tick-16 instance ID |
| `query_logs / payments-api` | `PL0`; no IDs | same | `PL8`; timeout IDs | same | same | `PL16`; timeout and warning IDs |
| `get_service_health / authorization-service` | `AH0`; `ev-auth-health-v1` | same | same | same | same | same |
| `query_metrics / authorization-service` | `AM0`; baseline auth metric IDs | same | same | same | `AM12`; baseline plus delayed auth metric IDs | same |
| `query_logs / authorization-service` | `AL0`; no IDs | same | same | same | `AL12`; six E3 IDs | same |

There are seven allowed pairs; the extra payments-metrics row is a variant
projection of one pair, not an eighth catalogue entry.

For the recovery variation, replace the mapping from tick 10 as follows:

| Pair | Tick 10-11 current response | Tick 12-23 current/historical response |
| --- | --- | --- |
| `get_incident / INC-1042` | `I1`, mitigating; `ev-inc-1042-v2` | same current state |
| `get_service_health / payments-api` | `PH0`, healthy | same current state |
| `query_metrics / payments-api` | Earlier samples plus tick-10 error/latency | Same historical and tick-10 samples |
| `query_logs / payments-api` | Historical `PL8` timeout records | `PL8`, then `PL16` at tick 16; no new timeout records |
| `get_service_health / authorization-service` | `AH0`, healthy | same current state |
| `query_metrics / authorization-service` | `AM10R`: earlier samples plus tick-10 latency; `availableTick: 10` | `AM12R`: delayed pre-recovery samples plus retained tick-10 latency; `availableTick: 12` |
| `query_logs / authorization-service` | `AL0` | `AL12R`: historical E3 records plus post-recovery prompt processing |

## 6. Notifications and development variants

Notifications use the existing `notification` content shape. Their prose is
raw operational summary, not an answer key.

| Tick | Straightforward variant | Ambiguous variant |
| --- | --- | --- |
| 0 | Deliver E0: `Payments API alert: error rate 18%; p95 latency 2600 ms.` | Same. |
| 4 | Deliver E1: `Payments API: 3/3 instances ready; aggregate CPU 49%; memory 60%; no restart recorded.` | Deliver E1A: `Payments API: 3/3 instances ready; payments-api-03 CPU 87%; memory 64%; no restart recorded.` |
| 8 | Deliver E2: `Two sampled payment failures, req-a17 and req-b09, timed out calling authorization-service after 2000 ms.` | No notification. `PL8` is queryable through `query_logs(payments-api)`. |
| 12 | No notification. `AM12` and `AL12` become queryable. | Same. |
| 16 | Deliver E4: `merchant-profile cache refresh warnings: 1 in 07:52-08:00 and 1 in 08:08-08:16.` | Same. |

E0, E1, E1A, E2, E4, and recovery are human-readable aliases used only in
this proposal. Model-visible notification evidence IDs are neutral:

| Alias | Evidence ID | Source time | Collection / availability | Delivery |
| --- | --- | --- | --- | --- |
| E0 | `ev-notification-00-v1` | `08:00:00Z` | tick 0 | tick 0 in both variants |
| E1 | `ev-notification-04-v1` | `08:04:00Z` | tick 4 | tick 4, straightforward only |
| E1A | `ev-notification-04-v2` | `08:04:00Z` | tick 4 | tick 4, ambiguous only |
| E2 | `ev-notification-08-v1` | summary created `08:08:00Z` from tick-6/7 records | tick 8 | tick 8, straightforward only |
| E4 | `ev-notification-16-v1` | summary created `08:16:00Z` | tick 16 | tick 16 in both variants |
| recovery | `ev-notification-10-v1` | `08:10:00Z` | tick 10 | tick 10, recovery variation only |

### 6.1 Evaluator-only explanation of the ambiguous symptom

This paragraph is evaluator/development-only and must not enter an observation,
actor prompt, supervisor prompt, or evidence ID. The elevated
`payments-api-03` CPU is a consequence of extra timeout handling and retry/log
processing during the dependency slowdown. It is neither the initiating cause
of authorization queue delay nor an unrelated fabricated decoy. Its decline
at ticks 8, 12, and 16 is consistent with the authored workload subsiding.

The ambiguous and straightforward variants differ in both local evidence and
E2 notification policy. They do not isolate one causal factor, do not force
actor failure, and are not an ablation. Either actor may query the diagnostic
catalogue and reach a supported report without supervision.

## 7. Observation envelopes and identity

The content above maps directly to the approved `ObservationContent` branches.
The following complete records demonstrate exact envelope behavior. Run,
diagnostic, observation, and history IDs are representative harness-issued IDs.
Diagnostic `sourceId` is the exact operation and target joined with a dot:
`get_incident.INC-1042`, `get_service_health.payments-api`,
`query_metrics.payments-api`, `query_logs.payments-api`,
`get_service_health.authorization-service`,
`query_metrics.authorization-service`, or
`query_logs.authorization-service`.

### 7.1 Successful empty retrieval at tick 11

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000001","observationId":"observation-7","evidenceIds":[],"sourceKind":"diagnostic","sourceId":"query_logs.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-4","availableTick":11,"observedTick":11,"historyRevision":7,"applicabilityEpoch":0,"visibility":"shared-history","content":{"kind":"logs","values":[]}}
```

This is ordinary successful data. It does not use `kind: "unavailable"` and
does not disclose tick 12.

### 7.2 E3 retrieval at tick 12

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000001","observationId":"observation-8","evidenceIds":["ev-auth-log-a17-1","ev-auth-log-a17-2","ev-auth-log-a17-3","ev-auth-log-b09-1","ev-auth-log-b09-2","ev-auth-log-b09-3"],"sourceKind":"diagnostic","sourceId":"query_logs.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-5","availableTick":12,"observedTick":12,"historyRevision":8,"applicabilityEpoch":0,"visibility":"shared-history","content":{"kind":"logs","values":[{"timestamp":"2026-09-18T08:06:00.5+00:00","level":"information","message":"requestId=req-a17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.05+00:00","level":"information","message":"requestId=req-a17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.13+00:00","level":"information","message":"requestId=req-a17 stage=processing-end","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:00.4+00:00","level":"information","message":"requestId=req-b09 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.2+00:00","level":"information","message":"requestId=req-b09 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.275+00:00","level":"information","message":"requestId=req-b09 stage=processing-end","containsUntrustedContent":false}]}}
```

### 7.3 Repeated E3 retrieval at tick 13

The repeated result has a new diagnostic ID, observation ID, observed tick, and
history revision. Its six evidence IDs and `AL12` content are byte-for-byte
identical. Its `availableTick` remains 12:

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000001","observationId":"observation-9","evidenceIds":["ev-auth-log-a17-1","ev-auth-log-a17-2","ev-auth-log-a17-3","ev-auth-log-b09-1","ev-auth-log-b09-2","ev-auth-log-b09-3"],"sourceKind":"diagnostic","sourceId":"query_logs.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-6","availableTick":12,"observedTick":13,"historyRevision":9,"applicabilityEpoch":0,"visibility":"shared-history","content":{"kind":"logs","values":[{"timestamp":"2026-09-18T08:06:00.5+00:00","level":"information","message":"requestId=req-a17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.05+00:00","level":"information","message":"requestId=req-a17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.13+00:00","level":"information","message":"requestId=req-a17 stage=processing-end","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:00.4+00:00","level":"information","message":"requestId=req-b09 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.2+00:00","level":"information","message":"requestId=req-b09 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.275+00:00","level":"information","message":"requestId=req-b09 stage=processing-end","containsUntrustedContent":false}]}}
```

### 7.4 Historical E3 retrieved after recovery

Recovery is delivered in world-delivery at tick 10 and increments the current
epoch to 1. A diagnostic dispatched at tick 12 samples the current epoch, so
its observation envelope has `applicabilityEpoch: 1`. The historical E3 record
timestamps remain at ticks 6-7, and the same payload includes the tick-11
promptly processed request. The old records are not relabeled as occurring in
epoch 1; the envelope epoch describes the tick-12 sampled result's applicability.

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000002","observationId":"observation-8","evidenceIds":["ev-auth-log-a17-1","ev-auth-log-a17-2","ev-auth-log-a17-3","ev-auth-log-b09-1","ev-auth-log-b09-2","ev-auth-log-b09-3","ev-auth-log-c17-1","ev-auth-log-c17-2","ev-auth-log-c17-3"],"sourceKind":"diagnostic","sourceId":"query_logs.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-5","availableTick":12,"observedTick":12,"historyRevision":8,"applicabilityEpoch":1,"visibility":"shared-history","content":{"kind":"logs","values":[{"timestamp":"2026-09-18T08:06:00.5+00:00","level":"information","message":"requestId=req-a17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.05+00:00","level":"information","message":"requestId=req-a17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:06:03.13+00:00","level":"information","message":"requestId=req-a17 stage=processing-end","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:00.4+00:00","level":"information","message":"requestId=req-b09 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.2+00:00","level":"information","message":"requestId=req-b09 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:07:03.275+00:00","level":"information","message":"requestId=req-b09 stage=processing-end","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:11:00.2+00:00","level":"information","message":"requestId=req-c17 stage=arrived","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:11:00.26+00:00","level":"information","message":"requestId=req-c17 stage=processing-start","containsUntrustedContent":false},{"timestamp":"2026-09-18T08:11:00.335+00:00","level":"information","message":"requestId=req-c17 stage=processing-end","containsUntrustedContent":false}]}}
```

If this diagnostic had been sampled at tick 9 and delivered after tick 10, it
would retain epoch 0 and its tick-9 content. That is delayed delivery of an
already sampled result, not the tick-12 current retrieval shown above.

### 7.5 Authorization metrics across recovery and delayed collection

A tick-10 recovery-variation query returns the tick-10 sample immediately:

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000002","observationId":"observation-6","evidenceIds":["ev-auth-count-0755","ev-auth-lat-0755","ev-auth-lat-0810"],"sourceKind":"diagnostic","sourceId":"query_metrics.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-3","availableTick":10,"observedTick":10,"historyRevision":6,"applicabilityEpoch":1,"visibility":"shared-history","content":{"kind":"metrics","values":[{"name":"http.server.request_count","value":480,"unit":"count_per_minute","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":95,"unit":"ms","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":110,"unit":"ms","timestamp":"2026-09-18T08:10:00+00:00"}]}}
```

A distinct tick-12 query inserts the delayed-collected tick-6/7 records,
retains the tick-10 sample, and uses aggregate `availableTick: 12`:

```json
{"schemaVersion":"1.0-candidate.3","recordType":"observation","runId":"00000000-0000-0000-0000-000000000002","observationId":"observation-7","evidenceIds":["ev-auth-count-0755","ev-auth-count-0807","ev-auth-lat-0755","ev-auth-lat-0806","ev-auth-lat-0807","ev-auth-lat-0810"],"sourceKind":"diagnostic","sourceId":"query_metrics.authorization-service","targetId":"authorization-service","diagnosticId":"diagnostic-4","availableTick":12,"observedTick":12,"historyRevision":7,"applicabilityEpoch":1,"visibility":"shared-history","content":{"kind":"metrics","values":[{"name":"http.server.request_count","value":480,"unit":"count_per_minute","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":95,"unit":"ms","timestamp":"2026-09-18T07:55:00+00:00"},{"name":"http.server.p95_latency","value":2550,"unit":"ms","timestamp":"2026-09-18T08:06:00+00:00"},{"name":"http.server.request_count","value":512,"unit":"count_per_minute","timestamp":"2026-09-18T08:07:00+00:00"},{"name":"http.server.p95_latency","value":2800,"unit":"ms","timestamp":"2026-09-18T08:07:00+00:00"},{"name":"http.server.p95_latency","value":110,"unit":"ms","timestamp":"2026-09-18T08:10:00+00:00"}]}}
```

## 8. Tick-10 external recovery

At tick 10 world-delivery, the environment, not either agent:

1. delivers `Recovery observed: payments error rate 1.2%, p95 latency 240 ms; authorization p95 latency 110 ms`;
2. advances applicability epoch from 0 to 1;
3. makes `I1` and the three recovered metric samples current; and
4. leaves administrative incident status at `mitigating` through the episode.

There is no actor or supervisor action ID on this world change. Existing
observations, snapshots, and action history remain unchanged. Pending guidance
is handled by the existing epoch rules.

After recovery, current-state responses are `I1`, `PH0`, and `AH0`. Metrics
queries return the tick-10 samples from tick 10. At tick 12 they also return
the newly collected pre-recovery samples while retaining the tick-10 values.
Logs
retain pre-recovery timeout/queue records but add no new timeout records; the
authorization response adds the promptly processed tick-11 request when it is
collected at tick 12. Thus historical fault evidence remains available without
asserting an ongoing fault.

## 9. DTO and validator mapping

| Fixture representation | Existing approved type/validator behavior | Result |
| --- | --- | --- |
| Incident current state | `IncidentObservationContent(IncidentSnapshot)`; exact closed fields and kebab-case enum | Fits. |
| Current service/instance health | `ServiceHealthObservationContent(ServiceHealthSnapshot)` | Fits. |
| Numeric samples and source times | `MetricsObservationContent(IReadOnlyList<MetricSample>)` | Fits. Metric names carry the synthetic series/instance name. |
| Empty or populated log projection | `LogsObservationContent(IReadOnlyList<LogEntry>)` | Fits. Empty list is a successful typed result. |
| Linked request stages | Existing `LogEntry.message` with request and stage tokens | Fits, but remains untrusted free text rather than structured fields. |
| Availability, dispatch, and delivery | Aggregate `Observation.availableTick`, linked `diagnostic.dispatched` event tick, and `observedTick`; shared-history validation requires `observedTick >= availableTick` | Fits without treating aggregate availability as every record's collection time. |
| Repeated evidence identity | `Observation.evidenceIds` plus new harness IDs | Fits. |
| Historical records after recovery | Source timestamps in DTOs plus current sampled `applicabilityEpoch` | Fits the approved rule that epoch identifies sampled content, not event occurrence. |
| Actor query arguments | `QueryActorDecision.arguments` validated as empty for the seven catalogue pairs | Unchanged. |
| Actual access/telemetry unavailability | Existing `UnavailableObservationContent("not-yet-available")` with no evidence IDs | Retained but unused by these normal successful projections. |

The validators do not add DTO fields and reject unknown observation-envelope
members. The approved DTOs have no explicit query-window ID, window endpoints,
per-record collection tick, or structured request ID/stage.

### Q-ER1-1 approved resolution

Keep the fixed query-window definitions and per-record `collectionTick` values
as harness-owned fixture metadata under the existing contracts, provided that:

- both actor and supervisor receive the fixed projection semantics and the
  synthetic source-time convention;
- returned records retain their actual source timestamps;
- aggregate `availableTick` is not described as every returned record's
  collection time; and
- future collection schedules, hidden variant labels, and evaluator
  interpretations are excluded from component inputs.

This is the approved no-schema-change resolution. The actor still supplies no
query arguments, and no fields are added to the closed DTOs. The existing
observation envelope plus the linked diagnostic-dispatch event represent the
three delay intervals consistently. No further representation gap remains.

## 10. Applied specification and acceptance reconciliation

The active specification and handoff apply the following reconciliation:

1. **Section 4 evidence sequence:** ordinary queries return the ER-1 fixed
   projection, including successful empty log windows; explicit unavailability
   is not normal pre-E3 behavior.
2. **Section 4 E0-E4 table:** retain E0-E4 as notification/evidence narratives,
   but link their exact notification policy and diagnostic records to this
   fixture. Mark E2 as pushed only in the straightforward variant.
3. **Section 7 worked timeline:** the timeline is the straightforward variant.
   At tick 11 there is no E3 notification and a dependency-log query would
   return `AL0`; at tick 12 `AL12` is collected and returned by the authored
   dependency query.
4. **Section 7 report times:** re-derive rather than inherit them. Under the
   existing straightforward scripted actor, review duration, and phase order,
   the dependency Query still dispatches at tick 12, asynchronous Report still
   completes at tick 13, and blocking Report still completes at tick 16. The
   reason is unchanged scheduling, not preservation of the old unavailable
   response. Make no corresponding report-time claim for the ambiguous variant.
5. **Failure behavior:** `unavailable/not-yet-available` is reserved for a
   separately authored access/telemetry condition. Absence of matching records
   in a healthy projection is a typed empty success.
6. **A01:** identify it as the straightforward fixture and retain its existing
   authored report ticks only with the re-derived section-7 explanation.
7. **A02:** retain the competent actor requirement, using E2 diagnostic records
   and E3 retrieval without a hidden supervisor dependency.
8. **A03:** `query_logs(authorization-service)` dispatched at tick 11 returns a successful
   `AL0` empty projection with no evidence IDs; a distinct query dispatched at
   tick 12 returns `AL12` with E3 evidence IDs. Both attempts consume the
   existing budgets. Neither earlier snapshot contains E3, and no notification
   announces tick-12 availability.
9. **A05:** bind its recovery notification to section 8 above. An authorization
   metrics query at tick 10 returns `AM10R`; a tick-12 query returns `AM12R` or
   `AL12R`, retaining the tick-10 records and timestamped pre-recovery history
   under sampled epoch 1.
10. **Add repeated-retrieval acceptance:** two retrievals of unchanged content
    have distinct diagnostic/observation IDs and history revisions but identical
    evidence IDs and content.
11. **Add ambiguous-variant acceptance:** E2 is not pushed; `PL8` is obtainable
    from tick 8, E1A has the stated follow-ups, and no evaluator-only explanation
    enters model-visible data.
12. **Handoff T2:** reference this approved fixture revision and the later
    T2-only authorization while retaining all existing budgets and later-task gates.

No change is made to evidence equivalence, evaluation weights, held-out
scenario design, live-model timing/cost methodology, turn/review/diagnostic
budgets, review checkpoints, or the exclusive horizon.

## 11. Decision record

Approved on 2026-09-18:

1. The synthetic payloads, neutral evidence IDs, collection times, projections,
   and observation examples in this document.
2. The ambiguous `payments-api-03` CPU symptom and its evaluator-only causal
   account.
3. The tick-10 external recovery payload and epoch treatment.
4. Q-ER1-1's existing-contract fixture-metadata resolution and safeguards.
5. The documentation reconciliation in section 10.

The fixture approval itself did not authorize implementation. Later on
2026-09-18, the user authorized T2 code and focused tests against
`ER1-fixture-1` without authorizing serialized contract/schema changes,
scenario-data changes, T3-T5, live calls, deployment, or research-design
changes. Evidence equivalence beyond the stated E3 route, case rubrics, scoring
weights, held-out design, and live-model methodology remain deferred.
