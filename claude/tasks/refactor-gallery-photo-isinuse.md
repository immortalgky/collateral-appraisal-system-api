# Refactor MarketComparableImage to GalleryPhotoId + Add IsInUse Tracking

## Part 1: Refactor MarketComparableImage (DocumentId → GalleryPhotoId)
- [x] Domain Entity: `MarketComparableImage.cs` - Rename `DocumentId` → `GalleryPhotoId`
- [x] Aggregate Root: `MarketComparable.cs` - Rename `AddImage()` parameter
- [x] EF Config: `MarketComparableImageConfiguration.cs` - Rename property + index
- [x] Command: `AddMarketComparableImageCommand.cs` - Rename field
- [x] Request: `AddMarketComparableImageRequest.cs` - Rename field
- [x] Handler: `AddMarketComparableImageCommandHandler.cs` - Use `GalleryPhotoId`
- [x] Endpoint: `AddMarketComparableImageEndpoint.cs` - Update mapping
- [x] Query DTO: `GetMarketComparableByIdResult.cs` - `ImageDto.DocumentId` → `GalleryPhotoId`
- [x] Query Handler: `GetMarketComparableByIdQueryHandler.cs` - Update mapping

## Part 2: Replace IsUsedInReport with IsInUse on AppraisalGallery
- [x] Domain Entity: `AppraisalGallery.cs` - Remove `IsUsedInReport`/`ReportSection`, add `IsInUse`, `MarkAsInUse()`, `MarkAsNotInUse()`
- [x] EF Config: `AppraisalGalleryConfiguration.cs` - Remove `ReportSection`, rename to `IsInUse`
- [x] Repository Interface: `IAppraisalGalleryRepository.cs` - Add `IsPhotoLinkedAnywhereAsync()`
- [x] Repository Impl: `AppraisalGalleryRepository.cs` - Implement with single SQL checking all 4 linkage tables
- [x] Query DTO: `GetGalleryPhotosResult.cs` - Replace `IsUsedInReport`/`ReportSection` with `IsInUse`
- [x] Query Handler: `GetGalleryPhotosQueryHandler.cs` - Pass `p.IsInUse`
- [x] Delete `MarkPhotoForReport/` folder (5 files)
- [x] Delete `UnmarkPhotoFromReport/` folder (4 files)

## Part 3: Update Handlers to Manage IsInUse
### Add-linkage handlers (mark IsInUse = true)
- [x] `LinkPhotoToPropertyCommandHandler` - Add `photo.MarkAsInUse()`
- [x] `AssignPhotoToTopicCommandHandler` - Add `photo.MarkAsInUse()` when topics added; check & unmark when all removed
- [x] `AddAppendixDocumentCommandHandler` - Inject gallery repo, load photo, `MarkAsInUse()`
- [x] `AddMarketComparableImageCommandHandler` - Inject gallery repo, load photo, `MarkAsInUse()`

### Remove-linkage handlers (check then possibly unmark)
- [x] `UnlinkPhotoFromPropertyCommandHandler` - After delete: check `IsPhotoLinkedAnywhereAsync()` → if false: `MarkAsNotInUse()`
- [x] `RemoveAppendixDocumentCommandHandler` - Get GalleryPhotoId before remove, then same check
- [x] `RemoveMarketComparableImageCommandHandler` - Get GalleryPhotoId from image before remove, then same check

## Part 4: EF Migration + Build Verification
- [x] Build passes with 0 errors
- [x] Migration created: `RefactorGalleryPhotoIsInUseAndMarketComparableImage`
- [x] Migration manually improved to use `RenameColumn` for `IsUsedInReport` → `IsInUse` (preserves data)

---

## Review

### Summary of Changes
- **9 files deleted** (MarkPhotoForReport + UnmarkPhotoFromReport features - no longer needed)
- **~20 files modified** across domain, infrastructure, and application layers
- **1 migration added** with 3 schema changes (2 renames, 1 drop)

### Migration Details
The migration uses `RenameColumn` for both renames to preserve existing data:
1. `MarketComparableImages.DocumentId` → `GalleryPhotoId` (rename, preserves data)
2. `AppraisalGallery.IsUsedInReport` → `IsInUse` (rename, preserves data)
3. `AppraisalGallery.ReportSection` dropped (no longer needed)

### IsPhotoLinkedAnywhereAsync SQL
Single-roundtrip query checking all 4 linkage tables:
- `PropertyPhotoMappings` (property links)
- `GalleryPhotoTopicMappings` (topic assignments)
- `AppendixDocuments` (appendix references)
- `MarketComparableImages` (market comparable references)

Uses `UNION ALL` + `EXISTS` for efficient short-circuit evaluation.

### Security Review
- No sensitive data exposed in API responses
- Parameterized SQL in `IsPhotoLinkedAnywhereAsync` (uses `@Id` parameter via Dapper)
- No new endpoints exposed; only existing endpoints modified
- Null-safe checks (`photo?.MarkAsInUse()`) prevent NullReferenceExceptions
