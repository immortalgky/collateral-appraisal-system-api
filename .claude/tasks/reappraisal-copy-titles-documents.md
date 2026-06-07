# Task: Copy RequestTitles + RequestDocuments in InitiateReappraisal

## Goal
When creating a new reappraisal Request, copy all RequestTitles (with TitleDocuments) and
all request-level RequestDocuments from the prior Request.

## Plan

- [x] Read handler, EF configs, DtoExtensions to understand shape
- [x] Extend `PriorRequestSnapshot` with `List<RequestTitleDto> Titles` and `List<RequestDocumentDto> Documents`
- [x] Add EF Core batch-load of RequestTitles (with owned Documents) keyed by RequestId
- [x] Add EF Core batch-load of Request.Documents (owned) keyed by RequestId
- [x] Populate both collections inside `FetchPriorRequestSnapshotsAsync`
- [x] Update `BuildCreateRequestData` to pass `snap?.Titles` / `snap?.Documents` instead of `null`
- [x] Remove the TODO comment from `BuildCreateRequestData`
- [x] Build verify: `dotnet build` — 0 errors (550 pre-existing warnings, 0 new)

## Design decisions
- Use EF Core (not Dapper) for both loads — owned entities are tricky to hydrate manually
- `title.ToDto()` from DtoExtensions handles full polymorphic conversion
- `document.ToDto()` from DtoExtensions covers all RequestDocument fields
- PKs (Title.Id, TitleDocument.Id, RequestDocument.Id) are NOT copied — CreateRequestService factory generates new ones
- DocumentId (the external file ref Guid) IS preserved verbatim
- Both loads use `RequestId IN @requestIds` — single batch per type, no N+1
- TitleDocuments auto-load with OwnsMany — no separate Include needed beyond `RequestTitles.Where(...)`
- RequestDocument is OwnsMany on Request — load via `Requests.Include(r => r.Documents).Where(r => requestIds.Contains(r.Id))`
