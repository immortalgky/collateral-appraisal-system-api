# #627 Assignment email: compose at assign (replace the saved draft)

Branch `immortalgky/implement-issue-627` (worktree `bluegill`), off the local integration branch
`feat/617-admin-company-email` @ 8ad65393. Parent spec #617; blocks #620.

## Todo
- [x] Preview: `GET /appraisals/{id}/assignment-email/preview` (template only, `APPRAISAL_ADMINISTRATION_EDIT`)
- [x] Assign carries `email` + `sendEmail` (EXT/EXTO only): Pending / NotSent / no row
- [x] Validation (format in the validator, live documents + size in the handler); invalid = 400, nothing assigned
- [x] Remove PUT draft, Draft status, draft index, IsFor/CompanyId/IsOffline, `?template`
- [x] Reshape `AddAssignmentEmails` in place (never applied)
- [x] Unit tests at the agreed seams (handler, validator, preview query)
- [x] Matt's code-review (Standards + Spec) and the built-in `/code-review` loop (4 rounds)
- [x] test-automator gates (build 0 errors; unit 12 projects green except 1 Workflow timing flake that passed on re-run; has-pending-model-changes clean; Appraisal integration 330/331, 1 SQL-timeout flake passed 7/7 on re-run)
- [ ] User test → merge `--no-ff` into the integration branch, close #627 by hand

## Settled decisions (do not re-raise)
- `sendEmail` is nullable. true → Pending row; false → NotSent row; null → no row (quotation auto-assign;
  #622 sends the template).
- A NotSent row stores only the status and the assignment link. The dialog's email is ignored and not validated.
- Email and `sendEmail` are read only for EXT/EXTO. The FE admin page sends only EXT/INT/EXTO, so P never carries one.
- Sending a choice (`sendEmail` present) needs `APPRAISAL_ADMINISTRATION_EDIT`, the same rule as the preview.
  Without it the assign is a 403 before anything is recorded.
- The 10-attachment cap counts distinct documents. DistinctBy keeps the first Source.
- Body HTML is stored as given (trusted, the same decision as the quotation email).
- The row is committed with the assigner and remark before the resume. The assign adds only its own row.
  - If `ResumeWorkflowAsync` throws, the handler deletes that row (ExecuteDelete, `CancellationToken.None`)
    and rethrows the original error.
  - If that delete fails, the failure is swallowed. #620/#622 send only on the events of a successful resume,
    so a row left this way is never sent.
- Rows of handovers that stood are history. A route-back reuses the same assignment row, so a re-assign adds a new
  email row beside the old ones.
  - r1 tried deleting every unsent row, then a unique unsent index. Both were dropped in r2: they deleted rows of
    handovers that stood.
- Accepted rare races:
  - a resume that commits but still throws (lost ack, cancellation during commit)
  - a process crash between the save and the resume
- Open for #620/#622 to design: how the sender picks the row for the handover it was triggered by. Several rows can
  share an AssignmentId after a route-back, or briefly during a concurrent failed attempt.
- The preview reads the stored assignment row. The FE saves the assignment draft before opening it (app#536).
- Declined r4 nits:
  - a logger for the swallowed cleanup failure
  - removing `IX_AssignmentEmails_AssignmentId` (it reverses r3's own advice)

## Review
- Domain `AssignmentEmail`: factories `ToSend` (Pending) and `NotSent`. Draft and its staleness keys are gone.
  `AssignmentId` is required.
- `AssignAppraisalCommand` gains `Email` and `SendEmail`.
  - The validator applies `AssignmentEmailInputValidator` when `SendsEmail`.
  - The handler checks permission and documents before anything is mutated, adds the row in the same save, and
    cleans it up when the resume fails.
- Migration `20261031033255_AddAssignmentEmails`:
  - `AssignmentId` NOT NULL
  - `IX_AssignmentEmails_AppraisalId` and `IX_AssignmentEmails_AssignmentId`
  - no CompanyId/IsOffline columns
