-- Load-test monitoring queries.
-- The HTTP load (create+submit) finishes fast; the appraisals are created
-- ASYNC via the outbox -> RabbitMQ -> Workflow/Appraisal consumers. Run these
-- repeatedly during and after the k6 run to watch the async drain.
--
-- Marker: generated requests have Requestor = Creator = 'loadtest'.

-- 1) How many load-test requests were submitted (the HTTP-side result).
SELECT COUNT(*) AS submitted_requests
FROM request.Requests
WHERE Requestor = 'loadtest' AND Status = 'Submitted';

-- 2) How many appraisals exist now (watch this climb toward the target AFTER the
--    HTTP load ends — this is the async-drain progress).
SELECT COUNT(*) AS appraisals_total FROM appraisal.Appraisals;

-- 3) Workflow instances (one per submitted request). CorrelationId = request Id.
SELECT COUNT(*) AS workflow_instances FROM workflow.WorkflowInstances;

-- 4) Outbox backlog — the gap between "submitted" and "appraised" lives here + in
--    RabbitMQ. Pending/Processing = not yet drained; Failed = dead-lettered.
--    NOTE: the table has no explicit schema in config (defaults to the publishing
--    DbContext schema, normally 'request'). If the line below errors, find the real
--    schema with query 4b and adjust.
SELECT Status, COUNT(*) AS cnt
FROM request.IntegrationEventOutbox
GROUP BY Status;

-- 4b) Locate the outbox table if the schema differs.
SELECT SCHEMA_NAME(t.schema_id) AS [schema], t.name
FROM sys.tables t
WHERE t.name = 'IntegrationEventOutbox';

-- 5) Drain rate over the last minute (run twice ~60s apart and diff appraisals_total,
--    or use CreatedAt if appraisals carry one).
SELECT
    MIN(CreatedAt) AS first_appraisal,
    MAX(CreatedAt) AS last_appraisal,
    COUNT(*)       AS appraisals_total
FROM appraisal.Appraisals;

-- 6) Error check — requests that never left Draft/New (submit failed) under the marker.
SELECT Status, COUNT(*) AS cnt
FROM request.Requests
WHERE Requestor = 'loadtest'
GROUP BY Status;

-- ============================================================================
-- TPS  (transactions/sec = appraisals actually created by the consumer)
-- This is the REAL business throughput, NOT in the k6 output. Clock =
-- appraisal.Appraisals.CreatedAt (stamped by the consumer at creation time).
-- A TPS number is "sustainable capacity" ONLY if the outbox backlog (query 4)
-- stays flat during the window; if the backlog grows, you've measured the max
-- drain rate (the knee) while offered RPS exceeds it.
-- Set @runStart to when you started the k6 run (scopes to this test). If the DB
-- is otherwise idle you can leave it as a wide lower bound.
-- ============================================================================
DECLARE @runStart datetime2 = DATEADD(hour, -1, GETDATE());   -- adjust to your run start

-- 7) TPS time series, per minute. The flat top (plateau) is the sustained TPS.
--    per_min / 60 = TPS. Switch the bucket to 'second' for high-rate runs.
SELECT
    DATEADD(minute, DATEDIFF(minute, 0, CreatedAt), 0) AS bucket_minute,
    COUNT(*)                                           AS per_min,
    CAST(COUNT(*) / 60.0 AS decimal(10,2))             AS tps
FROM appraisal.Appraisals
WHERE CreatedAt >= @runStart
GROUP BY DATEADD(minute, DATEDIFF(minute, 0, CreatedAt), 0)
ORDER BY bucket_minute;

-- 8) Average TPS over the whole window (total appraisals / elapsed seconds).
SELECT
    COUNT(*)                                                                AS appraisals,
    DATEDIFF(second, MIN(CreatedAt), MAX(CreatedAt))                        AS elapsed_sec,
    CAST(COUNT(*) * 1.0 / NULLIF(DATEDIFF(second, MIN(CreatedAt), MAX(CreatedAt)), 0)
         AS decimal(10,2))                                                  AS avg_tps
FROM appraisal.Appraisals
WHERE CreatedAt >= @runStart;

-- 9) Two-sample live TPS (no timestamp dependency): run this once, wait ~30s,
--    run again, then tps = (count2 - count1) / seconds_between.
SELECT GETDATE() AS sampled_at, COUNT(*) AS appraisals_total FROM appraisal.Appraisals;

-- 10) Stage TPS upstream of appraisal creation — workflow instances/sec
--     (lets you see whether the Workflow consumer or the Appraisal consumer is the limit).
--     Uses StartedOn (domain timestamp, always set when the instance is created).
SELECT
    DATEADD(minute, DATEDIFF(minute, 0, StartedOn), 0) AS bucket_minute,
    COUNT(*)                                           AS workflow_instances_per_min
FROM workflow.WorkflowInstances
WHERE StartedOn >= @runStart
GROUP BY DATEADD(minute, DATEDIFF(minute, 0, StartedOn), 0)
ORDER BY bucket_minute;
