# Module Summary

## Overview

This document provides a high-level summary of all modules in the Collateral Appraisal System, their responsibilities, and key entities.

## Module Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Bootstrapper/API                          │
│                  (ASP.NET Core 9.0)                          │
└─────────────────────────────────────────────────────────────┘
         │                │                │                │
    ┌────▼────┐     ┌────▼────┐     ┌────▼────┐     ┌────▼────┐
    │ Request │     │Appraisal│     │Collateral│    │Document │
    │ Module  │────▶│ Module  │────▶│ Module   │    │ Module  │
    └─────────┘     └─────────┘     └──────────┘    └─────────┘
         │                │                │              │
         └────────────────┴────────────────┴──────────────┘
                              │
                         ┌────▼────┐
                         │  Auth   │
                         │ Module  │
                         └─────────┘
```

## 1. Request Management Module

**Purpose**: Manage appraisal requests from creation to submission

**Schema**: `request.*`

**Aggregate Root**: `Request`

### Key Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| **Requests** | Main request entity | RequestNumber, Status, Priority, LoanAmount |
| **RequestCustomers** | Customer information (1:many) | FirstName, LastName, ContactNumber |
| **RequestPropertyTypes** | Property type information (1:many) | PropertyType, PropertySubType |
| **RequestDocuments** | Document attachments | DocumentId, DocumentType |
| **TitleDeedInfo** | Title deed details | TitleDeedNumber, DeedType, LandArea |
| **RequestStatusHistory** | Status change audit trail | FromStatus, ToStatus, ChangedAt |

### Responsibilities
- ✅ Create and manage appraisal requests
- ✅ Track multiple customers per request
- ✅ Track multiple property types per request
- ✅ Attach documents to requests
- ✅ Store title deed information
- ✅ Maintain status history audit trail
- ✅ Integration with LOS systems

### Domain Events
- `RequestCreatedEvent` → Triggers appraisal creation
- `RequestSubmittedEvent` → Notifies assignment system
- `RequestCancelledEvent` → Cleanup and notifications

### Key Features
- **One-to-Many Customers**: Support for primary borrower, co-borrowers, guarantors
- **One-to-Many Properties**: Single request can include multiple property types
- **Status Workflow**: Draft → Submitted → Assigned → InProgress → Completed
- **LOS Integration**: Manual creation or automated from Loan Origination System

---

## 2. Appraisal Module

**Purpose**: Handle complete appraisal lifecycle from assignment to completion

**Schema**: `appraisal.*`

**Aggregate Root**: `Appraisal`

### Key Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| **Appraisals** | Main appraisal entity | AppraisalNumber, Status, DueDate |
| **AppraisalAssignments** | Assignment tracking | AssignedTo, AssignmentStatus |
| **FieldSurveys** | Site visit records | SurveyDate, Location, GPS |
| **PropertyInformation** | General property info | PropertyType, Location |
| **ValuationAnalysis** | Valuation calculations | MarketValue, AppraisedValue |
| **AppraisalReports** | Generated reports | ReportType, ReportUrl |
| **AppraisalReviews** | Review workflow | ReviewType, ReviewStatus |
| **AppraisalGallery** | Photo collection container | GalleryName, TotalPhotos |
| **GalleryPhotos** | Individual photos | PhotoType, Caption, GPS |
| **PropertyPhotoMappings** | Link photos to property sections | SectionReference, PhotoPurpose |

### Property Detail Tables (One per Type)

| Entity | For Property Type | Key Characteristics |
|--------|------------------|---------------------|
| **LandAppraisalDetails** | Land | Topography, SoilType, Utilities, Access |
| **BuildingAppraisalDetails** | Building | Structure, Roof, Walls, Rooms, Kitchen |
| **CondoAppraisalDetails** | Condo | Project, Unit, Facilities, Common Areas |
| **VehicleAppraisalDetails** | Vehicle | Exterior, Interior, Mechanical, Mileage |
| **VesselAppraisalDetails** | Vessel | Hull, Engine, Navigation, Safety |
| **MachineryAppraisalDetails** | Machinery | Specifications, Condition, Maintenance |

### Responsibilities
- ✅ Manage appraisal assignments
- ✅ Track field surveys and site visits
- ✅ Store property-specific details (6 property types)
- ✅ Manage photo gallery and media (two-phase workflow)
- ✅ Generate valuation analysis
- ✅ Create appraisal reports
- ✅ Handle review and approval workflow

### Domain Events
- `AppraisalCreatedEvent` → Notifies appraiser
- `AppraisalAssignedEvent` → Assignment notifications
- `FieldSurveyCompletedEvent` → Triggers office work
- `AppraisalCompletedEvent` → Triggers collateral creation
- `AppraisalRejectedEvent` → Reassignment workflow

### Key Features
- **Two-Phase Photo Workflow**:
  - Phase 1 (Site Visit): Quick upload to gallery with minimal categorization
  - Phase 2 (Office): Link photos to specific property sections
- **Property Type Specialization**: Separate detail tables for each property type
- **Review Workflow**: Internal Checker → Internal Verifier → Committee Approver
- **GPS Tracking**: Automatic location capture for photos and surveys

---

## 3. Collateral Management Module

**Purpose**: Maintain collateral records post-appraisal completion

**Schema**: `collateral.*`

**Aggregate Root**: `Collateral`

### Key Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| **Collaterals** | Main collateral entity | CollateralNumber, Status, MarketValue |
| **LandCollaterals** | Land-specific data | LandArea, Topography, Zoning |
| **BuildingCollaterals** | Building-specific data | BuildingType, Area, Condition |
| **CondoCollaterals** | Condo-specific data | Project, Unit, Floor |
| **VehicleCollaterals** | Vehicle-specific data | Brand, Model, Year, Mileage |
| **VesselCollaterals** | Vessel-specific data | VesselType, Length, Engine |
| **MachineryCollaterals** | Machinery-specific data | Type, Brand, Capacity |
| **CollateralValuationHistory** | Revaluation tracking | ValuationDate, Values |
| **CollateralDocuments** | Final documentation | DocumentType, DocumentId |

### Responsibilities
- ✅ Store final collateral information
- ✅ Support multiple collateral types (6 types)
- ✅ Track valuation history over time
- ✅ Manage collateral lifecycle (Active, Disposed, Written Off)
- ✅ Link to appraisals and documents
- ✅ Revaluation scheduling and tracking

### Domain Events
- `CollateralCreatedEvent` → Notifies loan system
- `CollateralRevaluationScheduledEvent` → Schedule next appraisal
- `CollateralDisposedEvent` → Update loan records
- `CollateralValueChangedEvent` → Risk assessment updates

### Key Features
- **Valuation History**: Track all revaluations over time
- **Lifecycle Management**: Active → Under Review → Disposed → Written Off
- **Risk Monitoring**: Automated alerts for value changes
- **Revaluation Scheduling**: Automatic scheduling based on policy

---

## 4. Document Module

**Purpose**: Centralized document management with versioning and access control

**Schema**: `document.*`

**Aggregate Root**: `Document`

### Key Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| **Documents** | Main document entity | DocumentNumber, FileName, StorageUrl |
| **DocumentVersions** | Version history | VersionNumber, ChangeSummary |
| **DocumentRelationships** | Document links | RelationType, RelatedDocumentId |
| **DocumentAccess** | Access permissions | UserId, AccessLevel, ExpiresAt |
| **DocumentAccessLogs** | Access audit trail | AccessedBy, AccessedAt, Action |
| **DocumentTemplates** | Report templates | TemplateName, TemplateContent |

### Responsibilities
- ✅ Store all system documents (photos, PDFs, reports)
- ✅ Support document versioning
- ✅ Manage access control and permissions
- ✅ Track document access logs (audit trail)
- ✅ Cloud storage integration (Azure Blob, AWS S3)
- ✅ Document relationship management
- ✅ Template management for reports

### Domain Events
- `DocumentUploadedEvent` → Virus scanning, processing
- `DocumentAccessedEvent` → Audit logging
- `DocumentDeletedEvent` → Cleanup and notifications
- `DocumentSharedEvent` → Access notifications

### Key Features
- **Cloud Storage**: Azure Blob Storage or AWS S3 integration
- **Versioning**: Complete version history with rollback capability
- **Access Control**: User/role-based permissions with expiration
- **Encryption**: At-rest and in-transit encryption
- **Audit Trail**: Complete access logging for compliance

---

## 5. Authentication & Authorization Module

**Purpose**: User management, authentication, and role-based access control

**Schema**: `auth.*`

**Aggregate Root**: `User`

### Key Entities

| Entity | Purpose | Key Fields |
|--------|---------|------------|
| **Users** | User accounts | Username, Email, EmployeeId |
| **Roles** | Role definitions | RoleName, RoleDescription |
| **Permissions** | Permission definitions | PermissionName, Resource, Action |
| **UserRoles** | User-role assignments | UserId, RoleId |
| **RolePermissions** | Role-permission mappings | RoleId, PermissionId |
| **UserPermissions** | Direct user permissions | UserId, PermissionId |
| **Organizations** | External organizations | OrganizationName, Type |
| **UserOrganizations** | User-org relationships | UserId, OrganizationId |
| **AuditLogs** | System audit trail | Action, Entity, Changes |
| **SecurityPolicies** | Security settings | PolicyType, Configuration |

### Responsibilities
- ✅ User authentication (OAuth2/OpenIddict)
- ✅ Role-based access control (RBAC)
- ✅ Permission management
- ✅ Organization management (for external appraisers)
- ✅ Audit logging for security events
- ✅ Security policy enforcement
- ✅ Session management

### Domain Events
- `UserCreatedEvent` → Welcome notifications
- `UserRoleAssignedEvent` → Permission updates
- `UserLoginEvent` → Security monitoring
- `UserPasswordChangedEvent` → Security alerts
- `UnauthorizedAccessEvent` → Security alerts

### Key Features
- **OAuth2/OpenID Connect**: OpenIddict implementation
- **RBAC**: Role and permission-based authorization
- **Multi-Organization**: Support for external appraisal firms
- **Audit Logging**: Complete security audit trail
- **Password Policies**: Complexity, expiration, history

---

## Module Dependencies

### Event Flow

```
Request Created
    │
    ├─→ Appraisal Created (Event)
    │
    └─→ Documents Linked

Appraisal Completed
    │
    ├─→ Collateral Created (Event)
    │
    └─→ Documents Finalized

User Actions
    │
    └─→ Audit Logs Created
```

### Cross-Module Integration

| From Module | To Module | Integration Type | Purpose |
|-------------|-----------|------------------|---------|
| Request → Appraisal | Integration Event | Trigger appraisal creation |
| Appraisal → Collateral | Integration Event | Create final collateral record |
| All → Document | Direct Reference | Store/retrieve documents |
| All → Auth | Direct Query | Authorization checks |
| All → Auth | Audit Event | Log all actions |

### Shared Concepts

**Value Objects** (used across modules):
- `Money` - Amount with currency
- `Address` - Location information
- `ContactInfo` - Contact details
- `Location` - GPS coordinates
- `TimeSlot` - Date/time range
- `LandArea` - Rai, Ngan, Square Wa

**Common Enumerations**:
- `PropertyType` - Land, Building, Condo, Vehicle, Vessel, Machinery
- `Status` - Draft, Submitted, InProgress, Completed, Cancelled
- `Priority` - Low, Normal, High, Urgent

---

## Database Statistics Summary

| Module | Tables | Aggregate Roots | Value Objects | Enumerations |
|--------|--------|----------------|---------------|--------------|
| Request | 6 | 1 (Request) | 3 | 5 |
| Appraisal | 20+ | 1 (Appraisal) | 8 | 12 |
| Collateral | 9 | 1 (Collateral) | 2 | 4 |
| Document | 6 | 1 (Document) | 1 | 3 |
| Auth | 10 | 1 (User) | 0 | 2 |
| **Total** | **51+** | **5** | **14** | **26** |

---

## Technology Stack

### Backend
- **.NET 9.0** - Framework
- **ASP.NET Core** - Web API
- **Entity Framework Core** - ORM
- **SQL Server** - Database
- **MediatR** - CQRS pattern
- **MassTransit** - Event bus
- **Carter** - Minimal APIs

### Infrastructure
- **Redis** - Caching
- **RabbitMQ** - Message broker
- **Azure Blob Storage** - Document storage
- **Seq** - Structured logging
- **OpenIddict** - OAuth2 server

### Development
- **Docker** - Containerization
- **Docker Compose** - Local infrastructure
- **xUnit** - Unit testing
- **FluentValidation** - Input validation

---

## Next Steps

For detailed information about each module, refer to:

- **[02-request-module.md](02-request-module.md)** - Request Management
- **[03-appraisal-module.md](03-appraisal-module.md)** - Appraisal
- **[04-collateral-module.md](04-collateral-module.md)** - Collateral Management
- **[05-document-module.md](05-document-module.md)** - Document
- **[06-auth-module.md](06-auth-module.md)** - Authentication & Authorization

For implementation details:
- **[12-implementation-guide.md](12-implementation-guide.md)** - Developer guidelines
- **[15-sample-data.md](15-sample-data.md)** - Sample data for testing
