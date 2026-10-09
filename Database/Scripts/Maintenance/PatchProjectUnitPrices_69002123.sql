-- ============================================================
-- Appraisal 69002123 — load the committee-approved per-unit prices of a block condo
-- project into appraisal.ProjectUnitPrices, refresh appraisal.ValuationAnalyses, and push the new
-- appraised value into the workflow so the approval-tier switch sends it through a meeting.
-- Schemas: appraisal, workflow
-- Run by hand. NOT a DbUp migration: it targets one appraisal on one environment.
--
-- This is PatchProjectUnitPricesFromApprovedList.sql and PatchWorkflowAppraisalValueForTierRouting.sql
-- merged into one run, so the prices, the summary and the workflow value land in ONE transaction.
-- Those two headers carry the full reasoning; this one records what is specific to this run.
--
-- SOURCE: "69002123.xlsx", Sheet1, 513 unit rows. The sheet's trailing totals row is
-- dropped; the staged appraisal total is checked against it below. Customer data — do not commit.
-- Expected sums over the workbook rows:
--   ราคาประเมินที่เสนอขออนุมัติ (TotalAppraisalValueRounded) = 1408945000.00
--   มูลค่าประกันอัคคีภัย          (CoverageAmount)            = 397295250.00
--   ราคาบังคับขาย               (ForceSellingPrice)         = 986261500.00
--
-- COLUMN MAPPING (workbook -> database)
--   ห้องชุด            -> ProjectUnits.RoomNumber               match key 1
--   ห้องชุดเลขที่       -> ProjectUnits.CondoRegistrationNumber  match key 2 (fallback)
--   ชั้นที่ / อาคาร / แบบห้องชุด / พื้นที่ใช้สอย / ราคาขายโครงการ
--                     -> Floor / TowerName / ModelType / UsableArea / SellingPrice
--                        REPORTED as a diff, never written (Floor: see @FixFloor)
--   ราคา (บาท/ตร.ม.)   -> ProjectUnitPrices.StandardPrice
--   ราคาประเมินที่เสนอขออนุมัติ -> ProjectUnitPrices.TotalAppraisalValue AND TotalAppraisalValueRounded
--                        (stored verbatim: the condo calculator rounds per unit to the baht only,
--                        so a figure that is not a multiple of 1,000 is still app-shaped)
--   ราคาบังคับขาย (บาท) -> ProjectUnitPrices.ForceSellingPrice
--   มูลค่าประกันอัคคีภัย -> ProjectUnitPrices.CoverageAmount
--
-- MATCHING. A workbook row matches the unit whose RoomNumber equals ห้องชุด OR whose
-- CondoRegistrationNumber equals ห้องชุดเลขที่. The run aborts, writing nothing, if a row matches
-- two units (the keys point at different rooms) or a unit is claimed by two rows. A match made on
-- the registration number alone means the stored RoomNumber differs from the sheet — reported in
-- the attribute diff, not corrected.
--
-- FLOORS. Every floor in the sheet is a plain integer; nothing is re-interpreted.
--
-- THE WORKFLOW STEP (@PatchWorkflow = 1) does what AppraisalValueChangedIntegrationEventConsumer
-- would have done had the app written ValuationAnalyses itself:
--   * WorkflowInstances.Variables $.appraisalValue := the new AppraisedValue (a bare JSON number)
--   * MeetingQueueItems.AppraisalValue := the same, on every non-Released row of this appraisal
--     whose value differs
-- It never touches MeetingItems: nothing reads MeetingItems.AppraisalValue, and the consumer does
-- not refresh it either.
-- What it CANNOT do, and the report says which applies:
--   * An instance pinned to workflow version 1-3 routes on facilityLimit, not appraisalValue.
--     Migrating it (MigrateInstancesEndpoint) is a business decision.
--   * An instance that already went through approval-tier-switch on the old value and now sits
--     at pending-approval has its committee roster fixed. Only a committee route_back makes the
--     switch run again (it will then read this value).
--
-- ⚠ THE WAYS THESE NUMBERS DIE: pressing Calculate on the Unit Price tab, or re-uploading the units
-- Excel (ProjectUnitPrices cascade-delete). collateral.ProjectUnits.LastAppraisedValue is not synced.
--
-- Idempotent: re-running writes the same values again. @Apply = 0 touches nothing at all.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ── Inputs ────────────────────────────────────────────────────────────────────
DECLARE @AppraisalNumber nvarchar(50) = N'69002123';  -- fixed: the workbook below is this appraisal's
DECLARE @Apply           bit          = 0;     -- 0 = report only (writes nothing), 1 = write
DECLARE @FixFloor        tinyint      = 1;     -- ProjectUnits.Floor: 0 = never write,
                                               -- 1 = fill only where it is NULL, 2 = overwrite mismatches
DECLARE @SyncValuationSummary bit     = 1;     -- 1 = also rewrite appraisal.ValuationAnalyses
DECLARE @PatchWorkflow   bit          = 1;     -- 1 = also push appraisalValue into the workflow
                                               -- instance and live meeting-queue rows (needs the summary)
DECLARE @CreateMissingUnits bit       = 0;     -- 1 = INSERT a ProjectUnits row for a workbook room
                                               -- the project does not have (0 = abort instead)
DECLARE @MaxUnitsToCreate int         = 50;    -- refuse to create more than this many; see 50011
DECLARE @ForcedSaleFrom  tinyint      = 1;     -- ForcedSaleValue: 1 = appraised total x rate (what
                                               -- the app does), 2 = SUM of the per-unit column

-- Routing bands, from appraisal-workflow.json ('approval-tier-switch' cases and 'pending-approval'
-- memberSource.thresholds). Used for the printout only; the engine reads its own definition.
DECLARE @MeetingCutoff decimal(18,2) = 30000000.00;
DECLARE @SubMax        decimal(18,2) = 9999999.99;
DECLARE @CommMax       decimal(18,2) = 30000000.00;

PRINT CONCAT('Target appraisal number : ', @AppraisalNumber);
PRINT CONCAT('Mode                    : ', CASE WHEN @Apply = 1 THEN 'APPLY (writes)' ELSE 'REPORT ONLY (no writes)' END);
PRINT CONCAT('Missing units           : ', CASE WHEN @CreateMissingUnits = 1
                                               THEN CONCAT('create, up to ', @MaxUnitsToCreate)
                                               ELSE 'abort if any' END);
PRINT CONCAT('Valuation summary       : ', CASE WHEN @SyncValuationSummary = 1 THEN 'rewrite ValuationAnalyses' ELSE 'leave alone' END);
PRINT CONCAT('Workflow value          : ', CASE WHEN @PatchWorkflow = 1 THEN 'push appraisalValue + meeting queue' ELSE 'leave alone' END);
PRINT CONCAT('Forced-sale value from  : ', CASE @ForcedSaleFrom
                                               WHEN 1 THEN 'appraised total x rate (matches the app)'
                                               WHEN 2 THEN 'SUM of per-unit ForceSellingPrice'
                                               ELSE 'INVALID' END);
IF @ForcedSaleFrom NOT IN (1, 2)
    THROW 50010, 'Set @ForcedSaleFrom to 1 or 2.', 1;
PRINT CONCAT('Floor repair            : ', CASE @FixFloor WHEN 0 THEN 'off'
                                                          WHEN 1 THEN 'fill NULL floors only'
                                                          WHEN 2 THEN 'overwrite every mismatch'
                                                          ELSE 'INVALID' END);
IF @FixFloor NOT IN (0, 1, 2)
    THROW 50008, 'Set @FixFloor to 0, 1 or 2.', 1;
-- The workflow value IS the new ValuationAnalyses.AppraisedValue. Pushing it without storing it
-- would leave the committee routing on a figure the appraisal's own pages do not show.
IF @PatchWorkflow = 1 AND @SyncValuationSummary = 0
    THROW 50014, '@PatchWorkflow = 1 needs @SyncValuationSummary = 1: the workflow value is the new summary total.', 1;
PRINT '';

-- ── 1. Resolve the project ────────────────────────────────────────────────────
-- Walk PrevAppraisalId to the nearest ancestor that owns an appraisal.Projects row. The Path string
-- is a cycle guard. @ProjectAppraisalId is the appraisal RecomputeAsync would summarise, so it is
-- also the one whose ValuationAnalyses row, workflow instance and queue rows are patched.
DECLARE @ProjectId          uniqueidentifier;
DECLARE @ProjectAppraisalId uniqueidentifier;
DECLARE @ProjectType        nvarchar(2);
DECLARE @ProjectName        nvarchar(500);

;WITH Walk AS (
    SELECT a.Id AS AppraisalId, a.PrevAppraisalId, 0 AS Depth,
           CAST('|' + CAST(a.Id AS varchar(36)) + '|' AS varchar(max)) AS Path
    FROM appraisal.Appraisals a
    WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
    UNION ALL
    SELECT p.Id, p.PrevAppraisalId, w.Depth + 1,
           CAST(w.Path + CAST(p.Id AS varchar(36)) + '|' AS varchar(max))
    FROM Walk w
    JOIN appraisal.Appraisals p ON p.Id = w.PrevAppraisalId AND p.IsDeleted = 0
    WHERE CHARINDEX('|' + CAST(p.Id AS varchar(36)) + '|', w.Path) = 0
)
SELECT TOP (1) @ProjectId = pr.Id, @ProjectAppraisalId = pr.AppraisalId,
               @ProjectType = pr.ProjectType, @ProjectName = pr.ProjectName
FROM Walk w
JOIN appraisal.Projects pr ON pr.AppraisalId = w.AppraisalId
ORDER BY w.Depth, pr.Id
OPTION (MAXRECURSION 0);

IF @ProjectId IS NULL
    THROW 50002, 'No appraisal.Projects row is reachable from that appraisal number (walking PrevAppraisalId). Check the number, or that the appraisal is not soft-deleted.', 1;

-- ProjectType codes: U = Condo, LB = LandAndBuilding, L = Land. This workbook is a condo price list.
IF @ProjectType <> N'U'
    THROW 50003, 'That appraisal resolves to a project whose ProjectType is not U (Condo). This workbook is a condo price list; refusing to load it.', 1;

PRINT CONCAT('Resolved project        : ', @ProjectId, '  ', ISNULL(@ProjectName, N'(no name)'));
DECLARE @IsSameAppraisal bit =
    CASE WHEN EXISTS (SELECT 1 FROM appraisal.Appraisals a
                      WHERE a.Id = @ProjectAppraisalId AND a.AppraisalNumber = @AppraisalNumber)
         THEN 1 ELSE 0 END;
PRINT CONCAT('Summarised appraisal    : ', @ProjectAppraisalId,
             CASE @IsSameAppraisal WHEN 1 THEN '  (the number given)'
                                          ELSE '  (an ANCESTOR of the number given - its workflow is the one patched)' END);
PRINT '';

-- ── 2. Stage the workbook ─────────────────────────────────────────────────────
-- COLLATE DATABASE_DEFAULT is load-bearing: temp tables take tempdb's collation (Thai_CI_AS on the
-- bank's servers) and the joins below would not compile against the database's columns without it.
IF OBJECT_ID('tempdb..#Src')   IS NOT NULL DROP TABLE #Src;
IF OBJECT_ID('tempdb..#Match') IS NOT NULL DROP TABLE #Match;
IF OBJECT_ID('tempdb..#Post')  IS NOT NULL DROP TABLE #Post;

CREATE TABLE #Src
(
    RoomNumber              nvarchar(50)  COLLATE DATABASE_DEFAULT NOT NULL,   -- ห้องชุด
    CondoRegistrationNumber nvarchar(50)  COLLATE DATABASE_DEFAULT NOT NULL,   -- ห้องชุดเลขที่
    TowerName               nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    FloorText               nvarchar(10)  COLLATE DATABASE_DEFAULT NULL,   -- as printed: '07', '12A', ...
    Floor                   int                                    NULL,
    ModelType               nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    UsableArea              decimal(10,2)                          NULL,
    SellingPrice            decimal(18,2)                          NULL,
    PricePerSqm             decimal(18,2)                          NOT NULL,
    AppraisalValue          decimal(18,2)                          NOT NULL,
    ForceSellingPrice       decimal(18,2)                          NOT NULL,
    CoverageAmount          decimal(18,2)                          NOT NULL
);

INSERT #Src (RoomNumber, CondoRegistrationNumber, TowerName, FloorText, Floor, ModelType, UsableArea,
             SellingPrice, PricePerSqm, AppraisalValue, ForceSellingPrice, CoverageAmount)
VALUES
(N'001A12', N'129/1', N'A', N'1', 1, N'1 BEDROOM (1)', 25.02, 1932000.00, 77000.00, 1927000.00, 1348900.00, 625500.00),
(N'001A13', N'129/2', N'A', N'1', 1, N'1 BEDROOM (1)', 26.15, 1932000.00, 77000.00, 2014000.00, 1409800.00, 653750.00),
(N'001A14', N'129/3', N'A', N'1', 1, N'1 BEDROOM (2)', 29.07, 2677500.00, 82000.00, 2384000.00, 1668800.00, 726750.00),
(N'001A15', N'129/4', N'A', N'1', 1, N'1 BEDROOM (2)', 28.73, 2583000.00, 79000.00, 2270000.00, 1589000.00, 718250.00),
(N'001A16', N'129/5', N'A', N'1', 1, N'1 BEDROOM (2)', 29.21, 2572500.00, 79000.00, 2308000.00, 1615600.00, 730250.00),
(N'001A17', N'129/6', N'A', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 79000.00, 2275000.00, 1592500.00, 720000.00),
(N'001A18', N'129/7', N'A', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 79000.00, 2275000.00, 1592500.00, 720000.00),
(N'001A19', N'129/8', N'A', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 79000.00, 2275000.00, 1592500.00, 720000.00),
(N'001A20', N'129/9', N'A', N'1', 1, N'1 BEDROOM (2)', 28.80, 2530500.00, 79000.00, 2275000.00, 1592500.00, 720000.00),
(N'001A21', N'129/10', N'A', N'1', 1, N'1 BEDROOM (2)', 28.82, 2656500.00, 82000.00, 2363000.00, 1654100.00, 720500.00),
(N'002A01', N'129/11', N'A', N'2', 2, N'1 BEDROOM (2)', 29.27, 2698500.00, 83000.00, 2429000.00, 1700300.00, 731750.00),
(N'002A02', N'129/12', N'A', N'2', 2, N'1 BEDROOM (2)', 28.84, 2761500.00, 83000.00, 2394000.00, 1675800.00, 721000.00),
(N'002A03', N'129/14', N'A', N'2', 2, N'1 BEDROOM (2)', 28.61, 2761500.00, 83000.00, 2375000.00, 1662500.00, 715250.00),
(N'002A04', N'129/15', N'A', N'2', 2, N'1 BEDROOM (2)', 28.97, 2782500.00, 83000.00, 2405000.00, 1683500.00, 724250.00),
(N'002A05', N'129/16', N'A', N'2', 2, N'1 BEDROOM (1)', 25.97, 2394000.00, 84000.00, 2181000.00, 1526700.00, 649250.00),
(N'002A06', N'129/17', N'A', N'2', 2, N'1 BEDROOM (1)', 26.31, 2394000.00, 81000.00, 2131000.00, 1491700.00, 657750.00),
(N'002A07', N'129/18', N'A', N'2', 2, N'1 BEDROOM (1)', 25.60, 2320500.00, 78000.00, 1997000.00, 1397900.00, 640000.00),
(N'002A08', N'129/19', N'A', N'2', 2, N'1 BEDROOM (1)', 25.28, 2299500.00, 78000.00, 1972000.00, 1380400.00, 632000.00),
(N'002A09', N'129/20', N'A', N'2', 2, N'1 BEDROOM (1)', 25.58, 2383500.00, 78000.00, 1995000.00, 1396500.00, 639500.00),
(N'002A10', N'129/21', N'A', N'2', 2, N'1 BEDROOM (1)', 25.59, 2331000.00, 78000.00, 1996000.00, 1397200.00, 639750.00),
(N'002A11', N'129/22', N'A', N'2', 2, N'1 BEDROOM (1)', 25.58, 2289000.00, 78000.00, 1995000.00, 1396500.00, 639500.00),
(N'002A12', N'129/23', N'A', N'2', 2, N'1 BEDROOM (1)', 25.59, 2383500.00, 78000.00, 1996000.00, 1397200.00, 639750.00),
(N'002A13', N'129/24', N'A', N'2', 2, N'1 BEDROOM (1)', 26.15, 2373000.00, 78000.00, 2040000.00, 1428000.00, 653750.00),
(N'002A14', N'129/25', N'A', N'2', 2, N'1 BEDROOM (2)', 29.07, 2751000.00, 83000.00, 2413000.00, 1689100.00, 726750.00),
(N'002A15', N'129/26', N'A', N'2', 2, N'1 BEDROOM (2)', 28.73, 2604000.00, 80000.00, 2298000.00, 1608600.00, 718250.00),
(N'002A16', N'129/27', N'A', N'2', 2, N'1 BEDROOM (2)', 29.21, 2646000.00, 80000.00, 2337000.00, 1635900.00, 730250.00),
(N'002A17', N'129/28', N'A', N'2', 2, N'1 BEDROOM (2)', 28.80, 2614500.00, 80000.00, 2304000.00, 1612800.00, 720000.00),
(N'002A18', N'129/29', N'A', N'2', 2, N'1 BEDROOM (2)', 28.80, 2614500.00, 80000.00, 2304000.00, 1612800.00, 720000.00),
(N'002A19', N'129/30', N'A', N'2', 2, N'1 BEDROOM (2)', 28.80, 2614500.00, 80000.00, 2304000.00, 1612800.00, 720000.00),
(N'002A20', N'129/31', N'A', N'2', 2, N'1 BEDROOM (2)', 28.80, 2604000.00, 80000.00, 2304000.00, 1612800.00, 720000.00),
(N'002A21', N'129/32', N'A', N'2', 2, N'1 BEDROOM (2)', 28.82, 2719500.00, 83000.00, 2392000.00, 1674400.00, 720500.00),
(N'002A22', N'129/33', N'A', N'2', 2, N'2 BEDROOMS', 51.22, 4998000.00, 87000.00, 4456000.00, 3119200.00, 1280500.00),
(N'002A23', N'129/34', N'A', N'2', 2, N'2 BEDROOMS', 50.77, 4725000.00, 84000.00, 4265000.00, 2985500.00, 1269250.00),
(N'002A24', N'129/35', N'A', N'2', 2, N'1 BEDROOM (2)', 28.86, 2667000.00, 80000.00, 2309000.00, 1616300.00, 721500.00),
(N'002A25', N'129/36', N'A', N'2', 2, N'1 BEDROOM (2)', 29.39, 2688000.00, 80000.00, 2351000.00, 1645700.00, 734750.00),
(N'003A01', N'129/37', N'A', N'3', 3, N'1 BEDROOM (2)', 29.27, 2709000.00, 84000.00, 2459000.00, 1721300.00, 731750.00),
(N'003A02', N'129/38', N'A', N'3', 3, N'1 BEDROOM (2)', 28.84, 2843400.00, 84000.00, 2423000.00, 1696100.00, 721000.00),
(N'003A03', N'129/39', N'A', N'3', 3, N'1 BEDROOM (2)', 28.61, 2821350.00, 84000.00, 2403000.00, 1682100.00, 715250.00),
(N'003A04', N'129/40', N'A', N'3', 3, N'1 BEDROOM (2)', 28.97, 2916900.00, 84000.00, 2433000.00, 1703100.00, 724250.00),
(N'003A05', N'129/41', N'A', N'3', 3, N'1 BEDROOM (1)', 25.97, 2469600.00, 85000.00, 2207000.00, 1544900.00, 649250.00),
(N'003A06', N'129/42', N'A', N'3', 3, N'1 BEDROOM (1)', 26.31, 2415000.00, 82000.00, 2157000.00, 1509900.00, 657750.00),
(N'003A07', N'129/43', N'A', N'3', 3, N'1 BEDROOM (1)', 25.60, 2352000.00, 79000.00, 2022000.00, 1415400.00, 640000.00),
(N'003A08', N'129/44', N'A', N'3', 3, N'1 BEDROOM (1)', 25.28, 2310000.00, 79000.00, 1997000.00, 1397900.00, 632000.00),
(N'003A09', N'129/45', N'A', N'3', 3, N'1 BEDROOM (1)', 25.58, 2341500.00, 79000.00, 2021000.00, 1414700.00, 639500.00),
(N'003A10', N'129/46', N'A', N'3', 3, N'1 BEDROOM (1)', 25.59, 2341500.00, 79000.00, 2022000.00, 1415400.00, 639750.00),
(N'003A11', N'129/47', N'A', N'3', 3, N'1 BEDROOM (1)', 25.58, 2310000.00, 79000.00, 2021000.00, 1414700.00, 639500.00),
(N'003A12', N'129/48', N'A', N'3', 3, N'1 BEDROOM (1)', 25.59, 2341500.00, 79000.00, 2022000.00, 1415400.00, 639750.00),
(N'003A13', N'129/49', N'A', N'3', 3, N'1 BEDROOM (1)', 26.15, 2404500.00, 79000.00, 2066000.00, 1446200.00, 653750.00),
(N'003A14', N'129/50', N'A', N'3', 3, N'1 BEDROOM (2)', 29.07, 2761500.00, 86000.00, 2500000.00, 1750000.00, 726750.00),
(N'003A15', N'129/51', N'A', N'3', 3, N'1 BEDROOM (2)', 28.73, 2677500.00, 83000.00, 2385000.00, 1669500.00, 718250.00),
(N'003A16', N'129/52', N'A', N'3', 3, N'1 BEDROOM (2)', 29.21, 2667000.00, 83000.00, 2424000.00, 1696800.00, 730250.00),
(N'003A17', N'129/53', N'A', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 83000.00, 2390000.00, 1673000.00, 720000.00),
(N'003A18', N'129/54', N'A', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 83000.00, 2390000.00, 1673000.00, 720000.00),
(N'003A19', N'129/55', N'A', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 83000.00, 2390000.00, 1673000.00, 720000.00),
(N'003A20', N'129/56', N'A', N'3', 3, N'1 BEDROOM (2)', 28.80, 2625000.00, 83000.00, 2390000.00, 1673000.00, 720000.00),
(N'003A21', N'129/57', N'A', N'3', 3, N'1 BEDROOM (2)', 28.82, 2740500.00, 86000.00, 2479000.00, 1735300.00, 720500.00),
(N'003A22', N'129/58', N'A', N'3', 3, N'2 BEDROOMS', 51.22, 5222700.00, 88000.00, 4507000.00, 3154900.00, 1280500.00),
(N'003A23', N'129/59', N'A', N'3', 3, N'2 BEDROOMS', 50.77, 4767000.00, 85000.00, 4315000.00, 3020500.00, 1269250.00),
(N'003A24', N'129/60', N'A', N'3', 3, N'1 BEDROOM (2)', 28.86, 2740500.00, 81000.00, 2338000.00, 1636600.00, 721500.00),
(N'003A25', N'129/61', N'A', N'3', 3, N'1 BEDROOM (2)', 29.39, 2719500.00, 81000.00, 2381000.00, 1666700.00, 734750.00),
(N'004A01', N'129/62', N'A', N'4', 4, N'1 BEDROOM (2)', 29.27, 2793000.00, 85000.00, 2488000.00, 1741600.00, 731750.00),
(N'004A02', N'129/63', N'A', N'4', 4, N'1 BEDROOM (2)', 28.84, 2824500.00, 85000.00, 2451000.00, 1715700.00, 721000.00),
(N'004A03', N'129/64', N'A', N'4', 4, N'1 BEDROOM (2)', 28.61, 2824500.00, 85000.00, 2432000.00, 1702400.00, 715250.00),
(N'004A04', N'129/65', N'A', N'4', 4, N'1 BEDROOM (2)', 28.97, 2856000.00, 85000.00, 2462000.00, 1723400.00, 724250.00),
(N'004A05', N'129/66', N'A', N'4', 4, N'1 BEDROOM (1)', 25.97, 2457000.00, 86000.00, 2233000.00, 1563100.00, 649250.00),
(N'004A06', N'129/67', N'A', N'4', 4, N'1 BEDROOM (1)', 26.31, 2425500.00, 83000.00, 2184000.00, 1528800.00, 657750.00),
(N'004A07', N'129/68', N'A', N'4', 4, N'1 BEDROOM (1)', 25.60, 2352000.00, 80000.00, 2048000.00, 1433600.00, 640000.00),
(N'004A08', N'129/69', N'A', N'4', 4, N'1 BEDROOM (1)', 25.28, 2331000.00, 80000.00, 2022000.00, 1415400.00, 632000.00),
(N'004A09', N'129/70', N'A', N'4', 4, N'1 BEDROOM (1)', 25.58, 2362500.00, 80000.00, 2046000.00, 1432200.00, 639500.00),
(N'004A10', N'129/71', N'A', N'4', 4, N'1 BEDROOM (1)', 25.59, 2362500.00, 80000.00, 2047000.00, 1432900.00, 639750.00),
(N'004A11', N'129/72', N'A', N'4', 4, N'1 BEDROOM (1)', 25.58, 2320500.00, 80000.00, 2046000.00, 1432200.00, 639500.00),
(N'004A12', N'129/73', N'A', N'4', 4, N'1 BEDROOM (1)', 25.59, 2373000.00, 80000.00, 2047000.00, 1432900.00, 639750.00),
(N'004A13', N'129/74', N'A', N'4', 4, N'1 BEDROOM (1)', 26.15, 2415000.00, 80000.00, 2092000.00, 1464400.00, 653750.00),
(N'004A14', N'129/75', N'A', N'4', 4, N'1 BEDROOM (2)', 29.07, 2793000.00, 87000.00, 2529000.00, 1770300.00, 726750.00),
(N'004A15', N'129/76', N'A', N'4', 4, N'1 BEDROOM (2)', 28.73, 2698500.00, 84000.00, 2413000.00, 1689100.00, 718250.00),
(N'004A16', N'129/77', N'A', N'4', 4, N'1 BEDROOM (2)', 29.21, 2698500.00, 84000.00, 2454000.00, 1717800.00, 730250.00),
(N'004A17', N'129/78', N'A', N'4', 4, N'1 BEDROOM (2)', 28.80, 2709000.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'004A18', N'129/79', N'A', N'4', 4, N'1 BEDROOM (2)', 28.80, 2709000.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'004A19', N'129/80', N'A', N'4', 4, N'1 BEDROOM (2)', 28.80, 2709000.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'004A20', N'129/81', N'A', N'4', 4, N'1 BEDROOM (2)', 28.80, 2646000.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'004A21', N'129/82', N'A', N'4', 4, N'1 BEDROOM (2)', 28.82, 2761500.00, 87000.00, 2507000.00, 1754900.00, 720500.00),
(N'004A22', N'129/83', N'A', N'4', 4, N'2 BEDROOMS', 51.22, 5317200.00, 89000.00, 4559000.00, 3191300.00, 1280500.00),
(N'004A23', N'129/84', N'A', N'4', 4, N'2 BEDROOMS', 50.77, 4861500.00, 86000.00, 4366000.00, 3056200.00, 1269250.00),
(N'004A24', N'129/85', N'A', N'4', 4, N'1 BEDROOM (2)', 28.86, 2751000.00, 82000.00, 2367000.00, 1656900.00, 721500.00),
(N'004A25', N'129/86', N'A', N'4', 4, N'1 BEDROOM (2)', 29.39, 2772000.00, 82000.00, 2410000.00, 1687000.00, 734750.00),
(N'005A01', N'129/87', N'A', N'5', 5, N'1 BEDROOM (2)', 29.27, 2814000.00, 86000.00, 2517000.00, 1761900.00, 731750.00),
(N'005A02', N'129/88', N'A', N'5', 5, N'1 BEDROOM (2)', 28.84, 2937900.00, 86000.00, 2480000.00, 1736000.00, 721000.00),
(N'005A03', N'129/89', N'A', N'5', 5, N'1 BEDROOM (2)', 28.61, 2915850.00, 86000.00, 2460000.00, 1722000.00, 715250.00),
(N'005A04', N'129/90', N'A', N'5', 5, N'1 BEDROOM (2)', 28.97, 2845500.00, 86000.00, 2491000.00, 1743700.00, 724250.00),
(N'005A05', N'129/91', N'A', N'5', 5, N'1 BEDROOM (1)', 25.97, 2553600.00, 87000.00, 2259000.00, 1581300.00, 649250.00),
(N'005A06', N'129/92', N'A', N'5', 5, N'1 BEDROOM (1)', 26.31, 2457000.00, 84000.00, 2210000.00, 1547000.00, 657750.00),
(N'005A07', N'129/93', N'A', N'5', 5, N'1 BEDROOM (1)', 25.60, 2383500.00, 81000.00, 2074000.00, 1451800.00, 640000.00),
(N'005A08', N'129/94', N'A', N'5', 5, N'1 BEDROOM (1)', 25.28, 2352000.00, 81000.00, 2048000.00, 1433600.00, 632000.00),
(N'005A09', N'129/95', N'A', N'5', 5, N'1 BEDROOM (1)', 25.58, 2383500.00, 81000.00, 2072000.00, 1450400.00, 639500.00),
(N'005A10', N'129/96', N'A', N'5', 5, N'1 BEDROOM (1)', 25.59, 2383500.00, 81000.00, 2073000.00, 1451100.00, 639750.00),
(N'005A11', N'129/97', N'A', N'5', 5, N'1 BEDROOM (1)', 25.58, 2341500.00, 81000.00, 2072000.00, 1450400.00, 639500.00),
(N'005A12', N'129/98', N'A', N'5', 5, N'1 BEDROOM (1)', 25.59, 2373000.00, 81000.00, 2073000.00, 1451100.00, 639750.00),
(N'005A13', N'129/99', N'A', N'5', 5, N'1 BEDROOM (1)', 26.15, 2457000.00, 81000.00, 2118000.00, 1482600.00, 653750.00),
(N'005A14', N'129/100', N'A', N'5', 5, N'1 BEDROOM (2)', 29.07, 2814000.00, 88000.00, 2558000.00, 1790600.00, 726750.00),
(N'005A15', N'129/101', N'A', N'5', 5, N'1 BEDROOM (2)', 28.73, 2730000.00, 85000.00, 2442000.00, 1709400.00, 718250.00),
(N'005A16', N'129/102', N'A', N'5', 5, N'1 BEDROOM (2)', 29.21, 2709000.00, 85000.00, 2483000.00, 1738100.00, 730250.00),
(N'005A17', N'129/103', N'A', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'005A18', N'129/104', N'A', N'5', 5, N'1 BEDROOM (2)', 28.80, 2730000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'005A19', N'129/105', N'A', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'005A20', N'129/106', N'A', N'5', 5, N'1 BEDROOM (2)', 28.80, 2677500.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'005A21', N'129/107', N'A', N'5', 5, N'1 BEDROOM (2)', 28.82, 2803500.00, 88000.00, 2536000.00, 1775200.00, 720500.00),
(N'005A22', N'129/108', N'A', N'5', 5, N'2 BEDROOMS', 51.22, 5359200.00, 90000.00, 4610000.00, 3227000.00, 1280500.00),
(N'005A23', N'129/109', N'A', N'5', 5, N'2 BEDROOMS', 50.77, 4945500.00, 87000.00, 4417000.00, 3091900.00, 1269250.00),
(N'005A24', N'129/110', N'A', N'5', 5, N'1 BEDROOM (2)', 28.86, 2782500.00, 83000.00, 2395000.00, 1676500.00, 721500.00),
(N'005A25', N'129/111', N'A', N'5', 5, N'1 BEDROOM (2)', 29.39, 2803500.00, 83000.00, 2439000.00, 1707300.00, 734750.00),
(N'006A01', N'129/112', N'A', N'6', 6, N'1 BEDROOM (2)', 29.27, 2835000.00, 87000.00, 2546000.00, 1782200.00, 731750.00),
(N'006A02', N'129/113', N'A', N'6', 6, N'1 BEDROOM (2)', 28.84, 2908500.00, 87000.00, 2509000.00, 1756300.00, 721000.00),
(N'006A03', N'129/114', N'A', N'6', 6, N'1 BEDROOM (2)', 28.61, 2877000.00, 87000.00, 2489000.00, 1742300.00, 715250.00),
(N'006A04', N'129/115', N'A', N'6', 6, N'1 BEDROOM (2)', 28.97, 2919000.00, 87000.00, 2520000.00, 1764000.00, 724250.00),
(N'006A05', N'129/116', N'A', N'6', 6, N'1 BEDROOM (1)', 25.97, 2530500.00, 88000.00, 2285000.00, 1599500.00, 649250.00),
(N'006A06', N'129/117', N'A', N'6', 6, N'1 BEDROOM (1)', 26.31, 2467500.00, 85000.00, 2236000.00, 1565200.00, 657750.00),
(N'006A07', N'129/118', N'A', N'6', 6, N'1 BEDROOM (1)', 25.60, 2415000.00, 82000.00, 2099000.00, 1469300.00, 640000.00),
(N'006A08', N'129/119', N'A', N'6', 6, N'1 BEDROOM (1)', 25.28, 2373000.00, 82000.00, 2073000.00, 1451100.00, 632000.00),
(N'006A09', N'129/120', N'A', N'6', 6, N'1 BEDROOM (1)', 25.58, 2488500.00, 82000.00, 2098000.00, 1468600.00, 639500.00),
(N'006A10', N'129/121', N'A', N'6', 6, N'1 BEDROOM (1)', 25.59, 2404500.00, 82000.00, 2098000.00, 1468600.00, 639750.00),
(N'006A11', N'129/122', N'A', N'6', 6, N'1 BEDROOM (1)', 25.58, 2373000.00, 82000.00, 2098000.00, 1468600.00, 639500.00),
(N'006A12', N'129/123', N'A', N'6', 6, N'1 BEDROOM (1)', 25.59, 2415000.00, 82000.00, 2098000.00, 1468600.00, 639750.00),
(N'006A13', N'129/124', N'A', N'6', 6, N'1 BEDROOM (1)', 26.15, 2467500.00, 82000.00, 2144000.00, 1500800.00, 653750.00),
(N'006A14', N'129/125', N'A', N'6', 6, N'1 BEDROOM (2)', 29.07, 2835000.00, 89000.00, 2587000.00, 1810900.00, 726750.00),
(N'006A15', N'129/126', N'A', N'6', 6, N'1 BEDROOM (2)', 28.73, 2814000.00, 86000.00, 2471000.00, 1729700.00, 718250.00),
(N'006A16', N'129/127', N'A', N'6', 6, N'1 BEDROOM (2)', 29.21, 2740500.00, 86000.00, 2512000.00, 1758400.00, 730250.00),
(N'006A17', N'129/128', N'A', N'6', 6, N'1 BEDROOM (2)', 28.80, 2772000.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'006A18', N'129/129', N'A', N'6', 6, N'1 BEDROOM (2)', 28.80, 2772000.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'006A19', N'129/130', N'A', N'6', 6, N'1 BEDROOM (2)', 28.80, 2772000.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'006A20', N'129/131', N'A', N'6', 6, N'1 BEDROOM (2)', 28.80, 2709000.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'006A21', N'129/132', N'A', N'6', 6, N'1 BEDROOM (2)', 28.82, 2824500.00, 89000.00, 2565000.00, 1795500.00, 720500.00),
(N'006A22', N'129/133', N'A', N'6', 6, N'2 BEDROOMS', 51.22, 5250000.00, 91000.00, 4661000.00, 3262700.00, 1280500.00),
(N'006A23', N'129/134', N'A', N'6', 6, N'2 BEDROOMS', 50.77, 4966500.00, 88000.00, 4468000.00, 3127600.00, 1269250.00),
(N'006A24', N'129/135', N'A', N'6', 6, N'1 BEDROOM (2)', 28.86, 2814000.00, 84000.00, 2424000.00, 1696800.00, 721500.00),
(N'006A25', N'129/136', N'A', N'6', 6, N'1 BEDROOM (2)', 29.39, 2835000.00, 84000.00, 2469000.00, 1728300.00, 734750.00),
(N'007A01', N'129/137', N'A', N'7', 7, N'1 BEDROOM (2)', 29.27, 2877000.00, 88000.00, 2576000.00, 1803200.00, 731750.00),
(N'007A02', N'129/138', N'A', N'7', 7, N'1 BEDROOM (2)', 28.84, 2990400.00, 88000.00, 2538000.00, 1776600.00, 721000.00),
(N'007A03', N'129/139', N'A', N'7', 7, N'1 BEDROOM (2)', 28.61, 2968350.00, 88000.00, 2518000.00, 1762600.00, 715250.00),
(N'007A04', N'129/140', N'A', N'7', 7, N'1 BEDROOM (2)', 28.97, 2990400.00, 88000.00, 2549000.00, 1784300.00, 724250.00),
(N'007A05', N'129/141', N'A', N'7', 7, N'1 BEDROOM (1)', 25.97, 2606100.00, 89000.00, 2311000.00, 1617700.00, 649250.00),
(N'007A06', N'129/142', N'A', N'7', 7, N'1 BEDROOM (1)', 26.31, 2520000.00, 86000.00, 2263000.00, 1584100.00, 657750.00),
(N'007A07', N'129/143', N'A', N'7', 7, N'1 BEDROOM (1)', 25.60, 2446500.00, 83000.00, 2125000.00, 1487500.00, 640000.00),
(N'007A08', N'129/144', N'A', N'7', 7, N'1 BEDROOM (1)', 25.28, 2415000.00, 83000.00, 2098000.00, 1468600.00, 632000.00),
(N'007A09', N'129/145', N'A', N'7', 7, N'1 BEDROOM (1)', 25.58, 2446500.00, 83000.00, 2123000.00, 1486100.00, 639500.00),
(N'007A10', N'129/146', N'A', N'7', 7, N'1 BEDROOM (1)', 25.59, 2446500.00, 83000.00, 2124000.00, 1486800.00, 639750.00),
(N'007A11', N'129/147', N'A', N'7', 7, N'1 BEDROOM (1)', 25.58, 2404500.00, 83000.00, 2123000.00, 1486100.00, 639500.00),
(N'007A12', N'129/148', N'A', N'7', 7, N'1 BEDROOM (1)', 25.59, 2446500.00, 83000.00, 2124000.00, 1486800.00, 639750.00),
(N'007A13', N'129/149', N'A', N'7', 7, N'1 BEDROOM (1)', 26.15, 2509500.00, 83000.00, 2170000.00, 1519000.00, 653750.00),
(N'007A14', N'129/150', N'A', N'7', 7, N'1 BEDROOM (2)', 29.07, 2866500.00, 90000.00, 2616000.00, 1831200.00, 726750.00),
(N'007A15', N'129/151', N'A', N'7', 7, N'1 BEDROOM (2)', 28.73, 2793000.00, 87000.00, 2500000.00, 1750000.00, 718250.00),
(N'007A16', N'129/152', N'A', N'7', 7, N'1 BEDROOM (2)', 29.21, 2782500.00, 87000.00, 2541000.00, 1778700.00, 730250.00),
(N'007A17', N'129/153', N'A', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'007A18', N'129/154', N'A', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'007A19', N'129/155', N'A', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'007A20', N'129/156', N'A', N'7', 7, N'1 BEDROOM (2)', 28.80, 2740500.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'007A21', N'129/157', N'A', N'7', 7, N'1 BEDROOM (2)', 28.82, 2856000.00, 90000.00, 2594000.00, 1815800.00, 720500.00),
(N'007A22', N'129/158', N'A', N'7', 7, N'2 BEDROOMS', 51.22, 5474700.00, 92000.00, 4712000.00, 3298400.00, 1280500.00),
(N'007A23', N'129/159', N'A', N'7', 7, N'2 BEDROOMS', 50.77, 5019000.00, 89000.00, 4519000.00, 3163300.00, 1269250.00),
(N'007A24', N'129/160', N'A', N'7', 7, N'1 BEDROOM (2)', 28.86, 2824500.00, 85000.00, 2453000.00, 1717100.00, 721500.00),
(N'007A25', N'129/161', N'A', N'7', 7, N'1 BEDROOM (2)', 29.39, 2866500.00, 85000.00, 2498000.00, 1748600.00, 734750.00),
(N'008A01', N'129/162', N'A', N'8', 8, N'1 BEDROOM (2)', 29.27, 2866500.00, 89000.00, 2605000.00, 1823500.00, 731750.00),
(N'008A02', N'129/163', N'A', N'8', 8, N'1 BEDROOM (2)', 28.85, 2990400.00, 89000.00, 2568000.00, 1797600.00, 721250.00),
(N'008A03', N'129/164', N'A', N'8', 8, N'1 BEDROOM (2)', 28.61, 2908500.00, 89000.00, 2546000.00, 1782200.00, 715250.00),
(N'008A04', N'129/165', N'A', N'8', 8, N'1 BEDROOM (2)', 28.98, 2940000.00, 89000.00, 2579000.00, 1805300.00, 724500.00),
(N'008A05', N'129/166', N'A', N'8', 8, N'1 BEDROOM (1)', 25.97, 2551500.00, 90000.00, 2337000.00, 1635900.00, 649250.00),
(N'008A06', N'129/167', N'A', N'8', 8, N'1 BEDROOM (1)', 26.32, 2509500.00, 87000.00, 2290000.00, 1603000.00, 658000.00),
(N'008A07', N'129/168', N'A', N'8', 8, N'1 BEDROOM (1)', 25.61, 2436000.00, 84000.00, 2151000.00, 1505700.00, 640250.00),
(N'008A08', N'129/169', N'A', N'8', 8, N'1 BEDROOM (1)', 25.29, 2404500.00, 84000.00, 2124000.00, 1486800.00, 632250.00),
(N'008A09', N'129/170', N'A', N'8', 8, N'1 BEDROOM (1)', 25.58, 2436000.00, 84000.00, 2149000.00, 1504300.00, 639500.00),
(N'008A10', N'129/171', N'A', N'8', 8, N'1 BEDROOM (1)', 25.60, 2436000.00, 84000.00, 2150000.00, 1505000.00, 640000.00),
(N'008A11', N'129/172', N'A', N'8', 8, N'1 BEDROOM (1)', 25.58, 2457000.00, 84000.00, 2149000.00, 1504300.00, 639500.00),
(N'008A12', N'129/173', N'A', N'8', 8, N'1 BEDROOM (1)', 25.60, 2446500.00, 84000.00, 2150000.00, 1505000.00, 640000.00),
(N'008A13', N'129/174', N'A', N'8', 8, N'1 BEDROOM (1)', 26.15, 2509500.00, 84000.00, 2197000.00, 1537900.00, 653750.00),
(N'008A14', N'129/175', N'A', N'8', 8, N'1 BEDROOM (2)', 29.08, 2929500.00, 91000.00, 2646000.00, 1852200.00, 727000.00),
(N'008A15', N'129/176', N'A', N'8', 8, N'1 BEDROOM (2)', 28.73, 2793000.00, 88000.00, 2528000.00, 1769600.00, 718250.00),
(N'008A16', N'129/177', N'A', N'8', 8, N'1 BEDROOM (2)', 29.22, 2782500.00, 88000.00, 2571000.00, 1799700.00, 730500.00),
(N'008A17', N'129/178', N'A', N'8', 8, N'1 BEDROOM (2)', 28.80, 2793000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'008A18', N'129/179', N'A', N'8', 8, N'1 BEDROOM (2)', 28.81, 2793000.00, 88000.00, 2535000.00, 1774500.00, 720250.00),
(N'008A19', N'129/180', N'A', N'8', 8, N'1 BEDROOM (2)', 28.80, 2793000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'008A20', N'129/181', N'A', N'8', 8, N'1 BEDROOM (2)', 28.81, 2740500.00, 88000.00, 2535000.00, 1774500.00, 720250.00),
(N'008A21', N'129/182', N'A', N'8', 8, N'1 BEDROOM (2)', 28.82, 2908500.00, 91000.00, 2623000.00, 1836100.00, 720500.00),
(N'008A22', N'129/183', N'A', N'8', 8, N'2 BEDROOMS', 51.22, 5313000.00, 93000.00, 4763000.00, 3334100.00, 1280500.00),
(N'008A23', N'129/184', N'A', N'8', 8, N'2 BEDROOMS', 50.77, 5050500.00, 90000.00, 4569000.00, 3198300.00, 1269250.00),
(N'008A24', N'129/185', N'A', N'8', 8, N'1 BEDROOM (2)', 28.87, 2835000.00, 86000.00, 2483000.00, 1738100.00, 721750.00),
(N'008A25', N'129/186', N'A', N'8', 8, N'1 BEDROOM (2)', 29.39, 2877000.00, 86000.00, 2528000.00, 1769600.00, 734750.00),
(N'001B01', N'129/187', N'B', N'1', 1, N'1 BEDROOM (2)', 29.59, 2667000.00, 84000.00, 2486000.00, 1740200.00, 739750.00),
(N'001B02', N'129/188', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B03', N'129/189', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B04', N'129/190', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B05', N'129/191', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2530500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B06', N'129/192', N'B', N'1', 1, N'1 BEDROOM (2)', 28.84, 2656500.00, 87000.00, 2509000.00, 1756300.00, 721000.00),
(N'001B18', N'129/193', N'B', N'1', 1, N'1 BEDROOM (2)', 28.84, 2656500.00, 87000.00, 2509000.00, 1756300.00, 721000.00),
(N'001B19', N'129/194', N'B', N'1', 1, N'1 BEDROOM (2)', 28.53, 2572500.00, 84000.00, 2397000.00, 1677900.00, 713250.00),
(N'001B20', N'129/195', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B21', N'129/196', N'B', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 84000.00, 2419000.00, 1693300.00, 720000.00),
(N'001B22', N'129/197', N'B', N'1', 1, N'1 BEDROOM (2)', 29.59, 2646000.00, 84000.00, 2486000.00, 1740200.00, 739750.00),
(N'002B01', N'129/198', N'B', N'2', 2, N'1 BEDROOM (2)', 29.59, 2740500.00, 85000.00, 2515000.00, 1760500.00, 739750.00),
(N'002B02', N'129/199', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2667000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B03', N'129/200', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2667000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B04', N'129/201', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2667000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B05', N'129/202', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2604000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B06', N'129/203', N'B', N'2', 2, N'1 BEDROOM (2)', 28.84, 2719500.00, 88000.00, 2538000.00, 1776600.00, 721000.00),
(N'002B07', N'129/204', N'B', N'2', 2, N'2 BEDROOMS', 51.16, 5008500.00, 98000.00, 5014000.00, 3509800.00, 1279000.00),
(N'002B08', N'129/205', N'B', N'2', 2, N'1 BEDROOM (2)', 29.11, 2730000.00, 89000.00, 2591000.00, 1813700.00, 727750.00),
(N'002B09', N'129/206', N'B', N'2', 2, N'1 BEDROOM (2)', 28.71, 2950500.00, 89000.00, 2555000.00, 1788500.00, 717750.00),
(N'002B10', N'129/207', N'B', N'2', 2, N'1 BEDROOM (2)', 28.71, 2940000.00, 89000.00, 2555000.00, 1788500.00, 717750.00),
(N'002B11', N'129/208', N'B', N'2', 2, N'1 BEDROOM (2)', 28.69, 2929500.00, 89000.00, 2553000.00, 1787100.00, 717250.00),
(N'002B12', N'129/209', N'B', N'2', 2, N'2 BEDROOMS', 51.33, 5680500.00, 98000.00, 5030000.00, 3521000.00, 1283250.00),
(N'002B13', N'129/210', N'B', N'2', 2, N'2 BEDROOMS', 51.33, 5659500.00, 98000.00, 5030000.00, 3521000.00, 1283250.00),
(N'002B14', N'129/211', N'B', N'2', 2, N'1 BEDROOM (2)', 28.69, 2940000.00, 89000.00, 2553000.00, 1787100.00, 717250.00),
(N'002B15', N'129/212', N'B', N'2', 2, N'1 BEDROOM (2)', 28.71, 2940000.00, 89000.00, 2555000.00, 1788500.00, 717750.00),
(N'002B16', N'129/213', N'B', N'2', 2, N'1 BEDROOM (2)', 29.35, 3013500.00, 89000.00, 2612000.00, 1828400.00, 733750.00),
(N'002B17', N'129/214', N'B', N'2', 2, N'2 BEDROOMS', 51.16, 5449500.00, 98000.00, 5014000.00, 3509800.00, 1279000.00),
(N'002B18', N'129/215', N'B', N'2', 2, N'1 BEDROOM (2)', 28.84, 2719500.00, 88000.00, 2538000.00, 1776600.00, 721000.00),
(N'002B19', N'129/216', N'B', N'2', 2, N'1 BEDROOM (2)', 28.53, 2635500.00, 85000.00, 2425000.00, 1697500.00, 713250.00),
(N'002B20', N'129/217', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2667000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B21', N'129/218', N'B', N'2', 2, N'1 BEDROOM (2)', 28.80, 2667000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'002B22', N'129/219', N'B', N'2', 2, N'1 BEDROOM (2)', 29.59, 2740500.00, 85000.00, 2515000.00, 1760500.00, 739750.00),
(N'003B01', N'129/220', N'B', N'3', 3, N'1 BEDROOM (2)', 29.59, 2814000.00, 88000.00, 2604000.00, 1822800.00, 739750.00),
(N'003B02', N'129/221', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2667000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B03', N'129/222', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2677500.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B04', N'129/223', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B05', N'129/224', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2625000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B06', N'129/225', N'B', N'3', 3, N'1 BEDROOM (2)', 28.84, 2740500.00, 91000.00, 2624000.00, 1836800.00, 721000.00),
(N'003B07', N'129/226', N'B', N'3', 3, N'2 BEDROOMS', 51.16, 5040000.00, 99000.00, 5065000.00, 3545500.00, 1279000.00),
(N'003B08', N'129/227', N'B', N'3', 3, N'1 BEDROOM (2)', 29.11, 2786700.00, 90000.00, 2620000.00, 1834000.00, 727750.00),
(N'003B09', N'129/228', N'B', N'3', 3, N'1 BEDROOM (2)', 28.71, 3006150.00, 90000.00, 2584000.00, 1808800.00, 717750.00),
(N'003B10', N'129/229', N'B', N'3', 3, N'1 BEDROOM (2)', 28.71, 3006150.00, 90000.00, 2584000.00, 1808800.00, 717750.00),
(N'003B11', N'129/230', N'B', N'3', 3, N'1 BEDROOM (2)', 28.69, 2985150.00, 90000.00, 2582000.00, 1807400.00, 717250.00),
(N'003B12', N'129/231', N'B', N'3', 3, N'2 BEDROOMS', 51.33, 5701500.00, 99000.00, 5082000.00, 3557400.00, 1283250.00),
(N'003B13', N'129/232', N'B', N'3', 3, N'2 BEDROOMS', 51.33, 5701500.00, 99000.00, 5082000.00, 3557400.00, 1283250.00),
(N'003B14', N'129/233', N'B', N'3', 3, N'1 BEDROOM (2)', 28.69, 2995650.00, 90000.00, 2582000.00, 1807400.00, 717250.00),
(N'003B15', N'129/234', N'B', N'3', 3, N'1 BEDROOM (2)', 28.71, 3016650.00, 90000.00, 2584000.00, 1808800.00, 717750.00),
(N'003B16', N'129/235', N'B', N'3', 3, N'1 BEDROOM (2)', 29.35, 3070200.00, 90000.00, 2642000.00, 1849400.00, 733750.00),
(N'003B17', N'129/236', N'B', N'3', 3, N'2 BEDROOMS', 51.16, 5544000.00, 99000.00, 5065000.00, 3545500.00, 1279000.00),
(N'003B18', N'129/237', N'B', N'3', 3, N'1 BEDROOM (2)', 28.84, 2740500.00, 91000.00, 2624000.00, 1836800.00, 721000.00),
(N'003B19', N'129/238', N'B', N'3', 3, N'1 BEDROOM (2)', 28.53, 2656500.00, 88000.00, 2511000.00, 1757700.00, 713250.00),
(N'003B20', N'129/239', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B21', N'129/240', N'B', N'3', 3, N'1 BEDROOM (2)', 28.80, 2688000.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'003B22', N'129/241', N'B', N'3', 3, N'1 BEDROOM (2)', 29.59, 2761500.00, 88000.00, 2604000.00, 1822800.00, 739750.00),
(N'004B01', N'129/242', N'B', N'4', 4, N'1 BEDROOM (2)', 29.59, 2782500.00, 89000.00, 2634000.00, 1843800.00, 739750.00),
(N'004B02', N'129/243', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2698500.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B03', N'129/244', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2688000.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B04', N'129/245', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2698500.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B05', N'129/246', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2635500.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B06', N'129/247', N'B', N'4', 4, N'1 BEDROOM (2)', 28.84, 2751000.00, 92000.00, 2653000.00, 1857100.00, 721000.00),
(N'004B07', N'129/248', N'B', N'4', 4, N'2 BEDROOMS', 51.16, 5575500.00, 100000.00, 5116000.00, 3581200.00, 1279000.00),
(N'004B08', N'129/249', N'B', N'4', 4, N'1 BEDROOM (2)', 29.11, 3045000.00, 91000.00, 2649000.00, 1854300.00, 727750.00),
(N'004B09', N'129/250', N'B', N'4', 4, N'1 BEDROOM (2)', 28.71, 3013500.00, 91000.00, 2613000.00, 1829100.00, 717750.00),
(N'004B10', N'129/251', N'B', N'4', 4, N'1 BEDROOM (2)', 28.71, 3013500.00, 91000.00, 2613000.00, 1829100.00, 717750.00),
(N'004B11', N'129/252', N'B', N'4', 4, N'1 BEDROOM (2)', 28.69, 3003000.00, 91000.00, 2611000.00, 1827700.00, 717250.00),
(N'004B12', N'129/253', N'B', N'4', 4, N'2 BEDROOMS', 51.33, 5796000.00, 100000.00, 5133000.00, 3593100.00, 1283250.00),
(N'004B13', N'129/254', N'B', N'4', 4, N'2 BEDROOMS', 51.33, 5796000.00, 100000.00, 5133000.00, 3593100.00, 1283250.00),
(N'004B14', N'129/255', N'B', N'4', 4, N'1 BEDROOM (2)', 28.69, 2992500.00, 91000.00, 2611000.00, 1827700.00, 717250.00),
(N'004B15', N'129/256', N'B', N'4', 4, N'1 BEDROOM (2)', 28.71, 3181500.00, 91000.00, 2613000.00, 1829100.00, 717750.00),
(N'004B16', N'129/257', N'B', N'4', 4, N'1 BEDROOM (2)', 29.35, 3097500.00, 91000.00, 2671000.00, 1869700.00, 733750.00),
(N'004B17', N'129/258', N'B', N'4', 4, N'2 BEDROOMS', 51.16, 5683650.00, 100000.00, 5116000.00, 3581200.00, 1279000.00),
(N'004B18', N'129/259', N'B', N'4', 4, N'1 BEDROOM (2)', 28.84, 2761500.00, 92000.00, 2653000.00, 1857100.00, 721000.00),
(N'004B19', N'129/260', N'B', N'4', 4, N'1 BEDROOM (2)', 28.53, 2688000.00, 89000.00, 2539000.00, 1777300.00, 713250.00),
(N'004B20', N'129/261', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2709000.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B21', N'129/262', N'B', N'4', 4, N'1 BEDROOM (2)', 28.80, 2698500.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'004B22', N'129/263', N'B', N'4', 4, N'1 BEDROOM (2)', 29.59, 2761500.00, 89000.00, 2634000.00, 1843800.00, 739750.00),
(N'005B01', N'129/264', N'B', N'5', 5, N'1 BEDROOM (2)', 29.59, 2814000.00, 90000.00, 2663000.00, 1864100.00, 739750.00),
(N'005B02', N'129/265', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B03', N'129/266', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B04', N'129/267', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B05', N'129/268', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2677500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B06', N'129/269', N'B', N'5', 5, N'1 BEDROOM (2)', 28.84, 2803500.00, 93000.00, 2682000.00, 1877400.00, 721000.00),
(N'005B07', N'129/270', N'B', N'5', 5, N'2 BEDROOMS', 51.16, 5853750.00, 101000.00, 5167000.00, 3616900.00, 1279000.00),
(N'005B08', N'129/271', N'B', N'5', 5, N'1 BEDROOM (2)', 29.11, 3183600.00, 92000.00, 2678000.00, 1874600.00, 727750.00),
(N'005B09', N'129/272', N'B', N'5', 5, N'1 BEDROOM (2)', 28.71, 3150000.00, 92000.00, 2641000.00, 1848700.00, 717750.00),
(N'005B10', N'129/273', N'B', N'5', 5, N'1 BEDROOM (2)', 28.71, 3150000.00, 92000.00, 2641000.00, 1848700.00, 717750.00),
(N'005B11', N'129/274', N'B', N'5', 5, N'1 BEDROOM (2)', 28.69, 3202500.00, 92000.00, 2639000.00, 1847300.00, 717250.00),
(N'005B12', N'129/275', N'B', N'5', 5, N'2 BEDROOMS', 51.33, 6074250.00, 101000.00, 5184000.00, 3628800.00, 1283250.00),
(N'005B13', N'129/276', N'B', N'5', 5, N'2 BEDROOMS', 51.33, 6084750.00, 101000.00, 5184000.00, 3628800.00, 1283250.00),
(N'005B14', N'129/277', N'B', N'5', 5, N'1 BEDROOM (2)', 28.69, 3139500.00, 92000.00, 2639000.00, 1847300.00, 717250.00),
(N'005B15', N'129/278', N'B', N'5', 5, N'1 BEDROOM (2)', 28.71, 3160500.00, 92000.00, 2641000.00, 1848700.00, 717750.00),
(N'005B16', N'129/279', N'B', N'5', 5, N'1 BEDROOM (2)', 29.35, 3226650.00, 92000.00, 2700000.00, 1890000.00, 733750.00),
(N'005B17', N'129/280', N'B', N'5', 5, N'2 BEDROOMS', 51.16, 5874750.00, 101000.00, 5167000.00, 3616900.00, 1279000.00),
(N'005B18', N'129/281', N'B', N'5', 5, N'1 BEDROOM (2)', 28.84, 2803500.00, 93000.00, 2682000.00, 1877400.00, 721000.00),
(N'005B19', N'129/282', N'B', N'5', 5, N'1 BEDROOM (2)', 28.53, 2719500.00, 90000.00, 2568000.00, 1797600.00, 713250.00),
(N'005B20', N'129/283', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2793000.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B21', N'129/284', N'B', N'5', 5, N'1 BEDROOM (2)', 28.80, 2740500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'005B22', N'129/285', N'B', N'5', 5, N'1 BEDROOM (2)', 29.59, 2814000.00, 90000.00, 2663000.00, 1864100.00, 739750.00),
(N'006B01', N'129/286', N'B', N'6', 6, N'1 BEDROOM (2)', 29.59, 2898000.00, 91000.00, 2693000.00, 1885100.00, 739750.00),
(N'006B02', N'129/287', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2824500.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B03', N'129/288', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2761500.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B04', N'129/289', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2761500.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B05', N'129/290', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2698500.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B06', N'129/291', N'B', N'6', 6, N'1 BEDROOM (2)', 28.84, 2814000.00, 94000.00, 2711000.00, 1897700.00, 721000.00),
(N'006B07', N'129/292', N'B', N'6', 6, N'2 BEDROOMS', 51.16, 5691000.00, 102000.00, 5218000.00, 3652600.00, 1279000.00),
(N'006B08', N'129/293', N'B', N'6', 6, N'1 BEDROOM (2)', 29.11, 3118500.00, 93000.00, 2707000.00, 1894900.00, 727750.00),
(N'006B09', N'129/294', N'B', N'6', 6, N'1 BEDROOM (2)', 28.71, 3066000.00, 93000.00, 2670000.00, 1869000.00, 717750.00),
(N'006B10', N'129/295', N'B', N'6', 6, N'1 BEDROOM (2)', 28.71, 3076500.00, 93000.00, 2670000.00, 1869000.00, 717750.00),
(N'006B11', N'129/296', N'B', N'6', 6, N'1 BEDROOM (2)', 28.69, 3066000.00, 93000.00, 2668000.00, 1867600.00, 717250.00),
(N'006B12', N'129/297', N'B', N'6', 6, N'2 BEDROOMS', 51.33, 5911500.00, 102000.00, 5236000.00, 3665200.00, 1283250.00),
(N'006B13', N'129/298', N'B', N'6', 6, N'2 BEDROOMS', 51.33, 5911500.00, 102000.00, 5236000.00, 3665200.00, 1283250.00),
(N'006B14', N'129/299', N'B', N'6', 6, N'1 BEDROOM (2)', 28.69, 3066000.00, 93000.00, 2668000.00, 1867600.00, 717250.00),
(N'006B15', N'129/300', N'B', N'6', 6, N'1 BEDROOM (2)', 28.71, 3066000.00, 93000.00, 2670000.00, 1869000.00, 717750.00),
(N'006B16', N'129/301', N'B', N'6', 6, N'1 BEDROOM (2)', 29.35, 3150000.00, 93000.00, 2730000.00, 1911000.00, 733750.00),
(N'006B17', N'129/302', N'B', N'6', 6, N'2 BEDROOMS', 51.16, 5691000.00, 102000.00, 5218000.00, 3652600.00, 1279000.00),
(N'006B18', N'129/303', N'B', N'6', 6, N'1 BEDROOM (2)', 28.84, 2814000.00, 94000.00, 2711000.00, 1897700.00, 721000.00),
(N'006B19', N'129/304', N'B', N'6', 6, N'1 BEDROOM (2)', 28.53, 2730000.00, 91000.00, 2596000.00, 1817200.00, 713250.00),
(N'006B20', N'129/305', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2772000.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B21', N'129/306', N'B', N'6', 6, N'1 BEDROOM (2)', 28.80, 2761500.00, 91000.00, 2621000.00, 1834700.00, 720000.00),
(N'006B22', N'129/307', N'B', N'6', 6, N'1 BEDROOM (2)', 29.59, 2835000.00, 91000.00, 2693000.00, 1885100.00, 739750.00),
(N'007B01', N'129/308', N'B', N'7', 7, N'1 BEDROOM (2)', 29.59, 2877000.00, 92000.00, 2722000.00, 1905400.00, 739750.00),
(N'007B02', N'129/309', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B03', N'129/310', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B04', N'129/311', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B05', N'129/312', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2740500.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B06', N'129/313', N'B', N'7', 7, N'1 BEDROOM (2)', 28.84, 2856000.00, 95000.00, 2740000.00, 1918000.00, 721000.00),
(N'007B07', N'129/314', N'B', N'7', 7, N'2 BEDROOMS', 51.16, 5898900.00, 103000.00, 5269000.00, 3688300.00, 1279000.00),
(N'007B08', N'129/315', N'B', N'7', 7, N'1 BEDROOM (2)', 29.11, 3216150.00, 94000.00, 2736000.00, 1915200.00, 727750.00),
(N'007B09', N'129/316', N'B', N'7', 7, N'1 BEDROOM (2)', 28.71, 3291750.00, 94000.00, 2699000.00, 1889300.00, 717750.00),
(N'007B10', N'129/317', N'B', N'7', 7, N'1 BEDROOM (2)', 28.71, 3183337.50, 94000.00, 2699000.00, 1889300.00, 717750.00),
(N'007B11', N'129/318', N'B', N'7', 7, N'1 BEDROOM (2)', 28.69, 3183600.00, 94000.00, 2697000.00, 1887900.00, 717250.00),
(N'007B12', N'129/319', N'B', N'7', 7, N'2 BEDROOMS', 51.33, 6087900.00, 103000.00, 5287000.00, 3700900.00, 1283250.00),
(N'007B13', N'129/320', N'B', N'7', 7, N'2 BEDROOMS', 51.33, 6087900.00, 103000.00, 5287000.00, 3700900.00, 1283250.00),
(N'007B14', N'129/321', N'B', N'7', 7, N'1 BEDROOM (2)', 28.69, 3173100.00, 94000.00, 2697000.00, 1887900.00, 717250.00),
(N'007B15', N'129/322', N'B', N'7', 7, N'1 BEDROOM (2)', 28.71, 3183600.00, 94000.00, 2699000.00, 1889300.00, 717750.00),
(N'007B16', N'129/323', N'B', N'7', 7, N'1 BEDROOM (2)', 29.35, 3247650.00, 94000.00, 2759000.00, 1931300.00, 733750.00),
(N'007B17', N'129/324', N'B', N'7', 7, N'2 BEDROOMS', 51.16, 5877900.00, 103000.00, 5269000.00, 3688300.00, 1279000.00),
(N'007B18', N'129/325', N'B', N'7', 7, N'1 BEDROOM (2)', 28.84, 2856000.00, 95000.00, 2740000.00, 1918000.00, 721000.00),
(N'007B19', N'129/326', N'B', N'7', 7, N'1 BEDROOM (2)', 28.53, 2772000.00, 92000.00, 2625000.00, 1837500.00, 713250.00),
(N'007B20', N'129/327', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B21', N'129/328', N'B', N'7', 7, N'1 BEDROOM (2)', 28.80, 2793000.00, 92000.00, 2650000.00, 1855000.00, 720000.00),
(N'007B22', N'129/329', N'B', N'7', 7, N'1 BEDROOM (2)', 29.59, 2877000.00, 92000.00, 2722000.00, 1905400.00, 739750.00),
(N'008B01', N'129/330', N'B', N'8', 8, N'1 BEDROOM (2)', 29.60, 2877000.00, 93000.00, 2753000.00, 1927100.00, 740000.00),
(N'008B02', N'129/331', N'B', N'8', 8, N'1 BEDROOM (2)', 28.81, 2793000.00, 93000.00, 2679000.00, 1875300.00, 720250.00),
(N'008B03', N'129/332', N'B', N'8', 8, N'1 BEDROOM (2)', 28.80, 2793000.00, 93000.00, 2678000.00, 1874600.00, 720000.00),
(N'008B04', N'129/333', N'B', N'8', 8, N'1 BEDROOM (2)', 28.81, 2793000.00, 93000.00, 2679000.00, 1875300.00, 720250.00),
(N'008B05', N'129/334', N'B', N'8', 8, N'1 BEDROOM (2)', 28.81, 2730000.00, 93000.00, 2679000.00, 1875300.00, 720250.00),
(N'008B06', N'129/335', N'B', N'8', 8, N'1 BEDROOM (2)', 28.84, 2845500.00, 96000.00, 2769000.00, 1938300.00, 721000.00),
(N'008B07', N'129/336', N'B', N'8', 8, N'2 BEDROOMS', 51.16, 5733000.00, 104000.00, 5321000.00, 3724700.00, 1279000.00),
(N'008B08', N'129/337', N'B', N'8', 8, N'1 BEDROOM (2)', 29.11, 3160500.00, 95000.00, 2765000.00, 1935500.00, 727750.00),
(N'008B09', N'129/338', N'B', N'8', 8, N'1 BEDROOM (2)', 28.71, 3108000.00, 95000.00, 2727000.00, 1908900.00, 717750.00),
(N'008B10', N'129/339', N'B', N'8', 8, N'1 BEDROOM (2)', 28.72, 3183600.00, 95000.00, 2728000.00, 1909600.00, 718000.00),
(N'008B11', N'129/340', N'B', N'8', 8, N'1 BEDROOM (2)', 28.69, 3108000.00, 95000.00, 2726000.00, 1908200.00, 717250.00),
(N'008B12', N'129/341', N'B', N'8', 8, N'2 BEDROOMS', 51.33, 5964000.00, 104000.00, 5338000.00, 3736600.00, 1283250.00),
(N'008B13', N'129/342', N'B', N'8', 8, N'2 BEDROOMS', 51.33, 5964000.00, 104000.00, 5338000.00, 3736600.00, 1283250.00),
(N'008B14', N'129/343', N'B', N'8', 8, N'1 BEDROOM (2)', 28.70, 3108000.00, 95000.00, 2727000.00, 1908900.00, 717500.00),
(N'008B15', N'129/344', N'B', N'8', 8, N'1 BEDROOM (2)', 28.71, 3108000.00, 95000.00, 2727000.00, 1908900.00, 717750.00),
(N'008B16', N'129/345', N'B', N'8', 8, N'1 BEDROOM (2)', 29.36, 3192000.00, 95000.00, 2789000.00, 1952300.00, 734000.00),
(N'008B17', N'129/346', N'B', N'8', 8, N'2 BEDROOMS', 51.16, 5877900.00, 104000.00, 5321000.00, 3724700.00, 1279000.00),
(N'008B18', N'129/347', N'B', N'8', 8, N'1 BEDROOM (2)', 28.84, 2845500.00, 96000.00, 2769000.00, 1938300.00, 721000.00),
(N'008B19', N'129/348', N'B', N'8', 8, N'1 BEDROOM (2)', 28.54, 2761500.00, 93000.00, 2654000.00, 1857800.00, 713500.00),
(N'008B20', N'129/349', N'B', N'8', 8, N'1 BEDROOM (2)', 28.81, 2793000.00, 93000.00, 2679000.00, 1875300.00, 720250.00),
(N'008B21', N'129/350', N'B', N'8', 8, N'1 BEDROOM (2)', 28.81, 2793000.00, 93000.00, 2679000.00, 1875300.00, 720250.00),
(N'008B22', N'129/351', N'B', N'8', 8, N'1 BEDROOM (2)', 29.60, 2877000.00, 93000.00, 2753000.00, 1927100.00, 740000.00),
(N'001C05', N'129/352', N'C', N'1', 1, N'1 BEDROOM (2)', 28.84, 2656500.00, 84000.00, 2423000.00, 1696100.00, 721000.00),
(N'001C06', N'129/353', N'C', N'1', 1, N'1 BEDROOM (2)', 28.80, 2530500.00, 81000.00, 2333000.00, 1633100.00, 720000.00),
(N'001C07', N'129/354', N'C', N'1', 1, N'1 BEDROOM (2)', 28.80, 2593500.00, 81000.00, 2333000.00, 1633100.00, 720000.00),
(N'001C08', N'129/355', N'C', N'1', 1, N'1 BEDROOM (2)', 28.80, 2572500.00, 81000.00, 2333000.00, 1633100.00, 720000.00),
(N'001C09', N'129/356', N'C', N'1', 1, N'1 BEDROOM (2)', 28.84, 2593500.00, 81000.00, 2336000.00, 1635200.00, 721000.00),
(N'001C10', N'129/357', N'C', N'1', 1, N'1 BEDROOM (2)', 28.81, 2646000.00, 81000.00, 2334000.00, 1633800.00, 720250.00),
(N'001C11', N'129/358', N'C', N'1', 1, N'1 BEDROOM (2)', 29.47, 2593500.00, 84000.00, 2475000.00, 1732500.00, 736750.00),
(N'001C12', N'129/359', N'C', N'1', 1, N'1 BEDROOM (2)', 29.42, 2593500.00, 84000.00, 2471000.00, 1729700.00, 735500.00),
(N'001C13', N'129/360', N'C', N'1', 1, N'1 BEDROOM (1)', 25.29, 1932000.00, 79000.00, 1998000.00, 1398600.00, 632250.00),
(N'002C01', N'129/361', N'C', N'2', 2, N'1 BEDROOM (2)', 29.39, 2877000.00, 85000.00, 2498000.00, 1748600.00, 734750.00),
(N'002C02', N'129/362', N'C', N'2', 2, N'1 BEDROOM (2)', 28.86, 2772000.00, 85000.00, 2453000.00, 1717100.00, 721500.00),
(N'002C03', N'129/363', N'C', N'2', 2, N'2 BEDROOMS', 50.77, 5082000.00, 91000.00, 4620000.00, 3234000.00, 1269250.00),
(N'002C04', N'129/364', N'C', N'2', 2, N'2 BEDROOMS', 51.16, 5145000.00, 94000.00, 4809000.00, 3366300.00, 1279000.00),
(N'002C05', N'129/365', N'C', N'2', 2, N'1 BEDROOM (2)', 28.84, 2793000.00, 85000.00, 2451000.00, 1715700.00, 721000.00),
(N'002C06', N'129/366', N'C', N'2', 2, N'1 BEDROOM (2)', 28.80, 2730000.00, 82000.00, 2362000.00, 1653400.00, 720000.00),
(N'002C07', N'129/367', N'C', N'2', 2, N'1 BEDROOM (2)', 28.80, 2740500.00, 82000.00, 2362000.00, 1653400.00, 720000.00),
(N'002C08', N'129/368', N'C', N'2', 2, N'1 BEDROOM (2)', 28.80, 2740500.00, 82000.00, 2362000.00, 1653400.00, 720000.00),
(N'002C09', N'129/369', N'C', N'2', 2, N'1 BEDROOM (2)', 28.84, 2740500.00, 82000.00, 2365000.00, 1655500.00, 721000.00),
(N'002C10', N'129/370', N'C', N'2', 2, N'1 BEDROOM (2)', 28.81, 2793000.00, 82000.00, 2362000.00, 1653400.00, 720250.00),
(N'002C11', N'129/371', N'C', N'2', 2, N'1 BEDROOM (2)', 29.47, 2719500.00, 85000.00, 2505000.00, 1753500.00, 736750.00),
(N'002C12', N'129/372', N'C', N'2', 2, N'1 BEDROOM (2)', 29.42, 2709000.00, 85000.00, 2501000.00, 1750700.00, 735500.00),
(N'002C13', N'129/373', N'C', N'2', 2, N'1 BEDROOM (1)', 25.55, 2446500.00, 80000.00, 2044000.00, 1430800.00, 638750.00),
(N'002C14', N'129/374', N'C', N'2', 2, N'1 BEDROOM (1)', 25.58, 2404500.00, 80000.00, 2046000.00, 1432200.00, 639500.00),
(N'002C15', N'129/375', N'C', N'2', 2, N'1 BEDROOM (1)', 25.59, 2404500.00, 80000.00, 2047000.00, 1432900.00, 639750.00),
(N'002C16', N'129/376', N'C', N'2', 2, N'1 BEDROOM (1)', 25.58, 2404500.00, 80000.00, 2046000.00, 1432200.00, 639500.00),
(N'002C17', N'129/377', N'C', N'2', 2, N'1 BEDROOM (1)', 25.59, 2404500.00, 80000.00, 2047000.00, 1432900.00, 639750.00),
(N'002C18', N'129/378', N'C', N'2', 2, N'1 BEDROOM (1)', 25.41, 2446500.00, 80000.00, 2033000.00, 1423100.00, 635250.00),
(N'002C19', N'129/379', N'C', N'2', 2, N'1 BEDROOM (2)', 29.31, 2835000.00, 85000.00, 2491000.00, 1743700.00, 732750.00),
(N'002C20', N'129/380', N'C', N'2', 2, N'2 BEDROOMS', 51.32, 5323500.00, 95000.00, 4875000.00, 3412500.00, 1283000.00),
(N'002C21', N'129/381', N'C', N'2', 2, N'1 BEDROOM (2)', 28.64, 2772000.00, 86000.00, 2463000.00, 1724100.00, 716000.00),
(N'002C22', N'129/382', N'C', N'2', 2, N'1 BEDROOM (2)', 29.44, 2845500.00, 86000.00, 2532000.00, 1772400.00, 736000.00),
(N'003C01', N'129/383', N'C', N'3', 3, N'1 BEDROOM (2)', 29.39, 2898000.00, 86000.00, 2528000.00, 1769600.00, 734750.00),
(N'003C02', N'129/384', N'C', N'3', 3, N'1 BEDROOM (2)', 28.86, 2866500.00, 86000.00, 2482000.00, 1737400.00, 721500.00),
(N'003C03', N'129/385', N'C', N'3', 3, N'2 BEDROOMS', 50.77, 5166000.00, 92000.00, 4671000.00, 3269700.00, 1269250.00),
(N'003C04', N'129/386', N'C', N'3', 3, N'2 BEDROOMS', 51.16, 5239500.00, 95000.00, 4860000.00, 3402000.00, 1279000.00),
(N'003C05', N'129/387', N'C', N'3', 3, N'1 BEDROOM (2)', 28.84, 2866500.00, 88000.00, 2538000.00, 1776600.00, 721000.00),
(N'003C06', N'129/388', N'C', N'3', 3, N'1 BEDROOM (2)', 28.80, 2751000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'003C07', N'129/389', N'C', N'3', 3, N'1 BEDROOM (2)', 28.80, 2814000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'003C08', N'129/390', N'C', N'3', 3, N'1 BEDROOM (2)', 28.80, 2814000.00, 85000.00, 2448000.00, 1713600.00, 720000.00),
(N'003C09', N'129/391', N'C', N'3', 3, N'1 BEDROOM (2)', 28.84, 2814000.00, 85000.00, 2451000.00, 1715700.00, 721000.00),
(N'003C10', N'129/392', N'C', N'3', 3, N'1 BEDROOM (2)', 28.81, 2866500.00, 85000.00, 2449000.00, 1714300.00, 720250.00),
(N'003C11', N'129/393', N'C', N'3', 3, N'1 BEDROOM (2)', 29.47, 2803500.00, 88000.00, 2593000.00, 1815100.00, 736750.00),
(N'003C12', N'129/394', N'C', N'3', 3, N'1 BEDROOM (2)', 29.42, 2803500.00, 86000.00, 2530000.00, 1771000.00, 735500.00),
(N'003C13', N'129/395', N'C', N'3', 3, N'1 BEDROOM (1)', 25.55, 2467500.00, 81000.00, 2070000.00, 1449000.00, 638750.00),
(N'003C14', N'129/396', N'C', N'3', 3, N'1 BEDROOM (1)', 25.58, 2467500.00, 81000.00, 2072000.00, 1450400.00, 639500.00),
(N'003C15', N'129/397', N'C', N'3', 3, N'1 BEDROOM (1)', 25.59, 2467500.00, 81000.00, 2073000.00, 1451100.00, 639750.00),
(N'003C16', N'129/398', N'C', N'3', 3, N'1 BEDROOM (1)', 25.58, 2467500.00, 81000.00, 2072000.00, 1450400.00, 639500.00),
(N'003C17', N'129/399', N'C', N'3', 3, N'1 BEDROOM (1)', 25.59, 2467500.00, 81000.00, 2073000.00, 1451100.00, 639750.00),
(N'003C18', N'129/400', N'C', N'3', 3, N'1 BEDROOM (1)', 25.41, 2457000.00, 81000.00, 2058000.00, 1440600.00, 635250.00),
(N'003C19', N'129/401', N'C', N'3', 3, N'1 BEDROOM (2)', 29.31, 2908500.00, 86000.00, 2521000.00, 1764700.00, 732750.00),
(N'003C20', N'129/402', N'C', N'3', 3, N'2 BEDROOMS', 51.32, 5355000.00, 96000.00, 4927000.00, 3448900.00, 1283000.00),
(N'003C21', N'129/403', N'C', N'3', 3, N'1 BEDROOM (2)', 28.64, 2845500.00, 87000.00, 2492000.00, 1744400.00, 716000.00),
(N'003C22', N'129/404', N'C', N'3', 3, N'1 BEDROOM (2)', 29.44, 2919000.00, 87000.00, 2561000.00, 1792700.00, 736000.00),
(N'004C01', N'129/405', N'C', N'4', 4, N'1 BEDROOM (2)', 29.39, 2913750.00, 87000.00, 2557000.00, 1789900.00, 734750.00),
(N'004C02', N'129/406', N'C', N'4', 4, N'1 BEDROOM (2)', 28.86, 2882250.00, 87000.00, 2511000.00, 1757700.00, 721500.00),
(N'004C03', N'129/407', N'C', N'4', 4, N'2 BEDROOMS', 50.77, 5040000.00, 93000.00, 4722000.00, 3305400.00, 1269250.00),
(N'004C04', N'129/408', N'C', N'4', 4, N'2 BEDROOMS', 51.16, 5323500.00, 96000.00, 4911000.00, 3437700.00, 1279000.00),
(N'004C05', N'129/409', N'C', N'4', 4, N'1 BEDROOM (2)', 28.84, 2845500.00, 89000.00, 2567000.00, 1796900.00, 721000.00),
(N'004C06', N'129/410', N'C', N'4', 4, N'1 BEDROOM (2)', 28.80, 2698500.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'004C07', N'129/411', N'C', N'4', 4, N'1 BEDROOM (2)', 28.80, 2782500.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'004C08', N'129/412', N'C', N'4', 4, N'1 BEDROOM (2)', 28.80, 2782500.00, 86000.00, 2477000.00, 1733900.00, 720000.00),
(N'004C09', N'129/413', N'C', N'4', 4, N'1 BEDROOM (2)', 28.84, 2793000.00, 86000.00, 2480000.00, 1736000.00, 721000.00),
(N'004C10', N'129/414', N'C', N'4', 4, N'1 BEDROOM (2)', 28.81, 2824500.00, 86000.00, 2478000.00, 1734600.00, 720250.00),
(N'004C11', N'129/415', N'C', N'4', 4, N'1 BEDROOM (2)', 29.47, 2761500.00, 89000.00, 2623000.00, 1836100.00, 736750.00),
(N'004C12', N'129/416', N'C', N'4', 4, N'1 BEDROOM (2)', 29.42, 2782500.00, 87000.00, 2560000.00, 1792000.00, 735500.00),
(N'004C13', N'129/417', N'C', N'4', 4, N'1 BEDROOM (1)', 25.55, 2415000.00, 82000.00, 2095000.00, 1466500.00, 638750.00),
(N'004C14', N'129/418', N'C', N'4', 4, N'1 BEDROOM (1)', 25.58, 2446500.00, 82000.00, 2098000.00, 1468600.00, 639500.00),
(N'004C15', N'129/419', N'C', N'4', 4, N'1 BEDROOM (1)', 25.59, 2446500.00, 82000.00, 2098000.00, 1468600.00, 639750.00),
(N'004C16', N'129/420', N'C', N'4', 4, N'1 BEDROOM (1)', 25.58, 2446500.00, 82000.00, 2098000.00, 1468600.00, 639500.00),
(N'004C17', N'129/421', N'C', N'4', 4, N'1 BEDROOM (1)', 25.59, 2425500.00, 82000.00, 2098000.00, 1468600.00, 639750.00),
(N'004C18', N'129/422', N'C', N'4', 4, N'1 BEDROOM (1)', 25.41, 2436000.00, 82000.00, 2084000.00, 1458800.00, 635250.00),
(N'004C19', N'129/423', N'C', N'4', 4, N'1 BEDROOM (2)', 29.31, 2940000.00, 87000.00, 2550000.00, 1785000.00, 732750.00),
(N'004C20', N'129/424', N'C', N'4', 4, N'2 BEDROOMS', 51.32, 5439000.00, 97000.00, 4978000.00, 3484600.00, 1283000.00),
(N'004C21', N'129/425', N'C', N'4', 4, N'1 BEDROOM (2)', 28.64, 2850750.00, 88000.00, 2520000.00, 1764000.00, 716000.00),
(N'004C22', N'129/426', N'C', N'4', 4, N'1 BEDROOM (2)', 29.44, 2934750.00, 88000.00, 2591000.00, 1813700.00, 736000.00),
(N'005C01', N'129/427', N'C', N'5', 5, N'1 BEDROOM (2)', 29.39, 2923200.00, 88000.00, 2586000.00, 1810200.00, 734750.00),
(N'005C02', N'129/428', N'C', N'5', 5, N'1 BEDROOM (2)', 28.86, 2891700.00, 88000.00, 2540000.00, 1778000.00, 721500.00),
(N'005C03', N'129/429', N'C', N'5', 5, N'2 BEDROOMS', 50.77, 5156550.00, 94000.00, 4772000.00, 3340400.00, 1269250.00),
(N'005C04', N'129/430', N'C', N'5', 5, N'2 BEDROOMS', 51.16, 5467350.00, 97000.00, 4963000.00, 3474100.00, 1279000.00),
(N'005C05', N'129/431', N'C', N'5', 5, N'1 BEDROOM (2)', 28.84, 2856000.00, 90000.00, 2596000.00, 1817200.00, 721000.00),
(N'005C06', N'129/432', N'C', N'5', 5, N'1 BEDROOM (2)', 28.80, 2803500.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'005C07', N'129/433', N'C', N'5', 5, N'1 BEDROOM (2)', 28.80, 2793000.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'005C08', N'129/434', N'C', N'5', 5, N'1 BEDROOM (2)', 28.80, 2793000.00, 87000.00, 2506000.00, 1754200.00, 720000.00),
(N'005C09', N'129/435', N'C', N'5', 5, N'1 BEDROOM (2)', 28.84, 2814000.00, 87000.00, 2509000.00, 1756300.00, 721000.00),
(N'005C10', N'129/436', N'C', N'5', 5, N'1 BEDROOM (2)', 28.81, 2866500.00, 87000.00, 2506000.00, 1754200.00, 720250.00),
(N'005C11', N'129/437', N'C', N'5', 5, N'1 BEDROOM (2)', 29.47, 2814000.00, 90000.00, 2652000.00, 1856400.00, 736750.00),
(N'005C12', N'129/438', N'C', N'5', 5, N'1 BEDROOM (2)', 29.42, 2793000.00, 88000.00, 2589000.00, 1812300.00, 735500.00),
(N'005C13', N'129/439', N'C', N'5', 5, N'1 BEDROOM (1)', 25.55, 2446500.00, 83000.00, 2121000.00, 1484700.00, 638750.00),
(N'005C14', N'129/440', N'C', N'5', 5, N'1 BEDROOM (1)', 25.58, 2467500.00, 83000.00, 2123000.00, 1486100.00, 639500.00),
(N'005C15', N'129/441', N'C', N'5', 5, N'1 BEDROOM (1)', 25.59, 2446500.00, 83000.00, 2124000.00, 1486800.00, 639750.00),
(N'005C16', N'129/442', N'C', N'5', 5, N'1 BEDROOM (1)', 25.58, 2467500.00, 83000.00, 2123000.00, 1486100.00, 639500.00),
(N'005C17', N'129/443', N'C', N'5', 5, N'1 BEDROOM (1)', 25.59, 2467500.00, 83000.00, 2124000.00, 1486800.00, 639750.00),
(N'005C18', N'129/444', N'C', N'5', 5, N'1 BEDROOM (1)', 25.41, 2457000.00, 83000.00, 2109000.00, 1476300.00, 635250.00),
(N'005C19', N'129/445', N'C', N'5', 5, N'1 BEDROOM (2)', 29.31, 2919000.00, 88000.00, 2579000.00, 1805300.00, 732750.00),
(N'005C20', N'129/446', N'C', N'5', 5, N'2 BEDROOMS', 51.32, 5687850.00, 98000.00, 5029000.00, 3520300.00, 1283000.00),
(N'005C21', N'129/447', N'C', N'5', 5, N'1 BEDROOM (2)', 28.64, 3049200.00, 89000.00, 2549000.00, 1784300.00, 716000.00),
(N'005C22', N'129/448', N'C', N'5', 5, N'1 BEDROOM (2)', 29.44, 3049200.00, 89000.00, 2620000.00, 1834000.00, 736000.00),
(N'006C01', N'129/449', N'C', N'6', 6, N'1 BEDROOM (2)', 29.39, 2954700.00, 89000.00, 2616000.00, 1831200.00, 734750.00),
(N'006C02', N'129/450', N'C', N'6', 6, N'1 BEDROOM (2)', 28.86, 2923200.00, 89000.00, 2569000.00, 1798300.00, 721500.00),
(N'006C03', N'129/451', N'C', N'6', 6, N'2 BEDROOMS', 50.77, 5209050.00, 95000.00, 4823000.00, 3376100.00, 1269250.00),
(N'006C04', N'129/452', N'C', N'6', 6, N'2 BEDROOMS', 51.16, 5519850.00, 98000.00, 5014000.00, 3509800.00, 1279000.00),
(N'006C05', N'129/453', N'C', N'6', 6, N'1 BEDROOM (2)', 28.84, 2877000.00, 91000.00, 2624000.00, 1836800.00, 721000.00),
(N'006C06', N'129/454', N'C', N'6', 6, N'1 BEDROOM (2)', 28.80, 2761500.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'006C07', N'129/455', N'C', N'6', 6, N'1 BEDROOM (2)', 28.80, 2824500.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'006C08', N'129/456', N'C', N'6', 6, N'1 BEDROOM (2)', 28.80, 2824500.00, 88000.00, 2534000.00, 1773800.00, 720000.00),
(N'006C09', N'129/457', N'C', N'6', 6, N'1 BEDROOM (2)', 28.84, 2845500.00, 88000.00, 2538000.00, 1776600.00, 721000.00),
(N'006C10', N'129/458', N'C', N'6', 6, N'1 BEDROOM (2)', 28.81, 2877000.00, 88000.00, 2535000.00, 1774500.00, 720250.00),
(N'006C11', N'129/459', N'C', N'6', 6, N'1 BEDROOM (2)', 29.47, 2824500.00, 91000.00, 2682000.00, 1877400.00, 736750.00),
(N'006C12', N'129/460', N'C', N'6', 6, N'1 BEDROOM (2)', 29.42, 2824500.00, 89000.00, 2618000.00, 1832600.00, 735500.00),
(N'006C13', N'129/461', N'C', N'6', 6, N'1 BEDROOM (1)', 25.55, 2488500.00, 84000.00, 2146000.00, 1502200.00, 638750.00),
(N'006C14', N'129/462', N'C', N'6', 6, N'1 BEDROOM (1)', 25.58, 2499000.00, 84000.00, 2149000.00, 1504300.00, 639500.00),
(N'006C15', N'129/463', N'C', N'6', 6, N'1 BEDROOM (1)', 25.59, 2499000.00, 84000.00, 2150000.00, 1505000.00, 639750.00),
(N'006C16', N'129/464', N'C', N'6', 6, N'1 BEDROOM (1)', 25.58, 2499000.00, 84000.00, 2149000.00, 1504300.00, 639500.00),
(N'006C17', N'129/465', N'C', N'6', 6, N'1 BEDROOM (1)', 25.59, 2478000.00, 84000.00, 2150000.00, 1505000.00, 639750.00),
(N'006C18', N'129/466', N'C', N'6', 6, N'1 BEDROOM (1)', 25.41, 2478000.00, 84000.00, 2134000.00, 1493800.00, 635250.00),
(N'006C19', N'129/467', N'C', N'6', 6, N'1 BEDROOM (2)', 29.31, 2919000.00, 89000.00, 2609000.00, 1826300.00, 732750.00),
(N'006C20', N'129/468', N'C', N'6', 6, N'2 BEDROOMS', 51.32, 5761350.00, 99000.00, 5081000.00, 3556700.00, 1283000.00),
(N'006C21', N'129/469', N'C', N'6', 6, N'1 BEDROOM (2)', 28.64, 3017700.00, 90000.00, 2578000.00, 1804600.00, 716000.00),
(N'006C22', N'129/470', N'C', N'6', 6, N'1 BEDROOM (2)', 29.44, 3101700.00, 90000.00, 2650000.00, 1855000.00, 736000.00),
(N'007C01', N'129/471', N'C', N'7', 7, N'1 BEDROOM (2)', 29.39, 3059700.00, 90000.00, 2645000.00, 1851500.00, 734750.00),
(N'007C02', N'129/472', N'C', N'7', 7, N'1 BEDROOM (2)', 28.86, 3017700.00, 90000.00, 2597000.00, 1817900.00, 721500.00),
(N'007C03', N'129/473', N'C', N'7', 7, N'2 BEDROOMS', 50.77, 5335050.00, 96000.00, 4874000.00, 3411800.00, 1269250.00),
(N'007C04', N'129/474', N'C', N'7', 7, N'2 BEDROOMS', 51.16, 5414850.00, 99000.00, 5065000.00, 3545500.00, 1279000.00),
(N'007C05', N'129/475', N'C', N'7', 7, N'1 BEDROOM (2)', 28.84, 2982000.00, 92000.00, 2653000.00, 1857100.00, 721000.00),
(N'007C06', N'129/476', N'C', N'7', 7, N'1 BEDROOM (2)', 28.80, 2866500.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'007C07', N'129/477', N'C', N'7', 7, N'1 BEDROOM (2)', 28.80, 2919000.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'007C08', N'129/478', N'C', N'7', 7, N'1 BEDROOM (2)', 28.80, 2919000.00, 89000.00, 2563000.00, 1794100.00, 720000.00),
(N'007C09', N'129/479', N'C', N'7', 7, N'1 BEDROOM (2)', 28.84, 2929500.00, 89000.00, 2567000.00, 1796900.00, 721000.00),
(N'007C10', N'129/480', N'C', N'7', 7, N'1 BEDROOM (2)', 28.81, 2982000.00, 89000.00, 2564000.00, 1794800.00, 720250.00),
(N'007C11', N'129/481', N'C', N'7', 7, N'1 BEDROOM (2)', 29.47, 2929500.00, 92000.00, 2711000.00, 1897700.00, 736750.00),
(N'007C12', N'129/482', N'C', N'7', 7, N'1 BEDROOM (2)', 29.42, 2919000.00, 90000.00, 2648000.00, 1853600.00, 735500.00),
(N'007C13', N'129/483', N'C', N'7', 7, N'1 BEDROOM (1)', 25.55, 2572500.00, 85000.00, 2172000.00, 1520400.00, 638750.00),
(N'007C14', N'129/484', N'C', N'7', 7, N'1 BEDROOM (1)', 25.58, 2572500.00, 85000.00, 2174000.00, 1521800.00, 639500.00),
(N'007C15', N'129/485', N'C', N'7', 7, N'1 BEDROOM (1)', 25.59, 2572500.00, 85000.00, 2175000.00, 1522500.00, 639750.00),
(N'007C16', N'129/486', N'C', N'7', 7, N'1 BEDROOM (1)', 25.58, 2572500.00, 85000.00, 2174000.00, 1521800.00, 639500.00),
(N'007C17', N'129/487', N'C', N'7', 7, N'1 BEDROOM (1)', 25.59, 2572500.00, 85000.00, 2175000.00, 1522500.00, 639750.00),
(N'007C18', N'129/488', N'C', N'7', 7, N'1 BEDROOM (1)', 25.41, 2562000.00, 85000.00, 2160000.00, 1512000.00, 635250.00),
(N'007C19', N'129/489', N'C', N'7', 7, N'1 BEDROOM (2)', 29.31, 3024000.00, 90000.00, 2638000.00, 1846600.00, 732750.00),
(N'007C20', N'129/490', N'C', N'7', 7, N'2 BEDROOMS', 51.32, 5635350.00, 100000.00, 5132000.00, 3592400.00, 1283000.00),
(N'007C21', N'129/491', N'C', N'7', 7, N'1 BEDROOM (2)', 28.64, 3101700.00, 91000.00, 2606000.00, 1824200.00, 716000.00),
(N'007C22', N'129/492', N'C', N'7', 7, N'1 BEDROOM (2)', 29.44, 3185700.00, 91000.00, 2679000.00, 1875300.00, 736000.00),
(N'008C01', N'129/493', N'C', N'8', 8, N'1 BEDROOM (2)', 29.40, 2986200.00, 91000.00, 2675000.00, 1872500.00, 735000.00),
(N'008C02', N'129/494', N'C', N'8', 8, N'1 BEDROOM (2)', 28.86, 2944200.00, 91000.00, 2626000.00, 1838200.00, 721500.00),
(N'008C03', N'129/495', N'C', N'8', 8, N'2 BEDROOMS', 50.77, 5261550.00, 97000.00, 4925000.00, 3447500.00, 1269250.00),
(N'008C04', N'129/496', N'C', N'8', 8, N'2 BEDROOMS', 51.16, 5572350.00, 100000.00, 5116000.00, 3581200.00, 1279000.00),
(N'008C05', N'129/497', N'C', N'8', 8, N'1 BEDROOM (2)', 28.84, 2929500.00, 93000.00, 2682000.00, 1877400.00, 721000.00),
(N'008C06', N'129/498', N'C', N'8', 8, N'1 BEDROOM (2)', 28.81, 2814000.00, 90000.00, 2593000.00, 1815100.00, 720250.00),
(N'008C07', N'129/499', N'C', N'8', 8, N'1 BEDROOM (2)', 28.80, 2919000.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'008C08', N'129/500', N'C', N'8', 8, N'1 BEDROOM (2)', 28.80, 2866500.00, 90000.00, 2592000.00, 1814400.00, 720000.00),
(N'008C09', N'129/501', N'C', N'8', 8, N'1 BEDROOM (2)', 28.84, 2877000.00, 90000.00, 2596000.00, 1817200.00, 721000.00),
(N'008C10', N'129/502', N'C', N'8', 8, N'1 BEDROOM (2)', 28.82, 2929500.00, 90000.00, 2594000.00, 1815800.00, 720500.00),
(N'008C11', N'129/503', N'C', N'8', 8, N'1 BEDROOM (2)', 29.47, 2856000.00, 93000.00, 2741000.00, 1918700.00, 736750.00),
(N'008C12', N'129/504', N'C', N'8', 8, N'1 BEDROOM (2)', 29.42, 2866500.00, 91000.00, 2677000.00, 1873900.00, 735500.00),
(N'008C13', N'129/505', N'C', N'8', 8, N'1 BEDROOM (1)', 25.55, 2572500.00, 86000.00, 2197000.00, 1537900.00, 638750.00),
(N'008C14', N'129/506', N'C', N'8', 8, N'1 BEDROOM (1)', 25.59, 2415000.00, 86000.00, 2201000.00, 1540700.00, 639750.00),
(N'008C15', N'129/507', N'C', N'8', 8, N'1 BEDROOM (1)', 25.59, 2415000.00, 86000.00, 2201000.00, 1540700.00, 639750.00),
(N'008C16', N'129/508', N'C', N'8', 8, N'1 BEDROOM (1)', 25.59, 2415000.00, 86000.00, 2201000.00, 1540700.00, 639750.00),
(N'008C17', N'129/509', N'C', N'8', 8, N'1 BEDROOM (1)', 25.59, 2520000.00, 86000.00, 2201000.00, 1540700.00, 639750.00),
(N'008C18', N'129/510', N'C', N'8', 8, N'1 BEDROOM (1)', 25.42, 2415000.00, 86000.00, 2186000.00, 1530200.00, 635500.00),
(N'008C19', N'129/511', N'C', N'8', 8, N'1 BEDROOM (2)', 29.32, 2950500.00, 91000.00, 2668000.00, 1867600.00, 733000.00),
(N'008C20', N'129/512', N'C', N'8', 8, N'2 BEDROOMS', 51.32, 5792850.00, 101000.00, 5183000.00, 3628100.00, 1283000.00),
(N'008C21', N'129/513', N'C', N'8', 8, N'1 BEDROOM (2)', 28.65, 3049200.00, 92000.00, 2636000.00, 1845200.00, 716250.00),
(N'008C22', N'129/514', N'C', N'8', 8, N'1 BEDROOM (2)', 29.44, 3112200.00, 92000.00, 2708000.00, 1895600.00, 736000.00);

IF (SELECT COUNT(*) FROM #Src) <> 513
    THROW 50004, 'The staged workbook is not the expected row count. The VALUES block has been edited or truncated.', 1;

IF (SELECT COUNT(DISTINCT RoomNumber) FROM #Src) <> 513
   OR (SELECT COUNT(DISTINCT CondoRegistrationNumber) FROM #Src) <> 513
    THROW 50005, 'The staged workbook contains a duplicate room or registration number. Refusing to guess which price wins.', 1;

IF (SELECT SUM(AppraisalValue) FROM #Src) <> 1408945000.00
    THROW 50016, 'The staged appraisal total no longer equals the workbook totals row. The VALUES block has been edited.', 1;

-- ── 3. Match workbook rows to units ───────────────────────────────────────────
-- LEFT JOIN: a workbook room with no unit survives into #Match (IsNew = 1) so @CreateMissingUnits
-- can mint one for it.
SELECT s.*,
       ProjectUnitId  = pu.Id,
       IsNew          = CONVERT(bit, CASE WHEN pu.Id IS NULL THEN 1 ELSE 0 END),
       MatchedBy      = CONVERT(varchar(12), CASE
                            WHEN pu.Id IS NULL THEN NULL
                            WHEN pu.RoomNumber = s.RoomNumber
                             AND pu.CondoRegistrationNumber = s.CondoRegistrationNumber THEN 'both'
                            WHEN pu.RoomNumber = s.RoomNumber THEN 'room'
                            ELSE 'registration' END),
       pu.ProjectModelId,
       ProjectTowerId = CONVERT(uniqueidentifier, NULL),   -- filled in for new rows only
       SequenceNumber = CONVERT(int, NULL),                --            "
       IsSold         = ISNULL(pu.IsSold, CONVERT(bit, 0)),
       DbRoomNumber   = pu.RoomNumber, DbCondoReg     = pu.CondoRegistrationNumber,
       DbFloor        = pu.Floor,      DbTowerName    = pu.TowerName, DbModelType = pu.ModelType,
       DbUsableArea   = pu.UsableArea, DbSellingPrice = pu.SellingPrice
INTO #Match
FROM #Src s
LEFT JOIN appraisal.ProjectUnits pu
  ON pu.ProjectId = @ProjectId
 AND (pu.RoomNumber = s.RoomNumber OR pu.CondoRegistrationNumber = s.CondoRegistrationNumber);

DECLARE @Supplied       int = (SELECT COUNT(*) FROM #Src);
DECLARE @Matched        int = (SELECT COUNT(DISTINCT RoomNumber) FROM #Match WHERE IsNew = 0);
DECLARE @Unmatched      int = @Supplied - @Matched;
DECLARE @Ambiguous      int = (SELECT COUNT(*) FROM (SELECT RoomNumber FROM #Match WHERE IsNew = 0
                                                     GROUP BY RoomNumber HAVING COUNT(*) > 1) x);
DECLARE @DoubleClaimed  int = (SELECT COUNT(*) FROM (SELECT ProjectUnitId FROM #Match WHERE IsNew = 0
                                                     GROUP BY ProjectUnitId HAVING COUNT(*) > 1) x);
DECLARE @UnitsInProject int = (SELECT COUNT(*) FROM appraisal.ProjectUnits WHERE ProjectId = @ProjectId);
DECLARE @ByBoth         int = (SELECT COUNT(*) FROM #Match WHERE MatchedBy = 'both');
DECLARE @ByRoom         int = (SELECT COUNT(*) FROM #Match WHERE MatchedBy = 'room');
DECLARE @ByRegistration int = (SELECT COUNT(*) FROM #Match WHERE MatchedBy = 'registration');

PRINT CONCAT('Workbook rows           : ', @Supplied);
PRINT CONCAT('Units in this project   : ', @UnitsInProject);
PRINT CONCAT('Matched to a unit       : ', @Matched,
             '   (room+registration ', @ByBoth, ', room only ', @ByRoom,
             ', registration only ', @ByRegistration, ')');
PRINT CONCAT('Unmatched               : ', @Unmatched);
PRINT CONCAT('Ambiguous (>1 unit)     : ', @Ambiguous);
PRINT CONCAT('Units claimed twice     : ', @DoubleClaimed);
PRINT '';

-- The guards PRINT their offending rows as well as SELECTing them: an aborted batch leaves the grids
-- in SSMS's Results tab while the operator reads the error in Messages.
IF @Ambiguous > 0
BEGIN
    DECLARE @AmbiguousList nvarchar(max) = (
        SELECT STRING_AGG(CAST(x.RoomNumber AS nvarchar(max)), N', ') WITHIN GROUP (ORDER BY x.RoomNumber)
        FROM (SELECT TOP (20) RoomNumber FROM #Match WHERE IsNew = 0
              GROUP BY RoomNumber HAVING COUNT(*) > 1 ORDER BY RoomNumber) x);
    PRINT CONCAT('  rows matching more than one unit: ', @AmbiguousList,
                 CASE WHEN @Ambiguous > 20 THEN CONCAT('  ...and ', @Ambiguous - 20, ' more') ELSE '' END);
    SELECT m.RoomNumber, m.CondoRegistrationNumber, m.MatchedBy, UnitRoom = m.DbRoomNumber, UnitRegistration = m.DbCondoReg
    FROM #Match m
    WHERE m.RoomNumber IN (SELECT RoomNumber FROM #Match WHERE IsNew = 0 GROUP BY RoomNumber HAVING COUNT(*) > 1)
    ORDER BY m.RoomNumber;
    THROW 50006, 'A workbook row matched more than one project unit (its room and registration numbers point at different units). Resolve before loading.', 1;
END

IF @DoubleClaimed > 0
BEGIN
    SELECT m.ProjectUnitId, UnitRoom = m.DbRoomNumber, UnitRegistration = m.DbCondoReg,
           SheetRoom = m.RoomNumber, SheetRegistration = m.CondoRegistrationNumber, m.MatchedBy
    FROM #Match m
    WHERE m.ProjectUnitId IN (SELECT ProjectUnitId FROM #Match WHERE IsNew = 0
                              GROUP BY ProjectUnitId HAVING COUNT(*) > 1)
    ORDER BY m.ProjectUnitId, m.RoomNumber;
    THROW 50012, 'A project unit is claimed by two workbook rows (one by room number, one by registration number). Resolve before loading.', 1;
END

IF @Unmatched > 0
BEGIN
    DECLARE @UnmatchedList nvarchar(max) = (
        SELECT STRING_AGG(CAST(CONCAT(x.RoomNumber, N' (', x.CondoRegistrationNumber, N')') AS nvarchar(max)), N', ')
               WITHIN GROUP (ORDER BY x.RoomNumber)
        FROM (SELECT TOP (20) RoomNumber, CondoRegistrationNumber FROM #Match WHERE IsNew = 1 ORDER BY RoomNumber) x);
    PRINT CONCAT('  workbook rooms with no unit: ', @UnmatchedList,
                 CASE WHEN @Unmatched > 20 THEN CONCAT('  ...and ', @Unmatched - 20, ' more') ELSE '' END);

    -- Nearly all unmatched means the number FORMAT differs, so show what the project actually holds.
    DECLARE @HeldSample nvarchar(max) = (
        SELECT STRING_AGG(CAST(x.Held AS nvarchar(max)), N', ')
        FROM (SELECT TOP (20) Held = CONCAT(ISNULL(pu.RoomNumber, N'(null)'), N' (',
                                            ISNULL(pu.CondoRegistrationNumber, N'(null)'), N')')
              FROM appraisal.ProjectUnits pu
              WHERE pu.ProjectId = @ProjectId ORDER BY pu.SequenceNumber) x);
    PRINT CONCAT('  units this project holds   : ', @HeldSample);
    PRINT '';

    SELECT TOP (20) m.RoomNumber, m.CondoRegistrationNumber, m.TowerName, m.FloorText, m.ModelType, m.UsableArea
    FROM #Match m WHERE m.IsNew = 1 ORDER BY m.RoomNumber;

    SELECT TOP (20) pu.RoomNumber, pu.CondoRegistrationNumber, pu.TowerName, pu.Floor, pu.ModelType
    FROM appraisal.ProjectUnits pu
    WHERE pu.ProjectId = @ProjectId
    ORDER BY pu.SequenceNumber;

    IF @CreateMissingUnits = 0
        THROW 50007, 'Not every workbook row matched a unit, and @CreateMissingUnits is 0. Nothing was written. The unmatched rooms are named above (Messages), with fuller detail in the Results tab.', 1;

    IF @Unmatched > @MaxUnitsToCreate
        THROW 50011, 'More workbook rooms are missing than @MaxUnitsToCreate allows. That is a number-format mismatch, not a data gap — creating them would duplicate the project. Nothing was written.', 1;

    UPDATE #Match SET ProjectUnitId = NEWID() WHERE IsNew = 1;

    DECLARE @MaxSeq int =
        ISNULL((SELECT MAX(SequenceNumber) FROM appraisal.ProjectUnits WHERE ProjectId = @ProjectId), 0);

    ;WITH Numbered AS (
        SELECT SequenceNumber, NewSeq = @MaxSeq + ROW_NUMBER() OVER (ORDER BY RoomNumber)
        FROM #Match WHERE IsNew = 1
    )
    UPDATE Numbered SET SequenceNumber = NewSeq;

    -- Resolve tower and model the way Project.AutoCreateCondoTowersAndModels does. RESOLVE ONLY.
    UPDATE m
    SET m.ProjectTowerId = t.Id,
        m.ProjectModelId = md.Id
    FROM #Match m
    OUTER APPLY (SELECT TOP (1) pt.Id FROM appraisal.ProjectTowers pt
                 WHERE pt.ProjectId = @ProjectId AND pt.TowerName = m.TowerName
                 ORDER BY pt.Id) t
    OUTER APPLY (SELECT TOP (1) pm.Id FROM appraisal.ProjectModels pm
                 WHERE pm.ProjectId = @ProjectId AND pm.ModelName = m.ModelType
                   AND (pm.ProjectTowerId = t.Id OR (pm.ProjectTowerId IS NULL AND t.Id IS NULL))
                 ORDER BY pm.Id) md
    WHERE m.IsNew = 1;

    DECLARE @NewNoModel int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 1 AND ProjectModelId IS NULL);
    PRINT CONCAT('  units to CREATE            : ', @Unmatched,
                 '   (', @Unmatched - @NewNoModel, ' linked to a model, ', @NewNoModel, ' could not be)');
    IF @NewNoModel > 0
        PRINT '  ^ a unit with no ProjectModelId contributes nothing to ValuationAnalyses or the'
            + ' Decision Summary. Create the missing ProjectModels in the app first if that matters.';
    PRINT '';
END

-- ── 4. Report ─────────────────────────────────────────────────────────────────
DECLARE @NotInWorkbook  int = @UnitsInProject - @Matched;
DECLARE @NoModel        int = (SELECT COUNT(*) FROM #Match WHERE ProjectModelId IS NULL);
DECLARE @Sold           int = (SELECT COUNT(*) FROM #Match WHERE IsSold = 1);
DECLARE @ToCreate       int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 1);
DECLARE @HasPriceRow    int = (SELECT COUNT(*) FROM #Match m
                               WHERE EXISTS (SELECT 1 FROM appraisal.ProjectUnitPrices p WHERE p.ProjectUnitId = m.ProjectUnitId));

PRINT CONCAT('Units not in the workbook : ', @NotInWorkbook, '   (left untouched)');
PRINT CONCAT('Units to CREATE         : ', @ToCreate);
PRINT CONCAT('Price rows to UPDATE    : ', @HasPriceRow);
PRINT CONCAT('Price rows to INSERT    : ', @Supplied - @HasPriceRow);
PRINT '';
PRINT CONCAT('WARN units with ProjectModelId NULL : ', @NoModel, '   (excluded from ValuationAnalyses and Decision Summary)');
PRINT CONCAT('WARN units flagged IsSold = 1       : ', @Sold, '   (hidden from the Unit Price tab and the rollup)');
PRINT '';

IF @NotInWorkbook > 0
BEGIN
    PRINT 'Units in the project that the workbook does not price (first 20):';
    SELECT TOP (20) pu.SequenceNumber, pu.RoomNumber, pu.CondoRegistrationNumber, pu.TowerName, pu.Floor, pu.ModelType, pu.IsSold
    FROM appraisal.ProjectUnits pu
    WHERE pu.ProjectId = @ProjectId
      AND NOT EXISTS (SELECT 1 FROM #Match m WHERE m.ProjectUnitId = pu.Id)
    ORDER BY pu.SequenceNumber;
END

-- Floor gap (the parser's silent NULL, repaired by @FixFloor) vs floor disagreement (needs a human).
DECLARE @FloorNull     int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 0 AND DbFloor IS NULL AND Floor IS NOT NULL);
DECLARE @FloorMismatch int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 0 AND DbFloor IS NOT NULL AND Floor IS NOT NULL AND DbFloor <> Floor);
DECLARE @FloorToWrite  int = CASE @FixFloor WHEN 1 THEN @FloorNull
                                            WHEN 2 THEN @FloorNull + @FloorMismatch
                                            ELSE 0 END;

PRINT CONCAT('Units with Floor NULL that the workbook can fill : ', @FloorNull);
PRINT CONCAT('Units whose stored Floor disagrees with the file : ', @FloorMismatch);
PRINT CONCAT('Floor values this run would write               : ', @FloorToWrite);
IF @FloorNull > 0
    SELECT FloorInSheet = m.FloorText, WouldBecome = m.Floor, Units = COUNT(*)
    FROM #Match m WHERE m.IsNew = 0 AND m.DbFloor IS NULL AND m.Floor IS NOT NULL
    GROUP BY m.FloorText, m.Floor ORDER BY m.Floor;
IF @FloorMismatch > 0
    SELECT TOP (50) m.RoomNumber, FloorDb = m.DbFloor, FloorFile = m.Floor, FloorInSheet = m.FloorText
    FROM #Match m WHERE m.IsNew = 0 AND m.DbFloor IS NOT NULL AND m.Floor IS NOT NULL AND m.DbFloor <> m.Floor
    ORDER BY m.RoomNumber;
PRINT '';

-- Attribute diff. NOT written — a mismatch means the units came from a different revision of the
-- price list, and a price only means something against the area it was computed from.
DECLARE @Diffs int = (
    SELECT COUNT(*) FROM #Match m
    WHERE m.IsNew = 0
      AND (ISNULL(m.DbRoomNumber, N'~')    <> m.RoomNumber
       OR ISNULL(m.DbCondoReg, N'~')       <> m.CondoRegistrationNumber
       OR ISNULL(m.DbFloor, -1)            <> ISNULL(m.Floor, -1)
       OR ISNULL(m.DbTowerName, N'~')      <> ISNULL(m.TowerName, N'~')
       OR ISNULL(m.DbModelType, N'~')      <> ISNULL(m.ModelType, N'~')
       OR ISNULL(m.DbUsableArea, -1)       <> ISNULL(m.UsableArea, -1)
       OR ISNULL(m.DbSellingPrice, -1)     <> ISNULL(m.SellingPrice, -1)));

PRINT CONCAT('Units whose attributes differ from the workbook : ', @Diffs);
IF @Diffs > 0
BEGIN
    PRINT '  (reported only, not written. Floor is listed too, but is the one column @FixFloor may repair.)';
    SELECT TOP (50)
        m.RoomNumber, m.MatchedBy,
        RoomDb  = m.DbRoomNumber,     RoomFile  = m.RoomNumber,
        RegDb   = m.DbCondoReg,       RegFile   = m.CondoRegistrationNumber,
        FloorDb = m.DbFloor,          FloorFile = m.Floor,      FloorFileText = m.FloorText,
        TowerDb = m.DbTowerName,      TowerFile = m.TowerName,
        ModelDb = m.DbModelType,      ModelFile = m.ModelType,
        AreaDb  = m.DbUsableArea,     AreaFile  = m.UsableArea,
        SellDb  = m.DbSellingPrice,   SellFile  = m.SellingPrice
    FROM #Match m
    WHERE m.IsNew = 0
      AND (ISNULL(m.DbRoomNumber, N'~')  <> m.RoomNumber
       OR ISNULL(m.DbCondoReg, N'~')     <> m.CondoRegistrationNumber
       OR ISNULL(m.DbFloor, -1)          <> ISNULL(m.Floor, -1)
       OR ISNULL(m.DbTowerName, N'~')    <> ISNULL(m.TowerName, N'~')
       OR ISNULL(m.DbModelType, N'~')    <> ISNULL(m.ModelType, N'~')
       OR ISNULL(m.DbUsableArea, -1)     <> ISNULL(m.UsableArea, -1)
       OR ISNULL(m.DbSellingPrice, -1)   <> ISNULL(m.SellingPrice, -1))
    ORDER BY m.RoomNumber;
END
PRINT '';

-- Existing price rows whose values this run would change.
DECLARE @PriceChanges int = (
    SELECT COUNT(*) FROM #Match m
    JOIN appraisal.ProjectUnitPrices p ON p.ProjectUnitId = m.ProjectUnitId
    WHERE ISNULL(p.StandardPrice, -1)              <> m.PricePerSqm
       OR ISNULL(p.TotalAppraisalValue, -1)        <> m.AppraisalValue
       OR ISNULL(p.TotalAppraisalValueRounded, -1) <> m.AppraisalValue
       OR ISNULL(p.ForceSellingPrice, -1)          <> m.ForceSellingPrice
       OR ISNULL(p.CoverageAmount, -1)             <> m.CoverageAmount);

PRINT CONCAT('Existing price rows that would change : ', @PriceChanges);
IF @PriceChanges > 0
    SELECT TOP (50)
        m.RoomNumber,
        StdBefore = p.StandardPrice,              StdAfter = m.PricePerSqm,
        ValBefore = p.TotalAppraisalValueRounded, ValAfter = m.AppraisalValue,
        FsvBefore = p.ForceSellingPrice,          FsvAfter = m.ForceSellingPrice,
        CovBefore = p.CoverageAmount,             CovAfter = m.CoverageAmount
    FROM #Match m
    JOIN appraisal.ProjectUnitPrices p ON p.ProjectUnitId = m.ProjectUnitId
    WHERE ISNULL(p.StandardPrice, -1)              <> m.PricePerSqm
       OR ISNULL(p.TotalAppraisalValue, -1)        <> m.AppraisalValue
       OR ISNULL(p.TotalAppraisalValueRounded, -1) <> m.AppraisalValue
       OR ISNULL(p.ForceSellingPrice, -1)          <> m.ForceSellingPrice
       OR ISNULL(p.CoverageAmount, -1)             <> m.CoverageAmount
    ORDER BY m.RoomNumber;
PRINT '';

-- ── 5. The appraisal-level summary ────────────────────────────────────────────
-- Reproduces AppraisalValuationSummaryService.RecomputeAsync's block branch against the state the
-- database WILL be in once this run's price writes land, so @Apply = 0 predicts what @Apply = 1
-- stores. #Post = every unit the rollup will count: the workbook rows (with their new figures) plus
-- the project's other units at whatever their existing price row holds. Membership is the app's:
-- unsold, a ProjectModel of this appraisal's project (INNER JOIN), and a price row.
SELECT ProjectUnitId     = m.ProjectUnitId,
       ProjectModelId    = m.ProjectModelId,
       AppraisalValue    = m.AppraisalValue,
       CoverageAmount    = m.CoverageAmount,
       ForceSellingPrice = m.ForceSellingPrice
INTO #Post
FROM #Match m
JOIN appraisal.ProjectModels pm ON pm.Id = m.ProjectModelId
JOIN appraisal.Projects p       ON p.Id  = pm.ProjectId
WHERE p.AppraisalId = @ProjectAppraisalId AND m.IsSold = 0

UNION ALL

SELECT pu.Id, pu.ProjectModelId,
       ISNULL(pup.TotalAppraisalValueRounded, 0),
       ISNULL(pup.CoverageAmount, 0),
       ISNULL(pup.ForceSellingPrice, 0)
FROM appraisal.ProjectUnits pu
JOIN appraisal.ProjectModels pm      ON pm.Id = pu.ProjectModelId
JOIN appraisal.Projects p            ON p.Id  = pm.ProjectId
JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = pu.Id
WHERE p.AppraisalId = @ProjectAppraisalId
  AND pu.IsSold = 0
  AND NOT EXISTS (SELECT 1 FROM #Match m WHERE m.ProjectUnitId = pu.Id);

DECLARE @RollupValue     decimal(18,2);
DECLARE @RollupInsurance decimal(18,2);
DECLARE @RollupUnits     int;
SELECT @RollupValue     = ISNULL(SUM(AppraisalValue), 0),
       @RollupInsurance = ISNULL(SUM(CoverageAmount), 0),
       @RollupUnits     = COUNT(*)
FROM #Post;

-- ForceSaleRateResolver's chain, in its order.
DECLARE @ForceSaleRate decimal(18,6);
DECLARE @RateSource    varchar(40);

SELECT @ForceSaleRate = va.ForceSaleRate
FROM appraisal.ValuationAnalyses va WHERE va.AppraisalId = @ProjectAppraisalId;
IF @ForceSaleRate IS NOT NULL SET @RateSource = 'appraisal override';

IF @ForceSaleRate IS NULL
BEGIN
    SELECT TOP (1) @ForceSaleRate = ppa.ForceSalePercentage
    FROM appraisal.Projects p
    JOIN appraisal.ProjectPricingAssumptions ppa ON ppa.ProjectId = p.Id
    WHERE p.AppraisalId = @ProjectAppraisalId
    ORDER BY ppa.Id;
    IF @ForceSaleRate IS NOT NULL SET @RateSource = 'project pricing assumption';
END

IF @ForceSaleRate IS NULL
BEGIN
    SELECT @ForceSaleRate = TRY_CONVERT(decimal(18,6), sc.Value)
    FROM common.SystemConfigurations sc
    WHERE sc.[Key] = N'ForceSaleRateDefaultPct' AND sc.IsActive = 1;
    IF @ForceSaleRate IS NOT NULL SET @RateSource = 'SystemConfigurations default';
END

IF @ForceSaleRate IS NULL OR @ForceSaleRate <= 0 OR @ForceSaleRate > 100
BEGIN
    SET @ForceSaleRate = 70;
    SET @RateSource = 'hardcoded 70 fallback';
END

DECLARE @NewAppraised   decimal(18,2)  = @RollupValue;                                -- not rounded, as in UpdateSummary
DECLARE @NewInsurance   decimal(18,2)  = ROUND(@RollupInsurance / 1000.0, 0) * 1000;  -- half-away-from-zero, as the app

-- Three forced-sale figures that do not agree; see PatchProjectUnitPricesFromApprovedList.sql.
--   @FsvFromRate    what the app stores and re-derives (@ForcedSaleFrom = 1, stable)
--   @FsvFromUnitSum the workbook's own column (@ForcedSaleFrom = 2; the next touch of the rate on
--                   the Decision Summary screen silently converts it back to @FsvFromRate)
--   @FsvFromModels  not selectable — what the Decision Summary SCREEN displays
DECLARE @FsvFromRate    decimal(18,2) =
    ROUND(CAST(@RollupValue AS decimal(38,6)) * @ForceSaleRate / 100.0 / 1000.0, 0) * 1000;
DECLARE @FsvFromUnitSum decimal(18,2) = (SELECT ISNULL(SUM(ForceSellingPrice), 0) FROM #Post);
DECLARE @FsvFromModels  decimal(18,2);
SELECT @FsvFromModels = ISNULL(SUM(ROUND(CAST(mt.ModelTotal AS decimal(38,6)) * @ForceSaleRate / 100.0 / 1000.0, 0) * 1000), 0)
FROM (SELECT ProjectModelId, ModelTotal = SUM(AppraisalValue) FROM #Post GROUP BY ProjectModelId) mt;

DECLARE @NewForcedSale decimal(18,2) =
    CASE @ForcedSaleFrom WHEN 2 THEN @FsvFromUnitSum ELSE @FsvFromRate END;

-- ValuationApproach: one distinct selected approach across the ProjectModel analyses, else 'Combined'.
DECLARE @Approach      nvarchar(100);
DECLARE @ApproachCount int;
SELECT @ApproachCount = COUNT(DISTINCT paa.ApproachType),
       @Approach      = MIN(paa.ApproachType)
FROM appraisal.PricingAnalysis pa
JOIN appraisal.ProjectModels pm  ON pm.Id  = pa.AnchorId
JOIN appraisal.Projects p        ON p.Id   = pm.ProjectId
JOIN appraisal.PricingAnalysisApproaches paa ON paa.PricingAnalysisId = pa.Id
WHERE p.AppraisalId = @ProjectAppraisalId AND pa.SubjectType = 1 AND paa.IsSelected = 1;
IF ISNULL(@ApproachCount, 0) <> 1 SET @Approach = N'Combined';

-- ValuationDate: never overwritten; only seeded for a new row from a real appointment.
DECLARE @VaId            uniqueidentifier;
DECLARE @VaDate          datetime2;
DECLARE @VaBefore        decimal(18,2);
DECLARE @SeedDate        datetime2;
DECLARE @SummaryAction   varchar(20);

SELECT @VaId = va.Id, @VaDate = va.ValuationDate, @VaBefore = va.AppraisedValue
FROM appraisal.ValuationAnalyses va WHERE va.AppraisalId = @ProjectAppraisalId;

IF @VaId IS NOT NULL
    SET @SummaryAction = 'UPDATE';
ELSE
BEGIN
    SELECT @SeedDate = MAX(ap.AppointmentDateTime)
    FROM appraisal.AppraisalAssignments aa
    JOIN appraisal.Appointments ap ON ap.AssignmentId = aa.Id
    WHERE aa.AppraisalId = @ProjectAppraisalId AND ap.Status <> 'Cancelled';

    SET @SummaryAction = CASE WHEN @SeedDate IS NOT NULL THEN 'INSERT' ELSE 'SKIP (no date)' END;
END

PRINT 'appraisal.ValuationAnalyses';
PRINT CONCAT('  units counted in the rollup  : ', @RollupUnits, ' of ', @UnitsInProject + @ToCreate,
             ' (unsold, with a model, and priced)');
PRINT CONCAT('  appraised value              : ', ISNULL(CONVERT(varchar(40), @VaBefore), '(no row)'), '  ->  ', @NewAppraised);
PRINT CONCAT('  force-sale rate              : ', @ForceSaleRate, '%  from ', @RateSource);
PRINT CONCAT('  forced sale, total x rate    : ', @FsvFromRate,
             CASE WHEN @ForcedSaleFrom = 1 THEN '   <-- storing this' ELSE '' END);
PRINT CONCAT('  forced sale, SUM of units    : ', @FsvFromUnitSum,
             CASE WHEN @ForcedSaleFrom = 2 THEN '   <-- storing this' ELSE '' END);
PRINT CONCAT('  forced sale, Decision Summary screen shows : ', @FsvFromModels);
PRINT CONCAT('  valuation approach           : ', @Approach);
PRINT CONCAT('  action                       : ',
             CASE WHEN @SyncValuationSummary = 0 THEN 'disabled (@SyncValuationSummary = 0)' ELSE @SummaryAction END);
IF @SummaryAction = 'SKIP (no date)'
    PRINT '  ^ no ValuationAnalyses row and no non-cancelled appointment to date one from. The summary'
        + ' will be left alone rather than stamping today on the appraisal.';
DECLARE @WorkbookTotal decimal(18,2) = (SELECT SUM(AppraisalValue) FROM #Src);
IF @RollupValue <> @WorkbookTotal
    PRINT CONCAT('  WARN the rollup (', @RollupValue, ') differs from the workbook total (',
                 @WorkbookTotal, ') - sold units, units without a model,',
                 ' or priced units outside the workbook. See the counts above.');

SELECT
    AppraisedValueBefore  = va.AppraisedValue,    AppraisedValueAfter  = @NewAppraised,
    ForcedSaleValueBefore = va.ForcedSaleValue,   ForcedSaleValueAfter = @NewForcedSale,
    InsuranceValueBefore  = va.InsuranceValue,    InsuranceValueAfter  = @NewInsurance,
    ApproachBefore        = va.ValuationApproach, ApproachAfter        = @Approach,
    ValuationDateUsed     = COALESCE(va.ValuationDate, @SeedDate),
    WorkbookTotalAllUnits = (SELECT SUM(AppraisalValue) FROM #Src)
FROM (SELECT 1 AS OneRow) d
LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = @ProjectAppraisalId;
PRINT '';

-- ── 6. The workflow routing value ─────────────────────────────────────────────
-- Mirrors AppraisalValueChangedIntegrationEventConsumer: the instance is found by CorrelationId =
-- the summarised appraisal's RequestId (stored lowercase — the LOWER() is load-bearing), and
-- queue rows by AppraisalId. See PatchWorkflowAppraisalValueForTierRouting.sql.
DECLARE @RequestId      uniqueidentifier;
DECLARE @FacilityLimit  decimal(18,2);
SELECT @RequestId = a.RequestId, @FacilityLimit = a.FacilityLimit
FROM appraisal.Appraisals a WHERE a.Id = @ProjectAppraisalId;

DECLARE @Correlation    varchar(36) = LOWER(CONVERT(varchar(36), @RequestId));
DECLARE @InstanceCount  int = (SELECT COUNT(*) FROM workflow.WorkflowInstances wi WHERE wi.CorrelationId = @Correlation);
DECLARE @InstanceId     uniqueidentifier;
DECLARE @WfStatus       nvarchar(50);
DECLARE @CurrentAct     nvarchar(200);
DECLARE @VersionId      uniqueidentifier;
DECLARE @VersionNo      int;
DECLARE @Schema         nvarchar(max);
DECLARE @SwitchExpr     nvarchar(200);
DECLARE @ThreshExpr     nvarchar(200);
DECLARE @VarsJson       nvarchar(max);
DECLARE @StoredRaw      nvarchar(4000);
DECLARE @StoredType     tinyint;
DECLARE @LastSwitchCase nvarchar(50);
DECLARE @WorkflowWrite  bit = 0;
DECLARE @Verdict        nvarchar(400);
DECLARE @QueueToChange  int = (SELECT COUNT(*) FROM workflow.MeetingQueueItems q
                               WHERE q.AppraisalId = @ProjectAppraisalId AND q.Status <> N'Released'
                                 AND q.AppraisalValue <> @NewAppraised);

PRINT 'Workflow';
IF @InstanceCount = 0
    PRINT '  no workflow instance for this request - nothing to route (the app''s consumer would skip too).';

IF @InstanceCount > 1
BEGIN
    PRINT CONCAT('  ', @InstanceCount, ' workflow instances share this CorrelationId - resolve by hand, or set @PatchWorkflow = 0.');
    SELECT wi.Id, wi.Status, wi.CurrentActivityId, wi.StartedOn
    FROM workflow.WorkflowInstances wi WHERE wi.CorrelationId = @Correlation;
END

IF @InstanceCount = 1
BEGIN
    SELECT @InstanceId = wi.Id, @WfStatus = wi.Status, @CurrentAct = wi.CurrentActivityId,
           @VersionId  = wi.WorkflowDefinitionVersionId, @VarsJson = wi.Variables
    FROM workflow.WorkflowInstances wi WHERE wi.CorrelationId = @Correlation;

    SELECT @VersionNo = v.Version, @Schema = v.JsonSchema
    FROM workflow.WorkflowDefinitionVersions v WHERE v.Id = @VersionId;

    -- Read by JSON path, never by LIKE (the stored definition HTML-escapes '<' and '>'). Two shapes
    -- exist: bare { activities } and the seeder-wrapped { workflowSchema: { activities } }.
    IF ISJSON(@Schema) = 1
    BEGIN
        SELECT TOP (1) @SwitchExpr = JSON_VALUE(a.value, '$.properties.expression')
        FROM (SELECT value FROM OPENJSON(@Schema, '$.activities')
              UNION ALL
              SELECT value FROM OPENJSON(@Schema, '$.workflowSchema.activities')) a
        WHERE JSON_VALUE(a.value, '$.id') = N'approval-tier-switch';

        SELECT TOP (1) @ThreshExpr = JSON_VALUE(a.value, '$.properties.memberSource.valueExpression')
        FROM (SELECT value FROM OPENJSON(@Schema, '$.activities')
              UNION ALL
              SELECT value FROM OPENJSON(@Schema, '$.workflowSchema.activities')) a
        WHERE JSON_VALUE(a.value, '$.id') = N'pending-approval';
    END

    -- Type matters as much as value: a quoted "123" does not compare numerically in the switch.
    -- OPENJSON type 2 = number.
    IF ISJSON(@VarsJson) = 1
        SELECT @StoredRaw = j.[value], @StoredType = j.[type]
        FROM OPENJSON(@VarsJson) j WHERE j.[key] = N'appraisalValue';

    SELECT TOP (1) @LastSwitchCase = CASE WHEN ISJSON(e.OutputData) = 1
                                          THEN JSON_VALUE(e.OutputData, '$.case') END
    FROM workflow.WorkflowActivityExecutions e
    WHERE e.WorkflowInstanceId = @InstanceId AND e.ActivityId = N'approval-tier-switch'
    ORDER BY e.StartedOn DESC;

    SET @WorkflowWrite = CASE WHEN ISJSON(@VarsJson) = 1
                               AND (ISNULL(@StoredType, 255) <> 2
                                    OR TRY_CONVERT(decimal(18,2), @StoredRaw) IS NULL
                                    OR TRY_CONVERT(decimal(18,2), @StoredRaw) <> @NewAppraised)
                              THEN 1 ELSE 0 END;

    SET @Verdict = CASE
        WHEN ISNULL(@SwitchExpr, N'') <> N'appraisalValue' OR ISNULL(@ThreshExpr, N'') <> N'appraisalValue'
            THEN CONCAT(N'NO EFFECT ON ROUTING - pinned workflow version ', @VersionNo, N' routes on ''',
                        ISNULL(@SwitchExpr, N'?'), N''' / ''', ISNULL(@ThreshExpr, N'?'),
                        N''', not appraisalValue. Migrating the instance to v4+ is a business decision.')
        WHEN @WfStatus IN (N'Completed', N'Cancelled', N'Failed')
            THEN CONCAT(N'NO EFFECT - the instance is ', @WfStatus, N'.')
        WHEN @CurrentAct = N'pending-meeting'
            THEN N'OK - already waiting for a meeting; its meeting-queue value is refreshed.'
        WHEN @NewAppraised <= @MeetingCutoff
            THEN N'NO MEETING - the new value is not above 30,000,000; the switch will go direct to committee.'
        WHEN @CurrentAct = N'pending-approval' AND @LastSwitchCase = N'<= 30000000'
            THEN N'TOO LATE - it went straight to committee on the old value and the roster is fixed. A committee route_back is needed; the switch then re-runs and reads this value.'
        WHEN @CurrentAct = N'pending-approval'
            THEN N'OK - it came through a meeting; the committee is already voting.'
        ELSE N'OK - approval-tier-switch has not run yet (or will run again) and will read this value -> meeting.'
    END;

    PRINT CONCAT('  instance                     : ', @InstanceId, '  ', @WfStatus, ' @ ', ISNULL(@CurrentAct, '(none)'));
    PRINT CONCAT('  pinned version / routes on   : ', ISNULL(CONVERT(varchar(10), @VersionNo), '?'), ' / ', ISNULL(@SwitchExpr, '(not found)'));
    PRINT CONCAT('  appraisalValue stored now    : ', ISNULL(@StoredRaw, '(absent)'),
                 CASE WHEN @StoredType = 1 THEN '  (a STRING - will be rewritten as a number)' ELSE '' END);
    PRINT CONCAT('  appraisalValue after         : ', @NewAppraised,
                 CASE WHEN @WorkflowWrite = 1 THEN '' ELSE '  (already stored - no write)' END);
    IF ISJSON(@VarsJson) = 0
        PRINT '  WARN WorkflowInstances.Variables is not valid JSON - the workflow step will refuse to run.';
    PRINT CONCAT('  verdict                      : ', @Verdict);

    SELECT AppraisalNumber   = @AppraisalNumber,
           InstanceStatus    = @WfStatus,
           CurrentActivity   = @CurrentAct,
           PinnedVersion     = @VersionNo,
           RoutingExpression = @SwitchExpr,
           FacilityLimit     = @FacilityLimit,
           StoredInWorkflow  = @StoredRaw,
           NewAppraisalValue = @NewAppraised,
           LastSwitchCase    = @LastSwitchCase,
           NewRouting        = CASE WHEN @NewAppraised > @MeetingCutoff THEN N'via meeting' ELSE N'direct to committee' END,
           NewCommittee      = CASE WHEN @NewAppraised <= @SubMax  THEN N'SUB_COMMITTEE'
                                    WHEN @NewAppraised <= @CommMax THEN N'COMMITTEE'
                                    ELSE N'COMMITTEE_WITH_MEETING' END,
           Verdict           = @Verdict;
END

PRINT CONCAT('  live meeting-queue rows to refresh : ', @QueueToChange);
SELECT q.Id, q.AppraisalNo, q.Status, q.MeetingId,
       CurrentQueueValue = q.AppraisalValue, NewQueueValue = @NewAppraised,
       WillChange = CASE WHEN q.AppraisalValue <> @NewAppraised THEN 'yes' ELSE 'no' END
FROM workflow.MeetingQueueItems q
WHERE q.AppraisalId = @ProjectAppraisalId AND q.Status <> N'Released';
PRINT '';

-- ── 7. Write ──────────────────────────────────────────────────────────────────
IF @Apply = 0
BEGIN
    PRINT 'REPORT ONLY — nothing was written. Set @Apply = 1 to apply.';
    RETURN;
END

-- Workflow blockers are checked here rather than in the report, so a dry run always prints in full.
-- The project belongs to an ANCESTOR appraisal: the app would push the value to the ancestor's
-- workflow, not to the one for the number given, so the meeting goal would quietly miss. Ask first.
IF @PatchWorkflow = 1 AND @IsSameAppraisal = 0
    THROW 50019, 'The project is owned by an ancestor appraisal, so the workflow patched would be the ancestor''s, not this appraisal''s. Confirm with the business, or set @PatchWorkflow = 0. Nothing was written.', 1;
IF @PatchWorkflow = 1 AND @InstanceCount > 1
    THROW 50013, 'Several workflow instances share this CorrelationId. Resolve by hand, or set @PatchWorkflow = 0. Nothing was written.', 1;
IF @PatchWorkflow = 1 AND @SummaryAction NOT IN ('UPDATE', 'INSERT')
    THROW 50015, 'The valuation summary will not be written (no row, no appointment date), so there is no stored value to push to the workflow. Set @PatchWorkflow = 0 to load the prices alone. Nothing was written.', 1;
IF @PatchWorkflow = 1 AND @InstanceId IS NOT NULL AND ISJSON(@VarsJson) = 0
    THROW 50017, 'WorkflowInstances.Variables is not valid JSON; refusing to JSON_MODIFY it. Nothing was written.', 1;

BEGIN TRAN;

-- Units first: the price rows below FK to them. UploadBatchId gets the same system sentinel the
-- bank-file load used, so script-created rows stay identifiable.
DECLARE @UnitsCreated int = 0;
IF @CreateMissingUnits = 1 AND EXISTS (SELECT 1 FROM #Match WHERE IsNew = 1)
BEGIN
    INSERT appraisal.ProjectUnits
        (Id, ProjectId, UploadBatchId, SequenceNumber, RoomNumber, CondoRegistrationNumber, TowerName,
         ModelType, Floor, UsableArea, SellingPrice, ProjectTowerId, ProjectModelId, IsSold,
         CreatedAt, CreatedBy)
    SELECT m.ProjectUnitId, @ProjectId, '00000000-0000-0000-0000-0000CA5B0001', m.SequenceNumber,
           m.RoomNumber, m.CondoRegistrationNumber, m.TowerName,
           m.ModelType, m.Floor, m.UsableArea, m.SellingPrice, m.ProjectTowerId, m.ProjectModelId, 0,
           SYSDATETIME(), 'SYSTEM'
    FROM #Match m
    WHERE m.IsNew = 1;

    SET @UnitsCreated = @@ROWCOUNT;
END

DECLARE @FloorWritten int = 0;
IF @FixFloor > 0
BEGIN
    UPDATE pu
    SET pu.Floor       = m.Floor,
        pu.UpdatedAt   = SYSDATETIME(),
        pu.UpdatedBy   = 'SYSTEM'
    FROM appraisal.ProjectUnits pu
    JOIN #Match m ON m.ProjectUnitId = pu.Id AND m.IsNew = 0
    WHERE m.Floor IS NOT NULL
      AND (pu.Floor IS NULL OR (@FixFloor = 2 AND pu.Floor <> m.Floor));

    SET @FloorWritten = @@ROWCOUNT;
END

UPDATE p
SET p.StandardPrice              = m.PricePerSqm,
    p.TotalAppraisalValue        = m.AppraisalValue,
    p.TotalAppraisalValueRounded = m.AppraisalValue,
    p.ForceSellingPrice          = m.ForceSellingPrice,
    p.CoverageAmount             = m.CoverageAmount,
    p.UpdatedAt                  = SYSDATETIME(),
    p.UpdatedBy                  = 'SYSTEM'
FROM appraisal.ProjectUnitPrices p
JOIN #Match m ON m.ProjectUnitId = p.ProjectUnitId;

DECLARE @Updated int = @@ROWCOUNT;

INSERT appraisal.ProjectUnitPrices
    (Id, ProjectUnitId, IsCorner, IsEdge, IsOther, IsPoolView, IsSouth, IsNearGarden,
     StandardPrice, TotalAppraisalValue, TotalAppraisalValueRounded,
     ForceSellingPrice, CoverageAmount, CreatedAt, CreatedBy)
SELECT NEWID(), m.ProjectUnitId, 0, 0, 0, 0, 0, 0,
       m.PricePerSqm, m.AppraisalValue, m.AppraisalValue,
       m.ForceSellingPrice, m.CoverageAmount, SYSDATETIME(), 'SYSTEM'
FROM #Match m
WHERE NOT EXISTS (SELECT 1 FROM appraisal.ProjectUnitPrices p WHERE p.ProjectUnitId = m.ProjectUnitId);

DECLARE @Inserted int = @@ROWCOUNT;

-- Re-read the rollup from the tables. It must equal what the report printed, or everything rolls back.
DECLARE @PostValue     decimal(18,2);
DECLARE @PostInsurance decimal(18,2);
DECLARE @PostForcedSum decimal(18,2);
SELECT @PostValue     = ISNULL(SUM(pup.TotalAppraisalValueRounded), 0),
       @PostInsurance = ISNULL(SUM(pup.CoverageAmount), 0),
       @PostForcedSum = ISNULL(SUM(pup.ForceSellingPrice), 0)
FROM appraisal.ProjectUnits pu
JOIN appraisal.ProjectUnitPrices pup ON pup.ProjectUnitId = pu.Id
JOIN appraisal.ProjectModels pm      ON pm.Id = pu.ProjectModelId
JOIN appraisal.Projects p            ON p.Id  = pm.ProjectId
WHERE p.AppraisalId = @ProjectAppraisalId AND pu.IsSold = 0;

IF @PostValue <> @RollupValue OR @PostInsurance <> @RollupInsurance OR @PostForcedSum <> @FsvFromUnitSum
BEGIN
    PRINT CONCAT('Predicted ', @RollupValue, ' / ', @RollupInsurance, ' / ', @FsvFromUnitSum,
                 ' but the tables now hold ', @PostValue, ' / ', @PostInsurance, ' / ', @PostForcedSum, '.');
    THROW 50009, 'The post-write rollup does not match what the report predicted. Everything is rolled back.', 1;
END

DECLARE @SummaryWritten varchar(20) = 'not written';

IF @SyncValuationSummary = 1 AND @SummaryAction = 'UPDATE'
BEGIN
    -- ValuationDate is deliberately absent from this SET list.
    UPDATE appraisal.ValuationAnalyses
    SET ValuationApproach = @Approach,
        AppraisedValue    = @NewAppraised,
        ForcedSaleValue   = @NewForcedSale,
        InsuranceValue    = @NewInsurance,
        UpdatedAt         = SYSDATETIME(),
        UpdatedBy         = 'SYSTEM'
    WHERE Id = @VaId;

    SET @SummaryWritten = 'updated';
END
ELSE IF @SyncValuationSummary = 1 AND @SummaryAction = 'INSERT'
BEGIN
    -- Id is omitted on purpose: the column carries a newsequentialid() default.
    INSERT appraisal.ValuationAnalyses
        (AppraisalId, ValuationApproach, ValuationDate, AppraisedValue, ForcedSaleValue,
         InsuranceValue, Currency, CreatedAt, CreatedBy)
    VALUES
        (@ProjectAppraisalId, @Approach, @SeedDate, @NewAppraised, @NewForcedSale,
         @NewInsurance, N'THB', SYSDATETIME(), 'SYSTEM');

    SET @SummaryWritten = 'inserted';
END

-- The consumer's two effects. JSON_MODIFY with a decimal-typed value emits a BARE JSON NUMBER,
-- exactly what the consumer writes; a string or a float cast would break the switch's comparison.
DECLARE @WfWritten    int = 0;
DECLARE @QueueWritten int = 0;
IF @PatchWorkflow = 1
BEGIN
    IF @InstanceId IS NOT NULL AND @WorkflowWrite = 1
    BEGIN
        UPDATE workflow.WorkflowInstances
        SET Variables = JSON_MODIFY(Variables, '$.appraisalValue', @NewAppraised)
        WHERE Id = @InstanceId;

        SET @WfWritten = @@ROWCOUNT;

        IF NOT EXISTS (SELECT 1
                       FROM workflow.WorkflowInstances wi
                       CROSS APPLY OPENJSON(wi.Variables) j
                       WHERE wi.Id = @InstanceId
                         AND j.[key] = N'appraisalValue'
                         AND j.[type] = 2
                         AND TRY_CONVERT(decimal(18,2), j.[value]) = @NewAppraised)
            THROW 50018, 'appraisalValue did not land as a JSON number equal to the new total. Everything is rolled back.', 1;
    END

    UPDATE workflow.MeetingQueueItems
    SET AppraisalValue = @NewAppraised
    WHERE AppraisalId = @ProjectAppraisalId
      AND Status <> N'Released'
      AND AppraisalValue <> @NewAppraised;

    SET @QueueWritten = @@ROWCOUNT;
END

COMMIT;

PRINT CONCAT('Applied: ', @UnitsCreated, ' units created, ', @Updated, ' price rows updated, ',
             @Inserted, ' price rows inserted, ', @FloorWritten, ' unit floors repaired.');
PRINT CONCAT('ValuationAnalyses: ', @SummaryWritten,
             CASE WHEN @SummaryWritten = 'not written' THEN '' ELSE CONCAT(
                  '  appraised=', @NewAppraised,
                  '  forcedSale=', @NewForcedSale,
                  '  insurance=', @NewInsurance) END);
IF @PatchWorkflow = 1
BEGIN
    PRINT CONCAT('Workflow: appraisalValue ', CASE WHEN @WfWritten = 1 THEN 'written' ELSE 'unchanged' END,
                 ', ', @QueueWritten, ' meeting-queue row(s) refreshed.');
    PRINT CONCAT('Verdict : ', ISNULL(@Verdict, '(no single workflow instance)'));
END
ELSE
    PRINT 'Workflow was NOT touched (@PatchWorkflow = 0).';
PRINT 'Reminder: do NOT press Calculate on the Unit Price tab, and do NOT re-upload the units Excel.';

-- What actually landed. RawFragment must read "appraisalValue":<number> with NO quotes.
IF @PatchWorkflow = 1 AND @InstanceId IS NOT NULL
    SELECT InstanceId  = wi.Id,
           StoredNow   = JSON_VALUE(wi.Variables, '$.appraisalValue'),
           RawFragment = SUBSTRING(wi.Variables, CHARINDEX('"appraisalValue"', wi.Variables), 45)
    FROM workflow.WorkflowInstances wi
    WHERE wi.Id = @InstanceId;

DROP TABLE #Post;
DROP TABLE #Match;
DROP TABLE #Src;
