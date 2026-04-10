# Task: Implement GetRequestById Part 2 & 3

## Goal
Refactor Documents to use owned entities (OwnsMany) and implement complete RequestTitle mapping in GetRequestById query handler.

## Part 2: Change Documents to Owned Entities

### Configuration Changes

- [x] Refactor RequestDocumentConfiguration to use IOwnedEntityConfiguration
  - Change from IEntityTypeConfiguration to IOwnedEntityConfiguration
  - Update Configure method signature to use OwnedNavigationBuilder
  - Add ToTable() call to keep same table name
  - Add WithOwner() and HasForeignKey()
  - Add necessary using statements

- [x] Refactor TitleDocumentConfiguration to use IOwnedEntityConfiguration
  - Change from IEntityTypeConfiguration to IOwnedEntityConfiguration
  - Update Configure method signature to use OwnedNavigationBuilder
  - Keep ToTable() call
  - Add WithOwner() and HasForeignKey()

- [x] Update RequestConfiguration.cs Documents relationship
  - Replace HasMany relationship with OwnsMany
  - Inline the RequestDocumentConfiguration

- [x] Update RequestTitleConfiguration.cs Documents relationship
  - Replace HasMany relationship with OwnsMany
  - Inline the TitleDocumentConfiguration

- [x] Update RequestDbContext.cs
  - Remove DbSet<RequestDocument> line
  - Keep DbSet<RequestTitle>

## Part 3: Add RequestTitle Mapping and Update Handler

### DTO Extension Methods

- [x] Add ToDto extension for TitleDocument
  - Maps TitleDocument entity to RequestTitleDocumentDto

- [x] Add ToDto extension for RequestTitle
  - Maps RequestTitle base entity to RequestTitleDto
  - Uses pattern matching to handle 11 derived types
  - Maps type-specific properties for each title type

### Update Query Handler

- [x] Update GetRequestByIdQueryHandler
  - Remove .Include(r => r.Documents) since Documents are owned
  - Add query for RequestTitles with AsNoTracking
  - Map titles using ToDto() extension method
  - Populate Titles property in result

## Testing

- [x] Run dotnet build to verify compilation
- [x] Create and apply new migration
- [ ] Test GetRequestById endpoint
- [ ] Verify all title types map correctly
- [ ] Verify documents are loaded for both Request and RequestTitle

## Review

### Implementation Summary

Successfully implemented Part 2 and Part 3 of the GetRequestById enhancement plan. The implementation refactored document entities to use the owned entity pattern (OwnsMany) and added complete RequestTitle mapping with all 11 title type variants.

### Key Changes

**Part 2: Documents to Owned Entities**

1. **RequestDocumentConfiguration.cs** - Converted to `IOwnedEntityConfiguration<Domain.Requests.Request, RequestDocument>`
   - Changed from `IEntityTypeConfiguration` to owned entity pattern
   - Added `ToTable("RequestDocuments")` to preserve table name
   - Added `WithOwner().HasForeignKey(d => d.RequestId)`
   - Added `using Shared.Data.Extensions`

2. **TitleDocumentConfiguration.cs** - Converted to `IOwnedEntityConfiguration<RequestTitle, TitleDocument>`
   - Changed from `IEntityTypeConfiguration` to owned entity pattern
   - Kept `ToTable("RequestTitleDocuments")` for backward compatibility
   - Added `WithOwner().HasForeignKey(d => d.TitleId)`
   - Removed redundant `builder.Property(x => x.TitleId)` line

3. **RequestConfiguration.cs** - Changed Documents relationship
   - Replaced `builder.HasMany(p => p.Documents)` with `builder.OwnsMany(r => r.Documents, doc => new RequestDocumentConfiguration().Configure(doc))`

4. **RequestTitleConfiguration.cs** - Changed Documents relationship
   - Replaced `builder.HasMany(p => p.Documents)` with `builder.OwnsMany(t => t.Documents, doc => new TitleDocumentConfiguration().Configure(doc))`

5. **RequestDbContext.cs** - Removed `DbSet<RequestDocument>` since documents are now owned entities

**Part 3: RequestTitle Mapping**

6. **DtoExtensions.cs** - Added two new ToDto extension methods:
   - `ToDto(this TitleDocument doc)` - Maps TitleDocument entity to RequestTitleDocumentDto
   - `ToDto(this RequestTitle title)` - Maps RequestTitle with pattern matching for 11 title types:
     - TitleLand
     - TitleBuilding
     - TitleLandBuilding
     - TitleCondo
     - TitleLeaseAgreementLand
     - TitleLeaseAgreementBuilding
     - TitleLeaseAgreementLandBuilding
     - TitleLeaseAgreementCondo
     - TitleVehicle
     - TitleVessel
     - TitleMachine
   - Includes proper null safety for all value objects (TitleDeedInfo, LandLocationInfo, etc.)

7. **GetRequestByIdQueryHandler.cs** - Updated query handler
   - Removed `.Include(r => r.Documents)` - owned entities auto-load
   - Added separate query for RequestTitles with AsNoTracking
   - Changed `Titles = null` to `Titles = titles.Select(t => t.ToDto()).ToList()`
   - Now returns complete title data with all type-specific properties

### Database Migration

- Migration: `20260201040047_RefactorDocumentsToOwnedEntities`
- **Result**: Empty migration (no schema changes)
- This confirms backward compatibility - table structure remains identical
- The `ToTable()` calls successfully preserved existing table names

### Files Modified (7 files)

1. `/Modules/Request/Request/Infrastructure/Configurations/RequestDocumentConfiguration.cs`
2. `/Modules/Request/Request/Infrastructure/Configurations/TitleDocumentConfiguration.cs`
3. `/Modules/Request/Request/Infrastructure/Configurations/RequestConfiguration.cs`
4. `/Modules/Request/Request/Infrastructure/Configurations/RequestTitleConfiguration.cs`
5. `/Modules/Request/Request/Infrastructure/RequestDbContext.cs`
6. `/Modules/Request/Request/Extensions/DtoExtensions.cs`
7. `/Modules/Request/Request/Application/Features/Requests/GetRequestById/GetRequestByIdQueryHandler.cs`

### Build Status

- **Solution builds successfully: 0 errors**
- Pre-existing warnings only (10 warnings from other files, none from our changes)
- Build time: 1.77 seconds

### Technical Benefits

1. **Cleaner Architecture**: Documents are now properly modeled as owned entities (no identity outside parent)
2. **Simpler Queries**: No need for explicit `.Include()` calls - owned entities auto-load
3. **Type Safety**: Pattern matching ensures all 11 title types are properly mapped
4. **Null Safety**: All value objects are accessed with null-conditional operators
5. **Backward Compatible**: Zero database schema changes - existing data unaffected
6. **Performance**: AsNoTracking queries for read-only operations

### Next Steps

1. Test the GetRequestById endpoint with actual requests
2. Verify title documents load correctly for all 11 title types
3. Test with requests that have multiple titles and documents
4. Consider adding integration tests for the new mapping logic
