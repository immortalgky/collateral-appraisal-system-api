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
- [ ] Matt code-review, then the `/code-review` loop, then test-automator gates
- [ ] Commit; user test → merge `--no-ff` into the integration branch, close #623 by hand

## Settled decisions (user 2026-11-01)
- Migration reshaped in place (same topic, not on main/SIT). A local DB that applied it must roll back first.
- Resend targets the case's current assignment row (the one the preview reads): it must be EXT/EXTO with a company.
  A case with no email row yet can send too.
- Latest email of that assignment still Pending (waiting for its handover) → resend refused (400), so the company
  never gets two copies.
- History and view need `APPRAISAL_ADMINISTRATION_EDIT`, the same as the preview.
- Closed = appraisal status Completed or Cancelled → resend refused.
