# Request Project Enhancement Todos

> **Reference**: See full details in `request-project-enhancement.md`

## 📋 Task Overview
- **Total Tasks**: 13 (updated)
- **High Priority**: 5 tasks (Phase 1) - **COMPLETED ✅**
- **Medium Priority**: 4 tasks (Phase 2) 
- **Low Priority**: 4 tasks (Phase 3)

---

## 🔥 Phase 1: OpenAPI & Using Cleanup (High Priority) - **COMPLETED ✅**

### [✅] 1. Add OpenAPI specifications to RequestComment endpoints
**Files to Update:**
- `AddRequestCommentEndpoint.cs`
- `RemoveRequestCommentEndpoint.cs` 
- `UpdateRequestCommentEndpoint.cs`

**Required Specs:**
- WithName, WithSummary, WithDescription
- Produces/ProducesProblem status codes
- WithTags("Request Comments")

---

### [✅] 2. Create missing GET endpoints for RequestComments
**New Files to Create:**
- `GetRequestCommentById/` (5 files: Query, QueryHandler, Endpoint, Response, Result)
- `GetRequestCommentsByRequestId/` (5 files: Query, QueryHandler, Endpoint, Response, Result)

**Routes:**
- `GET /requests/{requestId}/comments/{commentId}`
- `GET /requests/{requestId}/comments`

---

### [✅] 3. Refactor GlobalUsing.cs with proper categorization
**Current Issues:**
- Poor organization and categorization
- Missing RequestComments namespaces
- Inconsistent namespace grouping

**New Structure:**
- System & Framework section
- Microsoft ASP.NET Core section
- Entity Framework section
- Third-party libraries section
- Shared infrastructure section
- Request module sections (Core, Contracts, Aggregates)

---

### [✅] 4. Clean up redundant using statements across ~20 files
**Target Files:**
- All RequestTitles feature files (9 files using RequestTitles.Dtos)
- All RequestComments feature files (8 files using RequestComments.Models)
- Various handler and validator files

**Strategy:**
- Remove redundant statements now covered by GlobalUsing
- Keep explicit imports only where namespace conflicts exist
- Maintain DTOs imports to avoid ambiguity

---

### [✅] 5. Refactor RequestTitle operations from Create/Delete to Add/Remove pattern
**Changes Made:**
- Renamed `CreateRequestTitle` → `AddRequestTitle` (6 files)
- Renamed `DeleteRequestTitle` → `RemoveRequestTitle` (6 files)
- Updated all namespaces, class names, and method references
- Updated GlobalUsing.cs to reference new namespaces
- Verified compilation with 0 errors

**Files Created:**
- `AddRequestTitle/` folder with Command, CommandHandler, CommandValidator, Endpoint, Request, Response, Result
- `RemoveRequestTitle/` folder with Command, CommandHandler, CommandValidator, Endpoint, Response, Result

---

## 🛠️ Phase 2: DDD Core Improvements (Medium Priority)

### [✅] 6. Implement missing domain events (Logging & Audit Only)
**Events Created:**
- ✅ `RequestTitleAddedEvent` - When title/collateral is added
- ✅ `RequestTitleRemovedEvent` - When title/collateral is removed
- ✅ `RequestCommentAddedEvent` - When comment is added
- ✅ `RequestCommentUpdatedEvent` - When comment is modified
- ✅ `RequestCommentRemovedEvent` - When comment is deleted

**Implementation Completed:**
- ✅ Created 5 domain event classes implementing `IDomainEvent`
- ✅ Updated `RequestTitle.Create()` to publish `RequestTitleAddedEvent`
- ✅ Updated `RequestComment.Create()` and `Update()` to publish events
- ✅ Updated removal command handlers to publish removal events
- ✅ Created 5 event handlers for logging and audit trail
- ✅ Updated `RequestComment` from `Entity<long>` to `Aggregate<long>`
- ✅ Added event handler registrations to GlobalUsing.cs
- ✅ Added `Microsoft.Extensions.Logging` to GlobalUsing.cs
- ✅ Cleaned up redundant using statements in all event files
- ✅ Verified compilation with 0 errors

**Scope:** Simplified to logging & audit only (no integration events or notifications)

---

### [ ] 7. Add domain services for complex business logic
**Services to Create:**
- `RequestDomainService` for cross-aggregate validation
- `RequestStatusTransitionService` for status change logic
- Business rule validation services

**Benefits:**
- Extract complex logic from aggregates
- Enable reusable business rules
- Improve testability of domain logic

---

### [✅] 8. Separate read/write repositories using CQRS pattern
**Implementation Completed:**
- ✅ Created `IRequestTitleReadRepository` + `RequestTitleReadRepository` using `BaseReadRepository`
- ✅ Created `IRequestCommentReadRepository` + `RequestCommentReadRepository` using `BaseReadRepository`
- ✅ Updated all 4 query handlers to use read repositories instead of DbContext
- ✅ Maintained performance with AsNoTracking operations in read repositories
- ✅ Clear separation: Query handlers use read repos, Command handlers use write repos
- ✅ Registered new read repositories in RequestModule.cs
- ✅ Verified compilation with 0 errors

**Query Handlers Updated:**
- `GetRequestTitlesByRequestIdQueryHandler` → uses `IRequestTitleReadRepository`
- `GetRequestTitleByIdQueryHandler` → uses `IRequestTitleReadRepository`
- `GetRequestCommentsByRequestIdQueryHandler` → uses `IRequestCommentReadRepository`
- `GetRequestCommentByIdQueryHandler` → uses `IRequestCommentReadRepository`

**CQRS Benefits Achieved:**
- Pure separation between read and write operations
- Read repositories optimized for queries (AsNoTracking)
- Write repositories focus on aggregate persistence
- Improved testability with repository abstraction

---

### [✅] 9. Add specification pattern for complex queries
**Implementation Completed:**
- ✅ Created comprehensive specifications for all three aggregates
- ✅ **Request Specifications**: `RequestsByStatusSpecification`, `RequestsByPurposeSpecification`, `RequestsByPrioritySpecification`, `RequestsWithAppraisalNumberSpecification`
- ✅ **RequestTitle Specifications**: `RequestTitlesByCollateralTypeSpecification`, `RequestTitlesByRequestIdSpecification`, `RequestTitlesByOwnerSpecification`, `RequestTitlesWithLandAreaSpecification`
- ✅ **RequestComment Specifications**: `RequestCommentsByRequestIdSpecification`, `RequestCommentsByAuthorSpecification`, `RequestCommentsByDateRangeSpecification`, `RequestCommentsContainingTextSpecification`, `RecentRequestCommentsSpecification`
- ✅ Integrated with `BaseReadRepository` for flexible querying (FindAsync, GetPaginatedAsync, CountAsync, ExistsAsync)
- ✅ Added specifications namespaces to GlobalUsing.cs
- ✅ Cleaned up redundant using statements in all specification files
- ✅ Verified compilation with 0 errors

**Benefits Achieved:**
- Reusable query conditions across different handlers
- Flexible querying capabilities with pagination support
- Clean separation of query logic from business logic
- Testable and composable query specifications

---

## 🚀 Phase 3: Advanced DDD (Low Priority)

### [ ] 10. Enrich value objects with business methods
**Target Value Objects:**
- `RequestStatus` - Add `CanTransitionTo()`, `IsActive()` methods
- `Address` - Add validation logic, formatting methods
- `AppraisalNumber` - Add generation and validation logic

**Benefits:**
- Move behavior closer to data
- Reduce primitive obsession
- Improve domain expressiveness

---

### [ ] 11. Add domain invariants enforcement
**Areas to Improve:**
- Aggregate root invariants validation
- Cross-property validation rules
- Business rule enforcement at domain level

**Implementation:**
- Add invariant checking in aggregate methods
- Create custom validation attributes
- Ensure data consistency at domain level

---

### [ ] 12. Review aggregate boundaries and relationships
**Current Issues:**
- `RequestComment` might belong to `Request` aggregate
- `RequestTitle` relationship boundaries unclear

**Analysis Needed:**
- Review transaction boundaries
- Assess data consistency requirements
- Consider aggregate size and complexity

---

### [ ] 13. Implement domain event-driven workflows
**Workflows to Implement:**
- Request creation → Title creation notification
- Comment addition → Notification to stakeholders
- Status changes → Audit trail events

**Benefits:**
- Loose coupling between aggregates
- Event-driven architecture patterns
- Better scalability and maintainability

---

## 📊 Progress Tracking

**Phase 1 Completion**: 5/5 tasks ✅✅✅✅✅
**Phase 2 Completion**: 3/4 tasks ✅✅✅⬜  
**Phase 3 Completion**: 0/4 tasks ⬜⬜⬜⬜

**Overall Progress**: 8/13 tasks (62%)

---

## 🎯 Success Criteria

- [ ] All endpoints have comprehensive OpenAPI documentation
- [ ] Clean, organized using statements with proper global imports
- [ ] Rich domain models with business logic encapsulation
- [ ] Clear separation of concerns following DDD principles
- [ ] Event-driven communication between aggregates
- [✅] Specification pattern for complex queries

---

*Last Updated: [Date will be updated as tasks progress]*
*Reference: `request-project-enhancement.md` for detailed technical specifications*