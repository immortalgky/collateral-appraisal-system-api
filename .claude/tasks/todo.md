# Todo: Pool Task Locking

## Goal
First-write-wins lock for pool tasks (AssignedType = "2"). Personal tasks unaffected. HeartbeatLock refreshes TTL; background service expires stale locks after 30 min.

---

## Step 1 — PendingTask.cs: add LockedAt + lock methods
- [x] Add `LockedAt` property
- [x] Add `Lock(username)`, `ReleaseLock()`, `IsLockedBy(username)`, `IsLockExpired(timeout)` methods

## Step 2 — PendingTaskConfiguration.cs: map LockedAt column
- [x] Add `LockedAt` property mapping

## Step 3 — IWorkflowNotificationService + impl: add lock/unlock notifications
- [x] Add `NotifyPoolTaskLocked` and `NotifyPoolTaskUnlocked` to interface
- [x] Implement both in WorkflowNotificationService

## Step 4 — New LockTask feature folder (4 commands)
- [x] LockTaskCommand + Handler + Endpoint (`POST /tasks/{id}/lock`)
- [x] UnlockTaskCommand + Handler + Endpoint (`DELETE /tasks/{id}/lock`)
- [x] HeartbeatTaskLockCommand + Handler + Endpoint (`PUT /tasks/{id}/lock/heartbeat`)
- [x] AdminUnlockTaskCommand + Handler + Endpoint (`DELETE /tasks/{id}/lock/admin`)

## Step 5 — TaskLockExpiryService background service
- [x] Create `Modules/Workflow/Workflow/Tasks/Services/TaskLockExpiryService.cs`
- [x] Register in WorkflowModule.cs

## Step 6 — PoolTaskDto + vw_TaskList.sql: add WorkingBy + LockedAt
- [x] Add fields to PoolTaskDto
- [x] Update SQL view SELECT

## Step 7 — EF Migration
- [x] Run `dotnet ef migrations add AddTaskLockTimestamp` — Done

## Step 8 — Build verification
- [x] `dotnet build` — 0 errors, 359 pre-existing warnings

---

## Review

### Files Created
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/LockTaskCommand.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/LockTaskCommandHandler.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/LockTaskEndpoint.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/UnlockTaskCommand.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/UnlockTaskCommandHandler.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/UnlockTaskEndpoint.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/HeartbeatTaskLockCommand.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/HeartbeatTaskLockCommandHandler.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/HeartbeatTaskLockEndpoint.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/AdminUnlockTaskCommand.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/AdminUnlockTaskCommandHandler.cs`
- `Modules/Workflow/Workflow/Tasks/Features/LockTask/AdminUnlockTaskEndpoint.cs`
- `Modules/Workflow/Workflow/Tasks/Services/TaskLockExpiryService.cs`
- EF migration `AddTaskLockTimestamp` (auto-generated in Workflow migrations folder)

### Files Modified
- `Modules/Workflow/Workflow/Tasks/Models/PendingTask.cs`
- `Modules/Workflow/Workflow/Data/Configurations/PendingTaskConfiguration.cs`
- `Modules/Workflow/Workflow/Workflow/Services/IWorkflowNotificationService.cs`
- `Modules/Workflow/Workflow/Workflow/Services/WorkflowNotificationService.cs`
- `Modules/Workflow/Workflow/Tasks/Features/GetPoolTasks/GetPoolTasksQuery.cs`
- `Modules/Workflow/Workflow/WorkflowModule.cs`
- `Database/Scripts/Views/Workflow/vw_TaskList.sql`
