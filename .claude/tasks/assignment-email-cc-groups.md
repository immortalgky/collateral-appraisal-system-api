# Assignment email Cc from groups, not roles (#617 follow-up)

Branch `fix/assignment-email-cc-groups` off `origin/release/v6.1` (the email feature is already on UAT).
PR to `release/v6.1` (squash), then `cherry-pick -x` onto a branch off `origin/main` with a PR to `main`. BE only, no migration.

## Decisions (user, 2026-11-02)
- Use groups, because dispatch uses groups (`assigneeGroup` in the workflow config, plus TeamFilter). Roles are permissions.
- `IntAdminAll` chip = usable members of the **group** `IntAdmin`. Before this change it was the role.
- `KeyerSupervisor` (EXTO) and `FollowupSupervisor` (EXT) = usable members of the group `IntAppraisalChecker` who share
  **any** Team (auth.TeamMembers) with the keyer / follow-up officer. GroupMonitoring is no longer used for this.
- If nobody is found, the chip keeps its token, has no recipients, and the send goes ahead (unchanged behaviour).
- `AssignmentEmailFailureNotifier` sends its in-app notice to the assigner plus the group `IntAdmin`. Before this change it went to the role.
- Group names are hard-coded (`IntAdmin`, `IntAppraisalChecker`). If someone renames a group, the Cc silently drops; accepted
  (the workflow config also names them).
- Other `IsInRole("IntAdmin")` checks (invoice, quotation) are permissions and are not touched.
- Also fix Sonar S6444 in `AssignmentEmailTemplate.cs` (a Regex without a timeout).

## Todo
- [x] Auth lookup: group-based emails/usernames; supervisor = checker group ∩ same team
- [x] Appraisal Cc expansion uses the IntAdmin group
- [x] Notification failure notifier uses the IntAdmin group
- [x] Sonar S6444 regex timeout
- [x] Tests (integration lookup + notifier unit)
- [x] /code-review loop
- [ ] test-automator gates
- [ ] User tests locally → PRs (release/v6.1 + main port)

## Deploy note
Local DB: role IntAdmin has 9 users, group IntAdmin has 2. Before deploying, check UAT/PRD group IntAdmin membership and
the checkers' team membership, otherwise people silently drop off Cc.

## Settled decisions (do not re-raise)
- The responsible user can appear in their own supervisor list when they are also a checker. They are already Cc'd through the Keyer chip, and addresses are de-duplicated. Declined.
- The Dev seeder skips a group name that exists in any scope: Development only (seeding is off outside Dev). Declined.
- ExtAdmin with USER_MANAGE can add users to Bank groups or teams through `PUT /auth/users/{id}/groups|teams`. This is a known, parked Auth hole (security follow-ups #0) and is not fixed here. The lookup fails closed instead (Bank scope, no CompanyId on the group or the user).

## Review log
- R1 (whole diff, high, all): 9 findings.
  - Accepted: scope/company filter on groups and users (security); usernames lookup uses the access window; tests use the seeded Bank groups plus negative cases; GeneratedRegex.
  - Declined: the 3 items above.
- R2 (fixes only, correctness/security): 7 findings.
  - Accepted: Bank-only teams on the supervisor side; the contract docs; a test for the group CompanyId branch.
  - Declined (test-only, or negligible):
    - sweep marker hardening (the fixture is always a fresh Testcontainer);
    - the unchecked DeleteAsync result;
    - a unique-index collision with a leftover group;
    - the regex timeout exception (the pattern is linear).
- R3 (fixes only): 5 minor findings, no new critical/high/medium, so the loop is closed.
  - Declined:
    - the subject user's usability is not checked (an expired keyer still resolves supervisors; the keyer is the assigned staff);
    - a doc nit ("blank emails dropped");
    - test-strength nits (a positive control, the mixed-team case);
    - a `ct = default` mirroring GetUsernamesInRoleAsync.

## Added 2026-11-02 (user test round)
- Body placeholder `{{responsible}}` renamed to `{{internal_staff}}` (user pick). Visible label "ผู้รับผิดชอบ:" unchanged.
  Legacy `{{responsible}}` still filled/refused for drafts saved on UAT before the rename. Unit 1722/1722.
- FE (worktree `collateral-appraisal-system-app/email-footer-gap`, branch `fix/assignment-email-footer-gap` off release/v6.1):
  drawer sticky footer `bottom-0` → `-bottom-4` (sticky offsets from the scroll container's py-4); placeholder regex
  highlights company|internal_staff|responsible; hint text says bank staff. tsc 0, vitest appraisal+parity 1101/1101.
