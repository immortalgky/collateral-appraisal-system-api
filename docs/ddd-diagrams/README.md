# DDD Diagrams - Collateral Appraisal System

## Overview

This directory contains comprehensive Domain-Driven Design (DDD) diagrams for the Collateral Appraisal System. These diagrams are essential for understanding the system architecture, module interactions, event flows, and design decisions.

## Purpose

These diagrams serve multiple purposes:
- **Communication**: Share system design with team members and stakeholders
- **Documentation**: Provide reference for developers implementing features
- **Onboarding**: Help new team members understand the architecture quickly
- **Design Reviews**: Facilitate architectural discussions and decisions
- **Knowledge Transfer**: Preserve architectural knowledge over time

## Diagram Inventory

### 1. [Context Map](01-context-map.md)
**Purpose**: Shows bounded contexts and their integration patterns

**What You'll Find**:
- Bounded context boundaries (5 modules)
- Integration patterns (Customer/Supplier, Partnership, Conformist, ACL)
- Module responsibilities and aggregate roots
- Cross-module communication rules
- Team organization structure
- Evolution path (Modular Monolith → Microservices)

**Key Takeaways**:
- Request → Appraisal → Collateral (core flow via events)
- Document and Auth are supporting contexts
- No direct database joins across modules
- Event-driven integration via MassTransit/RabbitMQ

**Use When**:
- Understanding overall system structure
- Making integration decisions
- Planning team responsibilities
- Evaluating microservices migration

---

### 2. [Event Flow Diagram](02-event-flow-diagram.md)
**Purpose**: Shows how domain and integration events flow through the system

**What You'll Find**:
- Complete event catalog (all modules)
- Event schemas with JSON examples
- Processing patterns (fire-and-forget, request-response, sagas)
- MassTransit configuration
- Retry strategies and error handling
- Event versioning approach

**Key Events**:
- `RequestCreatedEvent` → triggers Appraisal creation
- `AppraisalCompletedEvent` → triggers Collateral creation
- `DocumentUploadedEvent` → triggers virus scan
- `UserRoleAssignedEvent` → invalidates permission cache

**Use When**:
- Implementing new events
- Debugging event flow issues
- Understanding asynchronous workflows
- Configuring event bus

---

### 3. [Event Storming](03-event-storming.md)
**Purpose**: Collaborative workshop results showing domain events, commands, and actors

**What You'll Find**:
- Complete event timeline (Request → Collateral)
- Commands that trigger events
- Aggregates and their boundaries
- Actors (RM, Appraiser, Checker, etc.)
- Hotspots and unresolved questions
- Policies (automated reactions to events)
- Business rules and invariants

**Event Storming Legend**:
- 🟧 Orange = Domain Events
- 🟦 Blue = Commands
- 🟨 Yellow = Aggregates
- 🟩 Green = Actors
- 🟥 Red = Hotspots
- 🟪 Purple = Policies
- 📖 White = Read Models

**Use When**:
- Understanding business workflows
- Identifying domain boundaries
- Discovering missing requirements
- Validating business rules
- Planning development sprints

---

### 4. [Sequence Diagrams](04-sequence-diagrams.md)
**Purpose**: Detailed interaction flows for critical scenarios

**What You'll Find**:
- Complete Request-to-Collateral flow
- Request creation with validation
- Appraisal assignment algorithm
- Field survey and photo upload
- Property analysis with photo linking
- Review & approval workflow
- Collateral creation
- Document upload with access control
- User authentication

**Key Scenarios Documented**:
1. End-to-end request processing (7-10 days)
2. Mobile photo upload with GPS (2-5 sec/photo)
3. Two-phase photo workflow (field → office)
4. Multi-level review (Checker → Verifier → Committee)

**Use When**:
- Implementing new features
- Understanding timing and order
- Debugging interaction issues
- Writing integration tests
- API design discussions

---

### 5. [C4 Model Diagrams](05-c4-diagrams.md)
**Purpose**: Architectural views at different abstraction levels

**What You'll Find**:

**Level 1 - System Context**:
- Users and external systems
- System boundary
- Key integrations (LOS, Cloud Storage, Email, etc.)

**Level 2 - Container**:
- Web Application (React)
- Mobile App (React Native)
- API Gateway (ASP.NET Core)
- Modules (Request, Appraisal, Collateral, Document, Auth)
- Infrastructure (SQL Server, Redis, RabbitMQ, Seq)
- Cloud Storage (Azure Blob / AWS S3)

**Level 3 - Component (Appraisal Module)**:
- Carter Endpoints (HTTP)
- Command Handlers (CQRS Write)
- Query Handlers (CQRS Read)
- Domain Model (Aggregates)
- Event Handlers (MediatR)
- Integration Event Consumers (MassTransit)
- Repositories (with caching decorator)
- Validators (FluentValidation)

**Deployment View**:
- Load balancer configuration
- Application tier (3 instances)
- Data tier (SQL Primary + Read Replica)
- Redis cluster setup
- RabbitMQ cluster

**Use When**:
- Understanding technology choices
- Planning deployment
- Scaling decisions
- Infrastructure provisioning
- Explaining system to non-technical stakeholders

---

## How to Use These Diagrams

### For Developers
1. **Start with Context Map** to understand module boundaries
2. **Review Event Flow** for the feature you're implementing
3. **Study Sequence Diagrams** for detailed interactions
4. **Reference C4 Diagrams** for technical implementation details

### For Architects
1. **Context Map** for strategic design decisions
2. **Event Flow** for integration patterns
3. **C4 Diagrams** for deployment and scaling

### For Product Owners
1. **Event Storming** for business workflows
2. **Sequence Diagrams** for feature timelines
3. **Context Map** for understanding team dependencies

### For New Team Members
**Day 1**: Read README.md (this file)
**Day 2**: Study Context Map and System Context (C4 Level 1)
**Day 3**: Review Event Storming for business understanding
**Day 4**: Deep dive into your module's sequence diagrams
**Week 2**: Study component-level details (C4 Level 3)

---

## Diagram Maintenance

### When to Update
- ✅ **New module added**: Update Context Map, C4 diagrams
- ✅ **New event introduced**: Update Event Flow, Event Storming
- ✅ **Workflow changed**: Update Sequence Diagrams
- ✅ **Technology changed**: Update C4 diagrams
- ✅ **Integration pattern changed**: Update Context Map

### Update Checklist
- [ ] Update diagram file
- [ ] Update this README if new diagram added
- [ ] Review with team in architecture meeting
- [ ] Update code examples if applicable
- [ ] Update related documentation

### Tools Used
- **Mermaid**: For all diagrams (rendered by GitHub/GitLab)
- **PlantUML C4**: For C4 model diagrams
- **Markdown**: For documentation

---

## Related Documentation

### Data Model Documentation
- [00-overview.md](../data-model/00-overview.md) - System overview
- [01-module-summary.md](../data-model/01-module-summary.md) - Module responsibilities
- [02-request-module.md](../data-model/02-request-module.md) - Request module data model
- [03-appraisal-module.md](../data-model/03-appraisal-module.md) - Appraisal module data model
- [04-collateral-module.md](../data-model/04-collateral-module.md) - Collateral module data model
- [05-document-module.md](../data-model/05-document-module.md) - Document module data model
- [06-auth-module.md](../data-model/06-auth-module.md) - Auth module data model

### Implementation Documentation
- [CLAUDE.md](../../CLAUDE.md) - Developer setup and guidelines
- [Implementation WBS](../data-model/20-implementation-wbs.md) - Work breakdown structure
- [GitHub Epics](../data-model/21-github-epics.md) - Epic stories

---

## Quick Reference

### Key Architectural Patterns

| Pattern | Where Used | Purpose |
|---------|-----------|---------|
| **Modular Monolith** | Overall architecture | Logical separation with single deployment |
| **Domain-Driven Design** | All modules | Rich domain models with business logic |
| **CQRS** | All modules | Separate read and write models |
| **Event Sourcing** | N/A (not used) | Not implemented - traditional CRUD |
| **Event-Driven** | Cross-module integration | Loose coupling via events |
| **Repository Pattern** | Data access | Abstraction over data access |
| **Decorator Pattern** | Caching | Add caching without modifying repositories |
| **Mediator Pattern** | Command/Query handling | Decouple requests from handlers |
| **Saga Pattern** | Multi-step workflows | Coordinate distributed transactions |

### Key Technology Choices

| Category | Technology | Reason |
|----------|-----------|--------|
| **Runtime** | .NET 9.0 | Modern, performant, cross-platform |
| **Web Framework** | ASP.NET Core | Industry standard, mature ecosystem |
| **API Pattern** | Minimal APIs (Carter) | Lightweight, fast, easy to test |
| **ORM** | Entity Framework Core | Type-safe, migrations, LINQ |
| **CQRS/Mediator** | MediatR | Simple, testable, clear separation |
| **Event Bus** | MassTransit + RabbitMQ | Reliable, feature-rich, scalable |
| **Caching** | Redis | Fast, distributed, persistent |
| **Authentication** | OpenIddict | OAuth2/OIDC compliant, flexible |
| **Logging** | Serilog + Seq | Structured logs, powerful queries |

### Module Communication Matrix

|  | Request | Appraisal | Collateral | Document | Auth |
|--|---------|-----------|------------|----------|------|
| **Request** | - | Event → | - | Ref | Query |
| **Appraisal** | - | - | Event → | Ref | Query |
| **Collateral** | - | - | - | Ref | Query |
| **Document** | - | - | - | - | Query |
| **Auth** | - | - | - | - | - |

**Legend**:
- **Event →**: Publishes integration event to consumer
- **Ref**: Stores DocumentId/UserId reference
- **Query**: Direct API calls for user data
- **-**: No direct communication

---

## Glossary

**Aggregate**: Cluster of domain objects treated as a single unit (e.g., Request, Appraisal)

**Aggregate Root**: Entry point for accessing aggregate (e.g., `Appraisal` aggregate root)

**Bounded Context**: Explicit boundary within which a domain model applies (e.g., Appraisal Module)

**Command**: Request to change state (e.g., `CreateRequestCommand`)

**Domain Event**: Something that happened in the domain (e.g., `RequestCreatedEvent`)

**Integration Event**: Event published across bounded contexts (e.g., `RequestCreatedEvent` consumed by Appraisal Module)

**Value Object**: Immutable object defined by its attributes (e.g., `Money`, `Address`)

**Repository**: Interface for accessing aggregates

**CQRS**: Command Query Responsibility Segregation - separate read and write models

**Event Sourcing**: Store state as sequence of events (not used in this system)

**Saga**: Sequence of local transactions coordinated by events

**ACL**: Anti-Corruption Layer - protects domain from external models

---

## Diagram Conventions

### Colors & Shapes
- **Blue boxes**: Core modules
- **Green boxes**: Supporting services
- **Orange boxes**: External systems
- **Dashed lines**: Async communication
- **Solid lines**: Sync communication
- **Arrows**: Data flow direction

### Naming Conventions
- **Commands**: `<Verb><Noun>Command` (e.g., `CreateRequestCommand`)
- **Events**: `<Noun><Verb>Event` past tense (e.g., `RequestCreatedEvent`)
- **Queries**: `Get<Noun>By<Criteria>Query` (e.g., `GetRequestByIdQuery`)
- **Handlers**: `<CommandOrQuery>Handler` (e.g., `CreateRequestCommandHandler`)

---

## Contributing

When adding or updating diagrams:

1. **Keep diagrams up-to-date** with code changes
2. **Use consistent notation** across all diagrams
3. **Add explanations** for complex flows
4. **Update this README** when adding new diagrams
5. **Review with team** before committing major changes
6. **Test Mermaid rendering** on GitHub before pushing

---

## Questions?

- **Architecture**: Contact Architecture Team
- **Data Model**: See [data-model documentation](../data-model/)
- **Implementation**: See [CLAUDE.md](../../CLAUDE.md)
- **Issues**: Create issue in project repository

---

**Last Updated**: 2025-01-05
**Maintained By**: Architecture Team
**Review Frequency**: Quarterly or when major changes occur

---

## Next Steps

**For new developers**:
1. Read [Context Map](01-context-map.md)
2. Study [Event Storming](03-event-storming.md) for business understanding
3. Review [Sequence Diagrams](04-sequence-diagrams.md) for your module
4. Dive into [C4 Diagrams](05-c4-diagrams.md) for implementation details

**For architects**:
1. Review all diagrams for current state
2. Identify areas for improvement
3. Plan evolution (e.g., microservices migration)
4. Update diagrams as decisions are made

**For product owners**:
1. Understand workflows via [Event Storming](03-event-storming.md)
2. Review timelines in [Sequence Diagrams](04-sequence-diagrams.md)
3. Coordinate with teams based on [Context Map](01-context-map.md) dependencies