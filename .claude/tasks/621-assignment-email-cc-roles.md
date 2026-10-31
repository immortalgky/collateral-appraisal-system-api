# #621 Assignment email: expand Cc role chips

Branch `immortalgky/implement-621-ticket` (worktree `ribboneel`), off the local integration branch
`feat/617-admin-company-email` @ e13d7f58 (#620 merged). Parent spec #617.

## Flow
At queue time (`AssignmentEmailDispatcher.QueueExtoAsync`, before anything is mutated) the stored Cc is expanded:
- literal address → kept
- `IntAdminAll` → `IUserLookupService.GetEmailsInRoleAsync("IntAdmin")`
- `Keyer` / `FollowupStaff` → `GetEmailsAsync([responsible username])`
- `KeyerSupervisor` / `FollowupSupervisor` → `GetSupervisorEmailsAsync(responsible username)`
- a token the row no longer holds (chip removed) is not expanded; result de-duplicated (case-insensitive)

Auth contract queries (Auth module, own DbContext): only users that are `IsUsable` (active, inside any access window)
and have a non-blank email. Supervisors = members of non-deleted groups that monitor a non-deleted group the user is
in, the monitor group named `IntAdmin` excluded.

No deliverable To (company has no email, no extra To) → row `Failed` with `FailureReason`, nothing published.

## Todo
- [x] Auth contract: `GetEmailsInRoleAsync`, `GetEmailsAsync`, `GetSupervisorEmailsAsync` + integration tests
- [x] Domain: `AssignmentEmail.FailureReason`, `Fail(reason)` (Queued only)
- [x] Migration: FailureReason folded into `AddAssignmentEmails` (remove + re-add)
- [x] Dispatcher: Cc expansion + Failed on no To; dispatch-seam tests
- [ ] Matt code-review, then /code-review loop until clean
- [ ] test-automator gates
- [ ] User test → merge `--no-ff` into the integration branch, close #621 by hand

## Settled decisions
- IntAdmin exclusion is on the monitor side (user, 2026-11-01): an `IntAdmin` group monitoring the keyer's group does
  not count as a supervisor, so a removed `IntAdminAll` chip cannot come back through the supervisor chip. The
  keyer's own membership in the IntAdmin group still counts.
- FailureReason is folded into `AddAssignmentEmails` (user, 2026-11-01; one migration per topic). A local DB that
  already applied it must roll back first.
- Keyer and follow-up tokens both resolve to the responsible person the dispatcher was given (EXTO: the keyer);
  #622 passes the follow-up officer.
