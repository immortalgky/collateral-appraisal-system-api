# Collateral Appraisal System - Data Model Documentation

## Project Overview

The Collateral Appraisal System is a comprehensive platform designed to manage the complete lifecycle of collateral appraisals for financial institutions. The system supports multiple property types, complex workflows, role-based access control, and comprehensive documentation.

---

## Design Status (Updated: 2025-01-02)

✅ **Phase 1: COMPLETED** - Production-grade data model design completed with comprehensive documentation

### What Has Been Completed

1. **Module Architecture** - 5 modules with clear bounded contexts
2. **Complete Documentation** - 12 detailed documentation files created
3. **Simplified Request Module** - Customer and Property tracking simplified as requested
4. **All Module Schemas** - SQL schemas, C# models, indexes for all 5 modules
5. **Sample Data** - Comprehensive sample data for testing
6. **ER Diagrams** - Complete entity relationship diagrams

---

## System Architecture

### Design Principles

1. **Domain-Driven Design (DDD)**
   - Each module = bounded context
   - Aggregate roots with clear boundaries
   - Value objects for complex concepts
   - Domain events for cross-module communication

2. **Modular Monolith**
   - Logical separation with independent modules
   - Each module has its own DbContext
   - Cross-module references via IDs only (no FK constraints)
   - Event-driven integration via MassTransit/RabbitMQ

3. **CQRS Pattern**
   - Commands for state changes
   - Queries for read operations
   - MediatR for command/query handling

4. **Event-Driven Architecture**
   - Domain events within modules
   - Integration events across modules
   - Asynchronous processing with retry

---

## Modules Overview

### 1. Request Management Module
**Purpose**: Manage appraisal requests from creation to submission

**Key Tables** (6 total):
- `Requests` - Main request entity
- `RequestCustomers` - **SIMPLIFIED**: FirstName, LastName, ContactNumber only
- `RequestPropertyTypes` - **SIMPLIFIED**: PropertyType, PropertySubType only
- `RequestDocuments` - Document links
- `TitleDeedInfo` - Legal documentation
- `RequestStatusHistory` - Audit trail

**Key Design Decision**:
- Customer and property tables simplified to minimal fields
- Detailed information stored in TitleDeedInfo or external systems
- Focus on fast request creation by RMs

### 2. Appraisal Module
**Purpose**: Handle complete appraisal lifecycle

**Key Tables** (20+ total):
- `Appraisals` - Main appraisal entity
- `AppraisalAssignments` - Assignment tracking
- `FieldSurveys` - Site visit records
- `AppraisalGallery` - Photo container
- `GalleryPhotos` - Individual photos with GPS
- `PropertyPhotoMappings` - **CRITICAL**: Links photos to property sections
- `ValuationAnalysis` - Valuation calculations
- `AppraisalReviews` - Multi-level review workflow
- Property detail tables (6 types): Land, Building, Condo, Vehicle, Vessel, Machinery

**Key Design Decision**:
- **Two-Phase Photo Workflow**:
  - Phase 1 (Site Visit): Quick upload to gallery from mobile
  - Phase 2 (Office): Link photos to specific property sections
- Separate detail table per property type (not generic)
- PropertyPhotoMappings uses polymorphic reference

### 3. Collateral Management Module
**Purpose**: Maintain collateral records post-appraisal

**Key Tables** (9 total):
- `Collaterals` - Main collateral entity
- Type-specific tables: Land, Building, Condo, Vehicle, Vessel, Machinery
- `CollateralValuationHistory` - Revaluation tracking
- `CollateralDocuments` - Final documentation

**Key Design Decision**:
- Created only after appraisal completion
- Separate tables per collateral type
- Valuation history for revaluations
- Revaluation scheduling support

### 4. Document Module
**Purpose**: Centralized document management

**Key Tables** (6 total):
- `Documents` - Main document entity
- `DocumentVersions` - Version control
- `DocumentRelationships` - Document links
- `DocumentAccess` - Access permissions
- `DocumentAccessLogs` - Audit trail
- `DocumentTemplates` - Report templates

**Key Design Decision**:
- All system documents managed centrally
- Version control with rollback
- Access control with expiration
- Cloud storage integration (Azure Blob, AWS S3)

### 5. Authentication & Authorization Module
**Purpose**: User management and RBAC

**Key Tables** (10 total):
- `Users` - User accounts
- `Roles` - Role definitions
- `Permissions` - Permission definitions
- `UserRoles` - User-role assignments
- `RolePermissions` - Role-permission mappings
- `UserPermissions` - Direct permissions
- `Organizations` - External organizations
- `UserOrganizations` - User-org links
- `AuditLogs` - Security audit trail
- `SecurityPolicies` - Security settings

**Key Design Decision**:
- OAuth2/OpenID Connect via OpenIddict
- RBAC with permission-based control
- Support for external organizations
- Comprehensive audit logging

---

## System Workflow

### Complete Process Flow

```
1. RM creates Request
   ├─ Adds customers (name, contact only)
   ├─ Adds property types (type, subtype only)
   ├─ Attaches documents
   ├─ Enters title deed info
   └─ Submits request

2. System creates Appraisal (via RequestCreatedEvent)
   ├─ Auto-assigns or manual assignment
   └─ Appraiser receives notification

3. Appraiser conducts Field Survey
   ├─ Site visit with mobile app
   ├─ Quick upload photos to Gallery (Phase 1)
   ├─ Captures GPS, timestamp automatically
   └─ Minimal categorization during upload

4. Appraiser completes Appraisal (back at office)
   ├─ Reviews gallery photos
   ├─ Creates property detail record (specific type)
   ├─ Links photos to property sections (Phase 2)
   ├─ Adds annotations to photos
   ├─ Completes valuation analysis
   └─ Generates appraisal report

5. Review Workflow
   ├─ Internal Checker reviews
   ├─ Internal Verifier reviews
   └─ Committee approves

6. System creates Collateral (via AppraisalCompletedEvent)
   ├─ Final collateral record
   ├─ Links to appraisal and request
   └─ Active and ready for loan processing
```

---

## Property Types Supported

1. **Land** - Land only
2. **Building** - Building only
3. **LandAndBuilding** - Land with building (most common for houses)
4. **Condo** - Condominium unit
5. **Vehicle** - Car, truck, motorcycle
6. **Vessel** - Boat, ship, yacht
7. **Machinery** - Industrial, agricultural, construction equipment

Each property type has its own detail table with specific fields.

---

## User Roles

| Role | Code | Description |
|------|------|-------------|
| Relationship Manager | RM | Creates and manages requests |
| Administrator | ADMIN | System administration |
| Appraiser (Internal) | APPRAISER | Conducts appraisals |
| Internal Checker | CHECKER | First-level review |
| Internal Verifier | VERIFIER | Second-level review |
| Committee Approver | COMMITTEE | Final approval authority |
| External Admin | EXT_ADMIN | External firm admin |
| External Appraiser | EXT_APPRAISER | External appraiser |
| External Checker | EXT_CHECKER | External checker |
| External Verifier | EXT_VERIFIER | External verifier |

---

## Key Design Decisions & Rationale

### 1. Simplified Request Module Tables

**Decision**: RequestCustomers and RequestPropertyTypes contain minimal fields only

**Rationale**:
- RMs need fast request creation
- Detailed customer info managed in CRM/LOS systems
- Detailed property info captured during appraisal
- Reduces validation complexity
- Cleaner separation of concerns

**Fields**:
- `RequestCustomers`: FirstName, LastName, ContactNumber
- `RequestPropertyTypes`: PropertyType, PropertySubType

### 2. Two-Phase Photo Workflow

**Decision**: Separate photo capture from property detail creation

**Rationale**:
- Appraisers need to work quickly in the field
- Mobile app uploads photos to gallery immediately
- Back at office, appraiser links photos to specific sections
- One photo can map to multiple property sections
- Flexible and efficient workflow

**Implementation**:
- `AppraisalGallery` - Container for photos
- `GalleryPhotos` - Individual photos with GPS
- `PropertyPhotoMappings` - Links photos to property detail sections

### 3. Separate Tables Per Property Type

**Decision**: Create separate detail tables for each property type (Land, Building, Condo, etc.)

**Rationale**:
- Type safety (no nullable fields for irrelevant properties)
- Better performance (no sparse data)
- Clearer schema (easy to understand)
- Easier validation (type-specific rules)
- Better indexing strategies

**Alternative Rejected**: Single generic table with many nullable columns

### 4. Cross-Module References Without FKs

**Decision**: Store only IDs for cross-module references, no foreign key constraints

**Rationale**:
- Loose coupling between modules
- Eventual consistency acceptable
- Easier to split into microservices later
- No cascading deletes across modules
- Each module can be deployed independently

**Example**: Appraisal stores RequestId but no FK to Request table

### 5. Event-Driven Cross-Module Communication

**Decision**: Use integration events (MassTransit/RabbitMQ) for cross-module operations

**Rationale**:
- Loose coupling
- Asynchronous processing
- Retry capability
- Audit trail of events
- Resilient to temporary failures

**Events**:
- `RequestCreatedEvent` → Creates Appraisal
- `AppraisalCompletedEvent` → Creates Collateral

### 6. Centralized Document Module

**Decision**: All documents managed through dedicated Document module

**Rationale**:
- Single source of truth for documents
- Consistent versioning
- Centralized access control
- Common audit trail
- Reusable across all modules

**Benefits**:
- Photos, PDFs, reports all managed consistently
- Version control for all document types
- Unified permission model

---

## Database Design Standards

### Primary Keys
- GUID (UNIQUEIDENTIFIER) for distributed systems
- `Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID()`

### Business Keys
- Unique identifiers for business entities
- `RequestNumber VARCHAR(50) UNIQUE NOT NULL`
- Format: REQ-2025-00001, APR-2025-00001, COL-2025-00001

### Audit Fields (All Tables)
```sql
CreatedOn    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
CreatedBy    UNIQUEIDENTIFIER NOT NULL
UpdatedOn    DATETIME2 NOT NULL DEFAULT GETUTCDATE()
UpdatedBy    UNIQUEIDENTIFIER NOT NULL
RowVersion   ROWVERSION NOT NULL  -- Optimistic concurrency
```

### Soft Delete (Where Applicable)
```sql
IsDeleted    BIT NOT NULL DEFAULT 0
DeletedOn    DATETIME2 NULL
DeletedBy    UNIQUEIDENTIFIER NULL
```

### Schema Separation
- `request.*` - Request module tables
- `appraisal.*` - Appraisal module tables
- `collateral.*` - Collateral module tables
- `document.*` - Document module tables
- `auth.*` - Auth module tables

---

## Technology Stack

### Backend
- **.NET 9.0** - Framework
- **ASP.NET Core** - Web API
- **Entity Framework Core** - ORM
- **SQL Server** - Database
- **MediatR** - CQRS pattern
- **MassTransit** - Event bus
- **RabbitMQ** - Message broker
- **Carter** - Minimal APIs

### Infrastructure
- **Redis** - Caching
- **Azure Blob Storage / AWS S3** - Document storage
- **Seq** - Structured logging
- **OpenIddict** - OAuth2/OpenID Connect
- **Docker** - Containerization

---

## Documentation Files Created

All documentation located in `/docs/data-model/`:

1. **README.md** - Navigation guide and quick start
2. **00-overview.md** - System architecture and design principles
3. **01-module-summary.md** - All modules overview with statistics
4. **02-request-module.md** - Request Management detailed documentation
5. **03-appraisal-module.md** - Appraisal module detailed documentation
6. **04-collateral-module.md** - Collateral Management detailed documentation
7. **05-document-module.md** - Document module detailed documentation
8. **06-auth-module.md** - Authentication & Authorization detailed documentation
9. **07-property-details.md** - Property-specific detail tables (6 types)
10. **08-photo-gallery.md** - Photo gallery and media management
11. **10-er-diagrams.md** - Entity relationship diagrams
12. **15-sample-data.md** - Complete sample data for all scenarios

### What Each Documentation File Contains

Each module documentation (02-06) includes:
- Complete SQL schemas with constraints
- C# entity models with best practices
- All indexes for optimal performance
- Enumerations
- Relationships and foreign keys
- Design rationale

---

## Implementation Status

### ✅ Completed (Design Phase)
- [x] Module architecture defined
- [x] All 5 modules designed
- [x] 51+ tables with complete schemas
- [x] Simplified Request module per requirements
- [x] Two-phase photo workflow designed
- [x] Complete documentation created
- [x] Sample data provided
- [x] ER diagrams created

### 🚧 Next Steps (Implementation Phase)
- [ ] Create Entity Framework migrations
- [ ] Implement C# entity models
- [ ] Create repositories and interfaces
- [ ] Implement CQRS commands and queries
- [ ] Set up MassTransit/RabbitMQ
- [ ] Implement domain events
- [ ] Create API endpoints
- [ ] Add FluentValidation rules
- [ ] Write unit tests
- [ ] Write integration tests

---

## Key Statistics

| Metric | Count |
|--------|-------|
| Modules | 5 |
| Total Tables | 51+ |
| Aggregate Roots | 5 |
| Value Objects | 14+ |
| Enumerations | 26+ |
| Documentation Files | 12 |
| Property Types Supported | 7 |
| User Roles | 10 |

---

## Important Notes for Implementation

### 1. Request Module Simplification
- Only capture minimal customer info (name, contact)
- Only capture minimal property info (type, subtype)
- Detailed info goes in TitleDeedInfo or appraisal module
- This speeds up request creation significantly

### 2. Photo Gallery Workflow
- **Mobile Phase**: Quick upload with GPS, minimal categorization
- **Office Phase**: Link photos to property sections using PropertyPhotoMappings
- Don't try to categorize everything during site visit
- PropertyPhotoMappings is polymorphic (links to any property detail type)

### 3. Property Detail Tables
- Use specific table for property type (don't use generic table)
- One-to-one relationship: Appraisal → Property Detail
- Fields are property-type specific (no nullable spam)
- Examples: BuildingAppraisalDetail has RoofType, KitchenType, etc.

### 4. Cross-Module Communication
- Never use FK constraints across modules
- Always use events for cross-module operations
- Store only IDs for cross-module references
- Use eventual consistency

### 5. Document Management
- All documents go through Document module
- Photos are documents too
- Use versioning for important documents
- Access control per document
- Full audit trail

---

## Future Considerations

### Potential Enhancements
1. **Mobile App** for field surveys (already designed for)
2. **AI/ML** for photo categorization
3. **OCR** for title deed scanning
4. **GIS Integration** for mapping
5. **Comparable Property Database** for valuation
6. **Automated Report Generation** using templates
7. **Real-time Notifications** via SignalR
8. **Analytics Dashboard** for management
9. **Integration APIs** for LOS systems
10. **Blockchain** for document verification

### Scalability Considerations
- Horizontal scaling of API layer
- Read replicas for queries
- Table partitioning for historical data
- CDN for document delivery
- Caching strategy (Redis)
- Message queue for async processing

---

## Questions or Issues?

For questions about the data model design:
1. Review the specific module documentation (02-06)
2. Check the overview (00-overview.md)
3. Look at sample data (15-sample-data.md)
4. Review ER diagrams (10-er-diagrams.md)

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-01-15 | Initial data model design |
| 1.1 | 2025-01-16 | Added property detail tables |
| 1.2 | 2025-01-16 | Added photo gallery workflow |
| 2.0 | 2025-01-02 | **MAJOR**: Simplified Request module, created all 5 module docs |

---

**Status**: Design phase complete ✅ Ready for implementation 🚀
