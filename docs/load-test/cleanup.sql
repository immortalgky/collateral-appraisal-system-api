-- Reset the load-test data. Scoped to the marker (Requestor/Creator = 'loadtest')
-- so it never touches real MANUAL-channel data.
--
-- ⚠️ DEV ONLY. Run against the local dev database. Take the usual care: this DELETEs.
-- Order matters: child/dependent data first, then the requests.
-- Most child rows are removed by cascade FKs (Appraisal properties/assignments/fees,
-- Request owned collections). If a DELETE is blocked by an FK in your build, the
-- offending child table needs an explicit delete added above the parent.

SET NOCOUNT ON;

-- Collect the load-test request ids once.
IF OBJECT_ID('tempdb..#ltr') IS NOT NULL DROP TABLE #ltr;
SELECT Id
INTO #ltr
FROM request.Requests
WHERE Requestor = 'loadtest';

PRINT CONCAT('load-test requests: ', (SELECT COUNT(*) FROM #ltr));

-- Capture the load-test appraisal ids + their PropertyGroup ids BEFORE deleting the
-- appraisals — Pricing Analysis (phase 3) and Valuation Analyses are SEPARATE aggregates
-- with no cascade FK from the appraisal/group, so they'd be orphaned otherwise.
IF OBJECT_ID('tempdb..#lta') IS NOT NULL DROP TABLE #lta;
SELECT a.Id INTO #lta
FROM appraisal.Appraisals a JOIN #ltr r ON a.RequestId = r.Id;

IF OBJECT_ID('tempdb..#ltg') IS NOT NULL DROP TABLE #ltg;
SELECT pg.Id INTO #ltg
FROM appraisal.PropertyGroups pg JOIN #lta a ON pg.AppraisalId = a.Id;

-- 0a) Pricing analyses anchored on those groups (approaches/methods/etc. cascade).
DELETE p FROM appraisal.PricingAnalysis p JOIN #ltg g ON p.AnchorId = g.Id;

-- 0b) Rolled-up valuation summaries for those appraisals (group/property valuations cascade).
DELETE v FROM appraisal.ValuationAnalyses v JOIN #lta a ON v.AppraisalId = a.Id;

-- 1) Appraisals created from those requests (owned children cascade).
--    Appraisal has no FK to Request — join on RequestId.
DELETE a
FROM appraisal.Appraisals a
JOIN #lta x ON a.Id = x.Id;

DROP TABLE #lta; DROP TABLE #ltg;

-- 2) Workflow instances for those requests (CorrelationId = request Id as string).
--    If workflow child tables are not cascade-deleted, delete them first by InstanceId.
DELETE w
FROM workflow.WorkflowInstances w
JOIN #ltr r ON w.CorrelationId = CAST(r.Id AS varchar(36));

-- 3) Outbox rows for those requests (not FK'd; keyed by CorrelationId).
DELETE o
FROM request.IntegrationEventOutbox o
JOIN #ltr r ON o.CorrelationId = CAST(r.Id AS varchar(36));

-- 4) The requests themselves (owned collections: details/customers/properties/titles/
--    documents cascade).
DELETE FROM request.Requests WHERE Requestor = 'loadtest';

DROP TABLE #ltr;

PRINT 'load-test cleanup complete.';
