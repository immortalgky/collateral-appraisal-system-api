# PR plan — credit appraisal tracking

**Nothing has been staged, committed, branched or pushed.** This is the plan only.

## The problem this plan has to solve

Both working trees are shared with other sessions and both are behind `origin/main`:

```
BE   local d8061c26   origin f8f342c5   behind 2
FE   local b01640e7   origin 3c2514e1   behind 6
```

`git status` shows **82 modified + 24 untracked** files in the BE alone, and most of it is not
ours — post-approval summary auto-attach, upload size limits, machinery summary, a dino loader,
blob transfer, reporting contracts. `git add -A` or `git commit -a` would sweep all of it into our
PR. Two files are worse than that: they contain **our hunks and someone else's in the same file**.

Do NOT stage in place. Build the branch in a separate worktree off `origin/main` and copy in only
what is ours.

---

## Ownership audit

Every file below was classified by reading its diff, not by guessing from the path.

### BE — ours, whole file (12 modified)

```
Modules/Appraisal/Appraisal/Application/Features/Appraisals/ExportAppraisals/ExportAppraisalsQueryHandler.cs
Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdQueryHandler.cs
Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalDocuments/GetAppraisalDocumentsEndpoint.cs
Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalDocuments/GetAppraisalDocumentsQueryHandler.cs
Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisals/GetAppraisalsQueryHandler.cs
Modules/Appraisal/Appraisal/Application/Features/Appraisals/QuickSearch/QuickSearchQueryHandler.cs
Modules/Auth/Auth/AuthModule.cs
Modules/Auth/Auth/Infrastructure/Seed/AuthDataSeed.cs
Modules/Auth/Auth/Infrastructure/Seed/MenuSeedData.cs
Modules/Workflow/Workflow/Workflow/Features/GetAppraisalWorkflowProgress/GetAppraisalWorkflowProgressQuery.cs
Modules/Workflow/Workflow/Workflow/Features/GetAppraisalWorkflowProgress/GetAppraisalWorkflowProgressQueryHandler.cs
Shared/Shared/Identity/DevAuthenticationHandler.cs
```

`AuthModule.cs` also carries the `AddMonitoringAnyPolicy` → `AddUserPermissionAnyPolicy` rename.
That is ours (the helper was never Monitoring-specific and the new appraisal policy needs the same
shape); it touches four Monitoring policy registrations, so call it out in the PR description.

### BE — ours, new files

```
Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalBrief/   (4 files)
Modules/Appraisal/Appraisal/Application/Features/Shared/AppraisalFieldScope.cs
Shared/Shared/Exceptions/ForbiddenException.cs
Database/Migration/Scripts/20260914090000_SeedData_AppraisalTrackingForCredit.sql
Database/Migration/Scripts/20260914090100_Revoke_AppraisalWorkspaceFromCreditRoles.sql
.claude/tasks/credit-appraisal-tracking.md
.claude/tasks/credit-appraisal-tracking-test-checklist.md
.claude/tasks/credit-appraisal-tracking-pr-plan.md   (this file)
```

### BE — ⚠ MIXED, hand-merge required

`Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs` — two independent hunks:

| hunk | owner | content |
|---|---|---|
| `@@ -19,2 +19,32 @@` | **another session** | `InvalidDataException` + `BadHttpRequestException 413` arms for upload limits |
| `@@ -61,2 +91,15 @@` | **ours** | `ForbiddenException` arm above a now-mute `UnauthorizedAccessException` arm |

Take the second hunk only. `ForbiddenException.cs` is a new file of ours, so the arm compiles on
its own; the other session's arms are independent of it.

### BE — NOT ours (do not touch)

Everything else, notably: `Bootstrapper/Api/*`, all `Create*/Update*/Get*Property*` handlers,
`AppraisalModule.cs`, `Appraisal.csproj`, `AppraisalSummaryAutoAttach*`, `RegenerateAppraisalSummary/`,
`Modules/Reporting/*`, `Modules/Document/*`, `Modules/Integration/*`, `Shared/Shared.Messaging/*`,
`Shared/Shared/Configurations/FileStorageConfiguration.cs`, `Database/Scripts/Maintenance/Patch*.sql`,
the new EF migration `20260916065116_AddBuildingFinalCostAndInsuranceOverrides*`, `deploy/README.md`,
`docs/deployment/*`, and the other sessions' `.claude/tasks/*.md`.

### FE — ours, whole file

```
src/features/creditBrief/                                            (new, 13 files)
src/features/appraisal/hooks/useCanOpenAppraisalWorkspace.ts         (new)
src/shared/utils/saveBlob.ts                                         (new)
src/styles/collateralScene.css                                       (new)
src/features/appraisal/components/summary/ActivityTrackingContent.tsx
src/features/appraisal/components/search/ActivityTrackingSlideOver.tsx
src/features/appraisal/pages/AppraisalListPage.tsx
src/features/appraisal/api/workflow.ts
src/features/common/historySearch/components/PinDetailDrawer.tsx
src/shared/components/RoleProtectedRoute.tsx
src/shared/components/SlideOverPanel.tsx      (the `full` width is ours; two collapsed lines are prettier)
src/main.tsx                                  (one line: the collateralScene.css import)
src/i18n/locales/{en,th,zh}/appraisal.json
docs/poc/credit-collateral-scene-mock.html
docs/poc/credit-activity-log-mock.html
docs/poc/credit-collateral-brief-mock.html
deleted: src/features/appraisal/components/summary/ActivityLogTable.tsx
deleted: src/features/appraisal/components/summary/WorkflowProgressTrack.tsx
```

Both deletions are safe: `grep -rn` across `src/` returns zero importers for either.

### FE — ⚠ MIXED, hand-merge required

| file | ours | theirs |
|---|---|---|
| `src/app/router.tsx` | the `/appraisals/:appraisalId` guard, `deniedPermission`, the comment block | one prettier reflow of the `MachinerySummaryPage` lazy import |
| `src/features/appraisal/api/appraisalSearch.ts` | the `saveBlob` call | the whole export rewrite onto `@shared/api/blobTransfer` |

`appraisalSearch.ts` is the awkward one: their `downloadBlob` rewrite and our `saveBlob` call are in
the SAME function. **Recommended: leave the export function as they wrote it and drop our `saveBlob`
edit from the PR.** Their version already fixes the timeout problem ours addressed, and `saveBlob`
is still needed by `creditBrief/components/DocumentList.tsx`, so the new file still earns its place.
Decide this before branching — it is the only place where our two changes overlap semantically.

### FE — NOT ours (do not touch)

`src/index.css`, `src/i18n/locales/{en,th,zh}/common.json` (all additions are dino / documentViewer /
transfer), `src/i18n/locales/th/request.json`, `AdministrationPage.tsx`, `src/shared/api/blobTransfer.ts`,
`src/shared/api/uploadProgress.ts`, `src/shared/components/UploadProgressPanel.tsx`,
`src/shared/components/dinoLoader/`, `src/shared/hooks/useBlobViewerTab.ts`, `src/shared/components/Input.tsx`,
`src/shared/components/inputs/TextInput.test.tsx`, `src/app/{AppraisalLayout,Layout,TaskLayout}.tsx`,
every `docs/poc/*.html` that is not one of our three, and all the other feature `api/*.ts` files.

---

## Proposed procedure

Two PRs, backend first — the frontend calls `/appraisals/{id}/brief`, which does not exist until the
backend merges.

### 1. Backend PR

```bash
cd ~/Developer/collateral-appraisal-system-api
git fetch origin
git worktree add ../cas-api-credit -b feature/credit-appraisal-tracking origin/main
```

Copy the "ours, whole file" and "ours, new files" lists into the worktree, then hand-apply our single
hunk of `CustomExceptionHandler.cs` on top of the worktree's clean copy.

```bash
cd ../cas-api-credit
git status --short | wc -l        # expect exactly the file count from the lists above
dotnet build collateral-appraisal-system-api.sln
```

Before committing, re-run the two proofs this change rests on, against the dev database:
the extracted brief SQL (8 result sets, no `Msg`), and the two migration scripts under
`SET PARSEONLY ON`.

### 2. Frontend PR

```bash
cd ~/Developer/collateral-appraisal-system-app
git fetch origin
git worktree add ../cas-app-credit -b feature/credit-appraisal-tracking origin/main
```

Same copy-then-hand-merge, with the `appraisalSearch.ts` decision above applied. Then:

```bash
cd ../cas-app-credit
npm ci
npx tsc --noEmit -p tsconfig.app.json     # expect no error in any file we touched
npx prettier --check $(git diff --name-only origin/main | grep -E '\.(ts|tsx|css|json)$')
npx eslint src/features/creditBrief src/features/appraisal src/shared
npx vite build
```

`origin/main` is 6 commits ahead of this working tree, so expect real merge work in
`ActivityTrackingContent.tsx` and `router.tsx` — verify against the fetched files, not from memory.

### Known traps (all of these have bitten in this repo)

- `npm run lint` uses `eslint .` and `prettier/prettier` is an **error**, so an unformatted line
  fails CI. Run prettier `--check` on the changed set before pushing.
- `rtk` rewrites some commands and can swallow `tsc` output — read the exit code, not the text.
- `git add` inside a worktree script with `set -e` aborts the whole script on a pathspec miss.
  Count the staged files before pushing.
- `react-ci` is red on `main` already (`hypothesisForm.test.ts`, broken since 24 Aug) — that failure
  is not ours.
- The two SQL scripts are journalled by DbUp. Once they run on an environment they never run again,
  so any correction after merge needs a NEW script, not an edit.

---

## PR description — points that must be in it

1. **`20260914090100` changes what ~1,250 real people can open.** Inquiry (1,043) and Report (203)
   lose `APPRAISAL_VIEW`. The API must be restarted (MenuTreeCache has no TTL) and those users must
   re-login (permissions are a token claim). Rollback is printed by the script itself.
2. The `AddMonitoringAnyPolicy` → `AddUserPermissionAnyPolicy` rename touches four Monitoring policy
   registrations.
3. `GET /appraisals/{id}/brief` deliberately has **no company row scope** — five attempts, four of
   which locked out entitled firms; cross-company separation lives at the discovery layer
   (`AppraisalFilterBuilder`, `QuickSearchQueryHandler`). Reasoning is in the handler's XML doc.
4. Known writer bug handed to the workflow owner: pool tasks stored with `AssignedType '1'`.
   Documented in `.claude/tasks/credit-appraisal-tracking.md`.
5. Backlog carried out of this change, deliberately not fixed — listed in the same file.
6. 20 review rounds; the manual test checklist is
   `.claude/tasks/credit-appraisal-tracking-test-checklist.md`.
