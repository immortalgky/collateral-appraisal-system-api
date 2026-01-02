---
name: Epic
about: Parent issue to track a major feature or module (contains multiple child issues)
title: '[EPIC] '
labels: epic
assignees: ''
---

## 🎯 Epic Overview

**Epic ID**: <!-- e.g., EPIC-001 -->
**Module/Feature**: <!-- Auth / Request / Document / Appraisal / Collateral / Infrastructure -->
**Sprint**: <!-- Sprint 1, 2, 3, or 4 -->
**Total Estimated Time**: <!-- e.g., 40 hours (~1 week) -->
**Priority**: <!-- Critical, High, Medium, Low -->

## 📝 Description

<!-- High-level description of this epic and its business value -->

### Business Value
<!-- Why this epic is important -->

### Success Criteria
<!-- What does success look like for this epic? -->

## 🗺️ Epic Scope

### In Scope
- [ ] Feature/Module 1
- [ ] Feature/Module 2
- [ ] Feature/Module 3

### Out of Scope
- Item 1
- Item 2
- Item 3

## 📊 Child Issues

<!-- List all related issues that are part of this epic -->

### Infrastructure Tasks
- [ ] #issue-number - [INFRA-XXX] Task description
- [ ] #issue-number - [INFRA-XXX] Task description

### Implementation Tasks
- [ ] #issue-number - [MODULE-XXX] Task description
- [ ] #issue-number - [MODULE-XXX] Task description
- [ ] #issue-number - [MODULE-XXX] Task description

### Testing Tasks
- [ ] #issue-number - [TEST-XXX] Task description
- [ ] #issue-number - [TEST-XXX] Task description

### Documentation Tasks
- [ ] #issue-number - [DOC-XXX] Task description

**Total Child Issues**: X
**Completed**: 0/X
**Progress**: 0%

## 🎯 Acceptance Criteria

<!-- Epic-level acceptance criteria -->
- [ ] All child issues completed
- [ ] All tests passing (unit + integration)
- [ ] Code coverage ≥80%
- [ ] Documentation complete
- [ ] Code reviewed and approved
- [ ] Deployed to development environment
- [ ] Smoke tests passed

## 🔗 Dependencies

### Depends On
- Epic: #epic-number - Description
- Issue: #issue-number - Description

### Blocks
- Epic: #epic-number - Description
- Issue: #issue-number - Description

## 📅 Timeline

| Phase | Duration | Start | End | Status |
|-------|----------|-------|-----|--------|
| Planning | X days | YYYY-MM-DD | YYYY-MM-DD | ⏳ Not Started |
| Development | X days | YYYY-MM-DD | YYYY-MM-DD | ⏳ Not Started |
| Testing | X days | YYYY-MM-DD | YYYY-MM-DD | ⏳ Not Started |
| Documentation | X days | YYYY-MM-DD | YYYY-MM-DD | ⏳ Not Started |
| Review | X days | YYYY-MM-DD | YYYY-MM-DD | ⏳ Not Started |

**Total Duration**: X weeks

## 🏗️ Architecture Overview

<!-- High-level architecture for this epic -->

### Components
- **Component 1**: Description
- **Component 2**: Description
- **Component 3**: Description

### Database Schema
<!-- List main tables/entities -->
- Table 1
- Table 2
- Table 3

### API Endpoints
<!-- List main endpoints -->
- `POST /api/endpoint` - Description
- `GET /api/endpoint` - Description
- `PUT /api/endpoint` - Description

### Events
<!-- Domain/Integration events -->
- **EventName**: Published when... Consumed by...

## 💡 Technical Approach

### Technology Stack
- Framework: .NET 9.0
- Database: SQL Server
- ORM: Entity Framework Core
- CQRS: MediatR
- API: Carter (Minimal APIs)
- Events: MassTransit + RabbitMQ

### Design Patterns
- Domain-Driven Design (DDD)
- CQRS
- Repository Pattern
- Unit of Work
- Event Sourcing (if applicable)

### Key Design Decisions
1. **Decision 1**: Rationale
2. **Decision 2**: Rationale
3. **Decision 3**: Rationale

## 🧪 Testing Strategy

### Unit Testing
- **Target Coverage**: ≥80%
- **Focus Areas**:
  - Domain logic
  - Validators
  - Command/Query handlers

### Integration Testing
- **Scenarios**:
  - Happy path workflows
  - Edge cases
  - Error scenarios

### Performance Testing (if applicable)
- **Benchmarks**:
  - API response time: <200ms
  - Database queries: <50ms
  - Concurrent users: 100+

## 📚 Documentation Requirements

- [ ] Module documentation (`docs/data-model/XX-module.md`)
- [ ] API documentation (OpenAPI/Swagger)
- [ ] README updates
- [ ] Architecture diagrams
- [ ] User guides (if applicable)

## 🚀 Deployment Checklist

- [ ] Database migrations created
- [ ] Configuration settings documented
- [ ] Environment variables set
- [ ] Deployment scripts ready
- [ ] Rollback plan documented
- [ ] Monitoring/alerts configured

## 📊 Progress Tracking

### Velocity Metrics
- **Planned Points**: X
- **Completed Points**: 0
- **Velocity**: 0%

### Burndown
```
Week 1: ██████░░░░░░░░░░░░░░ 30% (X/Y tasks)
Week 2: ░░░░░░░░░░░░░░░░░░░░ 0% (0/Y tasks)
```

## 🔍 Risks & Mitigation

| Risk | Probability | Impact | Mitigation Strategy |
|------|-------------|--------|---------------------|
| Risk 1 | High/Med/Low | High/Med/Low | Strategy description |
| Risk 2 | High/Med/Low | High/Med/Low | Strategy description |

## 🎓 Learning & Research

<!-- Any new technologies or patterns to research -->
- [ ] Research topic 1
- [ ] Spike: Proof of concept for feature X
- [ ] Team training on technology Y

## 💬 Discussion & Decisions

<!-- Use this section to log important decisions and discussions -->

### Decision Log

**Decision 1** (YYYY-MM-DD)
- **Context**: ...
- **Decision**: ...
- **Rationale**: ...
- **Consequences**: ...

**Decision 2** (YYYY-MM-DD)
- **Context**: ...
- **Decision**: ...
- **Rationale**: ...

## 📌 Notes

<!-- Any additional context, warnings, or important information -->

### Important Considerations
- Consideration 1
- Consideration 2
- Consideration 3

### Reference Links
- Design Document: [Link]
- Figma/Mockups: [Link]
- Meeting Notes: [Link]
- Slack Discussion: [Link]

---

## 🏷️ Labels & Classification

**Milestone**: Sprint X - [Phase Name]
**Epic Owner**: @username
**Team**: Backend / Frontend / DevOps / Full Stack
**Module**: Auth / Request / Document / Appraisal / Collateral

---

## 📝 Epic Update Template

<!-- Use this template for regular epic updates -->

```markdown
### Epic Update - [Date]

**Progress**: X/Y tasks completed (Z%)
**Status**: On Track / At Risk / Blocked

**Completed This Week**:
- Task 1
- Task 2

**In Progress**:
- Task 3 (50% complete)
- Task 4 (30% complete)

**Upcoming Next Week**:
- Task 5
- Task 6

**Blockers/Issues**:
- Blocker 1
- Issue 2

**Decisions Needed**:
- Decision 1
- Decision 2
```

---

## ✅ Definition of Done

This epic is considered complete when:
- [ ] All child issues are closed
- [ ] All acceptance criteria met
- [ ] All tests passing (≥80% coverage)
- [ ] Code reviewed and approved
- [ ] Documentation complete and reviewed
- [ ] Deployed to staging environment
- [ ] Stakeholder demo completed
- [ ] Product owner acceptance received

---

**Created**: YYYY-MM-DD
**Last Updated**: YYYY-MM-DD
**Target Completion**: YYYY-MM-DD
