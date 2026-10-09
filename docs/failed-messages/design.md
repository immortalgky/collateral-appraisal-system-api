# Failed Messages monitor — design

Status: **design approved 2026-09-27, not implemented.** Mock: `collateral-appraisal-system-app/docs/poc/failed-messages-monitor-mock.html`
(artifact https://claude.ai/artifact/SkcFBTHR4rL9DwKm9VhoRf, v6). Outbox bug-fix plan: `todo-outbox-stuck-processing-fix.md`.

## Understanding summary
- An admin screen for the system team with three parts: (1) MassTransit messages that landed in `*_error` /
  `*_skipped`, retry into the original queue or discard, many at once; (2) `Failed` rows of the six module
  outboxes, resend; (3) per-node queue health.
- Why: failed messages sit invisible in the brokers today, and `Failed` outbox rows are silently deleted after 7 days.
- Two app nodes, **each with its own RabbitMQ broker (no clustering)**. The screen only reads the DB; each node's
  collector is the only thing that talks to its own broker, over localhost.
- Permissions and menu reach UAT/prod only through DbUp scripts; every instance must restart after the menu
  script (menu cache never expires).
- Non-goals: re-publish from current data, editing a body before retry, SignalR, alerts, replaying messages that
  already succeeded.

## Assumptions
1. Tens of failures a day or fewer; a handful of concurrent viewers. 15 s collector + 15 s screen polling is enough.
2. Body and headers are stored raw for 90 days, not encrypted at rest, and returned as stored — holding the menu
   permission implies the right to see the data (2026-10-03 user decision, see Changelog). A collector failure never affects normal message processing; messages stay in the broker.
3. `RabbitMQ.Client` becomes a direct package reference (pinned to what MassTransit.RabbitMQ 8.4.1 uses).
4. New config keys under `FailedMessages` in every appsettings file and the production template.
5. Audit log is kept forever (small).
6. Out of scope, reported separately: FE route `/admin/webhook-deliveries` has no permission guard.

## Decision log
| # | Decision | Alternatives | Why |
|---|---|---|---|
| D1 | Collect failed messages into a DB table; the screen reads the DB | Read `_error` queues live | Peeking a queue reorders/redelivers, can't filter/page/select; two brokers behind a LB |
| D2 | Retry = raw bytes back to the **original queue** via default exchange | Publish to the message-type exchange | Publish would re-deliver to every subscriber, not just the one that failed |
| D3 | The **owning node** performs the retry (API sets `RetryRequested`) | API connects to the other node's broker | No cross-node AMQP/credentials |
| D4 | Ordered (partitioned) endpoints: **warn, allow** | Block retry | User decision; stale-overwrite risk surfaced in drawer + confirm |
| D5 | Permissions `FAILED_MESSAGE_VIEW` (read) / `FAILED_MESSAGE_MANAGE` (retry/discard/resend) — VIEW / MANAGE only | One permission | Read vs act split; no third level |
| D6 | Retention 90 days for Retried/Discarded. Pending is never purged, except `Kind = Skipped` rows still Pending after `FailedMessages:SkippedPendingRetentionDays` (default 30, counted from the later of `CollectedAt` and the last admin `ActionAt`) | 30 days; forever | User decision; a Skipped message has no consumer so Retry cannot help, and a broken binding fed `_skipped` ~540 rows in 5 days on dev. Error rows are never purged while Pending |
| D7 | Outbox `Failed` retention 7 → 90 days, counted from the row's last claim (`ProcessingStartedAt`, else `OccurredAt`) so a resent row that fails again is not purged for being old (`Processed` stays 7) | Never delete; keep 7 | User decision; aligns with the screen |
| D8 | Management API account: add tag `management` to the **existing** app user; **bank runs it** | Separate read-only user | User decision (option ก). Collector degrades gracefully until done |
| D9 | Everything on screen comes from DB; queue health is a per-node latest-snapshot row | Live calls | LB + per-node brokers + localhost-only 15672 |
| D10 | Collector = per-node BackgroundService pulling `_error`/`_skipped` with RabbitMQ.Client (approach ก) | Intercept MassTransit error pipe | No change to the carefully ordered endpoint pipeline; nothing lost if DB is down |
| D11 | Dedup key `(MessageId, SourceQueue, Kind, FaultedAt)` | `MessageId` alone | One event fails at several consumers; a retried message keeps its MessageId |
| D12 | Reference column: first known key Appraisal → Request → Quotation → Meeting → Document; never guess from `CorrelationId` | Always appraisal | Many events have no appraisal; CorrelationId means RequestId or QuotationRequestId |
| D13 | `conversationId` not used for grouping; siblings found by `MessageId` | conversationId chains | Outbox publishes outside a consume context, so every publish starts a new conversation |
| D14 | Outbox: never flip `Processed` → `Pending`; resend only `Failed` | Allow any | Publish fans out to all consumers and gets a new MessageId, so InboxGuard can't dedupe |
| D15 | Outbox fixes: PR-A = items 1,2,5,6; PR-B = items 3 (`ProcessingStartedAt`, 6 migrations) + 4 (MessageId = outbox Id) together with the screen | — | User decision |

## Design

### 1. Data (schema `integration`)
**`FailedMessages`** — one row per failure: `Id` (v7), `Node`, `SourceQueue`, `Kind` (Error/Skipped), `MessageId`,
`ConversationId`, `MessageType`, `ConsumerType`, `ExceptionType`, `ExceptionMessage`, `StackTrace`, `RetryCount`,
`FaultedAt`, `CollectedAt`, `RefType`, `RefId`, `RefNumber`, `Body` (varbinary, raw), `ContentType`, `Headers`
(JSON), `Status` (Pending/RetryRequested/Retried/Discarded), `ActionBy` (user code), `ActionAt`, `ActionReason`,
`RowVersion`. Unique `(MessageId, SourceQueue, Kind, FaultedAt)` (filtered on MessageId not null). Indexes
`(Status, FaultedAt)`, `(Node, Status)`.

**`BrokerSnapshots`** — PK `Node`; `CollectedAt`, `ManagementStatus` (Ok/Unauthorized/Unreachable), `QueuesJson`
(per queue: name, ready, unacked, consumers, publish/deliver rate, 30 samples), `LastError`. Overwritten every round.

**`FailedMessageAuditLogs`** — `Action` (Retry/Discard/OutboxResend/RetryFailed), `Source` (Consumer/Outbox),
`TargetId`, `OutboxModule`, `ActorCode`, `IpAddress`, `Reason`, `At`. Kept forever.

**Existing tables** — `IntegrationEventOutbox` in six schemas gains `ProcessingStartedAt` (PR-B).
`OutboxCleanupJob`: `Processed` 7 days, `Failed` 90 days.

**Retention** — Hangfire `failed-messages-cleanup` daily: delete Retried/Discarded with `ActionAt` older than 90 days,
and `Kind = Skipped AND Status = Pending` rows with `COALESCE(ActionAt, CollectedAt)` older than `FailedMessages:SkippedPendingRetentionDays`
(default 30, must be 1 to 3650, validated at startup even when `FailedMessages:Enabled` is false because the job runs without the
collector). The Skipped rule keys on `COALESCE(ActionAt, CollectedAt)`, not `FaultedAt`: for a Skipped row `FaultedAt` is the AMQP/envelope
send time, which can be long before the row was collected, and `ActionAt` is set when a retry came back unroutable and the
row returned to Pending, so such a row gets a fresh window. Before deleting, the job logs one line per
`SourceQueue`/`MessageType` group with the count and the oldest/newest `CollectedAt` (one grouped SELECT, same predicate). Error rows are never purged by it, whatever their status; a
Skipped row that was Discarded/Retried stays on the 90-day `ActionAt` rule.

### 2. API (Integration module, Carter + MediatR; 1-based `pageNumber`/`pageSize`)
Read — policy for `FAILED_MESSAGE_VIEW`:
- `GET /admin/failed-messages/summary` → `pendingCount`, `oldestPendingAt`, `last24h {total, retried, discarded}`,
  `topGroups[] {queue, exceptionType, count}`, `outboxFailedCount`, `outboxStuckCount`,
  `nodes[] {node, collectedAt, managementStatus, queues[]}`.
- `GET /admin/failed-messages?status&queue&node&exceptionType&search&pageNumber&pageSize` → paginated rows incl.
  server-computed `isOrderedQueue`, `isNonTransient`, `siblingCount`.
- `GET /admin/failed-messages/{id}` → full headers (incl. `MT-Fault-*`), raw body, raw `stackTrace`, `siblings[]`,
  `history[]`.
- `GET /admin/outbox-messages?status=Failed|Stuck|Resent|All&module&search&pageNumber&pageSize` → `UNION ALL` over the
  six schemas; rows incl. `newerSentCount`, `typeResolvable`.
- `GET /admin/outbox-messages/{module}/{id}` → raw payload.

Write — policy for `FAILED_MESSAGE_MANAGE`:
- `POST /admin/failed-messages/retry` `{ids[], reason?}` → `{accepted[], skipped[{id, by, at}]}`; sets RetryRequested.
- `POST /admin/failed-messages/discard` `{ids[], reason}` (reason required) → same shape.
- `POST /admin/outbox-messages/resend` `{items[{module, id}], reason?}` → `{accepted[], skipped[]}`;
  `UPDATE … SET Status='Pending', RetryCount=0, Error=NULL WHERE Id=@id AND Status='Failed'`.

Rules: `module` checked against a six-item whitelist before it becomes a schema name; max 200 ids per call; search
terms are LIKE-escaped (`LikePattern.Escape`) and always bound as parameters.

### 3. Collector (BackgroundService on every node, no lease)
Config `FailedMessages { Enabled, Interval=15s, BatchPerQueue=100, SkippedPendingRetentionDays=30 }` (the last is read by the cleanup job, not the collector);
management URL = `RabbitMQ:ManagementUrl` (default `http://localhost:15672`), credentials = existing `RabbitMQ:Username/Password`; node = `Environment.MachineName`. Each round, each step in its
own try/catch:
1. **Discover** queues ending `_error`/`_skipped` with messages via `GET /api/queues`; if the Management API fails,
   fall back to the app's MassTransit endpoint names + suffix, checked with `QueueDeclarePassive` (verify the MT 8.4.1
   API for listing endpoints at runtime).
2. **Collect** with `BasicGet` up to `BatchPerQueue`: parse `MT-Fault-*` + envelope, resolve reference, INSERT, then
   `BasicAck`. Unique violation → ack (already stored). Other error → `BasicNack(requeue)` and move on.
3. **Retry** rows `RetryRequested` for this node: `BasicPublish` to default exchange, routing key = `SourceQueue`,
   raw body + original headers minus `MT-Fault-*`/`MT-Reason`, publisher confirms, then `Retried`. Unroutable / broker
   nack / the broker closing the channel with a soft error (406, 403, ...) / unbuildable publish → back to `Pending` +
   `RetryFailed` audit (a closed channel also ends the round); unknown outcome (incl. the 30 s confirm
   timeout, which also ends the round) → stay RetryRequested with the claim kept.
4. **Snapshot** `GET /api/queues?lengths_age=1800&lengths_incr=60&columns=…` (`columns` = only the fields the parser reads,
   `FailedMessageCollectorService.ManagementQueueColumns`; gzip accepted) → upsert `BrokerSnapshots`; always update
   `CollectedAt` and `ManagementStatus` (401 → Unauthorized, connect error → Unreachable).

Tests: unit (header/envelope parsing, reference resolution); integration against docker RabbitMQ
(fault → collected → retry → back in the source queue).

### 4. Frontend (`src/features/failedMessages/`)
Route `/admin/failed-messages` wrapped in `RoleProtectedRoute requiredPermission="FAILED_MESSAGE_VIEW"`; mutating
controls hidden without `FAILED_MESSAGE_MANAGE`. Menu `main.failed-messages` under `main.system` (DB-driven).
Components: `FailedMessagesPage` → `SummaryStrip`, `QueueHealthPanel` (hand-drawn SVG sparkline, "data as of", stale
> 2 min = amber, managementStatus message), `TopGroupChips`, `SourceSwitch`, `ConsumerFailuresTable`,
`OutboxFailuresTable`, bulk bar, `FailedMessageDrawer` / `OutboxMessageDrawer` (`SlideOverPanel`), confirm dialogs.
React Query only (`failedMessageKeys`), `refetchInterval` 15 s while the tab is visible. daisyUI + Tailwind `dark:`
(the app's own theming, not the mock's tokens). i18n namespace `failedMessages` (en/th/zh).
Checks: `tsc -b --force`, lint, `vite build`, vitest for helpers, measured against the mock at 1360/400 in both themes.

## Deployment notes (hand to the bank)

**PR-A and PR-B ship in the SAME release.** PR-A (`fix/outbox/stuck-processing`) and PR-B (this screen)
both touch `IntegrationEventDeliveryService`; PR-B's copy is PR-A's canonical shape plus this screen's
extras (`ResetOrphanedProcessingAsync`, `ProcessingStartedAt`, the MessageId publish callback,
`IntegrationEventNamespace.TryResolve`) — see `todo-outbox-stuck-processing-fix.md` "Rebase notes".
Deploying one without the other reintroduces the bugs the other one fixed.

On **each** app server's broker:
```
rabbitmq-plugins list | grep management          # rabbitmq_management must be enabled
rabbitmqctl list_users                            # see the app user's current tags
rabbitmqctl set_user_tags <app-user> management  # REPLACES existing tags — include any the user already has
```
New config key `FailedMessages:SkippedPendingRetentionDays` (default 30) is in `appsettings.json`, `appsettings.Development.json`
and the production template — no schema change. Add it to the server's single `appsettings` file (the template is
self-contained); leaving it out falls back to 30. A value outside 1 to 3650 stops the app at startup. The first nightly run after
deploy deletes every Skipped/Pending row older than the setting, in batches of 1000.

Then: run `dotnet run --project Database/Database.csproj migrate` (or the DBA bundle) and restart every instance
(menu cache). Until the tag is set the screen shows "Management API not authorised" in queue health; collection and
retry keep working over AMQP.

**`RabbitMQ:ManagementUrl` must not be plain http to a remote host.** The collector re-sends the app's AMQP
username/password as HTTP Basic auth to the Management API every round (15s), so `FailedMessagesOptions.Validate()`
fails host startup for an `http://` URL whose host is not loopback (`localhost`, `127.x.x.x`, `[::1]`). Allowed:
`https://` to any host, and `http://` to loopback — the per-node design and the prod template default
`http://localhost:15672`. If the Management API must be reached across hosts, front it with TLS (`https://...:15671`).

**Node decommissioning.** A `RetryRequested` row is only ever picked up by its owning node's collector
(`FailedMessage.Node`, design D3) — if that node is permanently removed from the fleet, the row is stuck
forever with nothing to republish it. There is no automatic transfer to another node (retry is
node-local by design, same reason as D3: no cross-node AMQP/credentials). The way out is manual: an
operator discards the row from the screen — `POST /admin/failed-messages/discard` accepts
`RetryRequested` rows for exactly this reason (api-contract.md) — and, if the underlying failure still
needs redoing, lets it reach the fault queue again through whatever produces it, or resends the
originating outbox message if there is one.

**Collector scope — renamed or removed consumer endpoints.** The collector only discovers and drains the
`_error`/`_skipped` queues of THIS application's own receive endpoints (see api-contract.md). If a consumer
endpoint is renamed or removed in a deploy, its old `_error`/`_skipped` queue is no longer one of those endpoints, so it
is no longer collected: messages already sitting in it stay there and never reach the screen. After such a deploy an
operator must drain or move that old queue with RabbitMQ tooling (management UI, `rabbitmqadmin`, or a shovel).

During a rolling deploy, a node still on old code marks outbox rows Processing without stamping
`ProcessingStartedAt`, and the new lease holder treats a NULL `ProcessingStartedAt` as orphaned and resets it —
so a message can be published twice within that one deploy window. Accepted: consumers behind `InboxGuard`
dedupe.

**`RABBITMQ_HOST` scheme.** Prefer an `amqp://` or `amqps://` URI. Verified directly against both
packages (MassTransit.RabbitMQ 8.4.1, RabbitMQ.Client 7.1.2): MassTransit's own `Host(Uri)` — see
`Bootstrapper/Api/Program.cs` — accepts `amqp(s)://` and `rabbitmq(s)://` interchangeably (its
`RabbitMqHostAddress` parses host/port/vhost identically either way), but the collector's direct
`RabbitMQ.Client.ConnectionFactory.Uri` throws `ArgumentException: Wrong scheme in AMQP URI` on
`rabbitmq(s)://`. Rather than rely on the bank always choosing the scheme RabbitMQ.Client insists on,
the collector converts it itself (`FailedMessageCollectorService.ToRabbitMqClientScheme` —
`rabbitmq→amqp`, `rabbitmqs→amqps`) before opening its own connection, so either scheme in
`RABBITMQ_HOST` works for both the bus and the collector.

## Implementation plan
- **PR-A (BE, outbox fix):** items 1, 2, 5, 6 of `todo-outbox-stuck-processing-fix.md`.
- **PR-B (BE):** outbox items 3 + 4 + D7; Integration tables + migration; collector; API; permissions/menu DbUp
  script + Development seeder; retention job; config in all appsettings + production template; tests.
- **PR-C (FE, new Orca worktree):** the screen, against the PR-B contract.
- Review: backend and frontend reviewer passes; no CRITICAL findings left. Nothing is committed without the user's go.

## Review
Implemented 2026-09-27 by a coordinated team (backend via ddd-expert, frontend via react-expert, reviewer), lead as
coordinator + QA. Nothing committed; no migration applied. QA checklist: `qa-checklist.md` (38/39 ✅, C10 ⚠️ app-wide
dark-mode gap). Final lead-run results: PR-B build 0 errors, Integration.Tests 79/79, Shared.Tests 36/36, FailedMessages
integration (Testcontainers SQL Server + RabbitMQ) 28/28; PR-A build 0 errors, Shared.Tests 33/33; PR-C tsc 82 = baseline
(0 in feature), vitest 41/41, vite build ok, 229 i18n keys × 3.

Issues found and fixed during QA/review (all closed): SourceQueue stored with suffix; discovery fallback limited to 7
queues (→ ReceiveEndpointDiscoveryObserver); resend audit not transactional; orphan reset at 60 s could double-publish (→ fixed 5 min);
retry republish non-persistent; poison inserts blocking a queue; duplicate rows for skipped/unparseable; a re-skipped retry
silently dropped; shutdown burning retries / cancelling the post-publish save; FE type names overflowing, "LIVE" badge,
stale-hours bug, stale drawers, page clamp, ref links without numbers, 500-char reason limit, number-changing
JSON reformat.

Accepted/known: newerSentCount N+1; the shared integration test host runs no outbox delivery services (`WebApplicationFactoryHelper` removes them, so tests that seed outbox rows are not raced);
one-deploy rolling window; Request integration tests 3/4 fail on main already (pre-existing); webhook admin list echoes a
0-based pageNumber and its FE route is unguarded (pre-existing, out of scope). PR-A↔PR-B rebase steps are in
`todo-outbox-stuck-processing-fix.md`.

## Changelog
- 2026-10-05: Skipped rows left Pending are now purged after `FailedMessages:SkippedPendingRetentionDays` (default 30), keyed on `COALESCE(ActionAt, CollectedAt)` (D6).
- 2026-10-03: PII masking/reveal removed by user decision — menu permission implies the right to see the data.
  Removed: body/header masking, the `reveal-body` / `reveal-payload` endpoints and their `RevealBody` audit action, the
  exception-message scrubber and the `ExceptionMessageScrubbed` column (edited out of the unreleased `AddFailedMessages`
  migration), and the `MaskedKeys` / `MaskedExactKeys` config keys. Detail endpoints return the raw body/payload, the
  stack trace and the full header set directly. Search runs on the real exception message.
