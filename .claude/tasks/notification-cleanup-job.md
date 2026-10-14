# Notification cleanup job

Goal: stop `notification.UserNotifications`, `notification.EmailSendLogs` and the inbox tables of the
inbox-only modules (Notification, Common) from growing forever.

## Decisions (user, 2026-10-12)
- UserNotifications: delete read rows older than 30 days, every row older than 90 days.
- EmailSendLogs: delete rows older than 90 days.
- InboxMessage: reuse `OutboxCleanupJob<T>` (Processed > 7 days, stale Processing) — option **A**: guard
  the outbox half of the job so it is skipped when the DbContext has no outbox entity. Rejected option B
  (add empty outbox tables): two empty tables per module + a silent trap if someone later injects
  `IIntegrationEventOutbox` there (no delivery service, not in the Failed Messages whitelist).
- Common gets `inbox-cleanup-common` too (one line, its JobSchedules table already exists).

## Todo
- [x] `OutboxCleanupJob`: skip outbox reset/deletes when the model has no `IntegrationEventOutboxMessage`
- [x] `NotificationDbContext.AddJobSchedules()` + migration (files only — user runs `migrate`)
- [x] `Notification/Scheduling/NotificationCleanupJob.cs` (Dapper, batched like `UserSessionCleanupJob`)
- [x] `Notification/Scheduling/NotificationRecurringJobs.cs`: `notification-cleanup` 03:30, `inbox-cleanup-notification` 02:00
- [x] `UseNotificationModule` → `UseModuleRecurringJobs<NotificationDbContext>`
- [x] `CommonRecurringJobs`: `inbox-cleanup-common` 02:00
- [x] Integration tests: retention boundaries + inbox-only `OutboxCleanupJob` run
- [x] Gates: build, unit tests, the new + existing outbox cleanup integration tests

## Review
- `OutboxCleanupJob`: outbox half wrapped in a `FindEntityType(IntegrationEventOutboxMessage)` guard; inbox half
  always runs. Outbox modules unchanged (`OutboxOrphanResetAndCleanupTests` 7/7).
- `NotificationCleanupJob` (Dapper, batch 4000): `CreatedAt < @ReadCutoff AND (IsRead = 1 OR CreatedAt < @AllCutoff)`
  + `EmailSendLogs.CreatedAt < @AllCutoff`. Both tables write `CreatedAt` from `ApplicationNow` — same clock as the cutoffs.
- Jobs: `notification-cleanup` 03:30, `inbox-cleanup-notification` 02:00, `inbox-cleanup-common` 02:00.
- Migration `20261012123303_AddNotificationJobSchedules`: `notification.JobSchedules` + `UX_JobSchedules_JobId`, and `IX_UserNotifications_CreatedAt` rebuilt with INCLUDE (IsRead). Not applied.
- Gates: solution build 0 errors · integration 11/11 · Shared 101, Common 61, Appraisal 700 pass ·
  Notification.Tests 26/31 — the 5 `NotificationEmailTemplatesTests` failures also fail on unmodified HEAD `d0402918` (pre-existing).
- Deploy: run `migrate`; include the migration in the DBA bundle. First prod run may delete a large backlog (batched).

## Code review log
### Round 1 (8 findings)
Accepted:
- Purge rescans unread 30–90d rows with a key lookup per batch → `IX_UserNotifications_CreatedAt` INCLUDE (IsRead), folded into the same migration.
- Dead `INotificationRepository.DeleteOldNotificationsAsync` (different retention rule, no callers) → deleted.
- `[OUTBOX-CLEANUP]` completion log says "outbox+inbox" for inbox-only contexts → reworded.
- Test doc comment claimed it touches no other rows → made truthful (no other integration test seeds these tables).

Declined (deliberate — do not re-raise):
- "Inbox rows now deleted after 7 days → replay of an 8-day-old message re-sends email": same 7-day rule every outbox
  module already has; the publisher's Processed outbox rows are also gone after 7 days, so the monitor cannot resend
  older ones, and `_error`-queue messages never reached Processed in the inbox anyway.
- "Split out a separate InboxCleanupJob<T>": user chose option A (guard inside OutboxCleanupJob) on 2026-10-12.
- "No CommandTimeout / cancellation token on Dapper calls": with the index, each batch is an index seek;
  matches UserSessionCleanupJob / LogsCleanupJob.
- "Fourth copy of the batched-delete loop → shared helper": refactors three unrelated jobs; out of scope.

### Round 2 (6 findings)
Accepted:
- BatchSize 5000 = lock-escalation threshold on the hot UserNotifications table → 4000.
- `JobScheduleEndpoints` doc "nine module schemas" → ten.
- `IntegrationRecurringJobs` comment "five outbox-cleanup jobs at 02:00" → reworded without a count.

Declined (deliberate — do not re-raise):
- "Stale-Processing inbox DELETE (5 min) can drop the claim of a still-running email handler": same
  `OrphanedProcessingThreshold` every module already runs under; SMTP send times out long before 5 minutes, and the
  harm needs a >5-min handler AND a later redelivery. A shared-policy question, not this PR's.
- "DropIndex + CreateIndex instead of CREATE INDEX ... WITH (DROP_EXISTING = ON)": EF runs the migration in one
  transaction, so no session sees an index-less window; DBA runs the bundle off-hours. Not hand-editing EF output.
- "Tests build the job via reflection / no test of the job registrations": same pattern as SessionCleanupTests
  (internal job class); the integration host boots through UseModuleRecurringJobs, so a broken registration fails startup.

### Round 3 (5 findings) — loop stopped here (converged on flip-flops/edge races)
Accepted: none.
Open question for the user: "read 30 days" counts from CreatedAt (no ReadAt column), so a 35-day-old notification
read today is purged tonight.

Declined (deliberate — do not re-raise):
- "INCLUDE(IsRead) costs a write on mark-read + deadlock risk": reverses round 1's accepted fix. The cleanup already
  touches IX_UserNotifications_UserId_IsRead on every delete, so lock-order exposure exists with or without INCLUDE.
- "MarkNotificationAsRead races the 03:30 purge → DbUpdateConcurrencyException": millisecond window, only on a
  30+-day-old row, at 03:30.
- "Old-binary nodes can't load the new job types after rollback": true of every new recurring job (known Hangfire
  shared-storage gotcha), not specific to this change.
- "Two more jobs at 02:00": inbox tables are small and the deletes are batched; not worth a schedule split.

## E2E on local dev (2026-10-13)
- `migrate` from this worktree applied `20261012123303_AddNotificationJobSchedules`; re-ran only `vw_CarryForwardDocuments`
  (identical in every tree). User's uncommitted `vw_RegulatoryExport`/`sp_RegulatoryExport` in the DB were untouched.
- API from this worktree on 7111; the 3 jobs triggered through the real scheduler (admin PUT cron `* * * * *`, then
  restored to `30 3 * * *` / `0 2 * * *`). All Succeeded, no overlap, no SqlException/timeout.
- First-run backlog on dev: notification-cleanup 213,369 notifications + 22 email logs in ~10.7 s;
  inbox-cleanup-notification 214,473 rows in ~16.4 s; inbox-cleanup-common 422,513 rows (105,451 stuck Processing) in ~23.8 s.
- 10/10 marker rows (read/unread 29/31/91 d, email 89/91 d, inbox 1/8 d in both schemas) behaved as expected; markers removed.
- Side finding (not this PR): common.InboxMessage had 105k rows stuck in Processing — consumers there fail/crash mid-handle.
