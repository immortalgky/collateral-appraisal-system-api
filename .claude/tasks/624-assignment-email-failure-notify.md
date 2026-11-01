# #624 Assignment email: failure → Failed status and in-app notification

Branch `immortalgky/implement-624-issue` (worktree `candlefish`), off the local integration branch
`feat/617-admin-company-email` @ e13d7f58 (#620 merged). Parent spec #617.

## Flow
1. The send keeps failing → MassTransit publishes `Fault<AssignmentEmailRequestedIntegrationEvent>` after retries.
   A permanent SMTP rejection (`SmtpCommandException` 5xx, e.g. size) is ignored by the retry policy, so it faults
   at once.
2. Notification `AssignmentEmailRequestFaultHandler` (InboxGuard) → `AssignmentEmailFailureNotifier`:
   - publishes `AssignmentEmailFailedIntegrationEvent {AppraisalId, EmailId, Reason}`
   - persisted notification `ส่งอีเมลแจ้งงาน {AppNo} ถึง {company} ไม่สำเร็จ: {reason}`, ActionUrl
     `/appraisals/{id}/administration`, type `AssignmentEmailFailed`, to the assigner ∪ every IntAdmin (deduped)
3. The no-To branch of `AssignmentEmailRequestedHandler` calls the same notifier (no send).
4. Appraisal `AssignmentEmailFailedIntegrationEventHandler`: Queued → Failed + FailureReason.

## Todo
- [x] Events: AppraisalNumber + CompanyName on the request; new Failed event
- [x] Notification: notifier + Fault consumer + no-To branch; NotificationType.AssignmentEmailFailed
- [x] Retry: permanent-SMTP predicate in Notification, wired in Program.cs IgnoreNonTransient
- [x] Appraisal: MarkFailed, FailureReason column + migration, Failed consumer, dispatcher fills the new fields
- [x] Tests (Notification seam: fault → event + notifications, non-retry predicate; Appraisal: Failed consumer)
- [x] Matt code-review: fixed inbox key = FaultedMessageId, reason only from MailKit/MimeKit, notifier in its
      own file, FailureReason nvarchar(200), blank App No. → "-"
- [x] /code-review r1 (11 findings): fixed redrive Failed → Sent, socket/timeout reasons shown, assigner checked
      against the user lookup (no "System" row, account spelling), MarkFailed caps the column, file renamed
- [x] /code-review r2 (6 findings): socket failure = fixed Thai text (the .NET message holds the relay IP:port),
      TimeoutException no longer passes through, null Reason guarded
- [x] /code-review r3 (5 findings, all accepted): r2's premise was wrong (EmailAttachmentAssembler skips attachment
      errors), so TimeoutException = unreachable; exact socket match (not WebSocketException); blank reason →
      "ไม่ทราบสาเหตุ"
- [x] /code-review r4 (3 findings): the unreachable check walks InnerException (STARTTLS wraps it in IOException)
- [ ] /code-review r5+ (fix diff only)
- [ ] Gates (test-automator)
- [ ] User test → merge `--no-ff` into the integration branch, close #624 by hand

## Settled decisions (user, 2026-11-01)
- Reason = the exception / SMTP server message, capped at 200 chars. No-To reason is fixed Thai text.
- New NotificationType `AssignmentEmailFailed`; app#539 must add it to `notificationTypeConfig` (the bell has no
  fallback for an unknown type).
- FailureReason gets its own migration now; fold all #617 migrations into one at the integration PR.
- Failure time = the row's UpdatedAt (no extra column).
- Reason shown to admins (user, from Matt review; refined in r1–r3):
  - MailKit/MimeKit exception → its message (the gateway's reply)
  - SocketException (+ExtendedSocketException) / TimeoutException → "เชื่อมต่อเซิร์ฟเวอร์อีเมลไม่ได้" (the .NET
    message names the relay's IP:port)
  - anything else → "ระบบส่งอีเมลขัดข้อง"; blank → "ไม่ทราบสาเหตุ"
  - the full exception stays in logs, EmailSendLogs and Failed Messages

## Declined in Matt review (do not re-raise)
- `MarkFailed` returns quietly on a non-Queued row (docs/DDD.md throws): same as `MarkSent`, idempotent on purpose.
- Bare `"IntAdmin"` string: the repo has no role constant; siblings do the same.
- Failed consumer duplicates the Sent consumer (~10 lines each): not worth folding.
- `appraisalNumber` passed separately to `QueueExtoAsync`: fine with one caller.
- One save per notification, so a DB failure midway re-notifies earlier users on retry (`ponytail:` comment).
- The no-To branch: if the notifier itself fails until retries run out, the fault handler notifies again with a
  generic reason. Edge case, accepted.
- The `r.Ignore` wiring in Program.cs has no automated test (the predicate does); check by hand.

## Declined in /code-review r1 (do not re-raise)
- The 5xx Ignore lives in the shared IgnoreNonTransient, so sibling email consumers also stop retrying a 5xx. A 5xx
  is the gateway's final answer (RFC 5321); retrying ends in the same _error queue, only later.
- No-To branch double notice when the notifier itself throws: declined in Matt review.
- No unit test for the FaultedMessageId inbox key: InboxGuard dedupes through a real SQL PK; InMemory can't show it.
- The FE bell lacks the new type until app#539: tracked, both repos ship together from the integration branch.
- Mail:Enabled=false marks the row Sent: #620 decision.

## Declined in /code-review r2 (do not re-raise)
- A deactivated assigner still gets the persisted notice: harmless; the user lookup has no IsActive filter.
- The failure notice stays in the bell after a redrive delivered the email: redrive is a deliberate ops action.
- The 200 cap exists in both modules: each module owns its own limit; cleanup, not correctness.

## Declined in /code-review r4 (do not re-raise)
- The 200 cut can split a surrogate pair / leave a trailing space: gateway replies are ASCII; low.
