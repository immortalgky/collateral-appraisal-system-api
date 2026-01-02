# Upload Session Sequence Diagrams

## Complete Two-Phase Upload Workflow

### Diagram 1: Complete Flow (Request Creation with Documents)

```mermaid
sequenceDiagram
    participant User
    participant Frontend
    participant DocumentAPI as Document API
    participant RequestAPI as Request API
    participant DocService as Document Service
    participant DocDB as Document DB
    participant RequestDB as Request DB
    participant MQ as RabbitMQ

    %% Phase 1: Create Upload Session
    Note over User,DocDB: Phase 1: Upload Documents (Before Request Created)

    User->>Frontend: Click "Create Request"
    Frontend->>DocumentAPI: POST /api/documents/sessions<br/>{intendedEntityType: "Request"}

    DocumentAPI->>DocDB: INSERT UploadSessions<br/>SessionToken="abc123"<br/>Status="InProgress"<br/>TotalDocuments=0
    DocDB-->>DocumentAPI: Session Created
    DocumentAPI-->>Frontend: {sessionToken: "abc123"}

    Frontend-->>User: Show upload form with session

    %% Upload Document 1
    Note over User,DocDB: Upload Document 1: Title Deed
    User->>Frontend: Select file: title-deed.pdf
    Frontend->>DocumentAPI: POST /api/documents/upload<br/>sessionToken="abc123"<br/>file=title-deed.pdf<br/>documentType=TitleDeed

    DocumentAPI->>DocumentAPI: Save file to disk<br/>/var/uploads/2025/01/xxx.pdf
    DocumentAPI->>DocDB: INSERT Documents<br/>UploadSessionId=session.Id<br/>ReferenceCount=0
    DocumentAPI->>DocDB: UPDATE UploadSessions<br/>TotalDocuments=1<br/>TotalSizeBytes+=1MB
    DocDB-->>DocumentAPI: Document Saved
    DocumentAPI-->>Frontend: {documentId: "doc-1"}
    Frontend-->>User: "title-deed.pdf uploaded ✓"

    %% Upload Document 2
    Note over User,DocDB: Upload Document 2: Owner ID
    User->>Frontend: Select file: owner-id.pdf
    Frontend->>DocumentAPI: POST /api/documents/upload<br/>sessionToken="abc123"<br/>file=owner-id.pdf<br/>documentType=IDCard

    DocumentAPI->>DocumentAPI: Save file to disk
    DocumentAPI->>DocDB: INSERT Documents<br/>UploadSessionId=session.Id<br/>ReferenceCount=0
    DocumentAPI->>DocDB: UPDATE UploadSessions<br/>TotalDocuments=2<br/>TotalSizeBytes+=512KB
    DocDB-->>DocumentAPI: Document Saved
    DocumentAPI-->>Frontend: {documentId: "doc-2"}
    Frontend-->>User: "owner-id.pdf uploaded ✓"

    %% Upload Document 3
    Note over User,DocDB: Upload Document 3: House Registration
    User->>Frontend: Select file: house-reg.pdf
    Frontend->>DocumentAPI: POST /api/documents/upload<br/>sessionToken="abc123"<br/>file=house-reg.pdf<br/>documentType=HouseRegistration

    DocumentAPI->>DocumentAPI: Save file to disk
    DocumentAPI->>DocDB: INSERT Documents<br/>UploadSessionId=session.Id<br/>ReferenceCount=0
    DocumentAPI->>DocDB: UPDATE UploadSessions<br/>TotalDocuments=3<br/>TotalSizeBytes+=800KB
    DocDB-->>DocumentAPI: Document Saved
    DocumentAPI-->>Frontend: {documentId: "doc-3"}
    Frontend-->>User: "house-reg.pdf uploaded ✓"

    %% Phase 2: Create Request
    Note over User,RequestDB: Phase 2: Create Request and Link Documents

    User->>Frontend: Fill form & click Submit
    Frontend->>RequestAPI: POST /api/requests<br/>{<br/>  requestDetail: {...},<br/>  uploadSessionToken: "abc123"<br/>}

    RequestAPI->>RequestDB: INSERT Requests<br/>RequestNumber="REQ-2025-001"
    RequestDB-->>RequestAPI: Request Created (Id=req-1)

    %% Link Documents
    RequestAPI->>DocService: LinkDocumentsToEntityAsync(<br/>  sessionToken="abc123",<br/>  entityType="Request",<br/>  entityId=req-1<br/>)

    DocService->>DocDB: SELECT Documents<br/>WHERE UploadSessionId=session.Id
    DocDB-->>DocService: 3 documents found

    loop For each document (3 docs)
        DocService->>DocDB: INSERT EntityDocuments<br/>DocumentId=doc.Id<br/>EntityType="Request"<br/>EntityId=req-1<br/>LinkType="Original"
        DocService->>DocDB: UPDATE Documents<br/>ReferenceCount=1<br/>LastLinkedAt=NOW()
    end

    DocService->>DocDB: UPDATE UploadSessions<br/>Status="Completed"<br/>LinkedEntityType="Request"<br/>LinkedEntityId=req-1
    DocDB-->>DocService: All documents linked

    DocService-->>RequestAPI: 3 documents linked successfully

    %% Publish Event
    RequestAPI->>MQ: PUBLISH RequestCreatedEvent<br/>{requestId, uploadSessionId}

    RequestAPI-->>Frontend: {requestId: "req-1", documentsLinked: 3}
    Frontend-->>User: "Request REQ-2025-001 created ✓<br/>3 documents attached"
```

---

### Diagram 2: Session Abandoned (Cleanup Flow)

```mermaid
sequenceDiagram
    participant User
    participant Frontend
    participant DocumentAPI as Document API
    participant DocDB as Document DB
    participant CleanupJob as Cleanup Job (Scheduled)

    %% User starts upload but abandons
    Note over User,DocDB: User Starts Upload but Abandons

    User->>Frontend: Click "Create Request"
    Frontend->>DocumentAPI: POST /api/documents/sessions
    DocumentAPI->>DocDB: INSERT UploadSessions<br/>SessionToken="xyz789"<br/>ExpiresAt=+24 hours
    DocDB-->>DocumentAPI: Session Created
    DocumentAPI-->>Frontend: {sessionToken: "xyz789"}

    User->>Frontend: Upload 2 documents
    Frontend->>DocumentAPI: POST /api/documents/upload (x2)
    DocumentAPI->>DocDB: INSERT Documents (x2)<br/>UploadSessionId=session.Id<br/>ReferenceCount=0
    DocDB-->>DocumentAPI: Documents Saved
    DocumentAPI-->>Frontend: Success

    %% User abandons (closes browser, etc.)
    Note over User: User closes browser<br/>or navigates away<br/>(never submits Request form)

    %% 24 hours later - Session Expiration Job
    Note over CleanupJob,DocDB: 24 Hours Later: Auto-Expire Sessions

    CleanupJob->>CleanupJob: Daily job runs (2:00 AM)
    CleanupJob->>DocDB: SELECT UploadSessions<br/>WHERE Status='InProgress'<br/>AND ExpiresAt < NOW()
    DocDB-->>CleanupJob: Session "xyz789" found

    CleanupJob->>DocDB: UPDATE UploadSessions<br/>Status='Expired'
    DocDB-->>CleanupJob: Session marked expired

    %% 7 days later - Document Cleanup Job
    Note over CleanupJob,DocDB: 7 Days After Expiration: Document Cleanup

    CleanupJob->>CleanupJob: Daily cleanup job runs
    CleanupJob->>DocDB: SELECT FROM vw_UnusedDocuments<br/>WHERE IsEligibleForCleanup=1
    DocDB-->>CleanupJob: 2 documents from session "xyz789"<br/>(ReferenceCount=0, Session=Expired)

    loop For each orphaned document
        CleanupJob->>DocDB: Double-check ReferenceCount=0
        DocDB-->>CleanupJob: Confirmed orphaned

        CleanupJob->>DocDB: UPDATE Documents<br/>IsOrphaned=1<br/>OrphanedReason='Session Abandoned'<br/>IsDeleted=1

        CleanupJob->>CleanupJob: Delete physical file<br/>/var/uploads/2025/01/xxx.pdf
    end

    CleanupJob->>DocDB: COMMIT cleanup transaction
    DocDB-->>CleanupJob: 2 documents cleaned up

    Note over CleanupJob: Cleanup Complete<br/>Orphaned documents removed
```

---

### Diagram 3: Share Document Across Entities (Polymorphic Linking)

```mermaid
sequenceDiagram
    participant User as Appraiser
    participant Frontend
    participant AppraisalAPI as Appraisal API
    participant DocService as Document Service
    participant DocDB as Document DB
    participant RequestDB as Request DB

    Note over User,RequestDB: Scenario: Owner ID already uploaded for Request,<br/>now share it with Appraisal

    %% Step 1: Get Request Documents
    User->>Frontend: View Request REQ-2025-001
    Frontend->>DocService: GET /api/documents/entity/Request/req-1

    DocService->>DocDB: SELECT ed.*, d.*<br/>FROM EntityDocuments ed<br/>JOIN Documents d<br/>WHERE EntityType='Request'<br/>AND EntityId=req-1
    DocDB-->>DocService: 3 documents found<br/>(Title Deed, Owner ID, House Reg)
    DocService-->>Frontend: Document list
    Frontend-->>User: Show documents:<br/>- title-deed.pdf<br/>- owner-id.pdf ✓ (selected)<br/>- house-reg.pdf

    %% Step 2: Create Appraisal
    User->>Frontend: Click "Create Appraisal for this Request"
    Frontend->>AppraisalAPI: POST /api/appraisals<br/>{requestId: req-1, ...}
    AppraisalAPI->>DocDB: INSERT Appraisals
    DocDB-->>AppraisalAPI: Appraisal Created (Id=apr-1)
    AppraisalAPI-->>Frontend: {appraisalId: "apr-1"}

    %% Step 3: Share Owner ID document
    User->>Frontend: Select "Share owner-id.pdf with Appraisal"
    Frontend->>DocService: POST /api/documents/share<br/>{<br/>  documentId: "doc-2",<br/>  targetEntityType: "Appraisal",<br/>  targetEntityId: "apr-1"<br/>}

    DocService->>DocDB: SELECT Documents<br/>WHERE Id=doc-2
    DocDB-->>DocService: Document found (ReferenceCount=1)

    DocService->>DocDB: INSERT EntityDocuments<br/>DocumentId=doc-2<br/>EntityType='Appraisal'<br/>EntityId=apr-1<br/>LinkType='Shared'<br/>IsPrimaryLink=false

    DocService->>DocDB: UPDATE Documents<br/>SET ReferenceCount=2<br/>LastLinkedAt=NOW()

    DocDB-->>DocService: Link created

    DocService-->>Frontend: Document shared successfully

    Frontend-->>User: "owner-id.pdf now linked to:<br/>✓ Request REQ-2025-001 (Original)<br/>✓ Appraisal APR-2025-001 (Shared)"

    %% Step 4: Verify polymorphic links
    Note over DocService,DocDB: Document now has 2 entity links

    User->>Frontend: View document details
    Frontend->>DocService: GET /api/documents/doc-2/links

    DocService->>DocDB: SELECT EntityDocuments<br/>WHERE DocumentId=doc-2<br/>AND IsDeleted=false
    DocDB-->>DocService: 2 links found

    DocService-->>Frontend: [<br/>  {entityType: "Request", entityId: req-1, linkType: "Original"},<br/>  {entityType: "Appraisal", entityId: apr-1, linkType: "Shared"}<br/>]

    Frontend-->>User: Show usage:<br/>Document shared across 2 entities
```

---

### Diagram 4: Unlink Document and Orphan Detection

```mermaid
sequenceDiagram
    participant Admin
    participant Frontend
    participant RequestAPI as Request API
    participant DocService as Document Service
    participant DocDB as Document DB

    Note over Admin,DocDB: Scenario: Admin removes document link from Request<br/>Document becomes orphaned

    %% Initial state
    Note over DocDB: Initial State:<br/>Document "doc-5" linked to:<br/>- Request (req-3) [Original]<br/>ReferenceCount = 1

    %% Admin views request documents
    Admin->>Frontend: View Request REQ-2025-003
    Frontend->>DocService: GET /api/documents/entity/Request/req-3
    DocService->>DocDB: SELECT documents for Request
    DocDB-->>DocService: 4 documents
    DocService-->>Frontend: Document list
    Frontend-->>Admin: Show 4 documents

    %% Admin removes document
    Admin->>Frontend: Click "Remove contract.pdf"
    Frontend->>RequestAPI: DELETE /api/requests/req-3/documents/doc-5

    RequestAPI->>DocDB: SELECT EntityDocuments<br/>WHERE EntityType='Request'<br/>AND EntityId=req-3<br/>AND DocumentId=doc-5
    DocDB-->>RequestAPI: Link found (link-id-123)

    %% Soft delete the link
    RequestAPI->>DocDB: UPDATE EntityDocuments<br/>SET IsDeleted=true<br/>DeletedOn=NOW()<br/>WHERE Id=link-id-123

    %% Update document reference count
    RequestAPI->>DocDB: SELECT Documents<br/>WHERE Id=doc-5
    DocDB-->>RequestAPI: Document (ReferenceCount=1)

    RequestAPI->>DocDB: UPDATE Documents<br/>SET ReferenceCount=0<br/>LastUnlinkedAt=NOW()

    %% Check if orphaned
    RequestAPI->>DocDB: SELECT COUNT(*) FROM EntityDocuments<br/>WHERE DocumentId=doc-5<br/>AND IsDeleted=false
    DocDB-->>RequestAPI: Count = 0

    alt No active links remaining
        RequestAPI->>DocDB: UPDATE Documents<br/>SET IsOrphaned=true<br/>OrphanedReason='All Links Removed'
        DocDB-->>RequestAPI: Document marked orphaned
    end

    RequestAPI-->>Frontend: Document unlinked successfully
    Frontend-->>Admin: "contract.pdf removed from Request"

    %% 30 days later - Cleanup
    Note over DocDB: 30 Days Later: Cleanup Job Runs

    participant CleanupJob as Cleanup Job

    CleanupJob->>DocDB: SELECT FROM vw_UnusedDocuments<br/>WHERE IsEligibleForCleanup=1
    DocDB-->>CleanupJob: Document doc-5 eligible<br/>(OrphanedReason='All Links Removed'<br/>LastUnlinkedAt > 30 days ago)

    CleanupJob->>DocDB: Double-check ReferenceCount
    DocDB-->>CleanupJob: ReferenceCount = 0 (confirmed)

    CleanupJob->>DocDB: UPDATE Documents<br/>SET IsDeleted=true<br/>DeletedOn=NOW()

    CleanupJob->>CleanupJob: Delete physical file<br/>/var/uploads/.../contract.pdf

    CleanupJob->>DocDB: COMMIT cleanup
    DocDB-->>CleanupJob: Document cleaned up

    Note over CleanupJob: Orphaned document removed
```

---

### Diagram 5: Error Handling - Session Token Invalid

```mermaid
sequenceDiagram
    participant User
    participant Frontend
    participant RequestAPI as Request API
    participant DocService as Document Service
    participant DocDB as Document DB

    Note over User,DocDB: Scenario: User submits form with expired/invalid session

    %% User uploads documents (session expires)
    Note over User: User uploads documents<br/>then waits 25 hours<br/>(session expires at 24h)

    %% Try to create request with expired session
    User->>Frontend: Submit Request form<br/>(after 25 hours)
    Frontend->>RequestAPI: POST /api/requests<br/>{<br/>  requestDetail: {...},<br/>  uploadSessionToken: "old-token-123"<br/>}

    RequestAPI->>RequestAPI: Validate request data
    RequestAPI->>DocDB: INSERT Requests
    DocDB-->>RequestAPI: Request Created (req-5)

    %% Try to link documents
    RequestAPI->>DocService: LinkDocumentsToEntityAsync(<br/>  sessionToken="old-token-123",<br/>  entityType="Request",<br/>  entityId=req-5<br/>)

    DocService->>DocDB: SELECT UploadSessions<br/>WHERE SessionToken='old-token-123'
    DocDB-->>DocService: Session found<br/>Status='Expired'

    alt Session Expired or Invalid
        DocService-->>RequestAPI: ❌ InvalidOperationException<br/>"Upload session expired"

        RequestAPI->>RequestAPI: Rollback transaction<br/>(if using transaction)

        RequestAPI-->>Frontend: 400 Bad Request<br/>{<br/>  error: "Upload session expired",<br/>  code: "SESSION_EXPIRED",<br/>  sessionToken: "old-token-123"<br/>}

        Frontend-->>User: ❌ Error: Your upload session expired.<br/>Please re-upload documents.

        Note over User,Frontend: User must:<br/>1. Create new session<br/>2. Re-upload documents<br/>3. Submit form again
    else Session Valid
        DocService->>DocDB: Link documents
        DocService-->>RequestAPI: Success
        RequestAPI-->>Frontend: Request created ✓
    end
```

---

## Key Takeaways from Diagrams

### Diagram 1: Normal Flow
- One session → Multiple documents
- Session tracks `TotalDocuments` and `TotalSizeBytes`
- All documents linked atomically when Request created
- Session marked `Completed` with linked entity info

### Diagram 2: Abandoned Session
- Session expires after 24 hours
- Documents remain orphaned (ReferenceCount=0)
- Cleanup job removes after 7 days
- Physical files deleted from disk

### Diagram 3: Polymorphic Linking
- Same document can link to multiple entities
- Each link tracks `LinkType` (Original, Shared)
- `ReferenceCount` incremented for each link
- Document survives as long as one link exists

### Diagram 4: Orphan Detection
- Unlinking decrements `ReferenceCount`
- When ReferenceCount=0, marked orphaned
- `LastUnlinkedAt` timestamp recorded
- Cleanup after 30-day retention period

### Diagram 5: Error Handling
- Expired sessions prevent document linking
- Request creation can be rolled back
- User notified to re-upload
- Prevents orphaned requests without documents
