# #620 Assignment email: send automatically when an EXTO case is assigned

Branch `immortalgky/implement-issue-620` (worktree `clam`), off the local integration branch
`feat/617-admin-company-email` @ 3269b5be (#627 merged). Parent spec #617. Updated by the comment on #620: no Draft,
the trigger takes the Pending row Assign wrote; NotSent → nothing; no row → template.

## Flow
1. `InternalAssignedIntegrationEventHandler` (Appraisal), method `Offline`, first landing only (the row was Pending):
   `AssignmentEmailDispatcher.QueueAsync` in the same save as the Assign.
   - newest Pending/NotSent row of this assignment: NotSent → nothing; Pending → fill `{{company}}` /
     `{{responsible}}` (= keyer) in the stored body and move it to **Queued**; none → build a row from the template
     (then Queued).
   - outbox `AssignmentEmailRequestedIntegrationEvent` {AppraisalId, EmailId, To (company + extra), Cc (literal
     addresses only; role tokens dropped in this slice), Subject, HtmlBody, AttachmentRefs ("document", id), AssignedBy}.
   - wrapped in try/catch: an email problem is logged and the assignment still saves.
2. `AssignmentEmailRequestedHandler` (Notification, InboxGuard): attachments via `EmailAttachmentAssembler`,
   `IEmailSender.SendAsync` with Source `AssignmentEmail`, ReferenceId = email id; then publishes
   `AssignmentEmailSentIntegrationEvent`. No To → log, no send (row stays Queued; failure = #624).
3. `AssignmentEmailSentIntegrationEventHandler` (Appraisal): Queued → Sent (idempotent).

Preview's template build moves into a shared composer so the trigger can use it without the admin permission check.

## Todo
- [x] Shared events (requested, sent)
- [x] Domain: `AssignmentEmail.FillPlaceholders`, `MarkSent`
- [x] Composer extracted from the preview handler
- [x] Dispatcher + hook in InternalAssigned handler (+ repository query for the row)
- [x] Sent consumer (Appraisal)
- [x] Notification handler
- [x] Tests: dispatch seam, handler guard (re-fire, failure isolation), Notification handler seam
- [x] Matt code-review (Standards + Spec): fixed Queued status, To required, redundant appraisalId param,
      alias constants, Sent consumer via unit of work, inline Source, composer in its own file
- [ ] built-in /code-review loop, test-automator gates
- [ ] User test → merge `--no-ff` into the integration branch, close #620 by hand

## Settled decisions
- Notification has no outbox. If publishing Sent fails after the gateway accepted the mail, it is logged and not
  retried: the company never gets a duplicate, and the row stays Queued (user, 2026-11-01). The publish is not
  cancellable for the same reason.
- The body goes out in the branded shell (`IEmailTemplateRenderer.AssignmentEmail` = the quotation `Wrap`) (user, 2026-11-01).
- Sends only on the handover itself (the assignment row was Pending), in the same save as the Assign. That covers
  redelivery and route-back re-fires without a new column.
- Status **Queued** (user, 2026-11-01, from review): the dispatch moves the row Pending → Queued, and the
  confirmation moves it Queued → Sent. A row already handed over (unconfirmed, or no To) is therefore never picked
  for a later handover. No migration (string column). #624 marks Failed from Queued.
- Row pick: the newest Pending/NotSent row of the assignment (CreatedAt, then Id).
  - Not a timestamp boundary: `AssignedAt` is `DateTime.Now`, while `CreatedAt` is `ApplicationNow`.
  - Known corners, accepted:
    - a Pending row left by a failed #627 cleanup is sent if a later assign carries no choice
    - an earlier handover's NotSent row silences a later no-choice assign
  - Only legacy / non-FE callers send no choice for EXTO.
- The filled body is written back to the row, so it holds what was sent.
- Reads happen before anything is added or published. Any failure is logged and the assignment saves without the email.
- No To (the company has no email and the admin added no extra To): logged and not sent, even when Cc has
  addresses. A mail to bank staff only is not the assignment email. The row stays Queued; failure reporting is #624.
- No From on the event: IEmailSender always uses the configured sender (bank gateway rule).
- A placeholder whose value is unknown (company lookup null) stays as is. With no company there is also no To, so
  it is never sent.
- `Mail:Enabled=false` returns normally from IEmailSender, so in dev the row becomes Sent (SkippedDisabled in EmailSendLogs).
- No migration: the Status column already holds Sent.

## Review
- (after implementation)
