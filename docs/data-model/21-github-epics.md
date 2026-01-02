# GitHub Epics & Issues - Collateral Appraisal System

## Overview

This document provides a complete list of all epics and their child issues for the Collateral Appraisal System implementation. Use this as a reference when creating GitHub issues from the WBS.

**Total Timeline**: 8 weeks (2 months)
**Total Epics**: 20
**Total Tasks**: ~150-200

---

## Sprint 1: Foundation & Infrastructure (Week 1-2)

### EPIC-001: Project Infrastructure Setup
**Priority**: Critical | **Estimated**: 18 hours | **Sprint**: 1

#### Child Issues:
1. **INFRA-001**: Create project structure and solution file (2h)
2. **INFRA-002**: Set up Docker infrastructure (3h)
3. **INFRA-003**: Configure database connection and EF Core (3h)
4. **INFRA-004**: Set up logging infrastructure (2h)
5. **INFRA-005**: Configure MediatR and CQRS infrastructure (4h)
6. **INFRA-006**: Configure MassTransit and RabbitMQ (4h)

**Total**: 6 issues

---

### EPIC-002: Shared Domain Infrastructure
**Priority**: Critical | **Estimated**: 16 hours | **Sprint**: 1

#### Child Issues:
1. **SHARED-001**: Create base entity classes (4h)
2. **SHARED-002**: Create value objects infrastructure (4h)
3. **SHARED-003**: Create domain events infrastructure (3h)
4. **SHARED-004**: Create result pattern infrastructure (3h)
5. **SHARED-005**: Create repository interfaces (2h)

**Total**: 5 issues

---

### EPIC-003: Authentication & Authorization Module
**Priority**: Critical | **Estimated**: 41 hours | **Sprint**: 1

#### Child Issues:
1. **AUTH-001**: Create Auth module structure (2h)
2. **AUTH-002**: Create User aggregate (4h)
3. **AUTH-003**: Create Role and Permission entities (4h)
4. **AUTH-004**: Create Organization entities (3h)
5. **AUTH-005**: Create AuditLog and SecurityPolicy entities (3h)
6. **AUTH-006**: Create AuthDbContext (3h)
7. **AUTH-007**: Create and run migrations (2h)
8. **AUTH-008**: Configure OpenIddict (6h)
9. **AUTH-009**: Create user management commands (6h)
10. **AUTH-010**: Create user queries (4h)
11. **AUTH-011**: Create API endpoints (Carter) (4h)
12. **AUTH-012**: Create role and permission endpoints (3h)
13. **TEST-001**: Write Auth module tests (9h)

**Total**: 13 issues

---

### EPIC-004: Testing Infrastructure
**Priority**: High | **Estimated**: 16 hours | **Sprint**: 1

#### Child Issues:
1. **TEST-INF-001**: Set up unit testing infrastructure (3h)
2. **TEST-INF-002**: Set up integration testing infrastructure (4h)
3. **TEST-INF-003**: Write Auth module unit tests (5h)
4. **TEST-INF-004**: Write Auth module integration tests (4h)

**Total**: 4 issues

---

## Sprint 2: Core Modules (Week 3-4)

### EPIC-005: Document Management Module
**Priority**: Critical | **Estimated**: 47 hours | **Sprint**: 2

#### Child Issues:
1. **DOC-001**: Create Document module structure (2h)
2. **DOC-002**: Create Document aggregate (5h)
3. **DOC-003**: Create DocumentVersion entity (3h)
4. **DOC-004**: Create DocumentRelationship entity (2h)
5. **DOC-005**: Create DocumentAccess entity (4h)
6. **DOC-006**: Create DocumentAccessLog entity (2h)
7. **DOC-007**: Create DocumentTemplate entity (2h)
8. **DOC-008**: Create DocumentDbContext (3h)
9. **DOC-009**: Configure cloud storage integration (5h)
10. **DOC-010**: Create document commands (6h)
11. **DOC-011**: Create document queries (4h)
12. **DOC-012**: Create document endpoints (4h)
13. **TEST-DOC-001**: Write Document module tests (5h)

**Total**: 13 issues

---

### EPIC-006: Request Management Module
**Priority**: Critical | **Estimated**: 47 hours | **Sprint**: 2

#### Child Issues:
1. **REQ-001**: Create Request module structure (2h)
2. **REQ-002**: Create value objects (4h)
3. **REQ-003**: Create Request aggregate (6h)
4. **REQ-004**: Create TitleDeedInfo entity (3h)
5. **REQ-005**: Create RequestDocument entity (2h)
6. **REQ-006**: Create RequestStatusHistory entity (2h)
7. **REQ-007**: Create RequestDbContext (3h)
8. **REQ-008**: Create domain events (3h)
9. **REQ-009**: Create request commands (8h)
10. **REQ-010**: Create request queries (5h)
11. **REQ-011**: Create request endpoints (5h)
12. **TEST-REQ-001**: Write Request module tests (6h)

**Total**: 12 issues

---

### EPIC-007: Request-to-Appraisal Event Integration
**Priority**: Critical | **Estimated**: 8 hours | **Sprint**: 2

#### Child Issues:
1. **EVENT-001**: Create integration event for RequestCreated (2h)
2. **EVENT-002**: Create event handler skeleton (3h)
3. **EVENT-003**: Test event-driven workflow (3h)

**Total**: 3 issues

---

### EPIC-008: Redis Caching Layer
**Priority**: Medium | **Estimated**: 14 hours | **Sprint**: 2

#### Child Issues:
1. **CACHE-001**: Set up Redis connection (2h)
2. **CACHE-002**: Create caching infrastructure (4h)
3. **CACHE-003**: Apply caching to Request repository (3h)
4. **CACHE-004**: Apply caching to Document repository (2h)
5. **TEST-CACHE-001**: Write caching tests (3h)

**Total**: 5 issues

---

## Sprint 3: Appraisal Module - Core (Week 5-6)

### EPIC-009: Appraisal Core Entities
**Priority**: Critical | **Estimated**: 27 hours | **Sprint**: 3

#### Child Issues:
1. **APR-001**: Create Appraisal module structure (2h)
2. **APR-002**: Create Appraisal aggregate (6h)
3. **APR-003**: Create AppraisalAssignment entity (4h)
4. **APR-004**: Create FieldSurvey entity (4h)
5. **APR-005**: Create ValuationAnalysis entity (4h)
6. **APR-006**: Create AppraisalReview entity (4h)
7. **APR-007**: Create AppraisalDbContext (3h)

**Total**: 7 issues

---

### EPIC-010: Appraisal Commands & Queries
**Priority**: Critical | **Estimated**: 17 hours | **Sprint**: 3

#### Child Issues:
1. **APR-008**: Create appraisal domain events (2h)
2. **APR-009**: Create appraisal commands (10h)
3. **APR-010**: Create appraisal queries (5h)

**Total**: 3 issues

---

### EPIC-011: Appraisal Event Handlers
**Priority**: Critical | **Estimated**: 8 hours | **Sprint**: 3

#### Child Issues:
1. **APR-011**: Create RequestCreated event consumer (4h)
2. **APR-012**: Create AppraisalCompleted event publisher (2h)
3. **APR-013**: Test event-driven workflows (2h)

**Total**: 3 issues

---

### EPIC-012: Appraisal API Endpoints
**Priority**: Critical | **Estimated**: 16 hours | **Sprint**: 3

#### Child Issues:
1. **APR-014**: Create appraisal endpoints (6h)
2. **APR-015**: Add authorization to endpoints (3h)
3. **TEST-APR-001**: Write Appraisal module tests (7h)

**Total**: 3 issues

---

### EPIC-013: Collateral Module - Basic
**Priority**: High | **Estimated**: 20 hours | **Sprint**: 3

#### Child Issues:
1. **COL-001**: Create Collateral module structure (2h)
2. **COL-002**: Create Collateral aggregate (basic) (4h)
3. **COL-003**: Create CollateralValuationHistory entity (3h)
4. **COL-004**: Create CollateralDbContext (basic) (2h)
5. **COL-005**: Create AppraisalCompleted event consumer (4h)
6. **COL-006**: Create basic collateral queries (3h)
7. **COL-007**: Create basic collateral endpoints (2h)

**Total**: 7 issues

---

## Sprint 4: Advanced Features (Week 7-8)

### EPIC-014: Photo Gallery System
**Priority**: High | **Estimated**: 30 hours | **Sprint**: 4

#### Child Issues:
1. **PHOTO-001**: Create photo gallery entities (4h)
2. **PHOTO-002**: Create PropertyPhotoMapping entity (3h)
3. **PHOTO-003**: Update AppraisalDbContext with photo entities (2h)
4. **PHOTO-004**: Create photo upload commands (8h)
5. **PHOTO-005**: Create photo queries (4h)
6. **PHOTO-006**: Create photo endpoints (4h)
7. **TEST-PHOTO-001**: Write photo gallery tests (5h)

**Total**: 7 issues

---

### EPIC-015: Property Detail Tables
**Priority**: High | **Estimated**: 32 hours | **Sprint**: 4

#### Child Issues:
1. **PROP-001**: Create LandAppraisalDetail entity (3h)
2. **PROP-002**: Create BuildingAppraisalDetail entity (3h)
3. **PROP-003**: Create CondoAppraisalDetail entity (2h)
4. **PROP-004**: Create VehicleAppraisalDetail entity (2h)
5. **PROP-005**: Create VesselAppraisalDetail entity (2h)
6. **PROP-006**: Create MachineryAppraisalDetail entity (2h)
7. **PROP-007**: Update AppraisalDbContext with property details (3h)
8. **PROP-008**: Create property detail commands (6h)
9. **PROP-009**: Create property detail endpoints (4h)
10. **TEST-PROP-001**: Write property detail tests (5h)

**Total**: 10 issues

---

### EPIC-016: Collateral Property Details
**Priority**: Medium | **Estimated**: 16 hours | **Sprint**: 4

#### Child Issues:
1. **COL-008**: Create collateral property detail entities (6h)
2. **COL-009**: Update CollateralDbContext (2h)
3. **COL-010**: Update AppraisalCompleted event consumer (4h)
4. **COL-011**: Create collateral property queries (3h)
5. **COL-012**: Create collateral endpoints (1h)

**Total**: 5 issues

---

### EPIC-017: Appraisal Report Generation
**Priority**: Medium | **Estimated**: 16 hours | **Sprint**: 4

#### Child Issues:
1. **REPORT-001**: Set up report generation library (2h)
2. **REPORT-002**: Create appraisal report template (6h)
3. **REPORT-003**: Create report generation command (4h)
4. **REPORT-004**: Create report endpoint (2h)
5. **TEST-REPORT-001**: Write report generation tests (2h)

**Total**: 5 issues

---

## Ongoing Tasks (Throughout All Sprints)

### EPIC-018: API Documentation
**Priority**: High | **Estimated**: 10 hours | **Ongoing**

#### Child Issues:
1. **DOC-API-001**: Set up OpenAPI/Swagger (2h)
2. **DOC-API-002**: Document all endpoints (6h)
3. **DOC-API-003**: Create Postman collection (2h)

**Total**: 3 issues

---

### EPIC-019: Performance Optimization
**Priority**: Medium | **Estimated**: 15 hours | **Ongoing**

#### Child Issues:
1. **PERF-001**: Database query optimization (4h)
2. **PERF-002**: Implement pagination (4h)
3. **PERF-003**: Add response compression (1h)
4. **PERF-004**: Profile and optimize (6h)

**Total**: 4 issues

---

### EPIC-020: Security Hardening
**Priority**: High | **Estimated**: 14 hours | **Ongoing**

#### Child Issues:
1. **SEC-001**: Implement rate limiting (3h)
2. **SEC-002**: Add input sanitization (4h)
3. **SEC-003**: Implement CORS policy (1h)
4. **SEC-004**: Add security headers (2h)
5. **SEC-005**: Security audit (4h)

**Total**: 5 issues

---

## Summary Statistics

### By Sprint

| Sprint | Epics | Issues | Estimated Hours |
|--------|-------|--------|-----------------|
| Sprint 1 | 4 | 28 | 91h (~2.3 weeks) |
| Sprint 2 | 4 | 33 | 116h (~2.9 weeks) |
| Sprint 3 | 5 | 23 | 88h (~2.2 weeks) |
| Sprint 4 | 4 | 27 | 94h (~2.4 weeks) |
| Ongoing | 3 | 12 | 39h |
| **Total** | **20** | **123** | **428h** |

### By Category

| Category | Issues | Estimated Hours |
|----------|--------|-----------------|
| Infrastructure | 11 | 34h |
| Module Implementation | 71 | 267h |
| Testing | 13 | 50h |
| Documentation | 3 | 10h |
| Performance & Security | 9 | 29h |
| Events & Integration | 6 | 14h |
| Caching | 5 | 14h |
| Photo & Media | 7 | 30h |
| **Total** | **125** | **448h** |

### By Priority

| Priority | Epics | Issues | Estimated Hours |
|----------|-------|--------|-----------------|
| Critical | 11 | 75 | 285h |
| High | 6 | 35 | 120h |
| Medium | 3 | 15 | 43h |
| **Total** | **20** | **125** | **448h** |

---

## Team Allocation

### Recommended Team Structure (3-5 developers)

**Backend Developer 1**:
- Document Module (EPIC-005)
- Photo Gallery (EPIC-014)
- Performance Optimization (EPIC-019)

**Backend Developer 2**:
- Request Module (EPIC-006)
- Property Details (EPIC-015)
- Authentication Module (EPIC-003)

**Backend Developer 3**:
- Appraisal Module (EPIC-009, 010, 011, 012)
- Collateral Module (EPIC-013, 016)
- Event Integration (EPIC-007)

**DevOps/Backend Lead**:
- Infrastructure (EPIC-001, 002)
- Caching (EPIC-008)
- Security (EPIC-020)

**QA Lead** (or rotate among developers):
- Testing Infrastructure (EPIC-004)
- All TEST-* issues

---

## How to Use This Document

1. **Create Epics First**: Create all epic issues using the epic template
2. **Create Child Issues**: For each epic, create child issues using appropriate templates
3. **Link Issues**: Link child issues to their parent epic
4. **Set Milestones**: Assign issues to sprint milestones
5. **Add Labels**: Apply appropriate labels (module, priority, sprint)
6. **Assign Team**: Assign issues to team members
7. **Track Progress**: Update epic checklists as issues are completed

---

## GitHub Project Board Structure

### Columns

1. **Backlog** - All planned work
2. **Sprint 1 - Ready** - Sprint 1 tasks ready to start
3. **Sprint 1 - In Progress** - Currently being worked on
4. **Sprint 1 - Review** - In code review
5. **Sprint 1 - Done** - Completed and merged
6. (Repeat for Sprints 2, 3, 4)

### Filters

- **By Sprint**: `milestone:"Sprint 1 - Foundation"`
- **By Module**: `label:"auth"`, `label:"request"`, etc.
- **By Priority**: `label:"priority-critical"`
- **By Team Member**: `assignee:username`
- **My Issues**: `assignee:@me`

---

## References

- **WBS Document**: `docs/data-model/20-implementation-wbs.md`
- **Issue Templates**: `.github/ISSUE_TEMPLATE/`
- **Issue Creation Guide**: `docs/data-model/22-github-issue-guide.md`

---

**Last Updated**: 2025-01-02
**Version**: 1.0
