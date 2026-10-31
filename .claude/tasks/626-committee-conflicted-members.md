# #626 Committee approval: exclude Conflicted members

Spec: GitHub issue #626. Branch `immortalgky/committee-conflicted-member-exclusion` (orca worktree `croaker`).

## Done
- [x] `SegregationOfDutiesGuard.GetConflictedUsersAsync`: the existing excludeAssigneesFrom rule applied to a whole member list
- [x] `ApprovalActivity` filters committee members and the meeting roster, records `{id}_excludedMembers` and a log line
- [x] `ApprovalRoundUnresolvableException` (400 `APPROVAL_ROUND_UNRESOLVABLE`): no members left, FixedCount, Fixed quorum in Quorum mode, RoleRequired, MinVotes
- [x] The engine and `WorkflowActivityBase` rethrow it, never `WorkflowStatus.Failed`
- [x] `TaskCompletedIntegrationEvent` goes through the outbox (user decision 2026-10-30, found during the rollback check)
- [x] DbUp script `20261030120000_SeedData_PendingApprovalConflictedMembers.sql` + Development seeder, 5 activities

## Review loop (`/code-review`)
- Round 1 (whole branch, high, all): 14 findings → 5 fixed (5c22926a), 6 declined (below), 3 split into separate tickets.
- Round 2 (fix diff b2a84916..5c22926a): 3 low, all declined (below). No new critical/high/medium → loop stopped.

## Settled decisions (do not re-raise)
- The exclude list has 5 activities: int-appraisal-execution, appraisal-book-verification, int-offline-book-keyin
  (user 2026-10-31), int-appraisal-check, int-appraisal-verification.
- MinVotes > members left is refused too (user 2026-10-31).
- The script inserts an active wildcard row when no ACTIVE wildcard row exists; an inactive leftover row is left alone
  (user 2026-10-31). Admins switch the rule off by clearing the list, which the script never overwrites.
- The exception is caught by type in 4 places (ApprovalActivity, WorkflowActivityBase, WorkflowEngine ×2). No shared
  "refusal" base type: there is one case today.
- `FindUnresolvableReason` is not merged with `MeetingRosterEligibility`: different inputs (ApprovalMemberInfo vs
  MeetingMember/Committee), and the round also knows the voting mode.
- A more specific pending-approval row with ExcludeAssigneesFrom NULL inherits the JSON (empty), not the wildcard
  row. That is the existing precedence of every TaskAssignmentConfiguration; not changed here.
- A null member username is impossible (MeetingMember.UserId and CommitteeMember.UserId are required).
- `WorkflowService` / `TransactionalBehavior` log the refusal at Error level, like every other 400 in a
  transactional command.
- `{id}_excludedMembers` holds the latest round only; each round also writes a log line.
- The Development seeder checks the pending-approval row on its own, because the DbUp script normally inserts it first.
  It checks "any pending-approval row", not "an active wildcard" like the script: in Development `migrate` (the
  script) always runs before the app.
- Refusal messages say "approve vote(s)": every committee group's target vote is approve.
- The activity list is written in both the script and the seeder, as CLAUDE.md requires; no drift test.

## Separate tickets (pre-existing, out of scope)
- `TaskConfigurationService.GetConfigurationAsync` swallows DB errors and returns null, so a config read failure
  silently switches the exclusion (and every other override) off.
- `ApprovalMemberResolver` throwing (committee has no active members / not found) still becomes a terminal
  `ActivityResult.Failed`.
- TaskAssigned / TaskStarted / ApprovalTasksAssigned still publish straight to MassTransit, so they leak on a
  rolled-back submit.

## Review
- Rollback was verified by reading the code. CompleteActivity and ReleaseMeetingItem both run inside one
  transaction (TransactionalBehavior → WorkflowService.ResumeWorkflowAsync inline), and every write in the
  submit, including outbox rows, rolls back with it. The one direct broker publish on that path
  (TaskCompletedIntegrationEvent) now goes through the outbox. The user's local E2E still has to confirm it.
- Tests: 19 new unit tests (ApprovalActivityConflictedMemberTests, WorkflowEngineTests, PoolTaskCompletedNotificationTests).
- Two fixtures in ApprovalActivityMemberOverrideTests went from a 1-person roster to 2 people, because a 1-member roster
  against a Fixed quorum of 2 is now refused (MeetingRosterEligibility already blocks it at release).
