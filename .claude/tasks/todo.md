# Add StartedBy Assignee Selection Strategy

## Steps

- [x] 1. Add `StartedBy` property to `AssignmentContext`
- [x] 2. Add `StartedBy` enum value + string mappings to `AssigneeSelectionStrategy`
- [x] 3. Create `StartedByAssigneeSelector` class
- [x] 4. Thread `StartedBy` into `AssignmentContext` in `AssignmentPipeline`
- [x] 5. Register strategy in `AssigneeSelectorFactory`
- [x] 6. Register DI in `WorkflowModule`
- [x] 7. `dotnet build` — verify 0 errors (322 pre-existing warnings)

## Review

### Summary of Changes

Six files changed (1 new, 5 modified) to add a `StartedBy` assignee selection strategy that routes tasks back to the user who originally started the workflow instance.

### Files

| File | Action | What |
|------|--------|------|
| `AssigneeSelection/Core/AssignmentContext.cs` | Modified | Added `string? StartedBy` property |
| `AssigneeSelection/Core/AssigneeSelectionStrategy.cs` | Modified | Added `StartedBy` enum value + `"started_by"` string mappings |
| `AssigneeSelection/Strategies/StartedByAssigneeSelector.cs` | **New** | Selector that reads `context.StartedBy` and returns success/failure |
| `AssigneeSelection/Pipeline/AssignmentPipeline.cs` | Modified | Threads `WorkflowInstance.StartedBy` into the `AssignmentContext` |
| `AssigneeSelection/Factories/AssigneeSelectorFactory.cs` | Modified | Added switch case for `StartedBy` → `StartedByAssigneeSelector` |
| `WorkflowModule.cs` | Modified | Registered `StartedByAssigneeSelector` as scoped service |

### How It Works

1. When a workflow starts, `WorkflowInstance.StartedBy` stores the initiator's user code.
2. `AssignmentPipeline.SelectAssigneeAsync()` now copies `WorkflowInstance.StartedBy` into `AssignmentContext.StartedBy`.
3. The `CascadingAssignmentEngine` iterates strategies. When it hits `"started_by"`, it resolves `StartedByAssigneeSelector` via `AssigneeSelectorFactory`.
4. `StartedByAssigneeSelector.SelectAssigneeAsync()` reads `context.StartedBy`:
   - If null/empty → returns `Failure(...)` so the engine tries the next strategy.
   - If present → returns `Success(startedBy, metadata)` with the initiator's user code.

### Usage

Add `"started_by"` to any activity's `assignmentStrategies` list in `appraisal-workflow.json`:

```json
"assignmentStrategies": ["started_by"]
```

### Build: 0 errors (322 pre-existing warnings)

---

# Fix: StartedBy blank in TaskActivity assignment

## Steps

- [x] 1. Add `StartedBy = context.WorkflowInstance.StartedBy` to `previousOwnerContext` (line ~103)
- [x] 2. Add `StartedBy = context.WorkflowInstance.StartedBy` to main `assignmentContext` (line ~183)
- [x] 3. `dotnet build` — verify 0 errors

## Review

### Root Cause

`TaskActivity.cs` creates two `AssignmentContext` objects but never set `StartedBy`. The `AssignmentPipeline` does this correctly, but `TaskActivity` bypasses the pipeline and calls selectors directly, so the `started_by` strategy would always fail with "StartedBy requires a non-empty StartedBy value".

### Fix

Added `StartedBy = context.WorkflowInstance.StartedBy` to both `AssignmentContext` instantiations in `TaskActivity.cs`:

| Location | Line | Purpose |
|----------|------|---------|
| `previousOwnerContext` | ~105 | Consistency — context used for PreviousOwner selector |
| `assignmentContext` | ~191 | **The actual bug** — context used for all configured strategies including `started_by` |

### Files Changed

| File | Lines Added |
|------|-------------|
| `Modules/Workflow/Workflow/Workflow/Activities/TaskActivity.cs` | 2 |

### Build: 0 errors (380 warnings, pre-existing)
