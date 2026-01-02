# Implementation Work Breakdown Structure (WBS)

## Project Overview

**Project**: Collateral Appraisal System Implementation
**Timeline**: 2 months (November - December 2025)
**Team Size**: Recommended 3-5 developers
**Methodology**: Agile/Scrum with 2-week sprints

---

## Timeline Summary

| Phase | Duration | Weeks | Deliverable |
|-------|----------|-------|-------------|
| **Phase 1: Foundation** | Week 1-2 | Sprint 1 | Infrastructure, Auth Module |
| **Phase 2: Core Modules** | Week 3-4 | Sprint 2 | Request & Document Modules |
| **Phase 3: Appraisal** | Week 5-6 | Sprint 3 | Appraisal Module (Basic) |
| **Phase 4: Advanced Features** | Week 7-8 | Sprint 4 | Photo Gallery, Property Details |
| **Testing & Deployment** | Week 8+ | Ongoing | Production Ready |

---

## Phase 1: Foundation & Infrastructure (Sprint 1: Week 1-2)

### 1.1 Project Setup & Infrastructure (Week 1, Day 1-2)

**Priority**: Critical
**Estimated Time**: 2 days
**Dependencies**: None
**Assigned To**: DevOps/Backend Lead

#### Tasks:
- [ ] **INFRA-001**: Create project structure and solution file
  - Create modular monolith structure
  - Set up Bootstrapper/Api project
  - Set up Shared infrastructure project
  - Configure .NET 9.0 settings
  - **Time**: 2 hours

- [ ] **INFRA-002**: Set up Docker infrastructure
  - Configure docker-compose.yml (SQL Server, Redis, RabbitMQ, Seq)
  - Create development environment setup scripts
  - Document infrastructure setup in README
  - **Time**: 3 hours

- [ ] **INFRA-003**: Configure database connection and EF Core
  - Set up SQL Server connection strings
  - Configure Entity Framework Core 9.0
  - Create base DbContext with conventions
  - Set up migration infrastructure
  - **Time**: 3 hours

- [ ] **INFRA-004**: Set up logging infrastructure
  - Configure Serilog with Seq
  - Set up structured logging
  - Create logging middleware
  - **Time**: 2 hours

- [ ] **INFRA-005**: Configure MediatR and CQRS infrastructure
  - Install and configure MediatR
  - Create base command/query handlers
  - Set up validation pipeline behavior
  - Set up logging pipeline behavior
  - **Time**: 4 hours

- [ ] **INFRA-006**: Configure MassTransit and RabbitMQ
  - Install and configure MassTransit
  - Set up RabbitMQ connection
  - Create integration event infrastructure
  - Set up retry policies
  - **Time**: 4 hours

**GitHub Issues**:
```markdown
Title: [INFRA] Project Setup and Infrastructure Configuration
Labels: infrastructure, sprint-1, priority-critical
Assignee: DevOps Lead
Milestone: Sprint 1 - Foundation
```

---

### 1.2 Shared Domain Infrastructure (Week 1, Day 3-4)

**Priority**: Critical
**Estimated Time**: 2 days
**Dependencies**: INFRA-001 to INFRA-006
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **SHARED-001**: Create base entity classes
  - Implement `Entity<TId>` base class
  - Implement `AggregateRoot<TId>` base class
  - Add audit fields (CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
  - Add soft delete support (IsDeleted, DeletedOn, DeletedBy)
  - Add RowVersion for optimistic concurrency
  - **Time**: 4 hours

- [ ] **SHARED-002**: Create value objects infrastructure
  - Implement `ValueObject` base class
  - Create common value objects (Money, Address, Contact, etc.)
  - Add validation to value objects
  - **Time**: 4 hours

- [ ] **SHARED-003**: Create domain events infrastructure
  - Implement `IDomainEvent` interface
  - Create `DomainEventDispatcher`
  - Set up domain event interceptor for EF Core
  - **Time**: 3 hours

- [ ] **SHARED-004**: Create result pattern infrastructure
  - Implement `Result<T>` pattern
  - Create error handling infrastructure
  - Set up custom exceptions
  - **Time**: 3 hours

- [ ] **SHARED-005**: Create repository interfaces
  - Define `IRepository<TEntity, TId>` interface
  - Define `IUnitOfWork` interface
  - Create specification pattern for queries
  - **Time**: 2 hours

**GitHub Issues**:
```markdown
Title: [SHARED] Shared Domain Infrastructure and Base Classes
Labels: domain, infrastructure, sprint-1, priority-critical
Assignee: Backend Developer 1
Milestone: Sprint 1 - Foundation
```

---

### 1.3 Authentication & Authorization Module (Week 1, Day 5 - Week 2, Day 3)

**Priority**: Critical
**Estimated Time**: 3 days
**Dependencies**: SHARED-001 to SHARED-005
**Assigned To**: Backend Developer 2

#### Tasks:
- [ ] **AUTH-001**: Create Auth module structure
  - Create Auth module project
  - Set up module registration
  - Configure module dependency injection
  - **Time**: 2 hours

- [ ] **AUTH-002**: Create User aggregate
  - Implement User entity with all fields
  - Add user creation logic
  - Add password hashing (using ASP.NET Core Identity)
  - Add user activation/deactivation
  - **Time**: 4 hours

- [ ] **AUTH-003**: Create Role and Permission entities
  - Implement Role entity
  - Implement Permission entity
  - Implement UserRole junction table
  - Implement RolePermission junction table
  - Implement UserPermission (direct permissions)
  - **Time**: 4 hours

- [ ] **AUTH-004**: Create Organization entities
  - Implement Organization entity
  - Implement UserOrganization junction table
  - Add organization approval workflow
  - **Time**: 3 hours

- [ ] **AUTH-005**: Create AuditLog and SecurityPolicy entities
  - Implement AuditLog entity
  - Implement SecurityPolicy entity
  - Add audit logging interceptor
  - **Time**: 3 hours

- [ ] **AUTH-006**: Create AuthDbContext
  - Set up AuthDbContext with all entities
  - Configure entity relationships
  - Apply global conventions (audit fields, soft delete)
  - Create indexes
  - **Time**: 3 hours

- [ ] **AUTH-007**: Create and run migrations
  - Create initial migration for Auth module
  - Apply migration to database
  - Seed system roles (ADMIN, RM, APPRAISER, CHECKER, VERIFIER, COMMITTEE)
  - Seed permissions
  - **Time**: 2 hours

- [ ] **AUTH-008**: Configure OpenIddict
  - Install OpenIddict packages
  - Configure OpenIddict in AuthDbContext
  - Set up token endpoint
  - Set up client credentials flow
  - Configure scopes and claims
  - **Time**: 6 hours

- [ ] **AUTH-009**: Create user management commands
  - CreateUserCommand and handler
  - UpdateUserCommand and handler
  - DeleteUserCommand and handler
  - AssignRoleCommand and handler
  - GrantPermissionCommand and handler
  - Add FluentValidation for all commands
  - **Time**: 6 hours

- [ ] **AUTH-010**: Create user queries
  - GetUserByIdQuery and handler
  - GetUserByUsernameQuery and handler
  - GetUsersQuery with filtering and pagination
  - GetUserPermissionsQuery and handler
  - **Time**: 4 hours

- [ ] **AUTH-011**: Create API endpoints (Carter)
  - POST /auth/register - Register user
  - POST /auth/token - Get access token
  - GET /users - List users
  - GET /users/{id} - Get user by ID
  - POST /users - Create user
  - PUT /users/{id} - Update user
  - DELETE /users/{id} - Delete user
  - POST /users/{id}/roles - Assign role
  - GET /users/{id}/permissions - Get user permissions
  - **Time**: 4 hours

- [ ] **AUTH-012**: Create role and permission endpoints
  - GET /roles - List roles
  - POST /roles - Create role
  - POST /roles/{id}/permissions - Assign permissions
  - GET /permissions - List permissions
  - **Time**: 3 hours

**GitHub Issues**:
```markdown
Title: [AUTH] Authentication & Authorization Module Implementation
Labels: auth, security, sprint-1, priority-critical
Assignee: Backend Developer 2
Milestone: Sprint 1 - Foundation
Dependencies: SHARED-001 to SHARED-005
```

---

### 1.4 Testing Infrastructure (Week 2, Day 4-5)

**Priority**: High
**Estimated Time**: 2 days
**Dependencies**: AUTH-001 to AUTH-012
**Assigned To**: QA/Backend Developer 3

#### Tasks:
- [ ] **TEST-001**: Set up unit testing infrastructure
  - Create test projects structure
  - Install xUnit, FluentAssertions, NSubstitute
  - Create test base classes
  - Set up test data builders
  - **Time**: 3 hours

- [ ] **TEST-002**: Set up integration testing infrastructure
  - Install Microsoft.AspNetCore.Mvc.Testing
  - Create WebApplicationFactory for testing
  - Set up test database (in-memory or test container)
  - Create integration test base class
  - **Time**: 4 hours

- [ ] **TEST-003**: Write Auth module unit tests
  - Test User aggregate behaviors
  - Test role assignment logic
  - Test permission checking logic
  - Test command validators
  - **Time**: 5 hours

- [ ] **TEST-004**: Write Auth module integration tests
  - Test user registration flow
  - Test authentication flow
  - Test role assignment via API
  - Test permission queries
  - **Time**: 4 hours

**GitHub Issues**:
```markdown
Title: [TEST] Testing Infrastructure Setup
Labels: testing, infrastructure, sprint-1, priority-high
Assignee: QA Lead
Milestone: Sprint 1 - Foundation
```

---

## Phase 2: Core Modules (Sprint 2: Week 3-4)

### 2.1 Document Module (Week 3, Day 1-3)

**Priority**: Critical
**Estimated Time**: 3 days
**Dependencies**: Phase 1 Complete
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **DOC-001**: Create Document module structure
  - Create Document module project
  - Set up module registration
  - Configure dependency injection
  - **Time**: 2 hours

- [ ] **DOC-002**: Create Document aggregate
  - Implement Document entity
  - Add upload logic
  - Add file validation (type, size)
  - Generate DocumentNumber
  - Calculate file checksum (SHA256)
  - **Time**: 5 hours

- [ ] **DOC-003**: Create DocumentVersion entity
  - Implement DocumentVersion entity
  - Add versioning logic
  - Track version history
  - **Time**: 3 hours

- [ ] **DOC-004**: Create DocumentRelationship entity
  - Implement DocumentRelationship entity
  - Add relationship types (Attachment, Reference, Supersedes, Amendment)
  - **Time**: 2 hours

- [ ] **DOC-005**: Create DocumentAccess entity
  - Implement DocumentAccess entity
  - Add permission checking logic
  - Add expiration handling
  - Add revocation support
  - **Time**: 4 hours

- [ ] **DOC-006**: Create DocumentAccessLog entity
  - Implement DocumentAccessLog entity
  - Add automatic logging for all document access
  - **Time**: 2 hours

- [ ] **DOC-007**: Create DocumentTemplate entity
  - Implement DocumentTemplate entity
  - Add template management
  - **Time**: 2 hours

- [ ] **DOC-008**: Create DocumentDbContext
  - Set up DocumentDbContext
  - Configure relationships
  - Apply conventions and indexes
  - Create and run migrations
  - **Time**: 3 hours

- [ ] **DOC-009**: Configure cloud storage integration
  - Create IStorageProvider interface
  - Implement AzureBlobStorageProvider
  - Implement LocalFileSystemProvider (for dev)
  - Configure storage settings
  - **Time**: 5 hours

- [ ] **DOC-010**: Create document commands
  - UploadDocumentCommand and handler
  - CreateDocumentVersionCommand and handler
  - GrantDocumentAccessCommand and handler
  - RevokeDocumentAccessCommand and handler
  - DeleteDocumentCommand and handler
  - Add FluentValidation
  - **Time**: 6 hours

- [ ] **DOC-011**: Create document queries
  - GetDocumentByIdQuery and handler
  - GetDocumentsByTypeQuery and handler
  - GetDocumentVersionsQuery and handler
  - CheckDocumentAccessQuery and handler
  - **Time**: 4 hours

- [ ] **DOC-012**: Create document endpoints
  - POST /documents/upload - Upload document
  - GET /documents/{id} - Get document
  - GET /documents/{id}/download - Download document
  - GET /documents/{id}/versions - Get versions
  - POST /documents/{id}/access - Grant access
  - DELETE /documents/{id}/access - Revoke access
  - **Time**: 4 hours

- [ ] **DOC-013**: Write Document module tests
  - Unit tests for document logic
  - Integration tests for upload/download
  - Test access control
  - Test versioning
  - **Time**: 5 hours

**GitHub Issues**:
```markdown
Title: [DOC] Document Management Module Implementation
Labels: document, storage, sprint-2, priority-critical
Assignee: Backend Developer 1
Milestone: Sprint 2 - Core Modules
Dependencies: Phase 1 Complete
```

---

### 2.2 Request Module (Week 3, Day 4-5, Week 4, Day 1-2)

**Priority**: Critical
**Estimated Time**: 3 days
**Dependencies**: DOC-001 to DOC-013
**Assigned To**: Backend Developer 2

#### Tasks:
- [ ] **REQ-001**: Create Request module structure
  - Create Request module project
  - Set up module registration
  - Configure dependency injection
  - **Time**: 2 hours

- [ ] **REQ-002**: Create value objects
  - RequestDetail value object
  - LoanDetail value object
  - Address value object (if not in Shared)
  - Contact value object (if not in Shared)
  - **Time**: 4 hours

- [ ] **REQ-003**: Create Request aggregate
  - Implement Request entity
  - Add RequestCustomer entity (simplified)
  - Add RequestPropertyType entity (simplified)
  - Add business logic for request creation
  - Generate RequestNumber
  - **Time**: 6 hours

- [ ] **REQ-004**: Create TitleDeedInfo entity
  - Implement TitleDeedInfo entity
  - Add title deed validation
  - **Time**: 3 hours

- [ ] **REQ-005**: Create RequestDocument entity
  - Implement RequestDocument entity
  - Link to Document module (store DocumentId)
  - **Time**: 2 hours

- [ ] **REQ-006**: Create RequestStatusHistory entity
  - Implement RequestStatusHistory entity
  - Add automatic status tracking
  - **Time**: 2 hours

- [ ] **REQ-007**: Create RequestDbContext
  - Set up RequestDbContext
  - Configure relationships
  - Apply conventions and indexes
  - Create and run migrations
  - **Time**: 3 hours

- [ ] **REQ-008**: Create domain events
  - RequestCreatedEvent
  - RequestSubmittedEvent
  - RequestApprovedEvent
  - RequestRejectedEvent
  - RequestCancelledEvent
  - **Time**: 3 hours

- [ ] **REQ-009**: Create request commands
  - CreateRequestCommand and handler
  - AddCustomerToRequestCommand and handler
  - AddPropertyTypeToRequestCommand and handler
  - AddTitleDeedInfoCommand and handler
  - AttachDocumentCommand and handler
  - SubmitRequestCommand and handler
  - ApproveRequestCommand and handler
  - RejectRequestCommand and handler
  - CancelRequestCommand and handler
  - Add FluentValidation for all commands
  - **Time**: 8 hours

- [ ] **REQ-010**: Create request queries
  - GetRequestByIdQuery and handler
  - GetRequestByNumberQuery and handler
  - GetRequestsQuery with filtering/pagination
  - GetRequestStatusHistoryQuery and handler
  - GetMyRequestsQuery (for current user)
  - **Time**: 5 hours

- [ ] **REQ-011**: Create request endpoints
  - POST /requests - Create request
  - GET /requests - List requests
  - GET /requests/{id} - Get request by ID
  - PUT /requests/{id}/customers - Add customer
  - PUT /requests/{id}/property-types - Add property type
  - PUT /requests/{id}/title-deed - Add title deed info
  - POST /requests/{id}/documents - Attach document
  - POST /requests/{id}/submit - Submit request
  - POST /requests/{id}/approve - Approve request
  - POST /requests/{id}/reject - Reject request
  - DELETE /requests/{id} - Cancel request
  - **Time**: 5 hours

- [ ] **REQ-012**: Write Request module tests
  - Unit tests for request aggregate
  - Test request workflow (create → submit → approve)
  - Integration tests for API endpoints
  - Test domain events are dispatched
  - **Time**: 6 hours

**GitHub Issues**:
```markdown
Title: [REQ] Request Management Module Implementation
Labels: request, core, sprint-2, priority-critical
Assignee: Backend Developer 2
Milestone: Sprint 2 - Core Modules
Dependencies: DOC-001 to DOC-013
```

---

### 2.3 Request-to-Appraisal Event Handler (Week 4, Day 3)

**Priority**: Critical
**Estimated Time**: 1 day
**Dependencies**: REQ-001 to REQ-012
**Assigned To**: Backend Developer 3

#### Tasks:
- [ ] **EVENT-001**: Create integration event for RequestCreated
  - Define RequestCreatedIntegrationEvent
  - Configure MassTransit to publish event
  - **Time**: 2 hours

- [ ] **EVENT-002**: Create event handler skeleton
  - Create placeholder consumer for Appraisal module
  - Test event flow (publish → consume)
  - Add logging for debugging
  - **Time**: 3 hours

- [ ] **EVENT-003**: Test event-driven workflow
  - Integration test for event publishing
  - Verify event reaches consumer
  - Test retry logic
  - **Time**: 3 hours

**GitHub Issues**:
```markdown
Title: [EVENT] Request-to-Appraisal Event Integration
Labels: events, integration, sprint-2, priority-critical
Assignee: Backend Developer 3
Milestone: Sprint 2 - Core Modules
Dependencies: REQ-012
```

---

### 2.4 Caching Layer (Week 4, Day 4-5)

**Priority**: Medium
**Estimated Time**: 2 days
**Dependencies**: REQ-012
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **CACHE-001**: Set up Redis connection
  - Configure Redis connection string
  - Install StackExchange.Redis
  - Create cache service interface
  - **Time**: 2 hours

- [ ] **CACHE-002**: Create caching infrastructure
  - Implement ICacheService
  - Create decorator pattern for repositories
  - Add cache invalidation logic
  - **Time**: 4 hours

- [ ] **CACHE-003**: Apply caching to Request repository
  - Create CachedRequestRepository
  - Register decorator in DI
  - Test cache hit/miss
  - **Time**: 3 hours

- [ ] **CACHE-004**: Apply caching to Document repository
  - Create CachedDocumentRepository
  - Configure cache expiration
  - **Time**: 2 hours

- [ ] **CACHE-005**: Write caching tests
  - Test cache decorator
  - Test invalidation scenarios
  - **Time**: 3 hours

**GitHub Issues**:
```markdown
Title: [CACHE] Redis Caching Layer Implementation
Labels: cache, performance, sprint-2, priority-medium
Assignee: Backend Developer 1
Milestone: Sprint 2 - Core Modules
```

---

## Phase 3: Appraisal Module - Core (Sprint 3: Week 5-6)

### 3.1 Appraisal Core Entities (Week 5, Day 1-3)

**Priority**: Critical
**Estimated Time**: 3 days
**Dependencies**: Phase 2 Complete
**Assigned To**: Backend Developer 2

#### Tasks:
- [ ] **APR-001**: Create Appraisal module structure
  - Create Appraisal module project
  - Set up module registration
  - Configure dependency injection
  - **Time**: 2 hours

- [ ] **APR-002**: Create Appraisal aggregate
  - Implement Appraisal entity
  - Add appraisal creation from request
  - Add status management (Pending → Assigned → FieldSurvey → InProgress → Review → Completed)
  - Generate AppraisalNumber
  - Store RequestId (no FK)
  - **Time**: 6 hours

- [ ] **APR-003**: Create AppraisalAssignment entity
  - Implement AppraisalAssignment entity
  - Add assignment logic (Initial, Reassignment, Escalation)
  - Track assignment acceptance/rejection
  - **Time**: 4 hours

- [ ] **APR-004**: Create FieldSurvey entity
  - Implement FieldSurvey entity
  - Add scheduling logic
  - Track actual survey times
  - Store GPS coordinates
  - **Time**: 4 hours

- [ ] **APR-005**: Create ValuationAnalysis entity
  - Implement ValuationAnalysis entity
  - Add valuation calculations
  - Support multiple valuation approaches (Market, Cost, Income)
  - **Time**: 4 hours

- [ ] **APR-006**: Create AppraisalReview entity
  - Implement AppraisalReview entity
  - Add review workflow (Checker → Verifier → Committee)
  - Track review status and comments
  - **Time**: 4 hours

- [ ] **APR-007**: Create AppraisalDbContext (without photo gallery)
  - Set up AppraisalDbContext with core entities
  - Configure relationships
  - Apply conventions and indexes
  - Create and run migrations
  - **Time**: 3 hours

**GitHub Issues**:
```markdown
Title: [APR] Appraisal Module - Core Entities Implementation
Labels: appraisal, core, sprint-3, priority-critical
Assignee: Backend Developer 2
Milestone: Sprint 3 - Appraisal Core
Dependencies: Phase 2 Complete
```

---

### 3.2 Appraisal Commands & Queries (Week 5, Day 4-5)

**Priority**: Critical
**Estimated Time**: 2 days
**Dependencies**: APR-001 to APR-007
**Assigned To**: Backend Developer 3

#### Tasks:
- [ ] **APR-008**: Create appraisal domain events
  - AppraisalCreatedEvent
  - AppraisalAssignedEvent
  - FieldSurveyScheduledEvent
  - AppraisalCompletedEvent
  - **Time**: 2 hours

- [ ] **APR-009**: Create appraisal commands
  - CreateAppraisalCommand and handler (from RequestCreatedEvent)
  - AssignAppraisalCommand and handler
  - AcceptAssignmentCommand and handler
  - RejectAssignmentCommand and handler
  - ScheduleFieldSurveyCommand and handler
  - CompleteFieldSurveyCommand and handler
  - AddValuationAnalysisCommand and handler
  - SubmitForReviewCommand and handler
  - ReviewAppraisalCommand and handler
  - CompleteAppraisalCommand and handler
  - Add FluentValidation
  - **Time**: 10 hours

- [ ] **APR-010**: Create appraisal queries
  - GetAppraisalByIdQuery and handler
  - GetAppraisalByNumberQuery and handler
  - GetAppraisalsQuery with filtering/pagination
  - GetMyAssignmentsQuery (for appraiser)
  - GetPendingReviewsQuery (for reviewers)
  - **Time**: 5 hours

**GitHub Issues**:
```markdown
Title: [APR] Appraisal Commands & Queries Implementation
Labels: appraisal, cqrs, sprint-3, priority-critical
Assignee: Backend Developer 3
Milestone: Sprint 3 - Appraisal Core
Dependencies: APR-007
```

---

### 3.3 Appraisal Event Handlers (Week 6, Day 1)

**Priority**: Critical
**Estimated Time**: 1 day
**Dependencies**: APR-008 to APR-010
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **APR-011**: Create RequestCreated event consumer
  - Implement RequestCreatedEventConsumer
  - Auto-create Appraisal when request is created
  - Map request data to appraisal
  - **Time**: 4 hours

- [ ] **APR-012**: Create AppraisalCompleted event publisher
  - Publish AppraisalCompletedIntegrationEvent
  - Include all necessary data for Collateral module
  - **Time**: 2 hours

- [ ] **APR-013**: Test event-driven workflows
  - Test Request → Appraisal creation
  - Test Appraisal → Collateral creation (placeholder)
  - **Time**: 2 hours

**GitHub Issues**:
```markdown
Title: [APR] Appraisal Event Handlers Implementation
Labels: appraisal, events, sprint-3, priority-critical
Assignee: Backend Developer 1
Milestone: Sprint 3 - Appraisal Core
Dependencies: APR-010
```

---

### 3.4 Appraisal API Endpoints (Week 6, Day 2-3)

**Priority**: Critical
**Estimated Time**: 2 days
**Dependencies**: APR-011 to APR-013
**Assigned To**: Backend Developer 2

#### Tasks:
- [ ] **APR-014**: Create appraisal endpoints
  - GET /appraisals - List appraisals
  - GET /appraisals/{id} - Get appraisal by ID
  - POST /appraisals/{id}/assign - Assign appraisal
  - POST /appraisals/{id}/accept - Accept assignment
  - POST /appraisals/{id}/reject - Reject assignment
  - POST /appraisals/{id}/field-surveys - Schedule field survey
  - PUT /appraisals/{id}/field-surveys/{surveyId} - Complete survey
  - POST /appraisals/{id}/valuation - Add valuation analysis
  - POST /appraisals/{id}/submit-review - Submit for review
  - POST /appraisals/{id}/review - Review appraisal
  - POST /appraisals/{id}/complete - Complete appraisal
  - **Time**: 6 hours

- [ ] **APR-015**: Add authorization to endpoints
  - Only assigned appraiser can update
  - Only reviewers can review
  - Role-based access control
  - **Time**: 3 hours

- [ ] **APR-016**: Write Appraisal module tests
  - Unit tests for appraisal aggregate
  - Test assignment workflow
  - Test review workflow
  - Integration tests for API endpoints
  - **Time**: 7 hours

**GitHub Issues**:
```markdown
Title: [APR] Appraisal API Endpoints Implementation
Labels: appraisal, api, sprint-3, priority-critical
Assignee: Backend Developer 2
Milestone: Sprint 3 - Appraisal Core
Dependencies: APR-013
```

---

### 3.5 Collateral Module - Basic (Week 6, Day 4-5)

**Priority**: High
**Estimated Time**: 2 days
**Dependencies**: APR-014 to APR-016
**Assigned To**: Backend Developer 3

#### Tasks:
- [ ] **COL-001**: Create Collateral module structure
  - Create Collateral module project
  - Set up module registration
  - **Time**: 2 hours

- [ ] **COL-002**: Create Collateral aggregate (basic)
  - Implement Collateral entity (without property details)
  - Store AppraisalId and RequestId (no FK)
  - Add status management
  - Generate CollateralNumber
  - **Time**: 4 hours

- [ ] **COL-003**: Create CollateralValuationHistory entity
  - Implement CollateralValuationHistory entity
  - Track revaluation records
  - **Time**: 3 hours

- [ ] **COL-004**: Create CollateralDbContext (basic)
  - Set up CollateralDbContext
  - Configure relationships
  - Create and run migrations
  - **Time**: 2 hours

- [ ] **COL-005**: Create AppraisalCompleted event consumer
  - Implement AppraisalCompletedEventConsumer
  - Auto-create Collateral when appraisal is completed
  - Map appraisal data to collateral
  - **Time**: 4 hours

- [ ] **COL-006**: Create basic collateral queries
  - GetCollateralByIdQuery and handler
  - GetCollateralsQuery with filtering
  - **Time**: 3 hours

- [ ] **COL-007**: Create basic collateral endpoints
  - GET /collaterals - List collaterals
  - GET /collaterals/{id} - Get collateral by ID
  - **Time**: 2 hours

**GitHub Issues**:
```markdown
Title: [COL] Collateral Module - Basic Implementation
Labels: collateral, sprint-3, priority-high
Assignee: Backend Developer 3
Milestone: Sprint 3 - Appraisal Core
Dependencies: APR-016
```

---

## Phase 4: Advanced Features (Sprint 4: Week 7-8)

### 4.1 Photo Gallery System (Week 7, Day 1-3)

**Priority**: High
**Estimated Time**: 3 days
**Dependencies**: Phase 3 Complete
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **PHOTO-001**: Create photo gallery entities
  - Implement AppraisalGallery entity
  - Implement GalleryPhoto entity
  - Link to Document module for storage
  - Store GPS coordinates automatically
  - **Time**: 4 hours

- [ ] **PHOTO-002**: Create PropertyPhotoMapping entity
  - Implement PropertyPhotoMapping entity (polymorphic)
  - Support linking to any property detail type
  - Add section reference (Roof, Kitchen, etc.)
  - **Time**: 3 hours

- [ ] **PHOTO-003**: Update AppraisalDbContext with photo entities
  - Add photo tables to AppraisalDbContext
  - Configure relationships
  - Create and run migrations
  - **Time**: 2 hours

- [ ] **PHOTO-004**: Create photo upload commands
  - CreateGalleryCommand and handler
  - UploadPhotoCommand and handler (Phase 1: quick upload)
  - LinkPhotoToPropertyCommand and handler (Phase 2: office linking)
  - DeletePhotoCommand and handler
  - Add image validation (type, size)
  - Add GPS extraction from EXIF data
  - **Time**: 8 hours

- [ ] **PHOTO-005**: Create photo queries
  - GetAppraisalGalleryQuery and handler
  - GetGalleryPhotosQuery and handler
  - GetPhotosByPropertySectionQuery and handler
  - **Time**: 4 hours

- [ ] **PHOTO-006**: Create photo endpoints
  - POST /appraisals/{id}/galleries - Create gallery
  - POST /appraisals/{id}/galleries/{galleryId}/photos - Upload photo
  - GET /appraisals/{id}/galleries/{galleryId}/photos - List photos
  - POST /photos/{photoId}/link - Link photo to property section
  - DELETE /photos/{photoId} - Delete photo
  - **Time**: 4 hours

- [ ] **PHOTO-007**: Write photo gallery tests
  - Test two-phase workflow
  - Test polymorphic linking
  - Test GPS extraction
  - Integration tests
  - **Time**: 5 hours

**GitHub Issues**:
```markdown
Title: [PHOTO] Photo Gallery System Implementation
Labels: photo, media, sprint-4, priority-high
Assignee: Backend Developer 1
Milestone: Sprint 4 - Advanced Features
Dependencies: Phase 3 Complete
```

---

### 4.2 Property Detail Tables (Week 7, Day 4-5, Week 8, Day 1)

**Priority**: High
**Estimated Time**: 2.5 days
**Dependencies**: PHOTO-001 to PHOTO-007
**Assigned To**: Backend Developer 2

#### Tasks:
- [ ] **PROP-001**: Create LandAppraisalDetail entity
  - Implement LandAppraisalDetail entity
  - Add all land-specific fields
  - Add validation rules
  - **Time**: 3 hours

- [ ] **PROP-002**: Create BuildingAppraisalDetail entity
  - Implement BuildingAppraisalDetail entity
  - Add all building-specific fields
  - Add validation rules
  - **Time**: 3 hours

- [ ] **PROP-003**: Create CondoAppraisalDetail entity
  - Implement CondoAppraisalDetail entity
  - Add all condo-specific fields
  - **Time**: 2 hours

- [ ] **PROP-004**: Create VehicleAppraisalDetail entity
  - Implement VehicleAppraisalDetail entity
  - Add all vehicle-specific fields
  - **Time**: 2 hours

- [ ] **PROP-005**: Create VesselAppraisalDetail entity
  - Implement VesselAppraisalDetail entity
  - Add all vessel-specific fields
  - **Time**: 2 hours

- [ ] **PROP-006**: Create MachineryAppraisalDetail entity
  - Implement MachineryAppraisalDetail entity
  - Add all machinery-specific fields
  - **Time**: 2 hours

- [ ] **PROP-007**: Update AppraisalDbContext with property details
  - Add property detail tables
  - Configure one-to-one relationships with Appraisal
  - Create and run migrations
  - **Time**: 3 hours

- [ ] **PROP-008**: Create property detail commands
  - AddLandDetailCommand and handler
  - AddBuildingDetailCommand and handler
  - AddCondoDetailCommand and handler
  - AddVehicleDetailCommand and handler
  - AddVesselDetailCommand and handler
  - AddMachineryDetailCommand and handler
  - Add FluentValidation for each
  - **Time**: 6 hours

- [ ] **PROP-009**: Create property detail endpoints
  - POST /appraisals/{id}/property-details/land - Add land details
  - POST /appraisals/{id}/property-details/building - Add building details
  - POST /appraisals/{id}/property-details/condo - Add condo details
  - POST /appraisals/{id}/property-details/vehicle - Add vehicle details
  - POST /appraisals/{id}/property-details/vessel - Add vessel details
  - POST /appraisals/{id}/property-details/machinery - Add machinery details
  - **Time**: 4 hours

- [ ] **PROP-010**: Write property detail tests
  - Unit tests for each property type
  - Test photo linking to property sections
  - Integration tests
  - **Time**: 5 hours

**GitHub Issues**:
```markdown
Title: [PROP] Property Detail Tables Implementation
Labels: property, appraisal, sprint-4, priority-high
Assignee: Backend Developer 2
Milestone: Sprint 4 - Advanced Features
Dependencies: PHOTO-007
```

---

### 4.3 Collateral Property Details (Week 8, Day 2-3)

**Priority**: Medium
**Estimated Time**: 2 days
**Dependencies**: PROP-001 to PROP-010
**Assigned To**: Backend Developer 3

#### Tasks:
- [ ] **COL-008**: Create collateral property detail entities
  - Implement LandCollateral entity
  - Implement BuildingCollateral entity
  - Implement CondoCollateral entity
  - Implement VehicleCollateral entity
  - Implement VesselCollateral entity
  - Implement MachineryCollateral entity
  - **Time**: 6 hours

- [ ] **COL-009**: Update CollateralDbContext
  - Add property detail tables
  - Configure relationships
  - Create and run migrations
  - **Time**: 2 hours

- [ ] **COL-010**: Update AppraisalCompleted event consumer
  - Copy property details to collateral
  - Map from appraisal property details
  - **Time**: 4 hours

- [ ] **COL-011**: Create collateral property queries
  - GetCollateralDetailsQuery and handler
  - Support all property types
  - **Time**: 3 hours

- [ ] **COL-012**: Create collateral endpoints
  - GET /collaterals/{id}/details - Get property details
  - **Time**: 1 hour

**GitHub Issues**:
```markdown
Title: [COL] Collateral Property Details Implementation
Labels: collateral, sprint-4, priority-medium
Assignee: Backend Developer 3
Milestone: Sprint 4 - Advanced Features
Dependencies: PROP-010
```

---

### 4.4 Report Generation (Week 8, Day 4-5)

**Priority**: Medium
**Estimated Time**: 2 days
**Dependencies**: COL-008 to COL-012
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **REPORT-001**: Set up report generation library
  - Choose report library (QuestPDF or similar)
  - Install packages
  - Create report service interface
  - **Time**: 2 hours

- [ ] **REPORT-002**: Create appraisal report template
  - Design PDF report layout
  - Include property details
  - Include photos
  - Include valuation analysis
  - **Time**: 6 hours

- [ ] **REPORT-003**: Create report generation command
  - GenerateAppraisalReportCommand and handler
  - Save report to Document module
  - Link report to appraisal
  - **Time**: 4 hours

- [ ] **REPORT-004**: Create report endpoint
  - POST /appraisals/{id}/generate-report - Generate PDF report
  - GET /appraisals/{id}/report - Download report
  - **Time**: 2 hours

- [ ] **REPORT-005**: Write report generation tests
  - Test report generation
  - Verify PDF output
  - **Time**: 2 hours

**GitHub Issues**:
```markdown
Title: [REPORT] Appraisal Report Generation
Labels: report, document, sprint-4, priority-medium
Assignee: Backend Developer 1
Milestone: Sprint 4 - Advanced Features
Dependencies: COL-012
```

---

## Additional Tasks (Ongoing)

### 5.1 API Documentation

**Priority**: High
**Estimated Time**: Ongoing
**Assigned To**: Technical Writer / Backend Lead

#### Tasks:
- [ ] **DOC-API-001**: Set up OpenAPI/Swagger
  - Configure Swagger in development
  - Add XML documentation comments
  - **Time**: 2 hours

- [ ] **DOC-API-002**: Document all endpoints
  - Add detailed descriptions
  - Add request/response examples
  - Add error codes
  - **Time**: 8 hours (spread across sprints)

- [ ] **DOC-API-003**: Create Postman collection
  - Export OpenAPI to Postman
  - Add example requests
  - **Time**: 2 hours

**GitHub Issues**:
```markdown
Title: [DOC] API Documentation
Labels: documentation, api, priority-high
Assignee: Technical Writer
Milestone: Ongoing
```

---

### 5.2 Performance Optimization

**Priority**: Medium
**Estimated Time**: Ongoing
**Assigned To**: Backend Developer 1

#### Tasks:
- [ ] **PERF-001**: Database query optimization
  - Add missing indexes
  - Optimize N+1 queries
  - Use projections where appropriate
  - **Time**: 4 hours

- [ ] **PERF-002**: Implement pagination
  - Add pagination to all list endpoints
  - Use cursor-based pagination for large datasets
  - **Time**: 4 hours

- [ ] **PERF-003**: Add response compression
  - Configure response compression middleware
  - **Time**: 1 hour

- [ ] **PERF-004**: Profile and optimize
  - Use profiling tools
  - Identify bottlenecks
  - Optimize hot paths
  - **Time**: 6 hours

**GitHub Issues**:
```markdown
Title: [PERF] Performance Optimization
Labels: performance, optimization, priority-medium
Assignee: Backend Developer 1
Milestone: Ongoing
```

---

### 5.3 Security Hardening

**Priority**: High
**Estimated Time**: Ongoing
**Assigned To**: Security Lead / Backend Developer 2

#### Tasks:
- [ ] **SEC-001**: Implement rate limiting
  - Add rate limiting middleware
  - Configure per-endpoint limits
  - **Time**: 3 hours

- [ ] **SEC-002**: Add input sanitization
  - Sanitize all user inputs
  - Prevent SQL injection (use parameterized queries)
  - Prevent XSS
  - **Time**: 4 hours

- [ ] **SEC-003**: Implement CORS policy
  - Configure CORS for production
  - Whitelist allowed origins
  - **Time**: 1 hour

- [ ] **SEC-004**: Add security headers
  - X-Content-Type-Options
  - X-Frame-Options
  - Content-Security-Policy
  - **Time**: 2 hours

- [ ] **SEC-005**: Security audit
  - Run security scanning tools
  - Fix vulnerabilities
  - **Time**: 4 hours

**GitHub Issues**:
```markdown
Title: [SEC] Security Hardening
Labels: security, priority-high
Assignee: Security Lead
Milestone: Ongoing
```

---

## Testing Strategy

### Unit Testing
- **Target**: 80%+ code coverage
- **Frequency**: Per feature implementation
- **Focus**: Domain logic, validators, commands, queries

### Integration Testing
- **Target**: Cover all API endpoints
- **Frequency**: Per module completion
- **Focus**: End-to-end workflows, database operations

### Load Testing (Optional)
- **Target**: 100 concurrent users
- **Tools**: k6, Apache JMeter
- **Focus**: Identify performance bottlenecks

---

## Deployment Checklist

### Pre-Production
- [ ] All migrations tested
- [ ] All tests passing (unit + integration)
- [ ] Security audit completed
- [ ] Performance benchmarks met
- [ ] API documentation complete
- [ ] Production configuration verified
- [ ] Backup and restore procedures tested

### Production Deployment
- [ ] Database backup created
- [ ] Migrations applied
- [ ] Application deployed
- [ ] Health checks passing
- [ ] Monitoring configured (Seq, Application Insights)
- [ ] Smoke tests passed

---

## Sprint Planning

### Sprint 1 (Week 1-2): Foundation
**Goal**: Infrastructure and Auth Module
**Deliverables**:
- Working development environment
- Authentication and authorization
- User management API
- OAuth2 token endpoint

**Team Capacity**: 80 hours (4 developers × 20 hours)
**Estimated Effort**: 75 hours
**Buffer**: 5 hours

---

### Sprint 2 (Week 3-4): Core Modules
**Goal**: Request and Document Modules
**Deliverables**:
- Document upload/download
- Request creation and management
- Request-to-Appraisal event flow
- Caching layer

**Team Capacity**: 80 hours
**Estimated Effort**: 78 hours
**Buffer**: 2 hours

---

### Sprint 3 (Week 5-6): Appraisal Core
**Goal**: Appraisal workflow and Collateral basic
**Deliverables**:
- Appraisal assignment
- Field survey management
- Review workflow
- Basic collateral creation

**Team Capacity**: 80 hours
**Estimated Effort**: 76 hours
**Buffer**: 4 hours

---

### Sprint 4 (Week 7-8): Advanced Features
**Goal**: Photo Gallery, Property Details, Reports
**Deliverables**:
- Photo gallery system
- Property detail tables (all 6 types)
- Collateral property details
- PDF report generation

**Team Capacity**: 80 hours
**Estimated Effort**: 73 hours
**Buffer**: 7 hours

---

## Risk Management

### High-Risk Items
1. **OAuth2 Integration** - Complex setup
   - **Mitigation**: Allocate extra time, use existing examples

2. **Event-Driven Architecture** - New to team
   - **Mitigation**: Spike solution early, thorough testing

3. **Photo Gallery Polymorphic Mapping** - Complex relationships
   - **Mitigation**: Create proof-of-concept first

4. **Cloud Storage Integration** - External dependency
   - **Mitigation**: Use local filesystem for development

### Dependencies
- External: None (all dependencies within project)
- Infrastructure: Docker services must be running
- Team: Minimum 3 developers required

---

## Success Metrics

### Code Quality
- Code coverage: ≥80%
- Static analysis: No critical issues
- Code review: 100% of PRs reviewed

### Performance
- API response time: <200ms (p95)
- Database queries: <50ms average
- Concurrent users: 100+

### Delivery
- Sprint completion: ≥90%
- Defect rate: <5% of stories
- On-time delivery: 100%

---

## Conclusion

This WBS provides a structured approach to implementing the Collateral Appraisal System within 2 months. The phased approach ensures:

1. **Foundation First**: Auth and infrastructure in Sprint 1
2. **Core Functionality**: Request and Document in Sprint 2
3. **Business Logic**: Appraisal workflow in Sprint 3
4. **Advanced Features**: Photo gallery and property details in Sprint 4

**Total Estimated Effort**: ~300 hours
**Team Size**: 3-5 developers
**Timeline**: 8 weeks (2 months)
**Success Probability**: High (with proper team and resources)

---

## Next Steps

1. **Create GitHub Project Board** with these sprints
2. **Convert tasks to GitHub Issues** using templates provided
3. **Assign team members** to initial tasks
4. **Set up CI/CD pipeline** for automated testing
5. **Schedule daily standups** and sprint planning meetings
6. **Begin Sprint 1** implementation

---

**Document Version**: 1.0
**Last Updated**: 2025-01-02
**Author**: Development Team
**Status**: Ready for Implementation ✅
