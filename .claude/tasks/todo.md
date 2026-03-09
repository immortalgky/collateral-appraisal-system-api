# Fix Orphan Document Handling Gaps

## Tasks

- [x] 1. Create `DocumentUnlinkedIntegrationEventHandler` in Document module
- [x] 2. Create `DocumentUpdatedIntegrationEventHandler` in Document module
- [x] 3. Publish `DocumentLinkedIntegrationEventV2` from Appraisal's `AddGalleryPhoto`
- [x] 4. Publish `DocumentUnlinkedIntegrationEvent` from Appraisal's `RemoveGalleryPhoto`
- [x] 5a. Add `SessionId` to `CreateRequestCommand`
- [x] 5b. Create `SessionCompletedIntegrationEvent` in Shared.Messaging
- [x] 5c. Publish session completed event from `CreateRequestCommandHandler`
- [x] 5d. Create `SessionCompletedIntegrationEventHandler` in Document module
- [x] 6. Build verification — 0 errors

## Review

### Summary of Changes

**4 new files created:**
- `Document/Application/EventHandlers/DocumentUnlinkedIntegrationEventHandler.cs` — consumes `DocumentUnlinkedIntegrationEvent`, calls `document.Unlink()`
- `Document/Application/EventHandlers/DocumentUpdatedIntegrationEventHandler.cs` — consumes `DocumentUpdatedIntegrationEvent`, unlinks old doc + links new doc
- `Document/Application/EventHandlers/SessionCompletedIntegrationEventHandler.cs` — consumes `SessionCompletedIntegrationEvent`, marks upload session as completed
- `Shared/Shared.Messaging/Events/SessionCompletedIntegrationEvent.cs` — new integration event contract

**4 existing files modified:**
- `AddGalleryPhotoCommandHandler.cs` — publishes `DocumentLinkedIntegrationEventV2` after adding photo
- `RemoveGalleryPhotoCommandHandler.cs` — publishes `DocumentUnlinkedIntegrationEvent` after deleting photo
- `CreateRequestCommand.cs` — added `Guid? SessionId` parameter
- `CreateRequestCommandHandler.cs` — publishes `SessionCompletedIntegrationEvent` when SessionId is present

### Security
- No user input directly used in queries (all via strongly-typed IDs)
- No sensitive data exposed in logs (only GUIDs logged)
- All new handlers follow same patterns as existing code
