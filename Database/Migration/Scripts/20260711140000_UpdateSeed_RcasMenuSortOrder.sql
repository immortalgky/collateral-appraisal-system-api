-- ============================================================
-- CA-468: Correct RCAS Operational Report menu ordering
-- Schema: auth
-- The report-menu seeder (MenuSeedData.cs / AuthDataSeed.UpsertTreeAsync)
-- is INSERT-ONLY, so rows already seeded keep whatever SortOrder they were
-- first created with. The RCAS children under "main.reports.operational"
-- were originally seeded out of numeric order
-- (001,002,004,008,009,010,003,005,006,007,011,012). Per FSD they must
-- list RCAS001 -> RCAS012 in sequence. MenuSeedData.cs has been corrected
-- for fresh DBs; this script backfills SortOrder on existing DBs.
-- Idempotent: re-running is a no-op once SortOrder already matches.
-- ============================================================

DECLARE @Ordering TABLE (ItemKey NVARCHAR(200) NOT NULL, SortOrder INT NOT NULL);
INSERT INTO @Ordering (ItemKey, SortOrder) VALUES
    (N'main.reports.operational.rcas001', 10),
    (N'main.reports.operational.rcas002', 20),
    (N'main.reports.operational.rcas003', 30),
    (N'main.reports.operational.rcas004', 40),
    (N'main.reports.operational.rcas005', 50),
    (N'main.reports.operational.rcas006', 60),
    (N'main.reports.operational.rcas007', 70),
    (N'main.reports.operational.rcas008', 80),
    (N'main.reports.operational.rcas009', 90),
    (N'main.reports.operational.rcas010', 100),
    (N'main.reports.operational.rcas011', 110),
    (N'main.reports.operational.rcas012', 120);

UPDATE m
SET m.SortOrder = o.SortOrder
FROM auth.MenuItems m
INNER JOIN @Ordering o ON o.ItemKey = m.ItemKey
WHERE m.SortOrder <> o.SortOrder;

PRINT 'Corrected RCAS operational report menu SortOrder (RCAS001 -> RCAS012)';
GO
