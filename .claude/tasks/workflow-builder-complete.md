# Workflow Builder — Complete Feature Plan

**Goal**: Make the workflow builder fully functional so we never need to manually edit appraisal-workflow.json again. Changes saved/published via the UI are persisted to DB and picked up by the workflow engine.

---

## Root Cause Analysis

### Why the builder can't load the appraisal workflow
- `WorkflowBuilderPage` loads from `GET /api/workflows/definitions/{id}/latest-version` which reads `WorkflowDefinitionVersion.JsonSchema`
- The appraisal workflow is seeded to `WorkflowDefinition` (legacy table) by the DB/migration scripts **but has NO corresponding `WorkflowDefinitionVersion` row**
- So the builder's API returns 404 / the appraisal workflow doesn't show in the builder list

### Why editing a TaskActivity drops important properties
- `TaskForm.onSubmit()` reconstructs `properties` from scratch with only 8 fields
- This **overwrites and silently drops** `actions`, `canRaiseFollowup`, `canRaiseQuotation`, `teamIdVariable`, `assignmentRules`, `assigneeVariable`, `inputMappings`
- Same issue in `ApprovalForm.onSubmit()` — drops `activityName`, `voteMovements`, and the full `memberSource` structure

### How it should work (when fixed)
1. App startup → seeder creates `WorkflowDefinition` + `WorkflowDefinitionVersion` (idempotent)
2. Builder list shows "Collateral Appraisal Workflow"
3. User opens it → `latest-version` API returns the schema → form loads all activity properties
4. User edits TaskActivity → ALL fields (including actions, canRaiseFollowup, etc.) are captured
5. Save draft → `PUT .../schema` → `WorkflowDefinitionVersion.JsonSchema` updated
6. Publish → `POST .../publish` → `WorkflowDefinition.JsonDefinition` updated (engine picks it up)

---

## Changes

### Backend

- [ ] **NEW `Modules/Workflow/Workflow/Workflow/Infrastructure/Seed/AppraisalWorkflowDefinitionSeeder.cs`**
  - Reads embedded `appraisal-workflow.json`
  - Creates `WorkflowDefinition` if none with name "Collateral Appraisal Workflow" exists
  - ALSO creates a matching `WorkflowDefinitionVersion` (v1, Published) so the builder can load it
  - Idempotent: if WorkflowDefinition already exists, ensures the version row also exists

- [ ] **MODIFY `Modules/Workflow/Workflow/WorkflowModule.cs`**
  - Register `AppraisalWorkflowDefinitionSeeder` as `IDataSeeder<WorkflowDbContext>`

---

### Frontend

- [ ] **MODIFY `types/index.ts`**
  - Add `TaskAction` interface: `{ value: string; label: string; assignmentMode: 'system' | 'user'; movement: 'F' | 'B' | 'C'; condition?: string }`
  - Extend `TaskProperties` with: `actions?: TaskAction[]`, `canRaiseFollowup?: boolean`, `canRaiseQuotation?: boolean`, `teamIdVariable?: string`, `assignmentRules?: { teamConstrained: boolean }`, `assigneeVariable?: string`, `inputMappings?: Record<string, string>`
  - Fix `ApprovalProperties.memberSource` type to: `{ type: string; valueExpression?: string; thresholds?: Array<{ maxValue: number | null; committeeCode: string }>; parameters?: Record<string, unknown> }`
  - Add `activityName?: string` and `voteMovements?: Record<string, string>` to `ApprovalProperties`

- [ ] **MODIFY `schemas/index.ts`**
  - Add action schema: `{ value, label, assignmentMode, movement, condition? }`
  - Add to `taskFormSchema`: `actions` array, `canRaiseFollowup`, `canRaiseQuotation`, `teamIdVariable`, `teamConstrained`, `assigneeVariable`, `inputMappings` array
  - Fix `assigneeRole` to be `optional()` (not all tasks have an explicit role)
  - Add `activityName`, `voteMovements` array to `approvalFormSchema`

- [ ] **MODIFY `components/panels/TaskForm.tsx`** — largest change
  - Add assignment strategy options: `pool`, `variable_assignee` (currently only 3 options, need 5)
  - Add **Actions** section: field array with value, label, assignmentMode (system/user), movement (F/B/C), optional condition
  - Add **Flags** section: canRaiseFollowup, canRaiseQuotation checkboxes
  - Add **Team Assignment** section: teamIdVariable text, teamConstrained checkbox, assigneeVariable text
  - Add **Input Mappings** section: key-value field array
  - Fix `onSubmit` to use spread: `properties: { ...(props ?? {}), activityName: ..., actions: ..., ... }` so any unrecognized fields survive

- [ ] **MODIFY `components/panels/ApprovalForm.tsx`**
  - Add `activityName` text field
  - Add `voteMovements` key-value field array
  - When memberSourceType === "threshold": show `valueExpression` text + thresholds array (committeeCode + maxValue pairs)
  - Fix `onSubmit` to use spread + correctly serialize the full `memberSource` structure

---

## Out of Scope
- No backend changes needed to endpoints (all exist and work)
- No changes to adapters (toWorkflowSchema.ts passes properties through as-is, which is correct)
- No changes to normalizeSchema.ts (it passes properties through correctly)
- CompanySelectionForm, InternalFollowupSelectionForm — these activities have empty `{}` properties in the JSON, no changes needed

---

## Review
_To be filled after implementation_
