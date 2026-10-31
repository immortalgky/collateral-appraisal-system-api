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
- [x] Matt code-review (Standards + Spec): all 6 AC met. Fixed: the no-To comment (an extra To still sends, story 50),
      dropped the unasked-for self-exclusion from the supervisor query, one `Clean` helper for To/Cc addresses
- [x] /code-review r1 (11 findings): fixed separator-only To ("; ,") now Failed (addresses split like EmailRecipients),
      a reason for "company not found", a warning log on Failed, the supervisor test no longer touches the seeded IntAdmin
      group. Open question to the user: IsSystem accounts in the IntAdmin Cc.
- [ ] /code-review r2+ (fix diff only)
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

## Settled decisions (do not re-raise)
- The IntAdmin group is matched by name: `20260729130000_UpdateSeed_BankCompanyScopes.sql`, `GetAppraisalBrief` and the
  workflow pool already depend on the group named `IntAdmin`.
- `UsableWithEmail` restates `ApplicationUser.IsUsable` in a form EF can translate; the doc comment links the two.
- A company with no email but an admin-added extra To still sends (story 50: Failed only with no deliverable To).
- The failure reason is an English string (house rule); the UI localises.
- A supervisor who is also the keyer is listed (no self-exclusion); Cc de-duplicates.
- Declined in r1: Cc expanded before the To check (keeps reads before writes; failure is rare); the responsible user
  looked up twice (one extra round-trip); `GetEmailsInRoleAsync` applying IsUsable while `GetUsernamesInRoleAsync`
  checks IsActive only (the spec asks for IsUsable here); "IntAdmin" constants per module (repo style); IntAdminAll
  not including the Admin role (spec says IntAdmin); tests using DateTime.Now with a ±1 day margin.
