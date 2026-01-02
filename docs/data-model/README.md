# Collateral Appraisal System - Data Model Documentation

## Overview

This directory contains comprehensive documentation for the production-grade data model design of the Collateral Appraisal System. The system follows Domain-Driven Design (DDD) principles and implements a modular monolith architecture.

## Documentation Structure

### Core Documentation
- **[00-overview.md](00-overview.md)** - System overview, architecture principles, and key concepts
- **[01-module-summary.md](01-module-summary.md)** - Summary of all modules and their responsibilities

### Module-Specific Documentation
- **[02-request-module.md](02-request-module.md)** - Request Management module data model
- **[03-appraisal-module.md](03-appraisal-module.md)** - Appraisal module data model
- **[04-collateral-module.md](04-collateral-module.md)** - Collateral Management module data model
- **[05-document-module.md](05-document-module.md)** - Document module data model
- **[06-auth-module.md](06-auth-module.md)** - Authentication & Authorization module data model

### Detailed Documentation
- **[07-property-details.md](07-property-details.md)** - Property-specific appraisal detail tables
- **[08-photo-gallery.md](08-photo-gallery.md)** - Photo gallery and media management
- **[09-workflows.md](09-workflows.md)** - System workflows and state machines
- **[10-er-diagrams.md](10-er-diagrams.md)** - Entity Relationship diagrams
- **[15-sample-data.md](15-sample-data.md)** - Complete sample data for all modules

### Technical Documentation
- **[11-design-decisions.md](11-design-decisions.md)** - Design decisions and rationale
- **[12-implementation-guide.md](12-implementation-guide.md)** - Implementation guidelines for developers
- **[13-value-objects.md](13-value-objects.md)** - Value Objects reference
- **[14-enumerations.md](14-enumerations.md)** - Enumerations reference

## Quick Start for Developers

### Understanding the System

1. Start with **[00-overview.md](00-overview.md)** to understand the overall architecture
2. Read **[01-module-summary.md](01-module-summary.md)** to see how modules interact
3. Review **[09-workflows.md](09-workflows.md)** to understand the business processes
4. Study **[10-er-diagrams.md](10-er-diagrams.md)** to see entity relationships

### Implementing a Module

1. Read the specific module documentation (e.g., **03-appraisal-module.md**)
2. Review **[11-design-decisions.md](11-design-decisions.md)** for patterns and best practices
3. Follow **[12-implementation-guide.md](12-implementation-guide.md)** for coding standards
4. Reference **[13-value-objects.md](13-value-objects.md)** and **[14-enumerations.md](14-enumerations.md)** as needed

### Working with Property Details

1. Read **[07-property-details.md](07-property-details.md)** for property-specific tables
2. Review **[08-photo-gallery.md](08-photo-gallery.md)** for media management
3. Understand the two-phase workflow (site visit → office work)

## Key Concepts

### Domain-Driven Design (DDD)

- **Aggregates**: Request, Appraisal, Collateral, Document, User
- **Value Objects**: Money, Address, ContactInfo, Location, etc.
- **Domain Events**: RequestCreatedEvent, AppraisalCompletedEvent, etc.
- **Repositories**: Interface-based with caching decorators

### Modular Architecture

- Each module is self-contained with its own DbContext
- Modules communicate via domain events (MassTransit/RabbitMQ)
- No direct database joins across modules
- Clear bounded contexts

### Data Integrity

- GUID primary keys for distributed systems
- Unique business keys (RequestNumber, AppraisalNumber, etc.)
- Audit fields on all entities (CreatedOn, CreatedBy, UpdatedOn, UpdatedBy)
- Optimistic concurrency control with RowVersion
- Soft delete support

## Technology Stack

- **.NET 9.0** with ASP.NET Core
- **Entity Framework Core** with SQL Server
- **MediatR** for CQRS pattern
- **MassTransit** + RabbitMQ for messaging
- **Redis** for caching
- **OpenIddict** for authentication
- **Serilog** for structured logging

## Module Boundaries

```
┌─────────────────────────────────────────────────────────────┐
│                    Bootstrapper/API                          │
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

## Data Flow

```
1. RM creates Request → RequestCreatedEvent
2. System creates Appraisal → AppraisalCreatedEvent
3. Appraiser does site visit → Uploads photos to Gallery
4. Appraiser completes appraisal → Creates Property Details
5. Appraiser links photos to property sections
6. Appraisal approved → AppraisalCompletedEvent
7. System creates Collateral → CollateralCreatedEvent
```

## Database Schema Organization

Each module uses its own schema namespace:

```sql
-- Request Module
request.Requests
request.RequestDocuments
request.TitleDeedInfo
request.RequestStatusHistory

-- Appraisal Module
appraisal.Appraisals
appraisal.AppraisalAssignments
appraisal.FieldSurveys
appraisal.AppraisalGallery
appraisal.GalleryPhotos
appraisal.LandAppraisalDetails
appraisal.BuildingAppraisalDetails
-- etc.

-- Collateral Module
collateral.Collaterals
collateral.LandCollaterals
collateral.BuildingCollaterals
-- etc.

-- Document Module
document.Documents
document.DocumentVersions
document.DocumentRelationships

-- Auth Module
auth.Users
auth.Roles
auth.Permissions
```

## Naming Conventions

### Tables
- Singular nouns: `Request`, `Appraisal`, `Document`
- PascalCase for multi-word: `AppraisalAssignment`, `GalleryPhoto`

### Columns
- PascalCase: `RequestNumber`, `CreatedOn`, `AppraisedValue`
- Foreign keys: `AppraisalId`, `UserId`, `DocumentId`
- Boolean fields: `Is*` or `Has*` prefix: `IsActive`, `HasElectricity`

### Enumerations
- PascalCase: `RequestStatus`, `CollateralType`, `AppraisalType`
- Enum values: PascalCase: `InProgress`, `Completed`, `Pending`

### Value Objects
- Descriptive names: `Money`, `Address`, `ContactInfo`, `Location`
- Suffix with "VO" in documentation: `Money VO`, `Address VO`

## Version History

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0 | 2025-01-15 | System Architect | Initial data model design |
| 1.1 | 2025-01-16 | System Architect | Added property detail tables |
| 1.2 | 2025-01-16 | System Architect | Added photo gallery and media management |

## Contributing

When updating this documentation:

1. Update the relevant module documentation file
2. Update ER diagrams if entity relationships change
3. Update the implementation guide with new patterns
4. Update version history in this README
5. Review and update cross-references

## Questions or Feedback

For questions about the data model design, please contact:
- Architecture Team
- Create an issue in the project repository
- Refer to the main CLAUDE.md for development guidelines

---

**Next Steps**: Start with [00-overview.md](00-overview.md) to understand the system architecture.
