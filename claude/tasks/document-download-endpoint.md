# Document Download Endpoint Implementation

## Todo Items

- [x] Create DownloadDocumentQuery.cs
- [x] Create DownloadDocumentResult.cs
- [x] Create DownloadDocumentQueryHandler.cs
- [x] Create DownloadDocumentEndpoint.cs
- [x] Build and verify solution

## Overview

Implemented `GET /documents/{id:guid}/download` endpoint to stream document files for frontend display.

## Review

### Files Created

```
Modules/Document/Document/Application/Features/Documents/DownloadDocument/
├── DownloadDocumentQuery.cs          # CQRS query record
├── DownloadDocumentResult.cs         # Result record with file info
├── DownloadDocumentQueryHandler.cs   # Business logic handler
└── DownloadDocumentEndpoint.cs       # Carter API endpoint
```

### Changes Summary

1. **DownloadDocumentQuery.cs**: Simple query record with document ID and optional `ForceDownload` flag
2. **DownloadDocumentResult.cs**: Result containing file path, MIME type, filename, and existence flag
3. **DownloadDocumentQueryHandler.cs**: Validates document exists, is active, not deleted, and file exists on disk
4. **DownloadDocumentEndpoint.cs**: Carter endpoint that streams file with proper content-disposition based on `download` query param

### Security Considerations

- **Path traversal**: Not possible - `StoragePath` comes from database, not user input
- **Authorization**: Endpoint is `AllowAnonymous` as per plan (adjust if auth needed)
- **Soft delete**: Deleted/inactive documents return 404
- **MIME type**: Uses stored value from upload, not user-provided

### API Usage

| Use Case | URL |
|----------|-----|
| Display image inline | `GET /documents/{id}/download` |
| Display PDF in iframe | `GET /documents/{id}/download` |
| Force file download | `GET /documents/{id}/download?download=true` |

### Build Status

✅ Build succeeded with 0 errors
