Round-Robin Assignment: Concurrency-Safe Design

Overview
- Stored procedure: `workflow.usp_RoundRobin_SelectNextAndMaybeReset`
- Locking: `UPDLOCK, ROWLOCK, HOLDLOCK` (strict ordering under concurrency)
- Isolation: Caller runs in `SERIALIZABLE` for the short DB-only segment
- Repository: Requires ambient EF transaction (Option A)
- Caller: Uses `IWorkflowPersistenceService.ExecuteInTransactionAsync(..., IsolationLevel.Serializable)`

Stored Procedure Contract
- Name: `workflow.usp_RoundRobin_SelectNextAndMaybeReset`
- Parameters:
  - `@ActivityName NVARCHAR(100)`
  - `@GroupsHash NVARCHAR(64)`
  - `@SelectedUserId NVARCHAR(450) OUTPUT`
- Behavior:
  - Selects the next eligible row with strict ordering
  - Atomically increments `AssignmentCount`, updates `LastAssignedAt`
  - If no active rows remain with `AssignmentCount = 0`, resets counts to 0 for the combo
  - Returns the selected `UserId` via `@SelectedUserId`
- Notes:
  - No `COMMIT/ROLLBACK` inside the proc; it enlists in the caller transaction

Indexing
- Added covering index to support the selection path:
  - Key: `(ActivityName, GroupsHash, IsActive, AssignmentCount, UserId)`
  - Include: `(LastAssignedAt)`
  - Migration: `20250907094000_AddRoundRobinSelectionIndex`

Repository Usage (Dapper + SqlConnectionFactory)
- Class: `AssignmentRepository`
- Method: `SelectNextUserWithRoundResetAsync(activityName, groupsHash, ct)`
- Requirements:
  - Ambient EF transaction must be present (`dbContext.Database.CurrentTransaction != null`)
  - Uses Dapper with shared `ISqlConnectionFactory` and enlists the ambient `DbTransaction`
- Example call:
  - Within a transactional segment:
    - `await persistence.ExecuteInTransactionAsync(() => assignmentRepo.SelectNextUserWithRoundResetAsync(...), IsolationLevel.Serializable)`

Transaction Boundary
- Activity-level (TaskActivity) wraps only the DB-critical assignment segment in `SERIALIZABLE` isolation
- External I/O, lifecycle actions, publishing are outside the DB transaction
- Engine does not create a broad transaction around activity execution

Fairness vs Throughput
- This design favors strict fairness (no skipping) under concurrency
- Expect more blocking than `READPAST` designs; keep transactional segments short

Retries & Resilience
- EF Core retry policy is enabled at provider level; SP calls use the ambient transaction via Dapper
- Keep the assignment segment short to minimize lock duration

