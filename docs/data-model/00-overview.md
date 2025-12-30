# System Overview

## Introduction

The Collateral Appraisal System is a comprehensive platform designed to manage the complete lifecycle of collateral appraisals for financial institutions. The system supports multiple property types, complex workflows, role-based access control, and comprehensive documentation.

## Business Context

### Purpose
Help financial institutions manage collateral appraisals for loan applications by:
- Streamlining request creation and processing
- Managing appraisal assignments and field surveys
- Supporting multiple collateral types (land, building, condo, vehicle, vessel, machinery)
- Maintaining comprehensive documentation with photos and reports
- Ensuring compliance with approval workflows

### Key Users
- **Relationship Managers (RM)**: Create appraisal requests
- **Appraisers**: Conduct site visits and property valuations
- **Checkers/Verifiers**: Review and verify appraisals
- **Committee Approvers**: Final approval authority
- **Administrators**: System configuration and user management

## Architecture Principles

### 1. Domain-Driven Design (DDD)

The system is built following DDD principles:

**Bounded Contexts**: Each module represents a bounded context with clear boundaries
```
Request Context    → Manages loan appraisal requests
Appraisal Context  → Handles property valuation process
Collateral Context → Maintains collateral records
Document Context   → Manages all documentation
Auth Context       → Handles authentication and authorization
```

**Aggregates**: Each module has one or more aggregate roots that enforce consistency
```
Request        → Aggregate root for request management
Appraisal      → Aggregate root for appraisal process
Collateral     → Aggregate root for collateral management
Document       → Aggregate root for document storage
User           → Aggregate root for user management
```

**Value Objects**: Immutable objects representing domain concepts
```
Money          → Amount with currency
Address        → Location information
ContactInfo    → Contact details
TimeSlot       → Date/time range
LandArea       → Area measurements (Rai, Ngan, Square Wa)
```

**Domain Events**: Communicate state changes across aggregates
```
RequestCreatedEvent       → Triggers appraisal creation
AppraisalCompletedEvent   → Triggers collateral creation
DocumentUploadedEvent     → Triggers audit logging
```

### 2. Modular Monolith

The system uses a modular monolith architecture:

**Benefits**:
- ✅ Logical separation of concerns
- ✅ Independent development and testing
- ✅ Clear module boundaries
- ✅ Easier to evolve into microservices if needed
- ✅ Simplified deployment and operations

**Module Structure**:
```
Modules/
├── Request/
│   ├── Request/              # Domain layer
│   │   ├── Models/
│   │   ├── Repositories/
│   │   ├── Features/
│   │   └── Events/
│   └── Request.Tests/
├── Appraisal/
│   └── Appraisal/
├── Collateral/
│   └── Collateral/
├── Document/
│   └── Document/
└── Auth/
    └── Auth/
```

### 3. CQRS (Command Query Responsibility Segregation)

**Commands**: Modify state
```csharp
CreateRequestCommand
UpdateAppraisalCommand
UploadDocumentCommand
```

**Queries**: Read state
```csharp
GetRequestByIdQuery
GetAppraisalsByStatusQuery
SearchDocumentsQuery
```

**Benefits**:
- ✅ Optimized read and write models
- ✅ Scalable query performance
- ✅ Clear separation of concerns
- ✅ Easier to cache read models

### 4. Event-Driven Architecture

**Integration Events**: Cross-module communication via message bus
```
Request Module → MassTransit/RabbitMQ → Appraisal Module
```

**Benefits**:
- ✅ Loose coupling between modules
- ✅ Asynchronous processing
- ✅ Resilience and retry capabilities
- ✅ Audit trail of all events

## System Workflow

### High-Level Process Flow

```
┌─────────────┐
│   RM/LOS    │
│   Creates   │
│   Request   │
└──────┬──────┘
       │
       ▼
┌─────────────────────┐
│  Request Created    │
│  - Attach Documents │
│  - Title Deed Info  │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│ Appraisal Created   │
│ - Auto Assignment   │
│ - Due Date Set      │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│  Field Survey       │
│  - Site Visit       │
│  - Upload Photos    │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│ Property Analysis   │
│ - Create Details    │
│ - Link Photos       │
│ - Valuation         │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│ Appraisal Report    │
│ - Generate Report   │
│ - Submit Review     │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│  Review Process     │
│  - Checker          │
│  - Verifier         │
│  - Committee        │
└──────┬──────────────┘
       │
       ▼
┌─────────────────────┐
│ Collateral Created  │
│ - Final Record      │
│ - Market Value      │
└─────────────────────┘
```

### Detailed Workflow

#### Phase 1: Request Creation
1. RM creates request (manual or from LOS system)
2. RM attaches required documents
3. RM enters title deed information
4. RM submits request
5. System fires `RequestCreatedEvent`

#### Phase 2: Appraisal Assignment
1. System receives `RequestCreatedEvent`
2. System creates appraisal record
3. System assigns appraiser (auto or manual)
4. Appraiser receives notification
5. Appraiser accepts assignment

#### Phase 3: Field Survey (Mobile)
1. Appraiser visits property
2. Appraiser uses mobile app to:
   - Upload photos with GPS coordinates
   - Record videos
   - Take voice notes
   - Quick categorization
3. Photos stored in `AppraisalGallery`
4. `UploadSession` tracks the batch

#### Phase 4: Property Analysis (Office)
1. Appraiser returns to office
2. Appraiser reviews gallery photos
3. Appraiser creates property detail record:
   - `LandAppraisalDetail` for land
   - `BuildingAppraisalDetail` for building
   - `CondoAppraisalDetail` for condo
   - etc.
4. Appraiser selects photos from gallery
5. System creates `PropertyPhotoMapping` to link photos to property sections
6. Appraiser adds annotations to photos
7. Appraiser marks photos for report inclusion

#### Phase 5: Valuation
1. Appraiser completes valuation analysis
2. Appraiser enters comparable properties
3. Appraiser calculates values:
   - Market value
   - Appraised value
   - Forced sale value
4. Appraiser creates appraisal report

#### Phase 6: Review & Approval
1. Appraiser submits for review
2. Internal Checker reviews
3. Internal Verifier reviews
4. Committee approves
5. System fires `AppraisalCompletedEvent`

#### Phase 7: Collateral Creation
1. System receives `AppraisalCompletedEvent`
2. System creates collateral record
3. System links to appraisal
4. Collateral becomes active

## Module Responsibilities

### Request Management Module
**Purpose**: Manage appraisal requests from creation to submission

**Key Responsibilities**:
- Create and track appraisal requests
- Store title deed information
- Link documents to requests
- Track request status history
- Integration with LOS systems

**Aggregate Root**: `Request`

**Key Entities**:
- Request
- RequestDocument
- TitleDeedInfo
- RequestStatusHistory

### Appraisal Module
**Purpose**: Handle the complete appraisal lifecycle

**Key Responsibilities**:
- Manage appraisal assignments
- Track field surveys and site visits
- Store property-specific details (6 types)
- Manage photo gallery and media
- Generate valuation analysis
- Create appraisal reports
- Handle review and approval workflow

**Aggregate Root**: `Appraisal`

**Key Entities**:
- Appraisal
- AppraisalAssignment
- FieldSurvey
- PropertyInformation
- ValuationAnalysis
- AppraisalReport
- AppraisalReview
- AppraisalGallery
- GalleryPhoto
- LandAppraisalDetail
- BuildingAppraisalDetail
- CondoAppraisalDetail
- VehicleAppraisalDetail
- VesselAppraisalDetail
- MachineryAppraisalDetail

### Collateral Management Module
**Purpose**: Maintain collateral records post-appraisal

**Key Responsibilities**:
- Store final collateral information
- Support multiple collateral types
- Track valuation history
- Manage collateral lifecycle
- Link to appraisals and documents

**Aggregate Root**: `Collateral`

**Key Entities**:
- Collateral
- LandCollateral
- BuildingCollateral
- CondoCollateral
- VehicleCollateral
- VesselCollateral
- MachineryCollateral
- CollateralValuationHistory
- CollateralDocument

### Document Module
**Purpose**: Centralized document management

**Key Responsibilities**:
- Store all system documents
- Support versioning
- Manage access control
- Track document access logs
- Cloud storage integration
- Document relationships

**Aggregate Root**: `Document`

**Key Entities**:
- Document
- DocumentVersion
- DocumentRelationship
- DocumentAccess
- DocumentAccessLog
- DocumentTemplate

### Authentication & Authorization Module
**Purpose**: User management and security

**Key Responsibilities**:
- User authentication (OAuth2/OpenIddict)
- Role-based access control (RBAC)
- Permission management
- Organization management (for external firms)
- Audit logging
- Security policies

**Aggregate Root**: `User`

**Key Entities**:
- User
- Role
- Permission
- UserRole
- RolePermission
- UserPermission
- Organization
- UserOrganization
- AuditLog
- SecurityPolicy

## Technical Architecture

### Database Design

**Schema Separation**: Each module has its own schema
```sql
request.*
appraisal.*
collateral.*
document.*
auth.*
```

**Primary Keys**: GUID (UUID) for distributed systems
```sql
Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID()
```

**Business Keys**: Unique identifiers for business entities
```sql
RequestNumber VARCHAR(50) UNIQUE NOT NULL
AppraisalNumber VARCHAR(50) UNIQUE NOT NULL
CollateralNumber VARCHAR(50) UNIQUE NOT NULL
```

**Audit Fields**: Standard on all entities
```sql
CreatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE()
CreatedBy UNIQUEIDENTIFIER NOT NULL
UpdatedOn DATETIME2 NOT NULL DEFAULT GETUTCDATE()
UpdatedBy UNIQUEIDENTIFIER NOT NULL
```

**Soft Delete**: Optional on sensitive entities
```sql
IsDeleted BIT NOT NULL DEFAULT 0
DeletedOn DATETIME2 NULL
DeletedBy UNIQUEIDENTIFIER NULL
```

**Concurrency Control**: Optimistic locking
```sql
RowVersion ROWVERSION NOT NULL
```

### Cross-Module Communication

**Domain Events**: Published within same module
```csharp
// In Request Module
public class RequestCreatedEvent : IDomainEvent
{
    public Guid RequestId { get; set; }
    public DateTime OccurredOn { get; set; }
}
```

**Integration Events**: Published to message bus
```csharp
// Published by Request Module, consumed by Appraisal Module
public class RequestCreatedIntegrationEvent : IntegrationEvent
{
    public Guid RequestId { get; set; }
    public string RequestNumber { get; set; }
    public DateTime RequestDate { get; set; }
}
```

### Data Integrity

**Foreign Key Strategy**:
- Within module: Use database foreign keys
- Cross-module: Store only ID, no FK constraint
- Use eventual consistency for cross-module references

**Transaction Boundaries**:
- One transaction per aggregate
- Domain events dispatched at commit
- Integration events via outbox pattern

### Performance Considerations

**Indexing Strategy**:
```sql
-- Frequently queried fields
CREATE INDEX IX_Request_Status ON request.Requests(Status)
CREATE INDEX IX_Request_RequestedBy ON request.Requests(RequestedBy)
CREATE INDEX IX_Appraisal_Status_DueDate ON appraisal.Appraisals(Status, DueDate)
```

**Caching**:
- Reference data cached in Redis
- Decorator pattern for repository caching
- Cache invalidation on updates

**Query Optimization**:
- Use read models for complex queries
- Implement pagination
- Use projection (SELECT only needed columns)
- Consider indexed views for reports

## Security & Compliance

### Authentication
- OAuth2/OpenID Connect via OpenIddict
- JWT tokens for API access
- Refresh token rotation

### Authorization
- Role-based access control (RBAC)
- Permission-based operations
- Row-level security for multi-tenant data

### Data Protection
- Encryption at rest for sensitive fields
- TLS for data in transit
- Field-level encryption for PII
- Document encryption in storage

### Audit & Compliance
- Comprehensive audit logging
- Document access tracking
- Data retention policies
- GDPR compliance support

## Scalability & Reliability

### Scalability Patterns
- Horizontal scaling of API layer
- Read replicas for queries
- Table partitioning for historical data
- CDN for document delivery

### Reliability Patterns
- Retry policies for external calls
- Circuit breakers for dependencies
- Message queue for async processing
- Database backups and replication

### Monitoring & Observability
- Structured logging with Serilog
- Performance metrics
- Error tracking
- Business metrics dashboards

## Development Guidelines

### Code Organization
```
Module/
├── Models/               # Domain entities and value objects
├── Repositories/         # Data access interfaces and implementations
├── Features/            # CQRS commands and queries
│   ├── CreateX/
│   │   ├── CreateXCommand.cs
│   │   ├── CreateXCommandHandler.cs
│   │   ├── CreateXEndpoint.cs
│   │   └── CreateXValidator.cs
│   └── GetX/
├── Events/              # Domain and integration events
├── Mappings/            # Entity Framework configurations
└── ModuleExtensions.cs  # DI registration
```

### Best Practices
- One aggregate per transaction
- Validate in domain model
- Use value objects for domain concepts
- Publish events for state changes
- Keep aggregates small
- Use repository pattern
- Implement CQRS for complex scenarios
- Cache reference data

### Testing Strategy
- Unit tests for domain logic
- Integration tests for repositories
- API tests for endpoints
- End-to-end tests for workflows

---

**Next**: Read [01-module-summary.md](01-module-summary.md) for detailed module breakdown.
