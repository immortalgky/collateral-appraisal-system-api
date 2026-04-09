# Remove MassTransit Saga — Full Cleanup

## Tasks

- [x] Delete all 26 saga-related files (saga infra, migrations, events, handlers, services, endpoints, notification handler)
- [x] Modify GlobalUsing.cs (Workflow + Bootstrapper) — remove saga imports
- [x] Modify Program.cs — remove saga DbContext + state machine registration
- [x] Modify WorkflowModule.cs — remove saga DbContext + IAssignmentService + migration
- [x] Modify EfCoreMigrationService.cs — remove saga context
- [x] Modify ServiceCollectionExtensions.cs — remove saga DbContext registration
- [x] Modify Workflow.csproj — remove saga folder reference
- [x] Build and verify 0 errors
- [x] Run tests

## Review

### Summary
Removed the entire MassTransit saga orchestration layer (`AppraisalStateMachine`) which was superseded by the `IWorkflowEngine`-based flow.

### Files Deleted (26)
- **Saga infrastructure**: `AppraisalSagaDbContext`, `AppraisalStateMachine` (+ Handlers partial), `AppraisalSagaState`, `AppraisalSagaStateConfiguration`
- **Saga migrations**: 3 files under `Data/Migrations/Saga/`
- **Saga events & handlers**: `AssignmentRequested`, `RequestSubmitted`, `AssignmentRequestedEventHandler`, `TransitionCompletedEventHandler`
- **Saga-driven service**: `IAssignmentService`, `AssignmentService`
- **Old endpoints**: `KickstartWorkflow` (3 files), `CompleteTask` (3 files), `GetTasks` (5 files)
- **Dead notification handler**: `GetWorkflowStatusQueryHandler` (entirely commented out)

### Files Modified (7)
1. **Bootstrapper/Api/GlobalUsing.cs** — Removed `Workflow.Data`, `Workflow.Sagas.AppraisalSaga`, `Workflow.Sagas.Models` imports
2. **Bootstrapper/Api/Program.cs** — Removed `AppraisalSagaDbContext` registration + `AddSagaStateMachine<AppraisalStateMachine>` block + `MassTransit.EntityFrameworkCoreIntegration` import
3. **Modules/Workflow/Workflow/GlobalUsing.cs** — Removed `Workflow.Events`, `Workflow.Sagas.AppraisalSaga`, `Workflow.Sagas.Models`, `Workflow.Services` imports
4. **Modules/Workflow/Workflow/WorkflowModule.cs** — Removed `IAssignmentService` registration, saga `DbContext` registration, and `UseMigration<AppraisalSagaDbContext>()`
5. **Database/Migration/EfCoreMigrationService.cs** — Removed `AppraisalSagaDbContext` from type array, switch case, and factory method
6. **Database/Extensions/ServiceCollectionExtensions.cs** — Removed `AppraisalSagaDbContext` registration block
7. **Modules/Workflow/Workflow/Workflow.csproj** — Removed `<Folder Include="Data\Migrations\Saga\" />`

### What Was Preserved
- `Tasks/Models/` (PendingTask, CompletedTask, RoundRobinQueue) — used by assignment selectors
- `Tasks/ValueObjects/TaskStatus.cs` — used by task models
- `Data/Repository/` (AssignmentRepository) — used by selectors
- `Shared.Messaging/Events/` (TaskAssigned, TaskCompleted, TransitionCompleted) — used by Notification module
- `Services/Configuration/`, `Services/Groups/`, `Services/Hashing/` — used by workflow engine

### Build Results
- 0 errors from saga removal (7 pre-existing Parameter module errors unrelated to this change)
- All test failures are pre-existing (NSubstitute mock mismatches, assertion failures) — none caused by saga removal
