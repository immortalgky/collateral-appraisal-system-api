# GitHub Issue Creation Guide

## Overview

This guide provides step-by-step instructions for creating GitHub issues from the Work Breakdown Structure (WBS) for the Collateral Appraisal System implementation.

**Reference Documents**:
- WBS: `docs/data-model/20-implementation-wbs.md`
- Epic List: `docs/data-model/21-github-epics.md`
- Issue Templates: `.github/ISSUE_TEMPLATE/`

---

## Quick Start

### 1. Initial Setup (One-Time)

#### Create Milestones

Navigate to GitHub → Your Repository → Issues → Milestones → New Milestone

Create 4 sprint milestones:

1. **Sprint 1 - Foundation**
   - Description: Infrastructure setup, shared domain, and authentication module
   - Due date: [Week 2 end date]

2. **Sprint 2 - Core Modules**
   - Description: Request and Document modules with event integration
   - Due date: [Week 4 end date]

3. **Sprint 3 - Appraisal Core**
   - Description: Appraisal module core features and basic collateral
   - Due date: [Week 6 end date]

4. **Sprint 4 - Advanced Features**
   - Description: Photo gallery, property details, and reports
   - Due date: [Week 8 end date]

#### Create Labels

Navigate to GitHub → Your Repository → Issues → Labels

**By Sprint**:
- `sprint-1` (Color: #0052CC)
- `sprint-2` (Color: #0052CC)
- `sprint-3` (Color: #0052CC)
- `sprint-4` (Color: #0052CC)

**By Module**:
- `auth` (Color: #D73A4A)
- `request` (Color: #D73A4A)
- `document` (Color: #D73A4A)
- `appraisal` (Color: #D73A4A)
- `collateral` (Color: #D73A4A)

**By Type**:
- `infrastructure` (Color: #5319E7)
- `module` (Color: #1D76DB)
- `testing` (Color: #0E8A16)
- `documentation` (Color: #FEF2C0)
- `epic` (Color: #B60205)
- `event` (Color: #FBCA04)
- `cache` (Color: #FBCA04)
- `photo` (Color: #FBCA04)
- `security` (Color: #D93F0B)
- `performance` (Color: #FBCA04)

**By Priority**:
- `priority-critical` (Color: #B60205)
- `priority-high` (Color: #D93F0B)
- `priority-medium` (Color: #FBCA04)
- `priority-low` (Color: #0E8A16)

**Other**:
- `quality` (Color: #0E8A16)
- `implementation` (Color: #1D76DB)
- `core` (Color: #D73A4A)
- `api` (Color: #1D76DB)

---

## 2. Creating Epics

### Step-by-Step: Create an Epic

**Example**: Creating EPIC-001: Project Infrastructure Setup

1. **Navigate to Issues**
   - Go to GitHub → Your Repository → Issues
   - Click "New Issue"

2. **Choose Template**
   - Select "Epic" template
   - Or click "Get started" next to Epic template

3. **Fill in Epic Details**

```markdown
Title: [EPIC] Project Infrastructure Setup

Labels: epic, infrastructure, sprint-1, priority-critical

Milestone: Sprint 1 - Foundation

Assignee: @devops-lead
```

4. **Complete Epic Template**

Replace template placeholders:

```markdown
## 🎯 Epic Overview

**Epic ID**: EPIC-001
**Module/Feature**: Infrastructure
**Sprint**: Sprint 1
**Total Estimated Time**: 18 hours (~2 days)
**Priority**: Critical

## 📝 Description

Set up the complete development infrastructure including project structure, Docker services, database configuration, and CQRS/Event-driven architecture foundations.

### Business Value
Enable the team to begin feature development with a solid, scalable foundation that supports the modular monolith architecture.

### Success Criteria
- All developers can run the project locally
- Docker services running (SQL Server, Redis, RabbitMQ, Seq)
- Database migrations working
- CQRS infrastructure functional
- Event bus configured and tested

## 📊 Child Issues

### Infrastructure Tasks
- [ ] #X - [INFRA-001] Create project structure and solution file
- [ ] #X - [INFRA-002] Set up Docker infrastructure
- [ ] #X - [INFRA-003] Configure database connection and EF Core
- [ ] #X - [INFRA-004] Set up logging infrastructure
- [ ] #X - [INFRA-005] Configure MediatR and CQRS infrastructure
- [ ] #X - [INFRA-006] Configure MassTransit and RabbitMQ

**Total Child Issues**: 6
**Completed**: 0/6
**Progress**: 0%

[... continue filling in the rest of the template ...]
```

5. **Create Epic**
   - Click "Submit new issue"
   - Note the epic issue number (e.g., #10)

6. **Repeat for All Epics**
   - Create all 20 epics from `21-github-epics.md`
   - Keep track of epic numbers

---

## 3. Creating Child Issues

### Step-by-Step: Create a Module Implementation Issue

**Example**: Creating AUTH-002: Create User aggregate

1. **Navigate to Issues**
   - Click "New Issue"

2. **Choose Template**
   - Select "Module Implementation" template

3. **Fill in Issue Details**

```markdown
Title: AUTH-002: Create User aggregate

Labels: auth, module, implementation, sprint-1, priority-critical

Milestone: Sprint 1 - Foundation

Assignee: @backend-dev-2
```

4. **Complete Module Template**

```markdown
## 📦 Module Information

**Module**: Auth
**Task ID**: AUTH-002
**Sprint**: Sprint 1
**Estimated Time**: 4 hours
**Priority**: Critical

## 📝 Description

Implement the User aggregate entity with all authentication and profile fields. Include user creation logic, password hashing, and activation/deactivation methods.

## ✅ Task Checklist

- [ ] **Create User entity** - Define User class with all fields (1h)
- [ ] **Add password hashing** - Implement using ASP.NET Core Identity (1h)
- [ ] **Add user creation method** - Factory method for creating users (1h)
- [ ] **Add activation/deactivation** - Methods to activate/deactivate users (1h)

**Total Estimated Time**: 4 hours

## 🎯 Acceptance Criteria

- [ ] User entity created with all fields from schema
- [ ] Password hashing implemented securely
- [ ] User creation method works correctly
- [ ] Activation/deactivation methods functional
- [ ] Unit tests written with ≥80% coverage
- [ ] Code reviewed and approved

## 🔗 Dependencies

- Depends on: #X (SHARED-001: Create base entity classes)
- Blocks: #X (AUTH-003: Create Role and Permission entities)

[... use the code examples from the template ...]

## 📌 Notes

- Use ASP.NET Core Identity's PasswordHasher<User>
- Ensure SecurityStamp is updated on password changes
- Follow DDD principles - User is an aggregate root

---

**Epic**: #10 (EPIC-001: Authentication & Authorization Module)
**Milestone**: Sprint 1 - Foundation
**Assignee Suggestion**: Backend Developer 2
**Related Modules**: None (Auth is foundational)
```

5. **Link to Epic**
   - After creating the issue, go to the Epic issue
   - Edit the epic to add the child issue number to the checklist
   - Example: Change `- [ ] #X - [AUTH-002]` to `- [ ] #25 - [AUTH-002]`

---

## 4. Issue Templates Usage

### Infrastructure Task Template

**When to use**:
- Setting up infrastructure (Docker, databases, etc.)
- DevOps tasks
- Configuration tasks
- Tool setup

**Example issues**:
- INFRA-001 to INFRA-006
- CACHE-001 to CACHE-005

---

### Module Implementation Template

**When to use**:
- Implementing domain entities
- Creating aggregates
- Building CQRS commands/queries
- API endpoint development

**Example issues**:
- AUTH-001 to AUTH-012
- REQ-001 to REQ-012
- DOC-001 to DOC-013
- APR-001 to APR-016
- COL-001 to COL-012

**Most common template** - Use this for ~70% of implementation work.

---

### Testing Task Template

**When to use**:
- Writing unit tests
- Writing integration tests
- Setting up test infrastructure
- Performance testing

**Example issues**:
- TEST-001 to TEST-004
- TEST-DOC-001
- TEST-REQ-001
- TEST-APR-001

---

### Documentation Task Template

**When to use**:
- API documentation
- Technical documentation
- README updates
- User guides

**Example issues**:
- DOC-API-001 to DOC-API-003

---

### Epic Template

**When to use**:
- Creating parent tracking issues
- Grouping related features
- Sprint planning

**Example epics**:
- All EPIC-001 to EPIC-020

---

## 5. Best Practices

### Issue Naming Convention

**Format**: `[MODULE-ID]: Brief description`

**Good Examples**:
- ✅ `AUTH-002: Create User aggregate`
- ✅ `REQ-009: Create request commands`
- ✅ `DOC-010: Create document commands`
- ✅ `INFRA-003: Configure database connection and EF Core`

**Bad Examples**:
- ❌ `Create user` (missing module and ID)
- ❌ `AUTH-002` (missing description)
- ❌ `Implement authentication stuff` (too vague)

---

### Label Application

**Every issue should have**:
- 1 sprint label (`sprint-1`, `sprint-2`, etc.)
- 1 priority label (`priority-critical`, `priority-high`, etc.)
- 1+ type labels (`module`, `infrastructure`, `testing`, etc.)
- 0-1 module labels (`auth`, `request`, etc.) - if applicable

**Example**:
```
Labels: auth, module, implementation, sprint-1, priority-critical
```

---

### Milestone Assignment

**All issues must have a milestone**:
- Sprint 1 issues → "Sprint 1 - Foundation"
- Sprint 2 issues → "Sprint 2 - Core Modules"
- Sprint 3 issues → "Sprint 3 - Appraisal Core"
- Sprint 4 issues → "Sprint 4 - Advanced Features"

---

### Assignee Guidelines

**Assign based on**:
- Team member expertise
- Current workload
- Sprint capacity
- Dependencies

**Can be assigned to**:
- Individual developer
- Team (using GitHub teams)
- Left unassigned initially (assign during sprint planning)

---

### Dependency Management

**Always specify dependencies**:
```markdown
## 🔗 Dependencies

- Depends on: #23 (SHARED-001: Create base entity classes)
- Blocks: #45 (REQ-003: Create Request aggregate)
```

**In GitHub**:
- Use issue numbers, not task IDs
- Update when issues are created/renumbered
- Check dependencies before starting work

---

## 6. Bulk Issue Creation

### Option 1: Manual Creation

**Pros**: Full control, detailed information
**Cons**: Time-consuming

**Process**:
1. Create all 20 epics first (Week 1, Day 1)
2. Create Sprint 1 child issues (Week 1, Day 1)
3. Create Sprint 2-4 issues as needed

---

### Option 2: GitHub CLI

**Install gh CLI**:
```bash
# macOS
brew install gh

# Login
gh auth login
```

**Create issue from command line**:
```bash
gh issue create \
  --title "AUTH-002: Create User aggregate" \
  --body "$(cat issue-template.md)" \
  --label "auth,module,sprint-1,priority-critical" \
  --milestone "Sprint 1 - Foundation" \
  --assignee "backend-dev-2"
```

**Bulk create with script**:
```bash
#!/bin/bash
# create-issues.sh

# Create AUTH module issues
gh issue create --title "AUTH-001: Create Auth module structure" --label "auth,module,sprint-1" --milestone "Sprint 1 - Foundation"
gh issue create --title "AUTH-002: Create User aggregate" --label "auth,module,sprint-1" --milestone "Sprint 1 - Foundation"
# ... more issues
```

---

### Option 3: GitHub API + Script

**Python script example**:
```python
import requests
import os

# GitHub settings
REPO_OWNER = "your-org"
REPO_NAME = "collateral-appraisal-system-api"
TOKEN = os.getenv("GITHUB_TOKEN")

headers = {
    "Authorization": f"token {TOKEN}",
    "Accept": "application/vnd.github.v3+json"
}

# Issue template
issue_data = {
    "title": "AUTH-002: Create User aggregate",
    "body": "... full template content ...",
    "labels": ["auth", "module", "sprint-1", "priority-critical"],
    "milestone": 1,  # Milestone ID
    "assignees": ["backend-dev-2"]
}

# Create issue
response = requests.post(
    f"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/issues",
    json=issue_data,
    headers=headers
)

print(f"Created issue #{response.json()['number']}")
```

---

## 7. Project Board Setup

### Create GitHub Project

1. **Navigate to Projects**
   - Go to GitHub → Your Repository → Projects
   - Click "New Project"

2. **Choose Template**
   - Select "Board" template
   - Name: "Collateral Appraisal System - 2 Month Sprint"

3. **Configure Columns**
   - **Backlog** - All planned work
   - **Sprint 1 - To Do** - Ready to start
   - **Sprint 1 - In Progress** - Being worked on
   - **Sprint 1 - Review** - In code review
   - **Sprint 1 - Done** - Completed
   - Repeat for Sprints 2, 3, 4

4. **Add Issues to Project**
   - Drag issues to appropriate columns
   - Or use automation rules

5. **Set Up Automation**
   - Auto-move to "In Progress" when assigned
   - Auto-move to "Review" when PR opened
   - Auto-move to "Done" when PR merged

---

## 8. Sprint Planning Workflow

### Week Before Sprint

1. **Review Epic List** (21-github-epics.md)
2. **Create Epic Issues** for upcoming sprint
3. **Create Child Issues** for sprint backlog
4. **Assign Issues** to team members
5. **Set Priorities** and dependencies

### Sprint Kickoff Meeting

1. **Review Sprint Goals**
2. **Walk Through Issues**
3. **Clarify Requirements**
4. **Confirm Assignments**
5. **Identify Blockers**

### During Sprint

1. **Daily Standups** - Update issue status
2. **Move Issues** on project board
3. **Update Epic Progress** - Check off completed child issues
4. **Create New Issues** if needed (scope changes)

### Sprint Review

1. **Close Completed Issues**
2. **Update Epic Completion** %
3. **Review Metrics** (velocity, burndown)
4. **Carry Over** incomplete issues to next sprint

---

## 9. Issue Workflow States

### Lifecycle

```
Backlog → To Do → In Progress → Review → Done
```

### State Transitions

| From | To | Trigger |
|------|-----|---------|
| Backlog | To Do | Sprint planning assigns to sprint |
| To Do | In Progress | Developer starts work, assigns to self |
| In Progress | Review | PR opened, requests review |
| Review | In Progress | Changes requested in PR |
| Review | Done | PR approved and merged |
| Any | Backlog | Deprioritized or blocked |

---

## 10. Common Scenarios

### Scenario 1: Issue is Blocked

1. **Add Comment** to issue explaining blocker
2. **Add Label** `blocked`
3. **Link** to blocking issue
4. **Update Epic** - note in progress section
5. **Move** to "Blocked" column (if you have one)

---

### Scenario 2: Issue Needs More Time

1. **Update Time Estimate** in issue description
2. **Add Comment** explaining why
3. **Notify** epic owner and team
4. **Adjust** sprint planning if necessary

---

### Scenario 3: Split Large Issue

1. **Create** sub-issues (TASK-XXX-1, TASK-XXX-2)
2. **Convert** original issue to epic (or link to epic)
3. **Distribute** estimates across sub-issues
4. **Assign** sub-issues to team members

---

### Scenario 4: Scope Change

1. **Create** new issue for additional work
2. **Link** to original issue
3. **Update** epic with new issue
4. **Discuss** in sprint planning if impacts timeline

---

## 11. Reporting & Metrics

### Burndown Chart

Track in GitHub Project:
- Total points/issues in sprint
- Completed points/issues per day
- Trend line to completion

### Velocity

Calculate after each sprint:
```
Velocity = Total Points Completed / Sprint Duration
```

Use to estimate future sprints.

### Code Coverage

Track per module:
- Target: ≥80%
- Report in issue comments
- Update epic with coverage %

---

## 12. Quick Reference

### Issue Creation Checklist

Before creating an issue:
- [ ] Check if epic exists
- [ ] Choose correct template
- [ ] Write clear title with ID
- [ ] Fill all required fields
- [ ] Add appropriate labels
- [ ] Set milestone
- [ ] Assign if known
- [ ] Link dependencies
- [ ] Add to project board

### Epic Update Checklist

Weekly epic updates:
- [ ] Update child issue checkboxes
- [ ] Calculate progress %
- [ ] Add status update comment
- [ ] Update timeline if needed
- [ ] Note blockers
- [ ] Communicate to team

---

## 13. Templates Quick Access

| Template | File | Use For |
|----------|------|---------|
| Infrastructure | `01-infrastructure-task.md` | DevOps, setup tasks |
| Module | `02-module-implementation.md` | Most development work |
| Testing | `03-testing-task.md` | Test writing |
| Documentation | `04-documentation-task.md` | Docs, API docs |
| Epic | `05-epic.md` | Parent tracking issues |

---

## 14. Helpful GitHub Commands

### Search Issues

```
# By label
label:auth

# By milestone
milestone:"Sprint 1 - Foundation"

# By assignee
assignee:@me

# By status
is:open
is:closed

# Combined
is:open label:auth milestone:"Sprint 1 - Foundation"
```

### Bulk Operations

```
# Close multiple issues
gh issue close 10 11 12 13

# Add label to multiple
gh issue edit 10 11 12 --add-label "sprint-1"

# Set milestone for multiple
gh issue edit 10 11 12 --milestone "Sprint 1 - Foundation"
```

---

## 15. FAQ

**Q: Should I create all 125 issues at once?**
A: No. Create epics first, then create child issues sprint by sprint.

**Q: Can I change issue numbers?**
A: GitHub auto-assigns issue numbers. Use task IDs (AUTH-001) in titles for reference.

**Q: What if I forgot to link an issue to its epic?**
A: Edit the epic and add the issue number to the checklist.

**Q: Can multiple people work on one issue?**
A: Yes, but assign to primary owner. Use comments to coordinate.

**Q: How do I handle urgent bugs during a sprint?**
A: Create issue with `priority-critical` and `bug` labels. Discuss in standup.

---

## Next Steps

1. **Set up milestones and labels** (30 minutes)
2. **Create Sprint 1 epics** (EPIC-001 to EPIC-004) (1 hour)
3. **Create Sprint 1 child issues** (~28 issues) (2-3 hours)
4. **Set up project board** (30 minutes)
5. **Assign Sprint 1 issues** to team (30 minutes)
6. **Begin development!** 🚀

---

**Document Version**: 1.0
**Last Updated**: 2025-01-02
**Author**: Development Team
