---
description: >
  Spawn an agent team to audit existing code across domain, application, infrastructure,
  and frontend layers. Each teammate runs the reviewer subagent on their layer in parallel.
  Usage: /team-review [scope: all | backend | frontend | security]
---

# Collateral Codebase Review Team

Scope: $ARGUMENTS (default: all layers)

---

Create an agent team with parallel review teammates.
All teammates spawn the `reviewer` subagent — they do not inspect code themselves.

---

## DOMAIN REVIEWER TEAMMATE

Spawn the `reviewer` subagent to audit:
- src/Domain/ — aggregates, value objects, domain events, invariants
- Focus: missing invariants, public setters, domain logic in wrong layer

Report findings classified as CRITICAL / WARNING / SUGGESTION.

---

## APPLICATION REVIEWER TEAMMATE

Spawn the `reviewer` subagent to audit:
- src/Application/ — commands, queries, handlers, validators, pipelines
- Focus: thin handlers, FluentValidation correctness, outbox usage, idempotency

Report findings classified as CRITICAL / WARNING / SUGGESTION.

---

## INFRASTRUCTURE REVIEWER TEAMMATE

Spawn the `reviewer` subagent to audit:
- src/Infrastructure/ — EF Core config, repositories, migrations, SQL queries
- Focus: implicit type conversions, parameter sniffing risks, lazy loading, owned entity config

Report findings classified as CRITICAL / WARNING / SUGGESTION.

---

## FRONTEND REVIEWER TEAMMATE

Spawn the `reviewer` subagent to audit:
- src/frontend/src/ — components, hooks, stores, schemas
- Focus: React Query key factories, Zustand selectors, Zod schema accuracy, optimistic update rollbacks

Report findings classified as CRITICAL / WARNING / SUGGESTION.

---

## LEAD INSTRUCTIONS

1. Spawn all four reviewer teammates in parallel.
2. Collect all findings when teammates complete.
3. Produce a consolidated report sorted by severity:
   - All CRITICALs across all layers (must fix)
   - All WARNINGs (should fix)
   - All SUGGESTIONs (nice to have)
4. Suggest which `/team-feature` tasks would address the CRITICALs.
