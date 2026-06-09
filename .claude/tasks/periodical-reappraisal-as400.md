# Periodical Reappraisal (AS400) — Backend Implementation Plan (Phases A–C)

## Phase A — Domain + Schema
- [x] `ReappraisalCandidateStatus` enum (string-backed: Pending/Consumed/Deleted)
- [x] `ReappraisalCandidate` entity in `Request.Domain.Reappraisal`
- [x] `ReappraisalCandidateConfiguration` in `Request.Infrastructure.Configurations`
- [x] `DbSet<ReappraisalCandidate>` added to `RequestDbContext`
- [x] EF migration: table + unique index `(SourceFileDate, CollateralId, SurveyNo)`
- [x] EF migration (raw SQL): persisted GeoPoint + spatial index on `request.ReappraisalCandidates`
- [x] `AppraisalGroupNumber` property on `Request` aggregate + `SetAppraisalGroupNumber()`
- [x] EF column in `RequestConfiguration` + migration for `AppraisalGroupNumber`
- [x] `IReappraisalGroupNumberGenerator` + `ReappraisalGroupNumberGenerator` impl
- [x] Register in `RequestModule.cs`
- [x] `dotnet build` — Phase A ✅

## Phase B — Ingestion
- [x] `IReappraisalFileSource` + `ReappraisalFileInfo` in `Shared/Shared/Reappraisal/`
- [x] `LocalFolderFileSource` (reads from configurable directory)
- [x] `SftpFileSource` (Renci.SshNet — add package to Shared.csproj)
- [x] Config classes: `ReappraisalOptions`, `LocalFolderOptions`, `SftpOptions`
- [x] `FixedWidthReappraisalParser` in `Request/Infrastructure/Reappraisal/`
- [x] `ReappraisalIngestionJob` in `Request/Infrastructure/Reappraisal/`
- [x] Register Hangfire monthly job in `Program.cs`
- [x] Register file source + options in `RequestModule.cs`
- [x] `dotnet build` — Phase B ✅

## Phase C — Queries/Endpoints
- [x] `vw_ReappraisalCandidates.sql` in `Database/Scripts/Views/Request/`
- [x] `GetReappraisalCandidatesQuery` + handler (list + filters + DynamicParameters + QueryPaginatedAsync)
- [x] `GetReappraisalCandidateByIdQuery` + handler (detail + nearby group list via STDistance)
- [x] `InitiateReappraisalCommand` + handler (group number → CreateRequestService × N → mark Consumed)
- [x] `DeleteReappraisalCandidateCommand` + handler (soft-delete)
- [x] Endpoints in `Bootstrapper/Api/Endpoints/Reappraisal/`
- [x] `dotnet build` — Phase C ✅

## TODO(confirm) items left as comments in code
1. Reappraisal purpose code — placeholder `"03"` (confirm with business)
2. Group number prefix/format — `"G"` / `{YY}G{000001}` (confirm with business)
3. Hangfire cron day/time — `Cron.Monthly()` first-of-month 01:00 Bangkok (confirm)
4. Total Outstanding filter omitted — field not in interface (candidates: MortgageAmount/FacilityLimit/CurrentValue)
5. PrevAppraisalId resolution fallback — NULL when SurveyNo has no in-system match (confirm if blocking for initiate)

## Review
Phase A adds 5 new files + modifies 3. Phase B adds 8 new files + modifies 3. Phase C adds 13 new files + modifies 2.
All cross-schema reads (lat/lon from appraisal schema) are Dapper-only (no EF nav cross-module). No dotnet ef database update run.
