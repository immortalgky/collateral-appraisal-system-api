# TODO: Replace Guid.NewGuid() with Guid.CreateVersion7()

## Background
UUID v7 (RFC 9562) is time-ordered and sortable, providing better database performance for indexed columns compared to UUID v4 (random). Since this project uses .NET 9.0, `Guid.CreateVersion7()` is available.

## Progress

### Request Module (4 files)
- [x] `Modules/Request/Request/Domain/Requests/Request.cs` - Entity ID (already migrated)
- [x] `Modules/Request/Request/Domain/RequestComments/RequestComment.cs` - Entity ID
- [x] `Modules/Request/Request/Domain/Requests/RequestDocument.cs` - Entity ID
- [x] `Modules/Request/Request/Domain/RequestTitles/TitleDocument.cs` - Entity ID

### Shared Infrastructure (2 files)
- [x] `Shared/Shared/DDD/IDomainEvent.cs` - EventId
- [x] `Shared/Shared.Messaging/Events/IntegrationEvent.cs` - EventId

### Document Module (1 file)
- [x] `Modules/Document/Document/Application/Services/DocumentService.cs` - Document ID

### Notification Module (2 files)
- [x] `Modules/Notification/Notification/Infrastructure/Seed/NotificationDataSeed.cs` - Seed data IDs (lines 29, 45, 64)
- [x] `Modules/Notification/Notification/Application/Services/NotificationService.cs` - Notification ID

### Appraisal Module (43 files)
- [x] Domain/Appraisals entities (25 files)
- [x] Domain/Quotations entities (6 files)
- [x] Domain/MarketComparables entities (4 files)
- [x] Domain/DocumentRequirements entities (2 files)
- [x] Domain/Committees entities (4 files)
- [x] Domain/Settings entities (2 files)

### Workflow Module (11 files)
- [x] Workflow models (WorkflowDefinition, WorkflowInstance, etc.)
- [x] WorkflowImportExportService (import IDs)
- [x] PendingTask model
- [x] WorkflowAuditEvent (IWorkflowAuditService)

## Verification
- [x] Build: `dotnet build` - **PASSED** (0 errors, 6 pre-existing warnings)
- [ ] Run tests: `dotnet test` (optional)

## Review

### Summary
Successfully replaced `Guid.NewGuid()` with `Guid.CreateVersion7()` across 60+ entity ID generation locations in the codebase. This migration provides time-ordered UUIDs that improve database index performance.

### Files Modified (61 total)

**Request Module (3 files):**
- `RequestComment.cs` - line 27
- `RequestDocument.cs` - line 41
- `TitleDocument.cs` - line 32

**Shared Infrastructure (2 files):**
- `IDomainEvent.cs` - line 7
- `IntegrationEvent.cs` - line 5

**Document Module (1 file):**
- `DocumentService.cs` - line 38

**Notification Module (2 files):**
- `NotificationDataSeed.cs` - lines 29, 45, 64
- `NotificationService.cs` - line 140

**Appraisal Module (43 files):**
- Domain/Appraisals: Appraisal, AppraisalAssignment, PricingAnalysis, CondoAppraisalAreaDetail, MachineryAppraisalDetail, VesselAppraisalDetail, AppraisalGallery, ComparableAdjustment, BuildingDepreciationDetail, LawAndRegulationImage, AppraisalFeePaymentHistory, AppraisalComparable, PropertyPhotoMapping, BuildingAppraisalSurface, PropertyValuation, AppraisalFee, GroupValuation, AppraisalFeeItem, Appointment, LawAndRegulation, AppraisalReview, AdjustmentTypeLookup, VehicleAppraisalDetail, ValuationAnalysis, AppointmentHistory
- Domain/Quotations: CompanyQuotation, QuotationRequest, QuotationInvitation, CompanyQuotationItem, QuotationRequestItem, QuotationNegotiation
- Domain/MarketComparables: MarketComparable, MarketComparableTemplate, MarketComparableImage, MarketComparableFactor
- Domain/DocumentRequirements: DocumentRequirement, DocumentType
- Domain/Committees: CommitteeVote, CommitteeApprovalCondition, CommitteeMember, Committee
- Domain/Settings: AutoAssignmentRule, AppraisalSettings

**Workflow Module (11 files):**
- WorkflowDefinition, WorkflowInstance, WorkflowExternalCall, WorkflowOutbox, WorkflowDefinitionVersion, WorkflowExecutionLog, WorkflowBookmark
- WorkflowImportExportService (2 locations)
- PendingTask
- IWorkflowAuditService (WorkflowAuditEvent)

### Files Intentionally Skipped (per plan)
- OAuth client IDs (Auth module) - security
- Correlation IDs in NotificationDataSeed metadata - tracing
- Operation IDs in workflow resilience/degradation - tracing
- Idempotency keys - must be random
- Fork/bookmark keys - branch tracking
- Shadow/fallback instance IDs - testing
- Expression helpers - utility code

### Technical Notes
1. `Guid.CreateVersion7()` generates a UUID v7 which embeds a timestamp, making IDs chronologically sortable
2. This improves B-tree index efficiency for primary keys in SQL Server
3. No schema changes required - still stores as `uniqueidentifier`
4. Backward compatible - existing UUID v4 data remains valid

### Build Status
- **Build succeeded** with 0 errors
- 6 pre-existing NuGet warnings (unrelated to this change)
