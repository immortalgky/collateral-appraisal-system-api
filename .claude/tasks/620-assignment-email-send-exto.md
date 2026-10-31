# #620 Assignment email: send automatically when an EXTO case is assigned

Branch `immortalgky/implement-issue-620` (worktree `clam`), off the local integration branch
`feat/617-admin-company-email` @ 3269b5be (#627 merged). Parent spec #617. Updated by the comment on #620: no Draft,
the trigger takes the Pending row Assign wrote; NotSent → nothing; no row → template.

## Flow
1. `InternalAssignedIntegrationEventHandler` (Appraisal), method `Offline`, first landing only (the row was Pending):
   `AssignmentEmailDispatcher.QueueExtoAsync` in the same save as the Assign (handover, email row and outbox atomic).
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
- [x] /code-review r1 (8 findings): fixed multi-address To, queue after the save (stale template), Sent consumer test
- [x] /code-review r2 (9 findings, from r1's two saves): back to one save, composer gets the EXTO handover, Cc minus To
- [x] /code-review r3 (7 findings): explicit compose/queue API for the EXTO handover, a test pinning publish-before-save
- [ ] /code-review r4 (fix diff only), test-automator gates
- [ ] User test → merge `--no-ff` into the integration branch, close #620 by hand

## Settled decisions
- Notification has no outbox. If publishing Sent fails after the gateway accepted the mail, it is logged and not
  retried: the company never gets a duplicate, and the row stays Queued (user, 2026-11-01). The publish is not
  cancellable for the same reason.
- The body goes out in the branded shell (`IEmailTemplateRenderer.AssignmentEmail` = the quotation `Wrap`) (user, 2026-11-01).
- Sends only on the handover itself (the assignment row was Pending). That covers redelivery and route-back re-fires
  without a new column.
- One save, atomic with the handover. r1 moved the email into a second save so the template would read fresh data, but
  r2 showed the cost: a cancellation or a failed second save loses the email, because the retry no longer sees a
  Pending row. Reverted. The stale-template problem is fixed at the source instead. At an EXTO handover the
  composer gets the keyer, and then treats the case as off-system: keyer = ผู้รับผิดชอบ, EXTO Cc roles, and the fee
  priced like AssignAppraisal (`external-offline`, `manual`, company). The stored row may still show the decision
  before a route-back.
- The Notification handler splits every To/Cc entry with `EmailRecipients.Parse`, because the company master can hold
  "a; b" (/code-review r1). A Cc address already in To is dropped (r2).
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
- Declined in /code-review r1:
  - An attachment that cannot be resolved is skipped and the email still goes out, the same as the quotation email.
    The documents were checked live at assign.
  - Placeholders the admin edited in the rich text editor are taken as the admin's intent.
  - The company is looked up twice on the template path (nit).
- The API is explicit (r3): `AssignmentEmailComposer.ComposeAsync` (preview, stored row) vs
  `ComposeForExtoHandoverAsync(appraisalId, keyerName, ct)`. `AssignmentEmailDispatcher.QueueExtoAsync` is EXTO-only;
  #622 adds EXT's own responsible person, Cc roles and fee question.
- Declined in /code-review r3:
  - Partial failure inside the dispatcher committing a Queued row with no outbox message. The only step after the
    mutations is `outbox.Publish`, which serializes a record of strings and Guids into an in-memory list and does
    not throw in practice.
  - A shared helper for the EXTO fee question (`external-offline` / `manual` / company), duplicated from
    AssignAppraisal. Cleanup, not a bug.
  - The fee is a re-simulation, not the tracked materialised fee. The preview shares its resolution with
    AssignmentFeeService by design, and it is the figure AssignAppraisal checked.
- Declined in /code-review r2: `EmailRecipients.Parse` splitting a display name with a comma. ExtraTo/Cc are plain
  addresses (the #627 validator), and the company master holds addresses.

## Review
- (after implementation)
