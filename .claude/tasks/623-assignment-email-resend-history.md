# #623 Assignment email: resend and history after assignment

Branch `immortalgky/implement-623-task` (worktree `mullet`), off the local integration branch
`feat/617-admin-company-email` @ e13d7f58 (#620 merged). Parent spec #617. #621 runs in parallel (worktree `ribboneel`)
and touches the dispatcher too: keep the dispatcher diff small.

## Endpoints (all `APPRAISAL_ADMINISTRATION_EDIT`, checked in the handler)
- `GET /appraisals/{id}/assignment-emails`: history, newest first (kind first/resend, status, actor code + name,
  created, sent, To that went out, Cc count, attachment count).
- `GET /appraisals/{id}/assignment-emails/{emailId}`: what went out (resolved To/Cc, subject, body, attachments with
  file names), plus the composed ExtraTo/Cc for the FE to pre-fill a resend.
- `POST /appraisals/{id}/assignment-emails/resend`: body = `AssignmentEmailInput` (FE pre-fills from the latest email,
  or from the preview when NotSent). New row `IsResend`, queued + published in one save → Sent (Failed is #624).

## Todo
- [x] Domain: `IsResend`, `SentTo`, `SentCc` (set by `Queue`), `SentAt` (set by `MarkSent`)
- [x] Migration: reshape `20261031033255_AddAssignmentEmails` in place
- [x] Share the attachment check (assign + resend) and the queue/publish step (EXTO handover + resend)
- [x] History query, view query, resend command, endpoints
- [x] Unit tests: history, view, resend (NotSent, closed, non-admin, Pending, not EXT/EXTO)
- [x] Matt code-review, then the `/code-review` loop (r1 9 findings → r2 no correctness/security, stopped), then test-automator gates
- [ ] Commit; user test → merge `--no-ff` into the integration branch, close #623 by hand

## Settled decisions (user 2026-11-01)
- Migration reshaped in place (same topic, not on main/SIT). A local DB that applied it must roll back first.
- Resend targets the case's current assignment row (the one the preview reads): it must be EXT/EXTO with a company.
  A case with no email row yet can send too.
- Latest email of that assignment still Pending (waiting for its handover) → resend refused (400), so the company
  never gets two copies.
- History and view need `APPRAISAL_ADMINISTRATION_EDIT`, the same as the preview.
- Closed = appraisal status Completed or Cancelled → resend refused.
- Matt review (Standards + Spec), fixed in bba7ac90: a resend that would reach nobody (no company email, no extra To)
  → 400, nothing saved; the view query projects to a DTO; `IsClosed` / `IsAssignedToCompany` live on
  `AssignmentEmailCase`; the Pending 400 no longer claims the email "goes out when the case reaches the company"
  (not true for EXT until #622); `SentAt` = when the confirmation arrived; event `AssignedBy` = assigner or resender.
- Declined in Matt review:
  - `QueueAsync`'s loose company/responsible/assignedBy parameters (data clump): left so the #621 merge stays small.
  - The two user-name formatters differ on purpose (dispatcher falls back to the username, history to null).
  - Subject placeholders: the template subject has none, and the admin's subject is taken as typed.
  - A document deleted since shows without its name in the view (accepted).
- Waiting on the user (asked at the end of the session):
  - "ส่งอีเมล" after มอบหมายโดยไม่ส่ง is stored `IsResend=true` (sent from the slip, not with the assignment), so the
    history labels the first email that actually went out as a resend.
  - Attachments are checked live but not against this case's documents (since #627): an admin can attach any
    document id. Follow-up ticket or fix here?
- /code-review r1 (9 findings), fixed: empty-To refused inside `QueueAsync` (`refuseWithoutTo`) before anything is
  queued or published (r1: refusing after the outbox Publish relied on no later save in the scope); history counts the
  JSON lists in memory instead of relying on OPENJSON translation; redundant empty guard around the user lookup.
- Declined in /code-review r1 (do not re-raise):
  - Two concurrent resends (double click / retry) both send: accepted race, the FE disables the button while sending.
  - EXTO handover sending the template while a resend already went out: the handover sends only when the assignment
    row was Pending (#620 settled); a route-back re-handover sending again is by design.
  - The resend fills `{{responsible}}` with the follow-up officer even when the preview left it open (automatic
    follow-up): filling at send is story 25's intent.
- Waiting on the user (r1): a `{{responsible}}` / `{{company}}` still unknown at send goes out as literal text
  (EXT auto follow-up not chosen yet, or a deleted company with an extra To).
- /code-review r2 (fix diff only): no correctness/security finding; 6 low. Accepted: `refuseWithoutTo` moved before
  the CancellationToken (CA1068); SQL Server translation of the JSON projections proven by an integration test
  (test-automator). Declined: NULL `Attachments` column (EF always writes `[]`, only a manual SQL write could make it
  NULL); the empty-To message also covering a deleted company; the flag living in the shared dispatcher (the handover's
  empty-To failure is #621/#624). → loop stopped (no new critical/high/medium).
- test-automator gates: build 0 errors; unit 12 projects green (Appraisal 1637, Notification 38, Workflow 1219 + 1
  existing skip, the rest green); has-pending-model-changes (AppraisalDbContext) clean; new Testcontainers test
  `AssignmentEmailQueriesSqlTests` (2/2) proves the JSON projections and the case reader's new columns on SQL Server;
  `~Assign` integration filter 30/30 (first run hit a Docker memory kill from another session's containers, rerun clean).

## Review
- Domain `AssignmentEmail`: `IsResend`, `SentTo`/`SentCc` (written by `Queue`), `SentAt` (written by `MarkSent`, the
  Sent consumer stamps `ApplicationNow`), factory `Resend`.
- `AssignmentEmailDispatcher.QueueAsync`: the queue + publish step shared by the EXTO handover and the resend;
  `refuseWithoutTo` makes the resend a 400 before anything is queued.
- `AssignmentEmailAttachments.CheckAsync`: the attachment check moved out of AssignAppraisal, shared by both.
- `AssignmentEmailCase` reads the appraisal status and the current assignment's id/type/status (`IsClosed`,
  `IsAssignedToCompany`).
- `ResendAssignmentEmailCommandHandler` + validator; `GetAssignmentEmailHistoryQueryHandler`,
  `GetAssignmentEmailSentQueryHandler`; `AssignmentEmailsEndpoint` (3 routes).
- Migration `20261031033255_AddAssignmentEmails` reshaped in place (4 columns).
- Tests: `AssignmentEmailTests` (domain 4), `ResendAssignmentEmailTests` (11), `AssignmentEmailHistoryTests` (5),
  `AssignmentEmailQueriesSqlTests` (integration 2).
- Security: all three endpoints check `APPRAISAL_ADMINISTRATION_EDIT` in the handler; the view looks the email up by
  id AND appraisal; the actor is the server-side bank code; values filled into HTML are encoded; nothing sensitive logged.
