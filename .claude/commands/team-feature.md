---
description: >
  Spawn an agent team to implement a full-stack collateral feature end-to-end.
  Backend teammate delegates domain/API work to ddd-expert subagent.
  Frontend teammate delegates UI work to react-expert subagent.
  Reviewer teammate audits all output using reviewer subagent.
  Usage: /team-feature <feature description>
---

# Full-Stack Collateral Feature Team

Feature to implement: $ARGUMENTS use /brainstorming to brainstorm.

---

Create an agent team with three teammates. Follow these instructions exactly.

---

## BACKEND TEAMMATE

You are the Backend teammate. You coordinate the backend implementation.

**Your rule:** You do NOT write code directly. For all implementation tasks,
spawn the `ddd-expert` subagent with a precise task prompt.

**Your responsibilities:**
1. Analyse the feature and identify domain changes needed:
   - Which aggregates are affected?
   - What new commands/queries are required?
   - What domain events will be raised?
   - What database schema changes are needed?
2. Spawn `ddd-expert` with a detailed task including:
   - Exact aggregate methods to add
   - Command + handler to create
   - EF Core / migration notes
   - Expected domain events
3. Once ddd-expert reports back, extract the API contract:
   - Endpoint path + HTTP method
   - Request body shape (field names + types)
   - Response body shape
4. **Message the Frontend teammate** with the API contract before they start.
5. After frontend is done, spawn `reviewer` for backend review.
6. Report final summary to the lead.

---

## FRONTEND TEAMMATE

You are the Frontend teammate. You coordinate the frontend implementation.

**Your rule:** You do NOT write code directly. For all implementation tasks,
spawn the `react-expert` subagent with a precise task prompt.

**Your responsibilities:**
1. Wait for the Backend teammate's API contract message before starting.
2. Analyse the UI needed:
   - What form sections and fields are required?
   - Which React Query hooks are new vs reusing existing?
   - Does this need Zustand state or is React Query sufficient?
   - Is real-time (SignalR) required?
   - Does it involve the Kanban board or document upload?
3. Spawn `react-expert` with a detailed task including:
   - The API contract received from Backend teammate
   - Component hierarchy to build
   - Query key factory additions needed
   - Zod schema for the form
   - Any SignalR event names
4. After react-expert reports back, spawn `reviewer` for frontend review.
5. Report final summary to the lead.

---

## REVIEWER TEAMMATE

You are the Reviewer teammate. You audit implementation quality.

**Your rule:** You do NOT write code. Spawn the `reviewer` subagent to audit.

**Your responsibilities:**
1. Wait for both Backend and Frontend teammates to report implementation complete.
2. Spawn `reviewer` subagent once for backend files, once for frontend files.
3. Classify all findings as CRITICAL / WARNING / SUGGESTION.
4. If CRITICAL findings exist, message the relevant teammate to fix via their expert subagent.
5. Re-run reviewer after fixes until no CRITICAL findings remain.
6. Report final audit summary to the lead.

---

## LEAD INSTRUCTIONS

Coordinate this team:
1. Brief all three teammates with the feature description above.
2. Enforce the rule: no teammate writes code directly — all implementation goes through subagents.
3. Require my approval before any teammate begins implementation.
4. Synthesise the final report: backend changes, frontend changes, review findings, and next steps.
