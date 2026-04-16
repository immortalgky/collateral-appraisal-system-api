# Task: Migrate Role-Based → Group-Based Task Assignment

## Todo
- [x] Rename `TeamMemberInfo.ActivityRoles` → `ActivityGroups`
- [x] Update `ITeamService` interface — `roleName` → `groupName`
- [x] Rewrite `CompanyTeamService` SQL — `AspNetUserRoles`/`AspNetRoles` → `GroupUsers`/`Groups`
- [x] Rewrite `TeamFilter` — read `assigneeGroup` instead of `assigneeRole`
- [x] Remove `ActivityRoleFilter` + DI registration
- [x] Rewrite `UserGroupService` SQL — roles → groups
- [x] Update `TaskActivity` — remove `assigneeRole` references, add backwards-compat fallback
- [x] Update `WorkflowActivityFactory` + `AdminReviewActivity`
- [x] Remove `assigneeRole` from workflow JSON configs (12 lines)
- [x] Update `MockTeamService`
- [x] Update tests
- [x] Create seed SQL script (`SeedWorkflowGroups.sql`)
- [x] Build and verify (0 errors, 121/121 assignment tests pass)
- [x] Fix code review findings (NEWSEQUENTIALID → NEWID, backwards-compat fallback)

## Review

### Summary of Changes
Migrated the workflow task assignment pipeline from `assigneeRole` (backed by `auth.AspNetRoles`/`auth.AspNetUserRoles`) to `assigneeGroup` (backed by `auth.Groups`/`auth.GroupUsers`). This gives a clean separation: **roles** = API authorization, **groups** = workflow task assignment.

### Files Modified (14 files)
- `Modules/Workflow/Workflow/AssigneeSelection/Teams/TeamMemberInfo.cs` — renamed `ActivityRoles` → `ActivityGroups`
- `Modules/Workflow/Workflow/AssigneeSelection/Teams/ITeamService.cs` — renamed `roleName` → `groupName`
- `Modules/Workflow/Workflow/AssigneeSelection/Teams/CompanyTeamService.cs` — SQL rewrite (roles → groups)
- `Modules/Workflow/Workflow/AssigneeSelection/Teams/MockTeamService.cs` — updated to match interface
- `Modules/Workflow/Workflow/AssigneeSelection/Pipeline/TeamFilter.cs` — reads `assigneeGroup` now
- `Modules/Workflow/Workflow/WorkflowModule.cs` — removed `ActivityRoleFilter` DI registration
- `Modules/Workflow/Workflow/Services/Groups/UserGroupService.cs` — SQL rewrite (roles → groups)
- `Modules/Workflow/Workflow/Workflow/Activities/TaskActivity.cs` — removed `assigneeRole`, added backwards-compat fallback
- `Modules/Workflow/Workflow/Workflow/Activities/Factories/WorkflowActivityFactory.cs` — replaced property definition
- `Modules/Workflow/Workflow/Workflow/Activities/AppraisalActivities/AdminReviewActivity.cs` — output key change
- `Modules/Workflow/Workflow/Workflow/Config/appraisal-workflow.json` — removed 11 `assigneeRole` lines
- `Modules/Workflow/Workflow/Workflow/Config/document-followup-workflow.json` — removed 1 `assigneeRole` line
- `Tests/Unit/Workflow.Tests/AssigneeSelection/Pipeline/AssignmentPipelineTests.cs` — updated
- `Tests/Unit/Workflow.Tests/AssigneeSelection/Teams/CompanyTeamServiceTests.cs` — updated

### Files Created (1 file)
- `Database/Scripts/Seed/SeedWorkflowGroups.sql` — seeds groups matching existing roles

### Files Deleted (1 file)
- `Modules/Workflow/Workflow/AssigneeSelection/Pipeline/ActivityRoleFilter.cs` — redundant (DB queries handle filtering)

### Team Constraint Status
Preserved identically. The 3-branch flow in `TeamFilter` is unchanged:
1. Not constrained → load all members in group across all teams
2. Constrained + no TeamId → load all (first assignment establishes team)
3. Constrained + TeamId → scoped to that team's group members only

### Code Review Findings (Fixed)
1. **BUG**: `NEWSEQUENTIALID()` in seed script → fixed to `NEWID()`
2. **In-flight workflows**: Added backwards-compat fallback (`?? GetProperty("assigneeRole")`) in 3 places in TaskActivity
