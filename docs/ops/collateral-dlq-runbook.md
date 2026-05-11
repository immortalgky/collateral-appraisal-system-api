# Collateral Consumer — DLQ Runbook

Scope: `AppraisalCompletedConsumer` in the Collateral module.
Audience: on-call engineer or backend developer.

---

## 1. Exceptions that produce dead-letters

> **PR-7 note:** `AliasWithoutParentException` was removed. The Collateral module
> is now a trusting consumer: alias-alone scenarios are resolved gracefully by
> re-anchoring the engagement to the parent IsMaster. The only remaining source
> of dead-letters is `ConflictException` (multi-title group overlap).

### 1.1 `ConflictException` — multi-title group overlap

**When:** `CollateralMasterUpsertService.UpsertLandGroupAsync` detects that two
or more titles in the same appraisal each match a *different* existing
`CollateralMaster` group.

**Why it dead-letters without retry:** `Program.cs` configures MassTransit to
skip retries for `ConflictException`:

```csharp
r.Ignore<Shared.Exceptions.ConflictException>();
```

Retrying will not help — the data (two separate master groups for the same
set of titles) will not change without admin action.

**What it means:** the request's land titles were previously appraised by two
different parties, creating two distinct master rows. The new appraisal
references titles from both. A merge is needed before the engagement can be
recorded.

---

## 2. How to investigate a dead-lettered message

### Step 1 — Find the message in the DLQ

Open the RabbitMQ management UI: `http://localhost:15672` (prod: use the
configured host). Credentials: `admin / P@ssw0rd` (dev); consult the
`appsettings.json` in the environment for prod.

Navigate to **Queues** and look for any queue named with `_error` or `_skipped`
suffix (e.g. `appraisal-completed_error`). Open it and **Get Messages**.

### Step 2 — Read the payload

The message body is JSON. Extract:

```
AppraisalId   — the Guid that failed
RequestId     — the upstream request
```

### Step 3 — Diagnose in Seq

Open Seq: `http://localhost:5341` (prod: use the configured host).
Credentials: `admin / P@ssw0rd` (dev).

Filter on the `AppraisalId` value:

```
AppraisalId = '<guid from payload>'
```

Look for `LogError` entries from `AppraisalCompletedConsumer`. The log entry
includes the exception type (`ConflictException`) and context.

### Step 4 — Identify conflicting masters (for ConflictException)

In SQL Server (schema `collateral`), find which master groups own the
conflicting titles:

```sql
SELECT cm.Id, cm.IsMaster, cm.ParentMasterId,
       ld.TitleNumber, ld.TitleType, ld.LandOfficeCode
FROM   collateral.CollateralMasters cm
JOIN   collateral.LandDetails ld ON ld.CollateralMasterId = cm.Id
WHERE  ld.TitleNumber IN ('<title1>', '<title2>', ...)   -- from the appraisal
ORDER  BY cm.Id;
```

A `ConflictException` means at least two distinct non-null `cm.Id` (IsMasters)
appear in the result. Identify the two groups and the engagements each carries:

```sql
SELECT e.AppraisalId, e.AppraisalDate, e.AppraisalCompanyId, e.CollateralMasterId
FROM   collateral.CollateralEngagements e
WHERE  e.CollateralMasterId IN ('<master1-id>', '<master2-id>')
ORDER  BY e.AppraisalDate DESC;
```

---

## 3. Resolution paths

### ConflictException — admin merge (future feature)

A proper admin merge endpoint is a post-v1 feature (`docs/ops` future work).

**Interim manual SQL (use with caution — backup first):**

1. Decide which master ID is the canonical IsMaster (the older/more-engaged one).
2. Re-point alias rows from the losing master to the winning master:

```sql
BEGIN TRANSACTION;

-- Re-parent any alias rows that pointed to the losing master
UPDATE collateral.CollateralMasters
SET    ParentMasterId = '<winner-id>'
WHERE  ParentMasterId = '<loser-id>';

-- Move engagements from the losing master to the winner
UPDATE collateral.CollateralEngagements
SET    CollateralMasterId = '<winner-id>'
WHERE  CollateralMasterId = '<loser-id>';

-- Soft-delete the losing master
UPDATE collateral.CollateralMasters
SET    IsDeleted = 1
WHERE  Id = '<loser-id>';

-- Verify, then commit
COMMIT;
```

3. After the merge, re-queue the dead-lettered message by moving it from the
   error queue back to the original queue via the RabbitMQ management UI
   (**Move messages** in the queue detail page).

---

## 4. Alerting recommendations

### Seq structured log alert

Create an alert in Seq on:

```
@Level = 'Error'
AND SourceContext LIKE '%AppraisalCompletedConsumer%'
AND @Message LIKE '%ConflictException%'
```

Send to the on-call Slack channel. Recommended threshold: fire on first
occurrence (every conflict is actionable).

### RabbitMQ DLQ depth alert

Monitor the `appraisal-completed_error` queue depth. Page when depth > 0 for
more than 15 minutes. RabbitMQ exposes this as a Prometheus metric:

```
rabbitmq_queue_messages{queue="appraisal-completed_error"}
```

Add this to the existing Grafana/Prometheus stack (`observability/` at the repo
root) and set an alert threshold of 1.
