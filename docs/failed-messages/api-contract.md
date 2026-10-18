# Failed Messages Monitor — API contract (Phase 1, frontend-facing)

Status: implemented — every endpoint below has a handler and is wired up (see `design.md`). This
document is the wire shape the FE builds against.

## Changelog (2026-10-04, round 15)

- **`failureClass`**: a `Failed` row whose error starts with `Disallowed type:` is `Disallowed` only when its
  `eventType` currently resolves to a type outside the allowed namespace. If it is unresolvable (legacy rows
  written before the delivery service told the two cases apart) it is `Unresolvable`; if it now resolves to an
  allowed type it is `Unresolvable` with `typeResolvable: true`. No shape change.

## Changelog (2026-10-04)

2026-10-04: **`failureClass`** added to the outbox list items and the outbox detail
(`GET /admin/outbox-messages`, `GET /admin/outbox-messages/{module}/{id}`) — see "Outbox module whitelist" for
its definition. The FE reads it instead of matching the start of `error`.

## Changelog (2026-10-03)

2026-10-03: PII masking/reveal removed by user decision — menu permission implies the right to see the data.

- **Removed**: `POST /admin/failed-messages/{id}/reveal-body`, `POST /admin/outbox-messages/{module}/{id}/reveal-payload`,
  the `RevealBody` audit action, the `bodyMasked`/`bodyRevealable`/`payloadMasked`/`payloadRevealable`
  flags, the `FailedMessages:MaskedKeys`/`MaskedExactKeys` config keys, the exception-message scrubber and
  the stored `ExceptionMessageScrubbed` column.
- **`GET /admin/failed-messages/{id}`** now returns the raw `body`, the raw `stackTrace` (new field,
  omitted when the fault had none) and the **full** `headers` set including `MT-Fault-Message` /
  `MT-Fault-StackTrace`. A non-JSON body is returned as its raw text (no `"(non-JSON body, N bytes)"`
  placeholder).
- **`GET /admin/outbox-messages/{module}/{id}`** now returns the raw `payload`; `error` is raw on both
  outbox endpoints.
- **`exceptionMessage`** (list and detail) is the raw message, and `search` matches it directly.
- **Permissions**: `FAILED_MESSAGE_VIEW` (read) / `FAILED_MESSAGE_MANAGE` (retry/discard/resend) only.

## Changelog (2026-09-28)

- **D1** — Summary response gains `serverTime`; FE computes staleness against it, not the browser clock.
- **D2** — `queues[].errorCount`/`skippedCount` (and the synthetic-queue rule) count `Pending` rows only, not `RetryRequested`.
- **D5** — Retry gains skip reason `TooSoon` (InboxGuard's 5-minute stale-claim window); list/detail rows gain `retryAvailableAt`.
- **D6** — Outbox `Stuck` now uses a 2-minute threshold, separate from the 5-minute auto-reset.
- `newerSentCount` is now `int | null` (`null` = unknown, history older than the 7-day Processed retention may be purged) on both outbox endpoints.
- Retry `NotPending` unroutable-queue case now bounces the row back to `Pending` with a `RetryFailed` history action; discard now also accepts `RetryRequested` rows.

## Changelog (2026-09-29)

- **Discard skip reason `Publishing`** — the collector now claims a `RetryRequested` row (single
  conditional UPDATE on `RetryClaimedAt`) immediately before publishing it, so a second collector
  instance sharing the same node name (an IIS overlapped recycle, or a second process) can never publish
  the same row twice. Discard's skipped reasons are now `NotFound | NotPending | Publishing`: a
  `RetryRequested` row whose claim is younger than 5 minutes is skipped as `Publishing` rather than
  discarded out from under an in-flight publish. A claim older than 5 minutes is stale — the owning node
  died or the publish failed silently — and does not block discard (or a fresh claim by the collector).
  `RetryClaimedAt` is internal-only, never returned on the wire.

## Changelog (round-3 review)

- **Search wildcards are escaped**: a literal `%`, `_`, or `[` in the search box now matches literally
  (`LIKE @p ESCAPE '\'`), not as a SQL wildcard. Applies to both `GET /admin/failed-messages?search=` and
  `GET /admin/outbox-messages?search=`.
- **Discard's `Publishing` skip reason unchanged** — still `NotFound | NotPending | Publishing`.
- **`RetryFailed` audit rows: `actorCode` confirmed `null`** — server-generated, no signed-in user; this
  was re-verified during round-3 review and intentionally NOT changed to `"system"`.

## Changelog (2026-09-29, round-4 review)

- **`nodes[].managementStatus` and `nodes[].collectedAt` are now omitted for a node with no snapshot at
  all** — a node can have `FailedMessages` rows (e.g. `Pending`) before its collector has ever completed
  a Snapshot round; it still appears in `nodes[]`, with `managementStatus`/`collectedAt`/`lastError` all
  omitted and its queue health built purely from the `FailedMessages` counts (same per-queue omission
  rule as the existing synthetic-queue case). FE shows "no snapshot yet" for that node.
- **Discovery is now scoped to OUR fault queues only** — the Management API response is a whole-vhost
  read, but the collector only ever treats a queue as a fault-queue candidate (and therefore only ever
  `BasicGet`s from it) when it's one of THIS application's own receive endpoints with `_error`/`_skipped`
  appended. Another application's fault queue in the same vhost is never drained, even though it's still
  visible in the vhost-wide queue list the snapshot's `queues[]` array is built from.
- **Summary performance**: `GET /admin/failed-messages/summary` is now one round trip (a multi-statement
  batch) instead of six, and the outbox `outboxFailedCount`/`outboxStuckCount` share one pass over the
  six-schema UNION instead of two. No change to the response shape.

- **camelCase**: `Bootstrapper/Api/Program.cs:362` sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.
- **Nulls are omitted, not `null`**: `Program.cs:360-361` sets `DefaultIgnoreCondition = WhenWritingNull`.
  A nullable field with no value is **absent from the JSON object**, not present as `"field": null`.
  FE must treat "key missing" the same as "key is null".
- **Enums are strings**: `JsonStringEnumConverter()` is registered globally (`Program.cs:363`), and every
  existing admin DTO (`WebhookDeliveryListDto.Status` in
  `Modules/Integration/Integration/Application/Features/WebhookDeliveries/GetWebhookDeliveries/GetWebhookDeliveriesQuery.cs:21`)
  types the field as `string`, not a C# enum, so no naming-policy surprises. This contract does the same:
  every enum-like field below is plain string, exact member name (PascalCase), e.g. `"Pending"`, not
  `"pending"`.
- **DateTime has no `Z` / offset, 0-7 fractional digits, trailing zeros trimmed**: the app never uses
  UTC on the wire (`Shared/Shared/Time/DateTimeProvider.cs` — `ApplicationNow` calls
  `TimeZoneInfo.ConvertTimeFromUtc(...)`, which returns `DateTimeKind.Unspecified`). No custom
  `DateTime` converter is registered in `Program.cs`, so default `System.Text.Json` serialization of an
  `Unspecified`-kind value emits ISO-8601 with no zone suffix, and it trims trailing zero fractional
  digits — a whole-second value serialises with **no** fractional part at all. Examples:
  `"2026-09-27T09:00:00"` (whole second) and `"2026-09-27T14:32:05.1234567"` (sub-second, no trailing
  zeros to trim). Treat every datetime on this API as **already in Bangkok local time** — do not call
  `.toISOString()` parsing that assumes UTC.
- **Pagination is 1-based on the response too**: query params `pageNumber`/`pageSize` are 1-based
  (mirrors `GetWebhookDeliveriesQueryValidator.cs:14`,
  `RuleFor(x => x.PageNumber).GreaterThanOrEqualTo(1)`). Response shape from
  `Shared/Shared/Pagination/PaginatedResult.cs`: `{ items: T[], count: number, pageNumber: number,
  pageSize: number }` — `count` is a `long` (`PaginatedResult.Count`), still just a JSON number.
  **Our** `pageNumber` in the response is 1-based, the same value the caller sent: `QueryPaginatedAsync`
  works in 0-based terms internally, so our handlers re-wrap its result with the original 1-based
  `pageNumber` before returning. **Do not copy the existing webhook endpoint's handling** — it echoes
  0-based: `GetWebhookDeliveriesQueryHandler.cs:68` passes `PageNumber - 1` into `PaginationRequest`,
  and `DapperPaginationExtensions.cs:134` returns that same 0-based value straight through as the
  result's `PageNumber`, unwrapped. `pageSize` default **20**, max **100** (new limits for this screen;
  the older webhook endpoint allows up to 200 — not reused here).
- **400 (validation)**: FluentValidation failures render via `Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs:75-80`
  as a `ProblemDetails` with `title: "ValidationException"`, plus an extension array:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "ValidationException",
    "status": 400,
    "detail": "<FluentValidation's default aggregate message>",
    "instance": "/admin/failed-messages",
    "traceId": "0HN...",
    "ValidationErrors": [
      { "propertyName": "PageSize", "errorMessage": "...", "errorCode": "...", "severity": 0 }
    ]
  }
  ```
  Every 400 on the write endpoints below (empty/oversized `ids[]`/`items[]`, blank discard `reason`,
  unknown `module`) is a FluentValidation failure, so it renders as `ValidationException` with
  `ValidationErrors`, the same as above — not a `BadRequestException`. A plain `BadRequestException`
  would render the same envelope shape with `title: "BadRequestException"`, `detail:
  <exception.Message>`, no `ValidationErrors` key, but nothing in this API throws one today.
- **404**: `NotFoundException` → `title: "NotFoundException"`, `status: 404`, `detail: <message>`
  (`CustomExceptionHandler.cs:81-86`).
- **403**: policy failure (missing permission) is handled by the auth middleware before MediatR runs,
  not by `CustomExceptionHandler` — standard ASP.NET `403` with an empty body.
- **409** (not used by any endpoint below today, documented for completeness): `ConflictException` →
  `title: "ConflictException"`, `status: 409`, optional `errorCode` extension
  (`Shared/Shared/Exceptions/ConflictException.cs`, `CustomExceptionHandler.cs:87-92,171-172`).
- **Permissions**: `FAILED_MESSAGE_VIEW` (read endpoints), `FAILED_MESSAGE_MANAGE` (write endpoints,
  retry/discard/resend) — these are the permission **codes** the FE checks against the logged-in
  user's permission list, same pattern as `WEBHOOK_DELIVERIES_VIEW` / `WEBHOOK_DELIVERIES_RETRY`
  registered in `Modules/Auth/Auth/AuthModule.cs:333-334`.

## Ordered (partitioned) receive endpoints

From `Bootstrapper/Api/Program.cs` (`grep 'ReceiveEndpoint("'`), exactly 7 queue names:

```
webhook-dispatch, appraisal-ext-cycle, appraisal-sync, appraisal-status-dashboard,
appraisal-sla-recalc, workflow-instance-variables, pma-sync-status
```

`sourceQueue` is always the base queue name — it never carries a `_error`/`_skipped` suffix; `kind`
(`Error`/`Skipped`) tells which fault queue the row was collected from. `isOrderedQueue = true` when a
row's `sourceQueue` is in this list.

Retention: a `Skipped` row still `Pending` is deleted once its last admin action (or, if none, the time the collector stored it) is older than
`FailedMessages:SkippedPendingRetentionDays` (default 30), so an old Skipped row can disappear from the list and summary
without any operator action. `Error` rows are never purged while `Pending`; Retried/Discarded rows go after 90 days.

## Non-transient exception match

Decided: `isNonTransient = true` when `exceptionType`'s **simple name** (the segment after the last
`.`) equals one of:

- `ConflictException` (`Shared.Exceptions.ConflictException`, `Shared/Shared/Exceptions/ConflictException.cs`)
- `MissingIdentityKeyException` (`Collateral.CollateralMasters.Exceptions.MissingIdentityKeyException`,
  `Modules/Collateral/Collateral/CollateralMasters/Exceptions/MissingIdentityKeyException.cs`)

Not the full namespace-qualified name.

## Outbox module whitelist

Six `IntegrationEventOutbox` tables (unqualified table name `IntegrationEventOutbox`,
`Shared/Shared/Data/Outbox/IntegrationEventOutboxConfiguration.cs:16`), one per module `DbContext`
default schema:

| module value | schema (`HasDefaultSchema`) | DbContext |
|---|---|---|
| `request` | `request` | `Modules/Request/Request/Infrastructure/RequestDbContext.cs:15` |
| `appraisal` | `appraisal` | `Modules/Appraisal/Appraisal/Infrastructure/AppraisalDbContext.cs:237` |
| `document` | `document` | `Modules/Document/Document/Infrastructure/DocumentDbContext.cs:19` |
| `workflow` | `workflow` | `Modules/Workflow/Workflow/Data/WorkflowDbContext.cs:79` |
| `collateral` | `collateral` | `Modules/Collateral/Collateral/Data/CollateralDbContext.cs:29` |
| `reporting` | `reporting` | `Modules/Reporting/Reporting.Data/ReportingDbContext.cs:24` |

`module` query/body values are these six lowercase strings; the API checks membership before
interpolating into a schema-qualified `UNION ALL` query. Decided: lowercase schema names, not
PascalCase module names.

Row columns (from `Shared/Shared/Data/Outbox/IntegrationEventOutboxMessage.cs`): `Id` (guid),
`EventType` (string), `Payload` (nvarchar(max) JSON text), `Headers` (`Dictionary<string,string>`),
`CorrelationId` (string?), `OccurredAt` (datetime), `ProcessedAt` (datetime?), `Error` (string?, max
2000 chars), `RetryCount` (int), `Status` (`Pending`/`Processing`/`Processed`/`Failed`, from
`OutboxMessageStatus` enum in the same file).

**`status` filter semantics** for `GET /admin/outbox-messages`:
- `Failed` (default) — `Status = 'Failed'`.
- `Stuck` — `Status = 'Processing' AND ProcessingStartedAt < DATEADD(MINUTE, -2, @now)`. For a
  `ProcessingStartedAt IS NULL` row, it counts as stuck only when `OccurredAt < DATEADD(MINUTE, -2, @now)`
  as well (rolling-deploy safety: a row that just entered `Processing` while the column is still being
  backfilled should not flash "stuck" immediately). This 2-minute display threshold is a **separate**
  constant from the 5-minute auto-reset threshold below — operators are meant to see a row as stuck
  *before* the background job resets it, so the two numbers cannot be the same constant. It is not a
  multiple of `OutboxDelivery:LeaseDuration` (30s) either — a threshold tied to the lease duration can be
  shorter than the time a batch legitimately stays in flight on another node (W1 code review).
  `outboxStuckCount` (summary) uses this identical definition.

  The **auto-reset** stays at 5 minutes (`Shared.Data.Outbox.OutboxOrphanPolicy.OrphanedProcessingThreshold`),
  used by the lease-holder's own orphan-reset (`IntegrationEventDeliveryService.ResetOrphanedProcessingAsync`)
  and `OutboxCleanupJob`, described in `todo-outbox-stuck-processing-fix.md` item 3 — unchanged by this
  delta. A `ProcessingStartedAt IS NULL` row is treated as orphaned by *that* job only when `OccurredAt`
  is also older than 5 minutes, not 2. `ProcessingStartedAt` does not exist in the schema yet — it ships
  in PR-B together with this screen (design decision D15), so by the time this endpoint is built the
  column is real.
- `Resent` — Decided: rows joined to a `FailedMessageAuditLogs` entry
  `Action = 'OutboxResend', OutboxModule = @module, TargetId = @id`. A resent row's own `Status` moves
  on immediately (back to `Pending`, then `Processed`/`Failed`), so this filter reads the audit log,
  not the row's current status.
- `All` — no status filter.

`newerSentCount` (design §2) — count of `Processed` rows in the same module with the same
`CorrelationId` and a later `OccurredAt`, i.e. how many messages for the same correlation group have
already gone out after this one failed; warns the operator that resending may be a stale overwrite.
Always `0` when `correlationId` is `null` (nothing to group by).
**`null` means unknown** (always emitted as an explicit `null`, never omitted): `OutboxCleanupJob` deletes
`Processed` rows after 7 days (`OutboxCleanupJob.RetentionDays`) while `Failed` rows are kept 90, so for a row
whose `occurredAt` is older than 7 days the newer `Processed` history may already have been purged and
"0" cannot be trusted. Clients must treat `null` as "can't verify" and warn, not as "safe to resend". The
`occurredAt` comparison is done as `datetime2`, so a sibling newer by less than 3.33 ms still counts.
`typeResolvable` — `true` if `EventType`'s assembly-qualified name (`Shared/Shared/Data/Outbox/IntegrationEventOutbox.cs:26`,
`$"{typeof(TEvent).FullName}, {typeof(TEvent).Assembly.GetName().Name}"`) still resolves to a loaded
type at read time; `false` for a payload whose event class was renamed/removed since it failed.

`failureClass` — `"Disallowed" | "Unresolvable" | "Deserialization" | null`, on both outbox list items and
the detail. Tells the client WHY a `Failed` row failed deterministically, so it never matches `error` text:
`Disallowed` = the event type resolved but is outside the allowed namespace (a resend can never help) — decided
from what `eventType` resolves to NOW, not from the error text alone, because rows failed by the OLD delivery code
carry "Disallowed type:" for an unresolvable type too: such a legacy row is `Unresolvable` (and, if the type has
been deployed since, `typeResolvable` is `true` next to it — "type now resolves");
`Unresolvable` = the type could not be resolved past the version-skew grace period (a resend helps once the
type is deployed everywhere — compare `typeResolvable`); `Deserialization` = the payload threw while
deserialising or deserialised to null past the grace period. `null` for any other error (including a
publish failure that exhausted its retries) and for every row whose `status` is not `Failed`.
Classified server-side from the error prefixes in `Shared.Messaging.Services.OutboxFailureReasons`, the same
constants the delivery service writes them with. Always serialised, as an explicit `null` — never omitted
(unlike the other optional fields).

`refType` / `refId` / `refNumber` — resolved from `Payload`, using the same first-known-key precedence
as consumer rows (design D12): Appraisal → Request → Quotation → Meeting → Document. Never derived
from `CorrelationId`. Omitted (not `null`) when no reference key resolves. `refType` is one of
`appraisal | request | quotation | meeting | document`; `refId` is a guid serialised as a string.

`processingStartedAt` — from `IntegrationEventOutboxMessage.ProcessingStartedAt` (PR-B,
`todo-outbox-stuck-processing-fix.md` item 3). Omitted when `NULL` (never entered `Processing`, or
was reset back to `Pending`). On a `Failed` row it is the row's last claim, kept so the nightly purge ages the row by it.

---

## Endpoints

### `GET /admin/failed-messages/summary`
Permission: `FAILED_MESSAGE_VIEW`. No query params.

```json
{
  "serverTime": "2026-09-27T14:30:05",
  "pendingCount": 12,
  "oldestPendingAt": "2026-09-26T09:14:02",
  "last24h": { "total": 40, "retried": 25, "discarded": 10 },
  "topGroups": [
    { "queue": "appraisal-sync", "exceptionType": "System.TimeoutException", "count": 6 }
  ],
  "outboxFailedCount": 3,
  "outboxStuckCount": 1,
  "nodes": [
    {
      "node": "APP-NODE-01",
      "collectedAt": "2026-09-27T14:30:00",
      "managementStatus": "Ok",
      "queues": [
        {
          "name": "webhook-dispatch",
          "ready": 4,
          "unacked": 0,
          "consumers": 2,
          "publishRate": 1.2,
          "deliverRate": 1.1,
          "samples": [0,0,1,1,2,2,3,4,4,3,3,2,1,1,0,0,0,1,2,2,3,3,4,4,3,2,1,1,0,0],
          "errorCount": 2,
          "skippedCount": 0,
          "isOrdered": true
        },
        {
          "name": "appraisal-sync",
          "errorCount": 3,
          "skippedCount": 0,
          "samples": [],
          "isOrdered": true
        }
      ]
    }
  ]
}
```

Field notes:
- `serverTime` — `IDateTimeProvider.ApplicationNow` at the moment the response is built, same zone-less
  format as every other datetime on this API. FE computes staleness (e.g. "node data is N seconds old")
  as `serverTime − nodes[].collectedAt`, never against the browser's own clock/zone.
- `oldestPendingAt` — omitted when `pendingCount = 0`.
- `managementStatus` ∈ `Ok | Unauthorized | Unreachable` (design §2/D9; `Unauthorized` = RabbitMQ
  management API returned 401, `Unreachable` = connection failed). **Omitted, along with `collectedAt`**
  (round-4 review), for a node that has `FailedMessages` rows but has never written a `BrokerSnapshot`
  row at all (its collector hasn't completed a Snapshot round yet, or the row was lost) — FE shows
  "no snapshot yet" for that node rather than a stale/misleading status. This is a NODE-level omission,
  distinct from the per-queue "no broker snapshot row" case below, which still has a real
  `managementStatus`/`collectedAt` for the node itself.
- `nodes[].lastError` — **node-level only** (`BrokerSnapshots.LastError`, design §1); present only
  when that node's last collection round failed, omitted otherwise. There is no per-queue `lastError`.
- `nodes[].queues[]` — one entry per **base** queue (a queue whose name does not end in `_error` or
  `_skipped`). Each base queue's fault twins are folded into its own `errorCount`/`skippedCount`
  rather than listed as separate rows; e.g. `webhook-dispatch_error` and `webhook-dispatch_skipped`
  never appear as their own queue entries. A queue also appears here if it has no broker snapshot row
  at all but does have `FailedMessages` rows with `Status = 'Pending'` (see the `appraisal-sync` example
  above) — for instance when `managementStatus = Unauthorized` and the collector has no queue list from
  the broker. In that case `ready`, `unacked`, `consumers`, `publishRate`, `deliverRate` are **omitted**
  and `samples` is `[]`.
  The same omission applies to **every** queue of a node whose latest round fetched no queue data from
  the Management API (`managementStatus` ≠ `Ok`, cooldown, or a malformed response): the collector writes
  an empty queue list for that round instead of carrying the previous round's depths forward, so
  `nodes[].collectedAt` never makes older `ready`/`unacked`/`consumers` look fresh.
- `errorCount` / `skippedCount` — **not** a broker read (the collector drains `_error`/`_skipped`
  every 15s, so a live count would almost always be zero). Instead: `COUNT` of `FailedMessages` rows
  for this `Node` + base `SourceQueue` + `Kind` (`Error`/`Skipped`) with **`Status = 'Pending'`** only —
  a row already in `RetryRequested` is on its way out and no longer counts as an outstanding error/skip.
- `samples` — 0 to 30 ints, oldest → newest, one point per 60s over the trailing 30 minutes
  (`GET /api/queues?lengths_age=1800&lengths_incr=60`, design §3.4), measuring `messages_ready`
  (queue depth), not throughput. Decided: no server-side padding — if RabbitMQ has fewer than 30
  points (new/restarted queue), fewer are returned and FE draws whatever it gets; `[]` for a queue
  with no broker snapshot row at all.
- `isOrdered` — see "Ordered (partitioned) receive endpoints" above.

Summary field definitions:
- `pendingCount` / `oldestPendingAt` — count / earliest `FaultedAt` of `FailedMessages` rows with
  `Status = 'Pending'`.
- `last24h.total` — count of `FailedMessages` rows with `FaultedAt` in the last 24 hours (any status).
- `last24h.retried` / `.discarded` — count of rows whose `Status` is `Retried` / `Discarded` **and**
  whose `ActionAt` falls in the last 24 hours (not `FaultedAt`).
- `topGroups` — `Pending` rows grouped by `(SourceQueue, ExceptionType)`, top 5 by count descending;
  `queue` in the response is the group's `SourceQueue`.
- `outboxFailedCount` — count across all six outbox tables with `Status = 'Failed'`.
- `outboxStuckCount` — count across all six outbox tables matching the same `Stuck` definition as
  `GET /admin/outbox-messages?status=Stuck` (see "Outbox module whitelist" above).

---

### `GET /admin/failed-messages`
Permission: `FAILED_MESSAGE_VIEW`.

| param | type | default | notes |
|---|---|---|---|
| `status` | string | `Pending` | one of `Pending, RetryRequested, Retried, Discarded, All` |
| `queue` | string? | — | exact match on `sourceQueue` |
| `node` | string? | — | exact match on `node` |
| `exceptionType` | string? | — | exact match |
| `search` | string? | — | matches `messageId`, `exceptionMessage`, `refNumber` (`LIKE @p ESCAPE '\'`, term's own `%`/`_`/`[` escaped) |
| `pageNumber` | int | `1` | 1-based |
| `pageSize` | int | `20` | max `100` |

Sort order: `faultedAt DESC` (newest failure first), matching the existing webhook pattern's
`CreatedAt DESC` (`GetWebhookDeliveriesQueryHandler.cs:72`).

Response — `PaginatedResult<FailedMessageListItem>`:
```json
{
  "items": [
    {
      "id": "0199...guid",
      "node": "APP-NODE-01",
      "sourceQueue": "appraisal-sync",
      "kind": "Error",
      "status": "Pending",
      "messageId": "b1a2...guid",
      "messageType": "Appraisal.WorkflowTransitionedIntegrationEvent",
      "consumerType": "Appraisal.Consumers.WorkflowTransitionedIntegrationEventHandler",
      "exceptionType": "System.TimeoutException",
      "exceptionMessage": "The operation has timed out.",
      "retryCount": 1,
      "faultedAt": "2026-09-27T09:00:00",
      "collectedAt": "2026-09-27T09:00:12",
      "retryAvailableAt": "2026-09-27T09:05:00",
      "refType": "appraisal",
      "refId": "8f2c...guid",
      "refNumber": "AP-2026-00042",
      "isOrderedQueue": true,
      "isNonTransient": false,
      "siblingCount": 2
    }
  ],
  "count": 37,
  "pageNumber": 1,
  "pageSize": 20
}
```
- `refType` ∈ `appraisal | request | quotation | meeting | document`, omitted (not `null`) when no
  reference key was found (design D12). `refId` is a guid, serialised as a string.
- `actionBy` / `actionAt` — present only once `status` is `RetryRequested`/`Retried`/`Discarded`;
  omitted while `Pending`.
- `siblingCount` — other `FailedMessages` rows sharing the same `messageId` (design D11/D13: one
  event failing at several consumers), **excluding** the row itself.
- `retryAvailableAt` — `max(faultedAt, collectedAt)` + InboxGuard's 5-minute stale-claim window (see `TooSoon` under
  `POST /admin/failed-messages/retry` below); the point in time retry stops returning `TooSoon` for this
  row. Omitted when `status` is not `Pending`, or when the window has already elapsed (retry is
  available now — FE has no "wait until" to show).

---

### `GET /admin/failed-messages/{id}`
Permission: `FAILED_MESSAGE_VIEW`. 404 if `id` not found.

All list fields **except `siblingCount`** (`siblings.length` gives the same number, so the detail DTO
doesn't repeat it), plus:
```json
{
  "conversationId": "c3d4...guid",
  "contentType": "application/vnd.masstransit+json",
  "headers": {
    "MT-Message-Type": "urn:message:Appraisal:WorkflowTransitionedIntegrationEvent",
    "MT-Fault-Message": "The operation has timed out.",
    "MT-Fault-StackTrace": "System.TimeoutException: ...\n   at ..."
  },
  "stackTrace": "System.TimeoutException: ...\n   at ...",
  "body": "{\"appraisalId\":\"8f2c...\",\"customerName\":\"John Doe\"}",
  "actionReason": "requeue after DB failover",
  "siblings": [
    { "id": "0199...guid2", "sourceQueue": "appraisal-status-dashboard", "kind": "Error", "status": "Pending", "faultedAt": "2026-09-27T09:00:00" }
  ],
  "history": [
    { "action": "Retry", "actorCode": "jsmith", "at": "2026-09-27T10:00:00" }
  ]
}
```
- `headers` — flat object; every AMQP header value is coerced to a string on the wire: `byte[]` →
  UTF-8-decoded string, `AmqpTimestamp` → unix seconds as a string, everything else via its natural
  string form. The **full** stored header set is returned, including `MT-Fault-Message` and
  `MT-Fault-StackTrace`.
- `stackTrace` — the raw stack trace; omitted (not `null`) when the fault had none.
- `body` — the raw message body decoded as UTF-8 text, returned as stored. A body that is not JSON is
  returned as its raw text too; there is no placeholder and no base64 path.
- `exceptionMessage` (inherited from the list fields) is the raw message, exactly as stored. `search`
  matches the same column.
- `history[].reason` — omitted (not `null`) when the action had no reason (e.g. a `Retry` with no
  `reason` supplied).
- `history[].action` ∈ `Retry | Discard | RetryFailed` — `RetryFailed` is a
  server-generated entry (no `actorCode`), written when a republish finds its queue gone; see below.

---

### `POST /admin/failed-messages/retry`
Permission: `FAILED_MESSAGE_MANAGE`.

Request:
```json
{ "ids": ["0199...guid1", "0199...guid2"], "reason": "requeue after DB failover" }
```
- `ids` — 1 to 200 entries; `[]`, missing, or over 200 → `400 ValidationException`.
- `reason` — optional for retry; max **500** chars (matches `FailedMessage.ActionReason`'s column
  length) → over that is `400 ValidationException`.
- Only rows currently `Pending` **and** past their stale-claim window (see `TooSoon` below) are
  retryable. Sets `Status = 'RetryRequested'`; the owning node's collector performs the actual AMQP
  republish (design D3, D10 step 3).
- If the collector's republish finds `SourceQueue` no longer exists on the broker (unroutable), it puts
  the row back to `Status = 'Pending'` with `actionReason = "Retry failed: queue not found"`, and the
  action appears in `history[]` as `RetryFailed` — this is not surfaced synchronously in this endpoint's
  response (the collector does the republish asynchronously), only via the row's later state/history.
  The same revert + `RetryFailed` entry happens when the broker explicitly **nacks** the publish
  (`actionReason = "Retry failed on <node>: broker rejected the publish (nack)"`, e.g. a full queue with
  `x-overflow=reject-publish`), the broker answers the publish by closing the channel with a soft error
  (`actionReason = "Retry failed on <node>: broker closed the channel (publish refused)"`, e.g. 406; this also
  ends that retry round, the remaining rows are retried next round), or the publish can't even be built from the
  row's data — otherwise such a row would be republished every round forever. A connection-level close (e.g. 320)
  stays an unknown outcome with the claim kept. A publish the broker never confirms within the 30 s timeout
  keeps the claim (outcome unknown) and ends that retry round; the remaining rows are retried next round.

Response `200`:
```json
{
  "accepted": ["0199...guid1"],
  "skipped": [
    { "id": "0199...guid2", "reason": "NotPending" }
  ]
}
```
- `skipped[].reason` ∈ `NotFound | NotPending | TooSoon`.
- `skipped[].by` / `.at` — who/when the row left `Pending` (the prior action); present only for
  `NotPending`, omitted (including for `NotFound` and `TooSoon`) otherwise.
- `TooSoon` — `max(faultedAt, collectedAt)` is younger than InboxGuard's 5-minute stale-claim window
  (`StaleThresholdMinutes`, `Shared/Shared.Messaging/Filters/InboxGuard.cs:16` — a hardcoded constant,
  not configurable via any config key). Reason: a consumer that throws without going through
  `InboxGuard.RunOnceAsync`'s release path leaves its inbox claim row `Processing` for up to 5 minutes;
  republishing before that window elapses risks the redelivered message landing on a still-`Processing`
  inbox claim and being silently skipped as a duplicate, with the work never actually redone. The server
  refuses the retry outright rather than accept it and have it silently no-op later. `collectedAt` is
  part of the max because `faultedAt` falls back to the message's publish time for an `Error` row with no
  parseable `MT-Fault-Timestamp`, which can be arbitrarily older than the real fault; `collectedAt` is
  always on or after it.
  Field note for FE: `TooSoon` is *expected* right after a failure — don't treat it as an error state,
  just offer retry again after the window elapses.

---

### `POST /admin/failed-messages/discard`
Permission: `FAILED_MESSAGE_MANAGE`. Same request/response shape as retry, except:
```json
{ "ids": ["0199...guid1"], "reason": "duplicate of AP-2026-00042, safe to drop" }
```
- `reason` — **required**; missing/blank → `400 ValidationException`; max **500** chars (same column
  limit as retry) → over that is also `400 ValidationException`.
- Discardable statuses: `Pending` **or** `RetryRequested` — sets `Status = 'Discarded'`. (`RetryRequested`
  is accepted here, unlike retry, as the way out for a row stuck `RetryRequested` because its owning
  node is dead and will never pick it up.) `skipped[].reason` ∈ `NotFound | NotPending | Publishing` —
  `NotPending`'s definition is unchanged (any status other than `Pending`/`RetryRequested`); discard has
  no `TooSoon` case, since a stuck `RetryRequested` row is exactly what it exists to clear.
- `Publishing` — the row is `RetryRequested` **and** the collector claimed it (internal `RetryClaimedAt`)
  less than 5 minutes ago, meaning a publish for it may be in flight right now. A claim 5 minutes or
  older is stale and does not block discard (the same 5-minute window the collector itself uses before
  re-claiming an abandoned row). `by`/`at` are omitted for `Publishing`, same as every reason besides
  `NotPending`.

---

### `GET /admin/outbox-messages`
Permission: `FAILED_MESSAGE_VIEW`.

| param | type | default | notes |
|---|---|---|---|
| `status` | string | `Failed` | `Failed \| Stuck \| Resent \| All` — see semantics above |
| `module` | string? | — | one of the six whitelist values above |
| `search` | string? | — | matches `EventType`, `CorrelationId` (`LIKE @p ESCAPE '\'`, term's own `%`/`_`/`[` escaped) |
| `pageNumber` | int | `1` | 1-based |
| `pageSize` | int | `20` | max `100` |

Sort order: `OccurredAt DESC`.

```json
{
  "items": [
    {
      "module": "appraisal",
      "id": "0199...guid",
      "eventType": "Appraisal.Events.AppraisalStatusChangedIntegrationEvent",
      "correlationId": "8f2c...",
      "occurredAt": "2026-09-27T08:00:00",
      "processingStartedAt": "2026-09-27T08:05:00",
      "error": "System.Net.Http.HttpRequestException: Connection refused",
      "retryCount": 5,
      "status": "Failed",
      "refType": "appraisal",
      "refId": "8f2c...guid",
      "refNumber": "AP-2026-00042",
      "newerSentCount": 1,
      "typeResolvable": true,
      "failureClass": null
    }
  ],
  "count": 3,
  "pageNumber": 1,
  "pageSize": 20
}
```
`processedAt` / `processingStartedAt` omitted (not `null`) when not applicable. `refType`/`refId`/
`refNumber` — see "Outbox module whitelist" above for resolution rules; omitted when unresolved.
`error` is the raw value as stored.

---

### `GET /admin/outbox-messages/{module}/{id}`
Permission: `FAILED_MESSAGE_VIEW`. `400 ValidationException` if `module` is not one of the six
whitelist values (never reaches the database as an arbitrary schema name). 404 if `id` not found in
that module's table.

All list fields (including `failureClass`), plus:
```json
{
  "payload": "{\"appraisalId\":\"8f2c...\",\"customerName\":\"John Doe\"}",
  "headers": { "traceparent": "00-..." }
}
```
- `payload` — the raw outbox payload text, returned as stored.

---

### `POST /admin/outbox-messages/resend`
Permission: `FAILED_MESSAGE_MANAGE`.

```json
{ "items": [ { "module": "appraisal", "id": "0199...guid" } ], "reason": "retrying after LOS outage" }
```
- `items` — 1 to 200 entries; `[]`, missing, or over 200 → `400 ValidationException`. Each `module`
  is validated against the whitelist independently at the row level; an invalid module there is a
  `skipped` entry (`UnknownModule`), not a whole-request `400`.
- `reason` — optional; max **500** chars (matches `FailedMessageAuditLogs.Reason`'s column length,
  same limit as retry/discard) → over that is `400 ValidationException`.
- Only `Status = 'Failed'` rows are resendable (design D14 — never touches `Processed`/`Pending`/`Processing`).
  `UPDATE ... SET Status='Pending', RetryCount=0, Error=NULL WHERE Id=@id AND Status='Failed'`.

```json
{
  "accepted": [ { "module": "appraisal", "id": "0199...guid" } ],
  "skipped": [ { "module": "appraisal", "id": "0199...guid2", "reason": "NotFailed" } ]
}
```
`skipped[].reason` ∈ `NotFound | NotFailed | UnknownModule`.
