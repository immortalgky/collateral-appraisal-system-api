# Task: Store Request Properties in Appraisal Module

## Todo Items

- [x] Add properties to `RequestSubmittedIntegrationEvent`
- [x] Populate new properties in Request module's `RequestSubmittedEventHandler`
- [x] Add new properties to Appraisal domain entity and update `Create` factory
- [x] Update `IAppraisalCreationService` interface and `AppraisalCreationService` implementation
- [x] Update `RequestSubmittedIntegrationEventHandler` in Appraisal module
- [x] Configure EF Core for new Appraisal properties and add migration
- [x] Update API response DTOs (GetById and List)
- [x] Verify no breaking changes to existing callers
- [x] Commit and push changes

## Review

### Summary of Changes

Added 6 new properties from the Request module to be carried through the `RequestSubmittedIntegrationEvent` and stored on the Appraisal entity:

| Property | Type | Purpose |
|---|---|---|
| **Priority** | `string?` | Already existed on Appraisal but was hardcoded to "Normal". Now uses actual request priority. |
| **IsPma** | `bool` | Indicates PMA (Professional Mortgage Appraisal) request. Used for workflow routing. |
| **Purpose** | `string?` | Purpose of the appraisal request. |
| **Channel** | `string?` | Channel through which the request was made. |
| **BankingSegment** | `string?` | Banking segment from loan detail. Used for workflow routing. |
| **FacilityLimit** | `decimal?` | Loan facility limit amount. Used for workflow routing and fee tier selection. |

### Files Modified (12 files, +199 lines, -6 lines)

1. **`Shared/Shared.Messaging/Events/RequestSubmittedIntegrationEvent.cs`** - Added 6 new properties to the integration event contract
2. **`Modules/Request/Request/Application/EventHandlers/Request/RequestSubmittedEventHandler.cs`** - Maps new properties from Request aggregate to integration event
3. **`Modules/Appraisal/Appraisal/Domain/Appraisals/Appraisal.cs`** - Added 5 new domain properties + updated constructor and `Create` factory (Priority already existed)
4. **`Modules/Appraisal/Appraisal/Application/Services/IAppraisalCreationService.cs`** - Added new parameters to interface method
5. **`Modules/Appraisal/Appraisal/Application/Services/AppraisalCreationService.cs`** - Passes new properties to `Appraisal.Create`, uses actual priority instead of hardcoded "Normal"
6. **`Modules/Appraisal/Appraisal/Application/EventHandlers/RequestSubmittedIntegrationEventHandler.cs`** - Passes new event properties to creation service
7. **`Modules/Appraisal/Appraisal/Infrastructure/Configurations/AppraisalAggregateConfiguration.cs`** - EF Core column config with max lengths and decimal precision
8. **`Modules/Appraisal/Appraisal/Infrastructure/Migrations/20260331080000_AddRequestPropertiesToAppraisal.cs`** - Migration to add 5 columns to Appraisals table
9. **`Modules/Appraisal/Appraisal/Infrastructure/Migrations/AppraisalDbContextModelSnapshot.cs`** - Updated model snapshot
10. **`Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdResult.cs`** - Added properties to detail query result
11. **`Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisalById/GetAppraisalByIdResponse.cs`** - Added properties to detail API response
12. **`Modules/Appraisal/Appraisal/Application/Features/Appraisals/GetAppraisals/GetAppraisalsResult.cs`** - Added key properties to list DTO

### Key Design Decisions

- All new parameters use default values (`null`/`false`) in `Appraisal.Create` to avoid breaking existing callers like `CreateAppraisalCommandHandler`
- `FacilityLimit` uses `decimal(18,2)` for SQL Server to match financial precision
- `IsPma` defaults to `false` in the migration for existing rows
- The SQL view `appraisal.vw_AppraisalDetail` will need to be updated to include the new columns if it doesn't use `SELECT *`

### Security Check

- No sensitive information exposed (no passwords, tokens, or PII beyond what already exists in the system)
- All new properties are business metadata, not authentication/authorization data
- `FacilityLimit` is financial data but is already available elsewhere in the system (Request module)
- No SQL injection risk (all queries use parameterized access via EF Core/Dapper)

### Note

- The SQL view `appraisal.vw_AppraisalDetail` may need updating to include the new columns. If the view uses explicit column list (not `SELECT *`), it will need to be modified separately.
- After deployment, run the migration: `dotnet ef database update --project Modules/Appraisal/Appraisal --startup-project Bootstrapper/Api`
