# Denormalize File Metadata in Appraisal Module

## Part 1: AppraisalGallery — Add Full File Metadata

- [x] **Domain** — `AppraisalGallery.cs`: Add FileName, FilePath, FileExtension, MimeType, FileSizeBytes, UploadedByName
- [x] **EF Config** — `AppraisalGalleryConfiguration.cs`: Max lengths (255, 500, 10, 100, 200)
- [x] **Request** — `AddGalleryPhotoRequest.cs`: Add 6 fields
- [x] **Command** — `AddGalleryPhotoCommand.cs`: Add 6 fields
- [x] **Handler** — `AddGalleryPhotoCommandHandler.cs`: Pass fields to Create()
- [x] **Endpoint** — `AddGalleryPhotoEndpoint.cs`: Map request → command

## Part 2: LawAndRegulationImage — Replace DocumentId with GalleryPhotoId

- [x] **Domain** — `LawAndRegulationImage.cs`: DocumentId → GalleryPhotoId, remove FileName/FilePath
- [x] **Parent** — `LawAndRegulation.cs`: AddImage() uses galleryPhotoId
- [x] **EF Config** — `SupportingConfiguration.cs`: GalleryPhotoId config + index
- [x] **Save Request** — `SaveLawAndRegulationsRequest.cs`: GalleryPhotoId only
- [x] **Save Handler** — `SaveLawAndRegulationsCommandHandler.cs`: GalleryPhotoId + MarkAsInUse
- [x] **Get Result** — `GetLawAndRegulationsResult.cs`: GalleryPhotoId
- [x] **Get Handler** — `GetLawAndRegulationsQueryHandler.cs`: Map GalleryPhotoId

## Part 3: Migration

- [x] Generate migration: `DenormalizeGalleryMetadataAndRefactorLawRegImages`
- [ ] Apply migration: `dotnet ef database update`

## Review

_To be filled after migration is applied._
