# Phase 5 — Application Layer (Meetings Feature Enhancement)

## Plan

### 5.0 Auth policies (AuthModule.cs)
- [x] Add `MeetingAdmin`, `MeetingSecretary`, `CommitteeMember` policies via `AddUserPermissionPolicy`
  - Permission keys: `MEETING_ADMIN`, `MEETING_SECRETARY`, `COMMITTEE_MEMBER`

### 5.1 New feature folders

- [x] `BulkCreateMeetings` — POST /meetings/bulk
- [x] `CutOffMeeting` — POST /meetings/{id}/cut-off
- [x] `SendInvitation` — POST /meetings/{id}/send-invitation
- [x] `ReleaseMeetingItem` — POST /meetings/{id}/items/{appraisalId}/release
- [x] `RouteBackMeetingItem` — POST /meetings/{id}/items/{appraisalId}/routeback (with validator)
- [x] `UpdateMeetingMembers` — AddMember, RemoveMember, ChangeMemberPosition (3 endpoints in one folder)
- [x] `UpdateMeetingAgenda` — PATCH /meetings/{id}/agenda
- [x] `CreateAcknowledgementQueueItem` — POST /meetings/acknowledgement-queue

### 5.2 Edit existing endpoints

- [x] `CreateMeetingEndpoint` — accept CommitteeId, StartAt?, EndAt?, Location?; load committee; call SnapshotCommittee + SetSchedule
- [x] `UpdateMeetingEndpoint` — accept StartAt?, EndAt?; call SetSchedule if provided
- [x] `ScheduleMeetingEndpoint` — alias/delegate to SendInvitation logic with [Obsolete] comment
- [x] `CancelMeetingEndpoint` — require non-empty reason (validator)
- [x] `GetMeetingDetailEndpoint` — project new fields + Members + grouped items by Kind/AppraisalType/AcknowledgementGroup
- [x] `GetMeetingsEndpoint` — project MeetingNo, StartAt, EndAt, InvitationSentAt, CutOffAt

### 5.3 Verification
- [x] `dotnet build` — 0 errors

## Phase 8 — Reviewer Fixes

- [x] C-1: `Cancel()` raises single `MeetingCancelledDomainEvent` with full `DecisionItems` payload; handler updated to batch-return queue items and always detach ack items.
- [x] C-2: `MeetingNoGenerator` race fixed with single MERGE + HOLDLOCK statement.
- [x] M-1: `EndMeetingEndpoint` uses `.RequireAuthorization("MeetingAdmin")`.
- [x] M-2: `End(DateTime now)` — clock removed from aggregate; caller passes `DateTime.UtcNow`.
- [x] M-5: `MeetingItem.WorkflowInstanceId` → `Guid?`, `ActivityId` → `string?`; EF config relaxed; `MeetingEndedDomainEventHandler` legacy path guarded; migration `RelaxAcknowledgementItemRequiredColumns` created.
- [x] Rec 8: `GetMeetingDetail` grouped shape → `List<MeetingItemGroupDto>` for both Decision and Ack buckets.
- [x] New unit test: `Cancel_on_meeting_with_zero_decision_items_still_raises_single_cancel_event_including_ack_detach`.
- [x] Build: 0 errors. Tests: 42/42 passed.

## Review

All Phase 5 tasks complete. Build: 0 errors, 6 warnings (all pre-existing).

### Files created
- `Meetings/Features/BulkCreateMeetings/BulkCreateMeetingsEndpoint.cs`
- `Meetings/Features/CutOffMeeting/CutOffMeetingEndpoint.cs`
- `Meetings/Features/SendInvitation/SendInvitationEndpoint.cs`
- `Meetings/Features/ReleaseMeetingItem/ReleaseMeetingItemEndpoint.cs`
- `Meetings/Features/RouteBackMeetingItem/RouteBackMeetingItemEndpoint.cs`
- `Meetings/Features/UpdateMeetingMembers/UpdateMeetingMembersEndpoint.cs`
- `Meetings/Features/UpdateMeetingAgenda/UpdateMeetingAgendaEndpoint.cs`
- `Meetings/Features/CreateAcknowledgementQueueItem/CreateAcknowledgementQueueItemEndpoint.cs`

### Files edited
- `Auth/Auth/AuthModule.cs` — 3 new policies
- `Meetings/Features/CreateMeeting/CreateMeetingEndpoint.cs`
- `Meetings/Features/UpdateMeeting/UpdateMeetingEndpoint.cs`
- `Meetings/Features/ScheduleMeeting/ScheduleMeetingEndpoint.cs`
- `Meetings/Features/CancelMeeting/CancelMeetingEndpoint.cs`
- `Meetings/Features/GetMeetings/GetMeetingsEndpoint.cs`
- `Meetings/Features/GetMeetingDetail/GetMeetingDetailEndpoint.cs`

### Key decisions
1. Auth policies used `AddUserPermissionPolicy` with `MEETING_ADMIN`, `MEETING_SECRETARY`, `COMMITTEE_MEMBER` permission keys — matches the project's existing claim-based permission pattern.
2. `ScheduleMeetingEndpoint` kept as alias to `SendInvitationCommand` with `[Obsolete]` attribute.
3. BulkCreate default schedule: 09:00–17:00 on the given date.
4. `GetMeetingDetailEndpoint` throws NotFoundException (handled by global exception handler) rather than returning null.
5. FluentValidation explicit `using FluentValidation;` added in Workflow files where validators needed (not globally imported in Workflow module).

---

# Task Summary Dashboard — Simplification (2026-04-14)

Plan file: `/Users/gky/.claude/plans/abstract-spinning-newell.md`

## Plan

- [x] Add `Database/Scripts/Views/Workflow/vw_UserTaskSummary.sql`
- [x] Rewrite `GetTaskSummaryQueryHandler` to query the view (single pivoted GROUP BY)
- [x] Verify no external consumer of `DailyTaskSummaries`
- [x] Delete `SlaBreachDashboardIntegrationEventHandler` (only touched the dropped table)
- [x] Remove `DailyTaskSummaries` writes from `TaskAssigned/TaskStarted/TaskCompleted/TaskClaimed` handlers — `TeamWorkloadSummaries` writes retained (out of scope)
- [x] Delete `DailyTaskSummary.cs` + `DailyTaskSummaryConfiguration.cs`; drop `DbSet` from `CommonDbContext`
- [x] Scaffold EF migration `DropDailyTaskSummaries` (Up drops table, Down recreates)
- [x] Solution build: 0 errors
- [ ] Parity-test endpoint output vs. pre-change behaviour (user to run)
- [ ] Apply migration (`dotnet ef database update --context CommonDbContext`) — user decision

## Review

### Why
`common.DailyTaskSummaries` was an event-maintained pre-aggregate feeding the Task Summary widget. Four integration-event handlers kept it in sync using MERGE+HOLDLOCK, but the `Completed` column was never written — handler queried `workflow.CompletedTasks` live instead. Worse, each counter was keyed to a different date column inside the table, so the UI's A/M/W/D filter produced inconsistent semantics across the four buckets.

### What changed
- Read path now runs one pivoted `GROUP BY` over a new view `workflow.vw_UserTaskSummary` (UNION of `PendingTasks` + `CompletedTasks`).
- Four handlers trimmed to their `TeamWorkloadSummaries` writes only.
- One handler (`SlaBreachDashboardIntegration...`) removed entirely.
- New migration drops `common.DailyTaskSummaries`.

### Files
- Added: `Database/Scripts/Views/Workflow/vw_UserTaskSummary.sql`
- Added: `Modules/Common/Common/Migrations/20260414160852_DropDailyTaskSummaries.cs` (+ Designer)
- Rewritten: `Modules/Common/Common/Application/Features/Dashboard/GetTaskSummary/GetTaskSummaryQueryHandler.cs`
- Trimmed: `Modules/Common/Common/Application/EventHandlers/Task{Assigned,Started,Completed,Claimed}DashboardIntegrationEventHandler.cs`
- Deleted: `Modules/Common/Common/Application/EventHandlers/SlaBreachDashboardIntegrationEventHandler.cs`
- Deleted: `Modules/Common/Common/Domain/ReadModels/DailyTaskSummary.cs`
- Deleted: `Modules/Common/Common/Infrastructure/Configurations/DailyTaskSummaryConfiguration.cs`
- Edited: `Modules/Common/Common/Infrastructure/CommonDbContext.cs` (removed DbSet)

### Out of scope
`TeamWorkloadSummaries`, `DailyAppraisalCounts`, `CompanyAppraisalSummaries`, `RequestStatusSummaries` — flagged for a follow-up audit.

### Security review
- SQL strings use static-constant period fragments; all runtime values bound via `DynamicParameters`.
- No new auth/authZ surface — endpoint unchanged.
- Migration `Down()` recreates the table schema for reversibility.

---

# Phase B — Income Calculation Engine (2026-04-14)

Plan file: `/Users/gky/.claude/plans/luminous-sauteeing-boole.md`

## Todo

- [x] Read all Phase A1-A3 domain files (IncomeAnalysis, IncomeSection, IncomeCategory, IncomeAssumption, IncomeMethod, IncomeSummary, Method01–14 detail records, MethodDetailSerializer)
- [x] Read LeaseholdCalculationService for pattern reference
- [x] Read TypeScript source (buildDiscountedCashFlowDerivedRules.ts) for all 14 method formulas
- [x] Create `IncomeCalculationResult.cs`
- [x] Create `IncomeCalculationService.cs` with all 14 method calculators
- [x] Add `ApplyCalculationResult` method to `IncomeAnalysis`
- [x] Update `PricingCalculationServiceResolver` — add "Income" case
- [x] Create `Tests/Unit/Appraisal.Tests/` test project
- [x] Write 30 unit tests covering all 14 methods + DCF summary + aggregation + aggregate mutation
- [x] Register test project in solution
- [x] Build: 0 errors
- [x] Tests: 30/30 passed

## Review

### Files created
- `/Modules/Appraisal/Appraisal/Domain/Services/IncomeCalculationResult.cs` — result DTO holding all server-computed year-indexed arrays and scalar finals
- `/Modules/Appraisal/Appraisal/Domain/Services/IncomeCalculationService.cs` — calculation engine; implements IPricingCalculationService; 14 method calculators + DCF aggregation pipeline
- `/Tests/Unit/Appraisal.Tests/Appraisal.Tests.csproj` — new test project
- `/Tests/Unit/Appraisal.Tests/Domain/Services/IncomeCalculationServiceTests.cs` — 30 unit tests

### Files modified
- `/Modules/Appraisal/Appraisal/Domain/Appraisals/Income/IncomeAnalysis.cs` — added `ApplyCalculationResult(IncomeCalculationResult)` and `SerializeArray` helper
- `/Modules/Appraisal/Appraisal/Domain/Services/PricingCalculationServiceResolver.cs` — added `"Income" => _income` case

### Deviations from TS reference
1. **Direct capitalization formula**: TS `totalNet / capitalizeRate / 100` = `totalNet / (capRate × 100)` due to left-associative division — financially wrong. Backend uses `totalNet / (capRate / 100)` which is the correct formula and consistent with the DCF path. Called out in test comments.
2. **Method 08 cross-reference**: Method 08 needs `totalSaleableAreaDeductByOccRate` from the first method-01 (or method-02) assumption. The TS does this via a live form-state lookup; the backend collects this during the method-01/02 calculation pass via a `crossRef` dictionary keyed by method code.
3. **Method 11 cross-reference**: Same pattern — needs method-06's `totalSaleableAreaDeductByOccRate`. Same solution.
4. **Method 10**: TS delegates entirely to pre-computed `totalPropertyTax[]` stored in the detail JSON. Backend honours the same stored array and repeats the last value if the array is shorter than `TotalNumberOfYears`.

### Build status
- Solution: 0 errors, warnings are pre-existing
- Tests: 30/30 passed

---

# Phase C — Application Layer: Income Analysis Features (2026-04-14)

Plan file: `/Users/gky/.claude/plans/luminous-sauteeing-boole.md`

## Todo

- [x] Update `PricingAnalysisRepository.GetByIdWithAllDataAsync` — add IncomeAnalysis include chain
- [x] Create DTOs in `Appraisal.Contracts/Appraisals/Dto/Income/` (6 files)
- [x] Create `IncomeAnalysisMapper.cs` — static domain → DTO mapper
- [x] Create `SaveIncomeAnalysis/` feature (5 files)
- [x] Create `GetIncomeAnalysis/` feature (4 files)
- [x] Create `InitializeIncomeAnalysis/` feature (5 files)
- [x] Add `Parameter` project reference to `Appraisal.csproj` for cross-module MediatR call
- [x] Add `ConflictException` to `Shared/Shared/Exceptions/` + register in `CustomExceptionHandler`
- [x] Build: 0 errors
- [x] Tests: 30/30 passed

## Review

### Files created
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeAnalysisDto.cs`
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeSectionDto.cs`
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeCategoryDto.cs`
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeAssumptionDto.cs`
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeMethodDto.cs` — uses `JsonElement` for passthrough Detail
- `Modules/Appraisal/Appraisal.Contracts/Appraisals/Dto/Income/IncomeSummaryDto.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/IncomeAnalysisMapper.cs` — static mapper; no Mapster/AutoMapper
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisRequest.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisCommand.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisCommandHandler.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisEndpoint.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisResult.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/SaveIncomeAnalysis/SaveIncomeAnalysisResponse.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/GetIncomeAnalysis/GetIncomeAnalysisQuery.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/GetIncomeAnalysis/GetIncomeAnalysisQueryHandler.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/GetIncomeAnalysis/GetIncomeAnalysisEndpoint.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/GetIncomeAnalysis/GetIncomeAnalysisResult.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/GetIncomeAnalysis/GetIncomeAnalysisResponse.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisRequest.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisCommand.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisCommandHandler.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisEndpoint.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisResult.cs`
- `Modules/Appraisal/Appraisal/Application/Features/PricingAnalysis/InitializeIncomeAnalysis/InitializeIncomeAnalysisResponse.cs`
- `Shared/Shared/Exceptions/ConflictException.cs`

### Files modified
- `Modules/Appraisal/Appraisal/Infrastructure/Repositories/PricingAnalysisRepository.cs` — added IncomeAnalysis → Sections → Categories → Assumptions include chain
- `Modules/Appraisal/Appraisal/Appraisal.csproj` — added `Parameter.csproj` reference
- `Shared/Shared/Exceptions/Handler/CustomExceptionHandler.cs` — added ConflictException → 409 mapping

### Endpoints added
- `PUT  /pricing-analysis/{pricingAnalysisId}/methods/{methodId}/income-analysis`
- `GET  /pricing-analysis/{pricingAnalysisId}/methods/{methodId}/income-analysis`
- `POST /pricing-analysis/{pricingAnalysisId}/methods/{methodId}/income-analysis:initialize`

### Build + test status
- Build: 0 errors, warnings pre-existing
- Unit tests: 30/30 passed (Appraisal.Tests)

---

## Code-Review Follow-Up Fixes (income-pricing-analysis)

### Fix 1 — Method-10 server-side bracket derivation
- [x] Added `Parameter.Contracts/PricingParameters/GetPricingTaxBracketsQuery.cs` — cross-module query + result records + `TaxBracketDto`
- [x] Added `Parameter/PricingParameters/Features/GetPricingTaxBrackets/GetPricingTaxBracketsQueryHandler.cs` — reads from `ParameterDbContext.PricingParameterTaxBrackets`, ordered by Tier
- [x] Modified `IncomeCalculationService`:
  - Added dual constructor (no-arg → NullLogger; DI-injected logger)
  - Added `Calculate(analysis, taxBrackets)` overload; parameterless overload delegates with null brackets + warning log
  - `ComputeMethod10` now accepts brackets; when present derives tax as `TotalPropertyPrice[y] × matchingBracket.TaxRate`; falls back to client array when absent
  - `DerivePropertyTax(price, brackets)` — public static, flat-rate lookup, iteration order (Tier 1 first), returns 0 when no bracket matches
- [x] Modified `SaveIncomeAnalysisCommandHandler` — injected `IncomeCalculationService` + `ISender`; loads brackets via `GetPricingTaxBracketsQuery` before Calculate
- [x] Modified `InitializeIncomeAnalysisCommandHandler` — injected `IncomeCalculationService`; loads brackets before Calculate

### Fix 2 — DI for IncomeCalculationService
- [x] `AppraisalModule.cs` — `services.AddScoped<IncomeCalculationService>()` + `PricingCalculationServiceResolver` changed from Singleton to Scoped
- [x] `PricingCalculationServiceResolver` — constructor-injected `IncomeCalculationService`; all other calc services remain `new()` (Option A, minimal blast radius, documented in code comment)

### New tests (9 added, total 43/43 green)
- `Method10_WithBrackets_DerivesTaxFromTotalPropertyPrice` — overrides client values with bracket-derived tax
- `Method10_WithBrackets_FallsBackToClientTaxWhenBracketsNull` — null brackets → client array preserved
- `Method10_WithBrackets_FallsBackToClientTaxWhenBracketsEmpty` — empty list → client array preserved
- `DerivePropertyTax_PriceBelowLowestTier_ReturnsZero` — 9_999_999 → 0
- `DerivePropertyTax_PriceAtTier1Min_ReturnsTier1Rate` — 10_000_000 × 0.02 = 200_000
- `DerivePropertyTax_PriceAtTier1Max_StaysInTier1` — 50_000_000 × 0.02 = 1_000_000
- `DerivePropertyTax_PriceAtTier2Min_UsesTier2Rate` — 50_000_001 × 0.03
- `DerivePropertyTax_PriceInTopTierNoUpperBound_UsesTopRate` — 999_999_999_999 × 0.10
- `Method10_WithBrackets_Tier3Rate` — 80_000_000 × 0.05 = 4_000_000

