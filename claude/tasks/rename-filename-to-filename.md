# Task: Rename `Filename` to `FileName` in TitleDocument

## Summary
Rename the `Filename` property to `FileName` (proper PascalCase) in the `TitleDocument` entity and all related files.

## Todo Items

- [x] 1. Update `TitleDocument.cs` - Domain entity and `TitleDocumentData` record
- [x] 2. Update `RequestTitleDocumentDto.cs` - DTO property
- [x] 3. Update `DtoExtensions.cs` - ToDto and ToTitleDocumentData mappings
- [x] 4. Update `RequestReadModels.cs` - `RequestTitleDocumentRow` property
- [x] 5. Update `GetLinkRequestTitleDocumentByIdResult.cs` - Result record parameter
- [x] 6. Update `GetLinkRequestTitleDocumentByIdQueryHandler.cs` - Property reference
- [x] 7. Update `CreateRequestService.cs` - Property reference
- [x] 8. Update `RequestSyncService.cs` - Property comparison
- [x] 9. Update `MappingConfiguration.cs` - Mapster mappings
- [x] 10. Update `RequestSubmittedEventHandler.cs` - Property reference
- [x] 11. Update `TitleDocumentConfiguration.cs` - EF property mapping
- [x] 12. Add database migration to rename column
- [x] 13. Build and verify solution

## Review

### Summary of Changes

This refactoring standardized the property name from `Filename` to `FileName` (proper PascalCase naming convention) across the entire codebase.

### Files Modified

| File | Changes |
|------|---------|
| `TitleDocument.cs` | Renamed property in entity class (line 11) and `TitleDocumentData` record (line 82). Updated all method references in Create(), Update(), UpdateDraft() |
| `RequestTitleDocumentDto.cs` | Renamed DTO property (line 9) |
| `DtoExtensions.cs` | Updated ToDto() mapping (line 122) and ToTitleDocumentData() mapping (line 500) |
| `RequestReadModels.cs` | Renamed `RequestTitleDocumentRow` property (line 141) |
| `GetLinkRequestTitleDocumentByIdResult.cs` | Renamed record parameter (line 8) |
| `GetLinkRequestTitleDocumentByIdQueryHandler.cs` | Updated property reference (line 23) |
| `CreateRequestService.cs` | Updated property reference (line 136) |
| `RequestSyncService.cs` | Updated property comparison in HasDocumentChanges() (line 176) |
| `MappingConfiguration.cs` | Updated Mapster mappings (lines 100, 116) |
| `RequestSubmittedEventHandler.cs` | Updated property reference (line 74) |
| `TitleDocumentConfiguration.cs` | Updated EF property mapping (line 17) |

### Migration Created

- `20260201172507_RenameTitleDocumentFilenameToFileName.cs`
- Uses `RenameColumn` which safely renames the column in-place without data loss

### Build Status

✅ Build succeeded with 0 errors (only pre-existing warnings)

### Next Steps

To apply the database migration:
```bash
dotnet ef database update --project Modules/Request/Request --startup-project Bootstrapper/Api --context RequestDbContext
```
