# GET /appraisals/{appraisalId}/workflow-progress — Implementation Plan

## Tasks

- [x] Read reference files (GetAppraisalsQueryHandler, GetTaskHistoryQueryHandler, IUserLookupService, Appraisal.csproj)
- [x] Create `GetWorkflowProgressQuery.cs` — query record + response/DTO classes
- [x] Create `GetWorkflowProgressQueryHandler.cs` — Dapper queries + BFS step builder + user lookup
- [x] Create `GetWorkflowProgressEndpoint.cs` — Carter endpoint
- [x] Edit `Appraisal.csproj` — add Auth.Contracts ProjectReference
- [x] Build solution to confirm no compile errors — 0 errors, 12 pre-existing warnings

## Files

| File | Action |
|---|---|
| `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetWorkflowProgress/GetWorkflowProgressQuery.cs` | CREATE |
| `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetWorkflowProgress/GetWorkflowProgressQueryHandler.cs` | CREATE |
| `Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetWorkflowProgress/GetWorkflowProgressEndpoint.cs` | CREATE |
| `Modules/Appraisal/Appraisal/Appraisal.csproj` | EDIT — add Auth.Contracts ref |

## Key design decisions

- Parallel SQL via `Task.WhenAll` for instance + activity log queries (definition waits on instance result)
- BFS traversal on parsed JsonDefinition to get ordered activity sequence
- Route detection: completedActivityIds → prefix scan → fallback AssignmentType
- Step deduplication via last-group tracking
- User display: IUserLookupService for AssignedType == "1" only
- All SQL params via DynamicParameters — no string interpolation
