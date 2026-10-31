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
- [ ] Events: AppraisalNumber + CompanyName on the request; new Failed event
- [ ] Notification: notifier + Fault consumer + no-To branch; NotificationType.AssignmentEmailFailed
- [ ] Retry: permanent-SMTP predicate in Notification, wired in Program.cs IgnoreNonTransient
- [ ] Appraisal: MarkFailed, FailureReason column + migration, Failed consumer, dispatcher fills the new fields
- [ ] Tests (Notification seam: fault → event + notifications, non-retry predicate; Appraisal: Failed consumer)
- [ ] Matt code-review, then /code-review loop, then gates
- [ ] User test → merge `--no-ff` into the integration branch, close #624 by hand

## Settled decisions (user, 2026-11-01)
- Reason = the exception / SMTP server message, capped at 200 chars. No-To reason is fixed Thai text.
- New NotificationType `AssignmentEmailFailed`; app#539 must add it to `notificationTypeConfig` (the bell has no
  fallback for an unknown type).
- FailureReason gets its own migration now; fold all #617 migrations into one at the integration PR.
- Failure time = the row's UpdatedAt (no extra column).
