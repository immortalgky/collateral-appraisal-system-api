-- ============================================================
-- Appraisal 69001114 — load the committee-approved per-unit prices of a block condo
-- project into appraisal.ProjectUnitPrices, refresh appraisal.ValuationAnalyses, and push the new
-- appraised value into the workflow so the approval-tier switch sends it through a meeting.
-- Schemas: appraisal, workflow
-- Run by hand. NOT a DbUp migration: it targets one appraisal on one environment.
--
-- This is PatchProjectUnitPricesFromApprovedList.sql and PatchWorkflowAppraisalValueForTierRouting.sql
-- merged into one run, so the prices, the summary and the workflow value land in ONE transaction.
-- Those two headers carry the full reasoning; this one records what is specific to this run.
--
-- SOURCE: "69001114.xlsx", Sheet1, 580 unit rows. The sheet's trailing totals row is
-- dropped; the staged appraisal total is checked against it below. Customer data — do not commit.
-- Expected sums over the workbook rows:
--   ราคาประเมินที่เสนอขออนุมัติ (TotalAppraisalValueRounded) = 3019811540.00
--   มูลค่าประกันอัคคีภัย          (CoverageAmount)            = 617900000.00
--   ราคาบังคับขาย               (ForceSellingPrice)         = 2113880000.00
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
-- FLOORS. The tower skips 13 and the sheet prices floor 12A as the 13th (price per sq.m. steps up continuously 12 -> 12A -> 14), so 12A is staged as Floor 13. FloorText keeps '12A'. Leading zeros ('07') are dropped.
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
DECLARE @AppraisalNumber nvarchar(50) = N'69001114';  -- fixed: the workbook below is this appraisal's
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
(N'A07A101', N'656/2', N'A', N'07', 7, N'1 Bed S', 29.63, 4355000.00, 130000.00, 3851900.00, 2696000.00, 889000.00),
(N'A07A202', N'656/3', N'A', N'07', 7, N'1 Bed S', 30.60, 4586250.00, 133000.00, 4069800.00, 2849000.00, 918000.00),
(N'A07D103', N'656/4', N'A', N'07', 7, N'2 Bed 1 Bath', 42.50, 5975000.00, 130000.00, 5525000.00, 3868000.00, 1275000.00),
(N'A07B2M04', N'656/5', N'A', N'07', 7, N'1 Bed M', 31.91, 3978750.00, 120000.00, 3829200.00, 2680000.00, 957000.00),
(N'A07B105', N'656/6', N'A', N'07', 7, N'1 Bed M', 32.16, 4320000.00, 120000.00, 3859200.00, 2701000.00, 965000.00),
(N'A07B1M06', N'656/7', N'A', N'07', 7, N'1 Bed M', 32.34, 4320000.00, 120000.00, 3880800.00, 2717000.00, 970000.00),
(N'A07D1M07', N'656/8', N'A', N'07', 7, N'2 Bed 1 Bath', 42.51, 6083750.00, 133500.00, 5675085.00, 3973000.00, 1275000.00),
(N'A07C208', N'656/9', N'A', N'07', 7, N'1 Bed Plus', 35.26, 5002500.00, 134000.00, 4724840.00, 3307000.00, 1058000.00),
(N'A07B109', N'656/10', N'A', N'07', 7, N'1 Bed M', 32.47, 4586250.00, 128500.00, 4172395.00, 2921000.00, 974000.00),
(N'A07B1M10', N'656/11', N'A', N'07', 7, N'1 Bed M', 32.39, 4647500.00, 128500.00, 4162115.00, 2913000.00, 972000.00),
(N'A07C111', N'656/12', N'A', N'07', 7, N'1 Bed L', 35.24, 5207500.00, 134000.00, 4722160.00, 3306000.00, 1057000.00),
(N'A07C112', N'656/14', N'A', N'07', 7, N'1 Bed L', 35.28, 5207500.00, 134000.00, 4727520.00, 3309000.00, 1058000.00),
(N'A07C213', N'656/15', N'A', N'07', 7, N'1 Bed Plus', 35.33, 5180000.00, 134000.00, 4734220.00, 3314000.00, 1060000.00),
(N'A07C214', N'656/16', N'A', N'07', 7, N'1 Bed Plus', 35.28, 5207500.00, 134000.00, 4727520.00, 3309000.00, 1058000.00),
(N'A07C215', N'656/17', N'A', N'07', 7, N'1 Bed Plus', 35.28, 5207500.00, 134000.00, 4727520.00, 3309000.00, 1058000.00),
(N'A07C216', N'656/18', N'A', N'07', 7, N'1 Bed Plus', 35.28, 5207500.00, 134000.00, 4727520.00, 3309000.00, 1058000.00),
(N'A07C217', N'656/19', N'A', N'07', 7, N'1 Bed Plus', 35.32, 5138750.00, 134000.00, 4732880.00, 3313000.00, 1060000.00),
(N'A07C218', N'656/20', N'A', N'07', 7, N'1 Bed Plus', 35.32, 5125000.00, 134000.00, 4732880.00, 3313000.00, 1060000.00),
(N'A07E119', N'656/21', N'A', N'07', 7, N'2 Bed 2 Bath', 58.16, 9280000.00, 153500.00, 8927560.00, 6249000.00, 1745000.00),
(N'A07E220', N'656/22', N'A', N'07', 7, N'2 Bed 2 Bath', 58.04, 9125000.00, 150000.00, 8706000.00, 6094000.00, 1741000.00),
(N'A07B1M21', N'656/23', N'A', N'07', 7, N'1 Bed M', 31.59, 4402500.00, 120000.00, 3790800.00, 2654000.00, 948000.00),
(N'A07B222', N'656/24', N'A', N'07', 7, N'1 Bed M', 31.94, 4435000.00, 120000.00, 3832800.00, 2683000.00, 958000.00),
(N'A07B2M23', N'656/25', N'A', N'07', 7, N'1 Bed M', 31.97, 4475000.00, 120000.00, 3836400.00, 2685000.00, 959000.00),
(N'A07B124', N'656/26', N'A', N'07', 7, N'1 Bed M', 32.16, 4475000.00, 120000.00, 3859200.00, 2701000.00, 965000.00),
(N'A07B1M25', N'656/27', N'A', N'07', 7, N'1 Bed M', 32.17, 4475000.00, 120000.00, 3860400.00, 2702000.00, 965000.00),
(N'A07C2M26', N'656/28', N'A', N'07', 7, N'1 Bed Plus', 35.03, 5116250.00, 134000.00, 4694020.00, 3286000.00, 1051000.00),
(N'A07C2M27', N'656/29', N'A', N'07', 7, N'1 Bed Plus', 35.06, 5116250.00, 134000.00, 4698040.00, 3289000.00, 1052000.00),
(N'A07A128', N'656/30', N'A', N'07', 7, N'1 Bed S', 30.00, 4250000.00, 130000.00, 3900000.00, 2730000.00, 900000.00),
(N'A07A1M29', N'656/31', N'A', N'07', 7, N'1 Bed S', 29.64, 4286250.00, 130000.00, 3853200.00, 2697000.00, 889000.00),
(N'A08A101', N'656/32', N'A', N'08', 8, N'1 Bed S', 29.63, 4347500.00, 131500.00, 3896345.00, 2727000.00, 889000.00),
(N'A08A202', N'656/33', N'A', N'08', 8, N'1 Bed S', 30.60, 4601250.00, 134500.00, 4115700.00, 2881000.00, 918000.00),
(N'A08D103', N'656/34', N'A', N'08', 8, N'2 Bed 1 Bath', 42.50, 5975000.00, 131500.00, 5588750.00, 3912000.00, 1275000.00),
(N'A08B2M04', N'656/35', N'A', N'08', 8, N'1 Bed M', 31.91, 3978750.00, 121500.00, 3877065.00, 2714000.00, 957000.00),
(N'A08B105', N'656/36', N'A', N'08', 8, N'1 Bed M', 32.16, 4320000.00, 121500.00, 3907440.00, 2735000.00, 965000.00),
(N'A08B1M06', N'656/37', N'A', N'08', 8, N'1 Bed M', 32.34, 4320000.00, 121500.00, 3929310.00, 2751000.00, 970000.00),
(N'A08D1M07', N'656/38', N'A', N'08', 8, N'2 Bed 1 Bath', 42.51, 6051250.00, 135000.00, 5738850.00, 4017000.00, 1275000.00),
(N'A08C208', N'656/39', N'A', N'08', 8, N'1 Bed Plus', 35.26, 4975000.00, 135500.00, 4777730.00, 3344000.00, 1058000.00),
(N'A08B109', N'656/40', N'A', N'08', 8, N'1 Bed M', 32.47, 4561250.00, 130000.00, 4221100.00, 2955000.00, 974000.00),
(N'A08B1M10', N'656/41', N'A', N'08', 8, N'1 Bed M', 32.39, 4647500.00, 130000.00, 4210700.00, 2947000.00, 972000.00),
(N'A08C111', N'656/42', N'A', N'08', 8, N'1 Bed L', 35.24, 5207500.00, 135500.00, 4775020.00, 3343000.00, 1057000.00),
(N'A08C112', N'656/43', N'A', N'08', 8, N'1 Bed L', 35.28, 5207500.00, 135500.00, 4780440.00, 3346000.00, 1058000.00),
(N'A08C213', N'656/44', N'A', N'08', 8, N'1 Bed Plus', 35.33, 5207500.00, 135500.00, 4787215.00, 3351000.00, 1060000.00),
(N'A08C214', N'656/45', N'A', N'08', 8, N'1 Bed Plus', 35.28, 5207500.00, 135500.00, 4780440.00, 3346000.00, 1058000.00),
(N'A08C215', N'656/46', N'A', N'08', 8, N'1 Bed Plus', 35.28, 5207500.00, 135500.00, 4780440.00, 3346000.00, 1058000.00),
(N'A08C216', N'656/47', N'A', N'08', 8, N'1 Bed Plus', 35.28, 5207500.00, 135500.00, 4780440.00, 3346000.00, 1058000.00),
(N'A08C217', N'656/48', N'A', N'08', 8, N'1 Bed Plus', 35.32, 5138750.00, 135500.00, 4785860.00, 3350000.00, 1060000.00),
(N'A08C218', N'656/49', N'A', N'08', 8, N'1 Bed Plus', 35.32, 5125000.00, 135500.00, 4785860.00, 3350000.00, 1060000.00),
(N'A08E119', N'656/50', N'A', N'08', 8, N'2 Bed 2 Bath', 58.16, 9236250.00, 155000.00, 9014800.00, 6310000.00, 1745000.00),
(N'A08E220', N'656/51', N'A', N'08', 8, N'2 Bed 2 Bath', 58.04, 9125000.00, 151500.00, 8793060.00, 6155000.00, 1741000.00),
(N'A08B1M21', N'656/52', N'A', N'08', 8, N'1 Bed M', 31.59, 4402500.00, 121500.00, 3838185.00, 2687000.00, 948000.00),
(N'A08B222', N'656/53', N'A', N'08', 8, N'1 Bed M', 31.94, 4435000.00, 121500.00, 3880710.00, 2716000.00, 958000.00),
(N'A08B2M23', N'656/54', N'A', N'08', 8, N'1 Bed M', 31.97, 4602500.00, 121500.00, 3884355.00, 2719000.00, 959000.00),
(N'A08B124', N'656/55', N'A', N'08', 8, N'1 Bed M', 32.16, 4602500.00, 121500.00, 3907440.00, 2735000.00, 965000.00),
(N'A08B1M25', N'656/56', N'A', N'08', 8, N'1 Bed M', 32.17, 4475000.00, 121500.00, 3908655.00, 2736000.00, 965000.00),
(N'A08C2M26', N'656/57', N'A', N'08', 8, N'1 Bed Plus', 35.03, 5088750.00, 135500.00, 4746565.00, 3323000.00, 1051000.00),
(N'A08C2M27', N'656/58', N'A', N'08', 8, N'1 Bed Plus', 35.06, 5116250.00, 135500.00, 4750630.00, 3325000.00, 1052000.00),
(N'A08A128', N'656/59', N'A', N'08', 8, N'1 Bed S', 30.00, 4386250.00, 131500.00, 3945000.00, 2762000.00, 900000.00),
(N'A08A1M29', N'656/60', N'A', N'08', 8, N'1 Bed S', 29.64, 4182500.00, 131500.00, 3897660.00, 2728000.00, 889000.00),
(N'A09A101', N'656/61', N'A', N'09', 9, N'1 Bed S', 29.63, 4367500.00, 133000.00, 3940790.00, 2759000.00, 889000.00),
(N'A09A202', N'656/62', N'A', N'09', 9, N'1 Bed S', 30.60, 4620000.00, 136000.00, 4161600.00, 2913000.00, 918000.00),
(N'A09D103', N'656/63', N'A', N'09', 9, N'2 Bed 1 Bath', 42.50, 5970000.00, 133000.00, 5652500.00, 3957000.00, 1275000.00),
(N'A09B2M04', N'656/64', N'A', N'09', 9, N'1 Bed M', 31.91, 4275000.00, 123000.00, 3924930.00, 2747000.00, 957000.00),
(N'A09B105', N'656/65', N'A', N'09', 9, N'1 Bed M', 32.16, 4316250.00, 123000.00, 3955680.00, 2769000.00, 965000.00),
(N'A09B1M06', N'656/66', N'A', N'09', 9, N'1 Bed M', 32.34, 4316250.00, 123000.00, 3977820.00, 2784000.00, 970000.00),
(N'A09D1M07', N'656/67', N'A', N'09', 9, N'2 Bed 1 Bath', 42.51, 6078750.00, 136500.00, 5802615.00, 4062000.00, 1275000.00),
(N'A09C208', N'656/68', N'A', N'09', 9, N'1 Bed Plus', 35.26, 4997500.00, 137000.00, 4830620.00, 3381000.00, 1058000.00),
(N'A09B109', N'656/69', N'A', N'09', 9, N'1 Bed M', 32.47, 4582500.00, 131500.00, 4269805.00, 2989000.00, 974000.00),
(N'A09B1M10', N'656/70', N'A', N'09', 9, N'1 Bed M', 32.39, 4668750.00, 131500.00, 4259285.00, 2981000.00, 972000.00),
(N'A09C111', N'656/71', N'A', N'09', 9, N'1 Bed L', 35.24, 5230000.00, 137000.00, 4827880.00, 3380000.00, 1057000.00),
(N'A09C112', N'656/72', N'A', N'09', 9, N'1 Bed L', 35.28, 5230000.00, 137000.00, 4833360.00, 3383000.00, 1058000.00),
(N'A09C213', N'656/73', N'A', N'09', 9, N'1 Bed Plus', 35.33, 5230000.00, 137000.00, 4840210.00, 3388000.00, 1060000.00),
(N'A09C214', N'656/74', N'A', N'09', 9, N'1 Bed Plus', 35.28, 5202500.00, 137000.00, 4833360.00, 3383000.00, 1058000.00),
(N'A09C215', N'656/75', N'A', N'09', 9, N'1 Bed Plus', 35.28, 5202500.00, 137000.00, 4833360.00, 3383000.00, 1058000.00),
(N'A09C216', N'656/76', N'A', N'09', 9, N'1 Bed Plus', 35.28, 5202500.00, 137000.00, 4833360.00, 3383000.00, 1058000.00),
(N'A09C217', N'656/77', N'A', N'09', 9, N'1 Bed Plus', 35.32, 5161250.00, 137000.00, 4838840.00, 3387000.00, 1060000.00),
(N'A09C218', N'656/78', N'A', N'09', 9, N'1 Bed Plus', 35.32, 5147500.00, 137000.00, 4838840.00, 3387000.00, 1060000.00),
(N'A09E119', N'656/79', N'A', N'09', 9, N'2 Bed 2 Bath', 58.16, 9272500.00, 156500.00, 9102040.00, 6371000.00, 1745000.00),
(N'A09E220', N'656/80', N'A', N'09', 9, N'2 Bed 2 Bath', 58.04, 9206250.00, 153000.00, 8880120.00, 6216000.00, 1741000.00),
(N'A09B1M21', N'656/81', N'A', N'09', 9, N'1 Bed M', 31.59, 4550000.00, 123000.00, 3885570.00, 2720000.00, 948000.00),
(N'A09B222', N'656/82', N'A', N'09', 9, N'1 Bed M', 31.94, 4582500.00, 123000.00, 3928620.00, 2750000.00, 958000.00),
(N'A09B2M23', N'656/83', N'A', N'09', 9, N'1 Bed M', 31.97, 4623750.00, 123000.00, 3932310.00, 2753000.00, 959000.00),
(N'A09B124', N'656/84', N'A', N'09', 9, N'1 Bed M', 32.16, 4647500.00, 123000.00, 3955680.00, 2769000.00, 965000.00),
(N'A09B1M25', N'656/85', N'A', N'09', 9, N'1 Bed M', 32.17, 4647500.00, 123000.00, 3956910.00, 2770000.00, 965000.00),
(N'A09C2M26', N'656/86', N'A', N'09', 9, N'1 Bed Plus', 35.03, 5138750.00, 137000.00, 4799110.00, 3359000.00, 1051000.00),
(N'A09C2M27', N'656/87', N'A', N'09', 9, N'1 Bed Plus', 35.06, 5138750.00, 137000.00, 4803220.00, 3362000.00, 1052000.00),
(N'A09A128', N'656/88', N'A', N'09', 9, N'1 Bed S', 30.00, 4428750.00, 133000.00, 3990000.00, 2793000.00, 900000.00),
(N'A09A1M29', N'656/89', N'A', N'09', 9, N'1 Bed S', 29.64, 4343750.00, 133000.00, 3942120.00, 2759000.00, 889000.00),
(N'A10A101', N'656/90', N'A', N'10', 10, N'1 Bed S', 29.63, 4371250.00, 134500.00, 3985235.00, 2790000.00, 889000.00),
(N'A10A202', N'656/91', N'A', N'10', 10, N'1 Bed S', 30.60, 4625000.00, 137500.00, 4207500.00, 2945000.00, 918000.00),
(N'A10D103', N'656/92', N'A', N'10', 10, N'2 Bed 1 Bath', 42.50, 6062500.00, 134500.00, 5716250.00, 4001000.00, 1275000.00),
(N'A10B2M04', N'656/93', N'A', N'10', 10, N'1 Bed M', 31.91, 4062500.00, 124500.00, 3972795.00, 2781000.00, 957000.00),
(N'A10B105', N'656/94', N'A', N'10', 10, N'1 Bed M', 32.16, 4410000.00, 124500.00, 4003920.00, 2803000.00, 965000.00),
(N'A10B1M06', N'656/95', N'A', N'10', 10, N'1 Bed M', 32.34, 4410000.00, 124500.00, 4026330.00, 2818000.00, 970000.00),
(N'A10D1M07', N'656/96', N'A', N'10', 10, N'2 Bed 1 Bath', 42.51, 6171250.00, 138000.00, 5866380.00, 4106000.00, 1275000.00),
(N'A10C208', N'656/97', N'A', N'10', 10, N'1 Bed Plus', 35.26, 5075000.00, 138500.00, 4883510.00, 3418000.00, 1058000.00),
(N'A10B109', N'656/98', N'A', N'10', 10, N'1 Bed M', 32.47, 4676250.00, 133000.00, 4318510.00, 3023000.00, 974000.00),
(N'A10B1M10', N'656/99', N'A', N'10', 10, N'1 Bed M', 32.39, 4737500.00, 133000.00, 4307870.00, 3016000.00, 972000.00),
(N'A10C111', N'656/100', N'A', N'10', 10, N'1 Bed L', 35.24, 5307500.00, 138500.00, 4880740.00, 3417000.00, 1057000.00),
(N'A10C112', N'656/101', N'A', N'10', 10, N'1 Bed L', 35.28, 5280000.00, 138500.00, 4886280.00, 3420000.00, 1058000.00),
(N'A10C213', N'656/102', N'A', N'10', 10, N'1 Bed Plus', 35.33, 5280000.00, 138500.00, 4893205.00, 3425000.00, 1060000.00),
(N'A10C214', N'656/103', N'A', N'10', 10, N'1 Bed Plus', 35.28, 5307500.00, 138500.00, 4886280.00, 3420000.00, 1058000.00),
(N'A10C215', N'656/104', N'A', N'10', 10, N'1 Bed Plus', 35.28, 5280000.00, 138500.00, 4886280.00, 3420000.00, 1058000.00),
(N'A10C216', N'656/105', N'A', N'10', 10, N'1 Bed Plus', 35.28, 5307500.00, 138500.00, 4886280.00, 3420000.00, 1058000.00),
(N'A10C217', N'656/106', N'A', N'10', 10, N'1 Bed Plus', 35.32, 5238750.00, 138500.00, 4891820.00, 3424000.00, 1060000.00),
(N'A10C218', N'656/107', N'A', N'10', 10, N'1 Bed Plus', 35.32, 5225000.00, 138500.00, 4891820.00, 3424000.00, 1060000.00),
(N'A10E119', N'656/108', N'A', N'10', 10, N'2 Bed 2 Bath', 58.16, 9398750.00, 158000.00, 9189280.00, 6432000.00, 1745000.00),
(N'A10E220', N'656/109', N'A', N'10', 10, N'2 Bed 2 Bath', 58.04, 9287500.00, 154500.00, 8967180.00, 6277000.00, 1741000.00),
(N'A10B1M21', N'656/110', N'A', N'10', 10, N'1 Bed M', 31.59, 4490000.00, 124500.00, 3932955.00, 2753000.00, 948000.00),
(N'A10B222', N'656/111', N'A', N'10', 10, N'1 Bed M', 31.94, 4522500.00, 124500.00, 3976530.00, 2784000.00, 958000.00),
(N'A10B2M23', N'656/112', N'A', N'10', 10, N'1 Bed M', 31.97, 4692500.00, 124500.00, 3980265.00, 2786000.00, 959000.00),
(N'A10B124', N'656/113', N'A', N'10', 10, N'1 Bed M', 32.16, 4562500.00, 124500.00, 4003920.00, 2803000.00, 965000.00),
(N'A10B1M25', N'656/114', N'A', N'10', 10, N'1 Bed M', 32.17, 4692500.00, 124500.00, 4005165.00, 2804000.00, 965000.00),
(N'A10C2M26', N'656/115', N'A', N'10', 10, N'1 Bed Plus', 35.03, 5216250.00, 138500.00, 4851655.00, 3396000.00, 1051000.00),
(N'A10C2M27', N'656/116', N'A', N'10', 10, N'1 Bed Plus', 35.06, 5216250.00, 138500.00, 4855810.00, 3399000.00, 1052000.00),
(N'A10A128', N'656/117', N'A', N'10', 10, N'1 Bed S', 30.00, 4287500.00, 134500.00, 4035000.00, 2825000.00, 900000.00),
(N'A10A1M29', N'656/118', N'A', N'10', 10, N'1 Bed S', 29.64, 4205000.00, 134500.00, 3986580.00, 2791000.00, 889000.00),
(N'A11A101', N'656/119', N'A', N'11', 11, N'1 Bed S', 29.63, 4390000.00, 136000.00, 4029680.00, 2821000.00, 889000.00),
(N'A11A202', N'656/120', N'A', N'11', 11, N'1 Bed S', 30.60, 4621250.00, 139000.00, 4253400.00, 2977000.00, 918000.00),
(N'A11D103', N'656/121', N'A', N'11', 11, N'2 Bed 1 Bath', 42.50, 6122500.00, 136000.00, 5780000.00, 4046000.00, 1275000.00),
(N'A11B2M04', N'656/122', N'A', N'11', 11, N'1 Bed M', 31.91, 4082500.00, 126000.00, 4020660.00, 2814000.00, 957000.00),
(N'A11B105', N'656/123', N'A', N'11', 11, N'1 Bed M', 32.16, 4430000.00, 126000.00, 4052160.00, 2837000.00, 965000.00),
(N'A11B1M06', N'656/124', N'A', N'11', 11, N'1 Bed M', 32.34, 4430000.00, 126000.00, 4074840.00, 2852000.00, 970000.00),
(N'A11D1M07', N'656/125', N'A', N'11', 11, N'2 Bed 1 Bath', 42.51, 6198750.00, 139500.00, 5930145.00, 4151000.00, 1275000.00),
(N'A11C208', N'656/126', N'A', N'11', 11, N'1 Bed Plus', 35.26, 5097500.00, 140000.00, 4936400.00, 3455000.00, 1058000.00),
(N'A11B109', N'656/127', N'A', N'11', 11, N'1 Bed M', 32.47, 4697500.00, 134500.00, 4367215.00, 3057000.00, 974000.00),
(N'A11B1M10', N'656/128', N'A', N'11', 11, N'1 Bed M', 32.39, 4758750.00, 134500.00, 4356455.00, 3050000.00, 972000.00),
(N'A11C111', N'656/129', N'A', N'11', 11, N'1 Bed L', 35.24, 5330000.00, 140000.00, 4933600.00, 3454000.00, 1057000.00),
(N'A11C112', N'656/130', N'A', N'11', 11, N'1 Bed L', 35.28, 5302500.00, 140000.00, 4939200.00, 3457000.00, 1058000.00),
(N'A11C213', N'656/131', N'A', N'11', 11, N'1 Bed Plus', 35.33, 5302500.00, 140000.00, 4946200.00, 3462000.00, 1060000.00),
(N'A11C214', N'656/132', N'A', N'11', 11, N'1 Bed Plus', 35.28, 5302500.00, 140000.00, 4939200.00, 3457000.00, 1058000.00),
(N'A11C215', N'656/133', N'A', N'11', 11, N'1 Bed Plus', 35.28, 5302500.00, 140000.00, 4939200.00, 3457000.00, 1058000.00),
(N'A11C216', N'656/134', N'A', N'11', 11, N'1 Bed Plus', 35.28, 5302500.00, 140000.00, 4939200.00, 3457000.00, 1058000.00),
(N'A11C217', N'656/135', N'A', N'11', 11, N'1 Bed Plus', 35.32, 5235000.00, 140000.00, 4944800.00, 3461000.00, 1060000.00),
(N'A11C218', N'656/136', N'A', N'11', 11, N'1 Bed Plus', 35.32, 5248750.00, 140000.00, 4944800.00, 3461000.00, 1060000.00),
(N'A11E119', N'656/137', N'A', N'11', 11, N'2 Bed 2 Bath', 58.16, 9436250.00, 159500.00, 9276520.00, 6494000.00, 1745000.00),
(N'A11E220', N'656/138', N'A', N'11', 11, N'2 Bed 2 Bath', 58.04, 9325000.00, 156000.00, 9054240.00, 6338000.00, 1741000.00),
(N'A11B1M21', N'656/139', N'A', N'11', 11, N'1 Bed M', 31.59, 4510000.00, 126000.00, 3980340.00, 2786000.00, 948000.00),
(N'A11B222', N'656/140', N'A', N'11', 11, N'1 Bed M', 31.94, 4542500.00, 126000.00, 4024440.00, 2817000.00, 958000.00),
(N'A11B2M23', N'656/141', N'A', N'11', 11, N'1 Bed M', 31.97, 4582500.00, 126000.00, 4028220.00, 2820000.00, 959000.00),
(N'A11B124', N'656/142', N'A', N'11', 11, N'1 Bed M', 32.16, 4713750.00, 126000.00, 4052160.00, 2837000.00, 965000.00),
(N'A11B1M25', N'656/143', N'A', N'11', 11, N'1 Bed M', 32.17, 4713750.00, 126000.00, 4053420.00, 2837000.00, 965000.00),
(N'A11C2M26', N'656/144', N'A', N'11', 11, N'1 Bed Plus', 35.03, 5212500.00, 140000.00, 4904200.00, 3433000.00, 1051000.00),
(N'A11C2M27', N'656/145', N'A', N'11', 11, N'1 Bed Plus', 35.06, 5212500.00, 140000.00, 4908400.00, 3436000.00, 1052000.00),
(N'A11A128', N'656/146', N'A', N'11', 11, N'1 Bed S', 30.00, 4306250.00, 136000.00, 4080000.00, 2856000.00, 900000.00),
(N'A11A1M29', N'656/147', N'A', N'11', 11, N'1 Bed S', 29.64, 4343750.00, 136000.00, 4031040.00, 2822000.00, 889000.00),
(N'A12A101', N'656/148', N'A', N'12', 12, N'1 Bed S', 29.63, 4432500.00, 137500.00, 4074125.00, 2852000.00, 889000.00),
(N'A12A202', N'656/149', N'A', N'12', 12, N'1 Bed S', 30.60, 4662500.00, 140500.00, 4299300.00, 3010000.00, 918000.00),
(N'A12D103', N'656/150', N'A', N'12', 12, N'2 Bed 1 Bath', 42.50, 6117500.00, 137500.00, 5843750.00, 4091000.00, 1275000.00),
(N'A12B2M04', N'656/151', N'A', N'12', 12, N'1 Bed M', 31.91, 4385000.00, 127500.00, 4068525.00, 2848000.00, 957000.00),
(N'A12B105', N'656/152', N'A', N'12', 12, N'1 Bed M', 32.16, 4426250.00, 127500.00, 4100400.00, 2870000.00, 965000.00),
(N'A12B1M06', N'656/153', N'A', N'12', 12, N'1 Bed M', 32.34, 4451250.00, 127500.00, 4123350.00, 2886000.00, 970000.00),
(N'A12D1M07', N'656/154', N'A', N'12', 12, N'2 Bed 1 Bath', 42.51, 6226250.00, 141000.00, 5993910.00, 4196000.00, 1275000.00),
(N'A12C208', N'656/155', N'A', N'12', 12, N'1 Bed Plus', 35.26, 5121250.00, 141500.00, 4989290.00, 3493000.00, 1058000.00),
(N'A12B109', N'656/156', N'A', N'12', 12, N'1 Bed M', 32.47, 4717500.00, 136000.00, 4415920.00, 3091000.00, 974000.00),
(N'A12B1M10', N'656/157', N'A', N'12', 12, N'1 Bed M', 32.39, 4778750.00, 136000.00, 4405040.00, 3084000.00, 972000.00),
(N'A12C111', N'656/158', N'A', N'12', 12, N'1 Bed L', 35.24, 5326250.00, 141500.00, 4986460.00, 3491000.00, 1057000.00),
(N'A12C112', N'656/159', N'A', N'12', 12, N'1 Bed L', 35.28, 5326250.00, 141500.00, 4992120.00, 3494000.00, 1058000.00),
(N'A12C213', N'656/160', N'A', N'12', 12, N'1 Bed Plus', 35.33, 5352500.00, 141500.00, 4999195.00, 3499000.00, 1060000.00),
(N'A12C214', N'656/161', N'A', N'12', 12, N'1 Bed Plus', 35.28, 5326250.00, 141500.00, 4992120.00, 3494000.00, 1058000.00),
(N'A12C215', N'656/162', N'A', N'12', 12, N'1 Bed Plus', 35.28, 5326250.00, 141500.00, 4992120.00, 3494000.00, 1058000.00),
(N'A12C216', N'656/163', N'A', N'12', 12, N'1 Bed Plus', 35.28, 5326250.00, 141500.00, 4992120.00, 3494000.00, 1058000.00),
(N'A12C217', N'656/164', N'A', N'12', 12, N'1 Bed Plus', 35.32, 5257500.00, 141500.00, 4997780.00, 3498000.00, 1060000.00),
(N'A12C218', N'656/165', N'A', N'12', 12, N'1 Bed Plus', 35.32, 5243750.00, 141500.00, 4997780.00, 3498000.00, 1060000.00),
(N'A12E119', N'656/166', N'A', N'12', 12, N'2 Bed 2 Bath', 58.16, 9517500.00, 161000.00, 9363760.00, 6555000.00, 1745000.00),
(N'A12E220', N'656/167', N'A', N'12', 12, N'2 Bed 2 Bath', 58.04, 9362500.00, 157500.00, 9141300.00, 6399000.00, 1741000.00),
(N'A12B1M21', N'656/168', N'A', N'12', 12, N'1 Bed M', 31.59, 4660000.00, 127500.00, 4027725.00, 2819000.00, 948000.00),
(N'A12B222', N'656/169', N'A', N'12', 12, N'1 Bed M', 31.94, 4717500.00, 127500.00, 4072350.00, 2851000.00, 958000.00),
(N'A12B2M23', N'656/170', N'A', N'12', 12, N'1 Bed M', 31.97, 4733750.00, 127500.00, 4076175.00, 2853000.00, 959000.00),
(N'A12B124', N'656/171', N'A', N'12', 12, N'1 Bed M', 32.16, 4758750.00, 127500.00, 4100400.00, 2870000.00, 965000.00),
(N'A12B1M25', N'656/172', N'A', N'12', 12, N'1 Bed M', 32.17, 4733750.00, 127500.00, 4101675.00, 2871000.00, 965000.00),
(N'A12C2M26', N'656/173', N'A', N'12', 12, N'1 Bed Plus', 35.03, 5235000.00, 141500.00, 4956745.00, 3470000.00, 1051000.00),
(N'A12C2M27', N'656/174', N'A', N'12', 12, N'1 Bed Plus', 35.06, 5261250.00, 141500.00, 4960990.00, 3473000.00, 1052000.00),
(N'A12A128', N'656/175', N'A', N'12', 12, N'1 Bed S', 30.00, 4471250.00, 137500.00, 4125000.00, 2888000.00, 900000.00),
(N'A12A1M29', N'656/176', N'A', N'12', 12, N'1 Bed S', 29.64, 4386250.00, 137500.00, 4075500.00, 2853000.00, 889000.00),
(N'A12AA101', N'656/177', N'A', N'12A', 13, N'1 Bed S', 29.63, 4432500.00, 139000.00, 4118570.00, 2883000.00, 889000.00),
(N'A12AA202', N'656/178', N'A', N'12A', 13, N'1 Bed S', 30.60, 4662500.00, 142000.00, 4345200.00, 3042000.00, 918000.00),
(N'A12AD103', N'656/179', N'A', N'12A', 13, N'2 Bed 1 Bath', 42.50, 6117500.00, 139000.00, 5907500.00, 4135000.00, 1275000.00),
(N'A12AB2M04', N'656/180', N'A', N'12A', 13, N'1 Bed M', 31.91, 4101250.00, 129000.00, 4116390.00, 2881000.00, 957000.00),
(N'A12AB105', N'656/181', N'A', N'12A', 13, N'1 Bed M', 32.16, 4451250.00, 129000.00, 4148640.00, 2904000.00, 965000.00),
(N'A12AB1M06', N'656/182', N'A', N'12A', 13, N'1 Bed M', 32.34, 4451250.00, 129000.00, 4171860.00, 2920000.00, 970000.00),
(N'A12AD1M07', N'656/183', N'A', N'12A', 13, N'2 Bed 1 Bath', 42.51, 6226250.00, 142500.00, 6057675.00, 4240000.00, 1275000.00),
(N'A12AC208', N'656/184', N'A', N'12A', 13, N'1 Bed Plus', 35.26, 5121250.00, 143000.00, 5042180.00, 3530000.00, 1058000.00),
(N'A12AB109', N'656/185', N'A', N'12A', 13, N'1 Bed M', 32.47, 4717500.00, 137500.00, 4464625.00, 3125000.00, 974000.00),
(N'A12AB1M10', N'656/186', N'A', N'12A', 13, N'1 Bed M', 32.39, 4778750.00, 137500.00, 4453625.00, 3118000.00, 972000.00),
(N'A12AC111', N'656/187', N'A', N'12A', 13, N'1 Bed L', 35.24, 5326250.00, 143000.00, 5039320.00, 3528000.00, 1057000.00),
(N'A12AC112', N'656/188', N'A', N'12A', 13, N'1 Bed L', 35.28, 5326250.00, 143000.00, 5045040.00, 3532000.00, 1058000.00),
(N'A12AC213', N'656/189', N'A', N'12A', 13, N'1 Bed Plus', 35.33, 5326250.00, 143000.00, 5052190.00, 3537000.00, 1060000.00),
(N'A12AC214', N'656/190', N'A', N'12A', 13, N'1 Bed Plus', 35.28, 5326250.00, 143000.00, 5045040.00, 3532000.00, 1058000.00),
(N'A12AC215', N'656/191', N'A', N'12A', 13, N'1 Bed Plus', 35.28, 5326250.00, 143000.00, 5045040.00, 3532000.00, 1058000.00),
(N'A12AC216', N'656/192', N'A', N'12A', 13, N'1 Bed Plus', 35.28, 5326250.00, 143000.00, 5045040.00, 3532000.00, 1058000.00),
(N'A12AC217', N'656/193', N'A', N'12A', 13, N'1 Bed Plus', 35.32, 5257500.00, 143000.00, 5050760.00, 3536000.00, 1060000.00),
(N'A12AC218', N'656/194', N'A', N'12A', 13, N'1 Bed Plus', 35.32, 5243750.00, 143000.00, 5050760.00, 3536000.00, 1060000.00),
(N'A12AE119', N'656/195', N'A', N'12A', 13, N'2 Bed 2 Bath', 58.16, 9473750.00, 162500.00, 9451000.00, 6616000.00, 1745000.00),
(N'A12AE220', N'656/196', N'A', N'12A', 13, N'2 Bed 2 Bath', 58.04, 9362500.00, 159000.00, 9228360.00, 6460000.00, 1741000.00),
(N'A12AB1M21', N'656/197', N'A', N'12A', 13, N'1 Bed M', 31.59, 4660000.00, 129000.00, 4075110.00, 2853000.00, 948000.00),
(N'A12AB222', N'656/198', N'A', N'12A', 13, N'1 Bed M', 31.94, 4562500.00, 129000.00, 4120260.00, 2884000.00, 958000.00),
(N'A12AB2M23', N'656/199', N'A', N'12A', 13, N'1 Bed M', 31.97, 4602500.00, 129000.00, 4124130.00, 2887000.00, 959000.00),
(N'A12AB124', N'656/200', N'A', N'12A', 13, N'1 Bed M', 32.16, 4733750.00, 129000.00, 4148640.00, 2904000.00, 965000.00),
(N'A12AB1M25', N'656/201', N'A', N'12A', 13, N'1 Bed M', 32.17, 4602500.00, 129000.00, 4149930.00, 2905000.00, 965000.00),
(N'A12AC2M26', N'656/202', N'A', N'12A', 13, N'1 Bed Plus', 35.03, 5261250.00, 143000.00, 5009290.00, 3507000.00, 1051000.00),
(N'A12AC2M27', N'656/203', N'A', N'12A', 13, N'1 Bed Plus', 35.06, 5261250.00, 143000.00, 5013580.00, 3510000.00, 1052000.00),
(N'A12AA128', N'656/204', N'A', N'12A', 13, N'1 Bed S', 30.00, 4323750.00, 139000.00, 4170000.00, 2919000.00, 900000.00),
(N'A12AA1M29', N'656/205', N'A', N'12A', 13, N'1 Bed S', 29.64, 4242500.00, 139000.00, 4119960.00, 2884000.00, 889000.00),
(N'A14A101', N'656/206', N'A', N'14', 14, N'1 Bed S', 29.63, 4451250.00, 140500.00, 4163015.00, 2914000.00, 889000.00),
(N'A14A202', N'656/207', N'A', N'14', 14, N'1 Bed S', 30.60, 4682500.00, 143500.00, 4391100.00, 3074000.00, 918000.00),
(N'A14D103', N'656/208', N'A', N'14', 14, N'2 Bed 1 Bath', 42.50, 6177500.00, 140500.00, 5971250.00, 4180000.00, 1275000.00),
(N'A14B2M04', N'656/209', N'A', N'14', 14, N'1 Bed M', 31.91, 4143750.00, 130500.00, 4164255.00, 2915000.00, 957000.00),
(N'A14B105', N'656/210', N'A', N'14', 14, N'1 Bed M', 32.16, 4471250.00, 130500.00, 4196880.00, 2938000.00, 965000.00),
(N'A14B1M06', N'656/211', N'A', N'14', 14, N'1 Bed M', 32.34, 4496250.00, 130500.00, 4220370.00, 2954000.00, 970000.00),
(N'A14D1M07', N'656/212', N'A', N'14', 14, N'2 Bed 1 Bath', 42.51, 6318750.00, 144000.00, 6121440.00, 4285000.00, 1275000.00),
(N'A14C208', N'656/213', N'A', N'14', 14, N'1 Bed Plus', 35.26, 5171250.00, 144500.00, 5095070.00, 3567000.00, 1058000.00),
(N'A14B109', N'656/214', N'A', N'14', 14, N'1 Bed M', 32.47, 4762500.00, 139000.00, 4513330.00, 3159000.00, 974000.00),
(N'A14B1M10', N'656/215', N'A', N'14', 14, N'1 Bed M', 32.39, 4823750.00, 139000.00, 4502210.00, 3152000.00, 972000.00),
(N'A14C111', N'656/216', N'A', N'14', 14, N'1 Bed L', 35.24, 5376250.00, 144500.00, 5092180.00, 3565000.00, 1057000.00),
(N'A14C112', N'656/217', N'A', N'14', 14, N'1 Bed L', 35.28, 5376250.00, 144500.00, 5097960.00, 3569000.00, 1058000.00),
(N'A14C213', N'656/218', N'A', N'14', 14, N'1 Bed Plus', 35.33, 5402500.00, 144500.00, 5105185.00, 3574000.00, 1060000.00),
(N'A14C214', N'656/219', N'A', N'14', 14, N'1 Bed Plus', 35.28, 5376250.00, 144500.00, 5097960.00, 3569000.00, 1058000.00),
(N'A14C215', N'656/220', N'A', N'14', 14, N'1 Bed Plus', 35.28, 5376250.00, 144500.00, 5097960.00, 3569000.00, 1058000.00),
(N'A14C216', N'656/221', N'A', N'14', 14, N'1 Bed Plus', 35.28, 5376250.00, 144500.00, 5097960.00, 3569000.00, 1058000.00),
(N'A14C217', N'656/222', N'A', N'14', 14, N'1 Bed Plus', 35.32, 5307500.00, 144500.00, 5103740.00, 3573000.00, 1060000.00),
(N'A14C218', N'656/223', N'A', N'14', 14, N'1 Bed Plus', 35.32, 5293750.00, 144500.00, 5103740.00, 3573000.00, 1060000.00),
(N'A14E119', N'656/224', N'A', N'14', 14, N'2 Bed 2 Bath', 58.16, 9555000.00, 164000.00, 9538240.00, 6677000.00, 1745000.00),
(N'A14E220', N'656/225', N'A', N'14', 14, N'2 Bed 2 Bath', 58.04, 9443750.00, 160500.00, 9315420.00, 6521000.00, 1741000.00),
(N'A14B1M21', N'656/226', N'A', N'14', 14, N'1 Bed M', 31.59, 4705000.00, 130500.00, 4122495.00, 2886000.00, 948000.00),
(N'A14B222', N'656/227', N'A', N'14', 14, N'1 Bed M', 31.94, 4762500.00, 130500.00, 4168170.00, 2918000.00, 958000.00),
(N'A14B2M23', N'656/228', N'A', N'14', 14, N'1 Bed M', 31.97, 4803750.00, 130500.00, 4172085.00, 2920000.00, 959000.00),
(N'A14B124', N'656/229', N'A', N'14', 14, N'1 Bed M', 32.16, 4778750.00, 130500.00, 4196880.00, 2938000.00, 965000.00),
(N'A14B1M25', N'656/230', N'A', N'14', 14, N'1 Bed M', 32.17, 4803750.00, 130500.00, 4198185.00, 2939000.00, 965000.00),
(N'A14C2M26', N'656/231', N'A', N'14', 14, N'1 Bed Plus', 35.03, 5312500.00, 144500.00, 5061835.00, 3543000.00, 1051000.00),
(N'A14C2M27', N'656/232', N'A', N'14', 14, N'1 Bed Plus', 35.06, 5285000.00, 144500.00, 5066170.00, 3546000.00, 1052000.00),
(N'A14A128', N'656/233', N'A', N'14', 14, N'1 Bed S', 30.00, 4512500.00, 140500.00, 4215000.00, 2951000.00, 900000.00),
(N'A14A1M29', N'656/234', N'A', N'14', 14, N'1 Bed S', 29.64, 4428750.00, 140500.00, 4164420.00, 2915000.00, 889000.00),
(N'A15A101', N'656/235', N'A', N'15', 15, N'1 Bed S', 29.63, 4517500.00, 142000.00, 4207460.00, 2945000.00, 889000.00),
(N'A15A202', N'656/236', N'A', N'15', 15, N'1 Bed S', 30.60, 4725000.00, 145000.00, 4437000.00, 3106000.00, 918000.00),
(N'A15D103', N'656/237', N'A', N'15', 15, N'2 Bed 1 Bath', 42.50, 6270000.00, 142000.00, 6035000.00, 4225000.00, 1275000.00),
(N'A15B2M04', N'656/238', N'A', N'15', 15, N'1 Bed M', 31.91, 4185000.00, 132000.00, 4212120.00, 2948000.00, 957000.00),
(N'A15B105', N'656/239', N'A', N'15', 15, N'1 Bed M', 32.16, 4516250.00, 132000.00, 4245120.00, 2972000.00, 965000.00),
(N'A15B1M06', N'656/240', N'A', N'15', 15, N'1 Bed M', 32.34, 4541250.00, 132000.00, 4268880.00, 2988000.00, 970000.00),
(N'A15D1M07', N'656/241', N'A', N'15', 15, N'2 Bed 1 Bath', 42.51, 6378750.00, 145500.00, 6185205.00, 4330000.00, 1275000.00),
(N'A15C208', N'656/242', N'A', N'15', 15, N'1 Bed Plus', 35.26, 5221250.00, 146000.00, 5147960.00, 3604000.00, 1058000.00),
(N'A15B109', N'656/243', N'A', N'15', 15, N'1 Bed M', 32.47, 4807500.00, 140500.00, 4562035.00, 3193000.00, 974000.00),
(N'A15B1M10', N'656/244', N'A', N'15', 15, N'1 Bed M', 32.39, 4845000.00, 140500.00, 4550795.00, 3186000.00, 972000.00),
(N'A15C111', N'656/245', N'A', N'15', 15, N'1 Bed L', 35.24, 5426250.00, 146000.00, 5145040.00, 3602000.00, 1057000.00),
(N'A15C112', N'656/246', N'A', N'15', 15, N'1 Bed L', 35.28, 5426250.00, 146000.00, 5150880.00, 3606000.00, 1058000.00),
(N'A15C213', N'656/247', N'A', N'15', 15, N'1 Bed Plus', 35.33, 5426250.00, 146000.00, 5158180.00, 3611000.00, 1060000.00),
(N'A15C214', N'656/248', N'A', N'15', 15, N'1 Bed Plus', 35.28, 5426250.00, 146000.00, 5150880.00, 3606000.00, 1058000.00),
(N'A15C215', N'656/249', N'A', N'15', 15, N'1 Bed Plus', 35.28, 5426250.00, 146000.00, 5150880.00, 3606000.00, 1058000.00),
(N'A15C216', N'656/250', N'A', N'15', 15, N'1 Bed Plus', 35.28, 5426250.00, 146000.00, 5150880.00, 3606000.00, 1058000.00),
(N'A15C217', N'656/251', N'A', N'15', 15, N'1 Bed Plus', 35.32, 5357500.00, 146000.00, 5156720.00, 3610000.00, 1060000.00),
(N'A15C218', N'656/252', N'A', N'15', 15, N'1 Bed Plus', 35.32, 5343750.00, 146000.00, 5156720.00, 3610000.00, 1060000.00),
(N'A15E119', N'656/253', N'A', N'15', 15, N'2 Bed 2 Bath', 58.16, 9636250.00, 165500.00, 9625480.00, 6738000.00, 1745000.00),
(N'A15E220', N'656/254', N'A', N'15', 15, N'2 Bed 2 Bath', 58.04, 9570000.00, 162000.00, 9402480.00, 6582000.00, 1741000.00),
(N'A15B1M21', N'656/255', N'A', N'15', 15, N'1 Bed M', 31.59, 4750000.00, 132000.00, 4169880.00, 2919000.00, 948000.00),
(N'A15B222', N'656/256', N'A', N'15', 15, N'1 Bed M', 31.94, 4807500.00, 132000.00, 4216080.00, 2951000.00, 958000.00),
(N'A15B2M23', N'656/257', N'A', N'15', 15, N'1 Bed M', 31.97, 4848750.00, 132000.00, 4220040.00, 2954000.00, 959000.00),
(N'A15B124', N'656/258', N'A', N'15', 15, N'1 Bed M', 32.16, 4823750.00, 132000.00, 4245120.00, 2972000.00, 965000.00),
(N'A15B1M25', N'656/259', N'A', N'15', 15, N'1 Bed M', 32.17, 4823750.00, 132000.00, 4246440.00, 2973000.00, 965000.00),
(N'A15C2M26', N'656/260', N'A', N'15', 15, N'1 Bed Plus', 35.03, 5335000.00, 146000.00, 5114380.00, 3580000.00, 1051000.00),
(N'A15C2M27', N'656/261', N'A', N'15', 15, N'1 Bed Plus', 35.06, 5335000.00, 146000.00, 5118760.00, 3583000.00, 1052000.00),
(N'A15A128', N'656/262', N'A', N'15', 15, N'1 Bed S', 30.00, 4555000.00, 142000.00, 4260000.00, 2982000.00, 900000.00),
(N'A15A1M29', N'656/263', N'A', N'15', 15, N'1 Bed S', 29.64, 4471250.00, 142000.00, 4208880.00, 2946000.00, 889000.00),
(N'A16A101', N'656/264', N'A', N'16', 16, N'1 Bed S', 29.63, 4536250.00, 143500.00, 4251905.00, 2976000.00, 889000.00),
(N'A16A202', N'656/265', N'A', N'16', 16, N'1 Bed S', 30.60, 4766250.00, 146500.00, 4482900.00, 3138000.00, 918000.00),
(N'A16D103', N'656/266', N'A', N'16', 16, N'2 Bed 1 Bath', 42.50, 6297500.00, 143500.00, 6098750.00, 4269000.00, 1275000.00),
(N'A16B2M04', N'656/267', N'A', N'16', 16, N'1 Bed M', 31.91, 4521250.00, 133500.00, 4259985.00, 2982000.00, 957000.00),
(N'A16B105', N'656/268', N'A', N'16', 16, N'1 Bed M', 32.16, 4561250.00, 133500.00, 4293360.00, 3005000.00, 965000.00),
(N'A16B1M06', N'656/269', N'A', N'16', 16, N'1 Bed M', 32.34, 4586250.00, 133500.00, 4317390.00, 3022000.00, 970000.00),
(N'A16D1M07', N'656/270', N'A', N'16', 16, N'2 Bed 1 Bath', 42.51, 6406250.00, 147000.00, 6248970.00, 4374000.00, 1275000.00),
(N'A16C208', N'656/271', N'A', N'16', 16, N'1 Bed Plus', 35.26, 5271250.00, 147500.00, 5200850.00, 3641000.00, 1058000.00),
(N'A16B109', N'656/272', N'A', N'16', 16, N'1 Bed M', 32.47, 4828750.00, 142000.00, 4610740.00, 3228000.00, 974000.00),
(N'A16B1M10', N'656/273', N'A', N'16', 16, N'1 Bed M', 32.39, 4890000.00, 142000.00, 4599380.00, 3220000.00, 972000.00),
(N'A16C111', N'656/274', N'A', N'16', 16, N'1 Bed L', 35.24, 5476250.00, 147500.00, 5197900.00, 3639000.00, 1057000.00),
(N'A16C112', N'656/275', N'A', N'16', 16, N'1 Bed L', 35.28, 5476250.00, 147500.00, 5203800.00, 3643000.00, 1058000.00),
(N'A16C213', N'656/276', N'A', N'16', 16, N'1 Bed Plus', 35.33, 5476250.00, 147500.00, 5211175.00, 3648000.00, 1060000.00),
(N'A16C214', N'656/277', N'A', N'16', 16, N'1 Bed Plus', 35.28, 5476250.00, 147500.00, 5203800.00, 3643000.00, 1058000.00),
(N'A16C215', N'656/278', N'A', N'16', 16, N'1 Bed Plus', 35.28, 5476250.00, 147500.00, 5203800.00, 3643000.00, 1058000.00),
(N'A16C216', N'656/279', N'A', N'16', 16, N'1 Bed Plus', 35.28, 5476250.00, 147500.00, 5203800.00, 3643000.00, 1058000.00),
(N'A16C217', N'656/280', N'A', N'16', 16, N'1 Bed Plus', 35.32, 5407500.00, 147500.00, 5209700.00, 3647000.00, 1060000.00),
(N'A16C218', N'656/281', N'A', N'16', 16, N'1 Bed Plus', 35.32, 5393750.00, 147500.00, 5209700.00, 3647000.00, 1060000.00),
(N'A16E119', N'656/282', N'A', N'16', 16, N'2 Bed 2 Bath', 58.16, 9718750.00, 167000.00, 9712720.00, 6799000.00, 1745000.00),
(N'A16E220', N'656/283', N'A', N'16', 16, N'2 Bed 2 Bath', 58.04, 9607500.00, 163500.00, 9489540.00, 6643000.00, 1741000.00),
(N'A16B1M21', N'656/284', N'A', N'16', 16, N'1 Bed M', 31.59, 4795000.00, 133500.00, 4217265.00, 2952000.00, 948000.00),
(N'A16B222', N'656/285', N'A', N'16', 16, N'1 Bed M', 31.94, 4852500.00, 133500.00, 4263990.00, 2985000.00, 958000.00),
(N'A16B2M23', N'656/286', N'A', N'16', 16, N'1 Bed M', 31.97, 4868750.00, 133500.00, 4267995.00, 2988000.00, 959000.00),
(N'A16B124', N'656/287', N'A', N'16', 16, N'1 Bed M', 32.16, 4868750.00, 133500.00, 4293360.00, 3005000.00, 965000.00),
(N'A16B1M25', N'656/288', N'A', N'16', 16, N'1 Bed M', 32.17, 4868750.00, 133500.00, 4294695.00, 3006000.00, 965000.00),
(N'A16C2M26', N'656/289', N'A', N'16', 16, N'1 Bed Plus', 35.03, 5385000.00, 147500.00, 5166925.00, 3617000.00, 1051000.00),
(N'A16C2M27', N'656/290', N'A', N'16', 16, N'1 Bed Plus', 35.06, 5385000.00, 147500.00, 5171350.00, 3620000.00, 1052000.00),
(N'A16A128', N'656/291', N'A', N'16', 16, N'1 Bed S', 30.00, 4575000.00, 143500.00, 4305000.00, 3014000.00, 900000.00),
(N'A16A1M29', N'656/292', N'A', N'16', 16, N'1 Bed S', 29.64, 4512500.00, 143500.00, 4253340.00, 2977000.00, 889000.00),
(N'A17A101', N'656/293', N'A', N'17', 17, N'1 Bed S', 29.63, 4578750.00, 145000.00, 4296350.00, 3007000.00, 889000.00),
(N'A17A202', N'656/294', N'A', N'17', 17, N'1 Bed S', 30.60, 4808750.00, 148000.00, 4528800.00, 3170000.00, 918000.00),
(N'A17D103', N'656/295', N'A', N'17', 17, N'2 Bed 1 Bath', 42.50, 6390000.00, 145000.00, 6162500.00, 4314000.00, 1275000.00),
(N'A17B2M04', N'656/296', N'A', N'17', 17, N'1 Bed M', 31.91, 4268750.00, 135000.00, 4307850.00, 3015000.00, 957000.00),
(N'A17B105', N'656/297', N'A', N'17', 17, N'1 Bed M', 32.16, 4631250.00, 135000.00, 4341600.00, 3039000.00, 965000.00),
(N'A17B1M06', N'656/298', N'A', N'17', 17, N'1 Bed M', 32.34, 4631250.00, 135000.00, 4365900.00, 3056000.00, 970000.00),
(N'A17D1M07', N'656/299', N'A', N'17', 17, N'2 Bed 1 Bath', 42.51, 6467500.00, 148500.00, 6312735.00, 4419000.00, 1275000.00),
(N'A17C208', N'656/300', N'A', N'17', 17, N'1 Bed Plus', 35.26, 5321250.00, 149000.00, 5253740.00, 3678000.00, 1058000.00),
(N'A17B109', N'656/301', N'A', N'17', 17, N'1 Bed M', 32.47, 4873750.00, 143500.00, 4659445.00, 3262000.00, 974000.00),
(N'A17B1M10', N'656/302', N'A', N'17', 17, N'1 Bed M', 32.39, 4935000.00, 143500.00, 4647965.00, 3254000.00, 972000.00),
(N'A17C111', N'656/303', N'A', N'17', 17, N'1 Bed L', 35.24, 5526250.00, 149000.00, 5250760.00, 3676000.00, 1057000.00),
(N'A17C112', N'656/304', N'A', N'17', 17, N'1 Bed L', 35.28, 5526250.00, 149000.00, 5256720.00, 3680000.00, 1058000.00),
(N'A17C213', N'656/305', N'A', N'17', 17, N'1 Bed Plus', 35.33, 5526250.00, 149000.00, 5264170.00, 3685000.00, 1060000.00),
(N'A17C214', N'656/306', N'A', N'17', 17, N'1 Bed Plus', 35.28, 5526250.00, 149000.00, 5256720.00, 3680000.00, 1058000.00),
(N'A17C215', N'656/307', N'A', N'17', 17, N'1 Bed Plus', 35.28, 5526250.00, 149000.00, 5256720.00, 3680000.00, 1058000.00),
(N'A17C216', N'656/308', N'A', N'17', 17, N'1 Bed Plus', 35.28, 5526250.00, 149000.00, 5256720.00, 3680000.00, 1058000.00),
(N'A17C217', N'656/309', N'A', N'17', 17, N'1 Bed Plus', 35.32, 5457500.00, 149000.00, 5262680.00, 3684000.00, 1060000.00),
(N'A17C218', N'656/310', N'A', N'17', 17, N'1 Bed Plus', 35.32, 5471250.00, 149000.00, 5262680.00, 3684000.00, 1060000.00),
(N'A17E119', N'656/311', N'A', N'17', 17, N'2 Bed 2 Bath', 58.16, 9800000.00, 168500.00, 9799960.00, 6860000.00, 1745000.00),
(N'A17E220', N'656/312', N'A', N'17', 17, N'2 Bed 2 Bath', 58.04, 9688750.00, 165000.00, 9576600.00, 6704000.00, 1741000.00),
(N'A17B1M21', N'656/313', N'A', N'17', 17, N'1 Bed M', 31.59, 4840000.00, 135000.00, 4264650.00, 2985000.00, 948000.00),
(N'A17B222', N'656/314', N'A', N'17', 17, N'1 Bed M', 31.94, 4897500.00, 135000.00, 4311900.00, 3018000.00, 958000.00),
(N'A17B2M23', N'656/315', N'A', N'17', 17, N'1 Bed M', 31.97, 4915000.00, 135000.00, 4315950.00, 3021000.00, 959000.00),
(N'A17B124', N'656/316', N'A', N'17', 17, N'1 Bed M', 32.16, 4915000.00, 135000.00, 4341600.00, 3039000.00, 965000.00),
(N'A17B1M25', N'656/317', N'A', N'17', 17, N'1 Bed M', 32.17, 4915000.00, 135000.00, 4342950.00, 3040000.00, 965000.00),
(N'A17C2M26', N'656/318', N'A', N'17', 17, N'1 Bed Plus', 35.03, 5435000.00, 149000.00, 5219470.00, 3654000.00, 1051000.00),
(N'A17C2M27', N'656/319', N'A', N'17', 17, N'1 Bed Plus', 35.06, 5435000.00, 149000.00, 5223940.00, 3657000.00, 1052000.00),
(N'A17A128', N'656/320', N'A', N'17', 17, N'1 Bed S', 30.00, 4617500.00, 145000.00, 4350000.00, 3045000.00, 900000.00),
(N'A17A1M29', N'656/321', N'A', N'17', 17, N'1 Bed S', 29.64, 4532500.00, 145000.00, 4297800.00, 3008000.00, 889000.00),
(N'A18A101', N'656/322', N'A', N'18', 18, N'1 Bed S', 29.63, 4655000.00, 146500.00, 4340795.00, 3039000.00, 889000.00),
(N'A18A202', N'656/323', N'A', N'18', 18, N'1 Bed S', 30.60, 4886250.00, 149500.00, 4574700.00, 3202000.00, 918000.00),
(N'A18D103', N'656/324', N'A', N'18', 18, N'2 Bed 1 Bath', 42.50, 6500000.00, 146500.00, 6226250.00, 4358000.00, 1275000.00),
(N'A18B2M04', N'656/325', N'A', N'18', 18, N'1 Bed M', 31.91, 4346250.00, 136500.00, 4355715.00, 3049000.00, 957000.00),
(N'A18B105', N'656/326', N'A', N'18', 18, N'1 Bed M', 32.16, 4688750.00, 136500.00, 4389840.00, 3073000.00, 965000.00),
(N'A18B1M06', N'656/327', N'A', N'18', 18, N'1 Bed M', 32.34, 4713750.00, 136500.00, 4414410.00, 3090000.00, 970000.00),
(N'A18D1M07', N'656/328', N'A', N'18', 18, N'2 Bed 1 Bath', 42.51, 6576250.00, 150000.00, 6376500.00, 4464000.00, 1275000.00),
(N'A18C208', N'656/329', N'A', N'18', 18, N'1 Bed Plus', 35.26, 5412500.00, 150500.00, 5306630.00, 3715000.00, 1058000.00),
(N'A18B109', N'656/330', N'A', N'18', 18, N'1 Bed M', 32.47, 4980000.00, 145000.00, 4708150.00, 3296000.00, 974000.00),
(N'A18B1M10', N'656/331', N'A', N'18', 18, N'1 Bed M', 32.39, 5017500.00, 145000.00, 4696550.00, 3288000.00, 972000.00),
(N'A18C111', N'656/332', N'A', N'18', 18, N'1 Bed L', 35.24, 5645000.00, 150500.00, 5303620.00, 3713000.00, 1057000.00),
(N'A18C112', N'656/333', N'A', N'18', 18, N'1 Bed L', 35.28, 5617500.00, 150500.00, 5309640.00, 3717000.00, 1058000.00),
(N'A18C213', N'656/334', N'A', N'18', 18, N'1 Bed Plus', 35.33, 5617500.00, 150500.00, 5317165.00, 3722000.00, 1060000.00),
(N'A18C214', N'656/335', N'A', N'18', 18, N'1 Bed Plus', 35.28, 5617500.00, 150500.00, 5309640.00, 3717000.00, 1058000.00),
(N'A18C215', N'656/336', N'A', N'18', 18, N'1 Bed Plus', 35.28, 5617500.00, 150500.00, 5309640.00, 3717000.00, 1058000.00),
(N'A18C216', N'656/337', N'A', N'18', 18, N'1 Bed Plus', 35.28, 5617500.00, 150500.00, 5309640.00, 3717000.00, 1058000.00),
(N'A18C217', N'656/338', N'A', N'18', 18, N'1 Bed Plus', 35.32, 5576250.00, 150500.00, 5315660.00, 3721000.00, 1060000.00),
(N'A18C218', N'656/339', N'A', N'18', 18, N'1 Bed Plus', 35.32, 5535000.00, 150500.00, 5315660.00, 3721000.00, 1060000.00),
(N'A18E119', N'656/340', N'A', N'18', 18, N'2 Bed 2 Bath', 58.16, 9948750.00, 170000.00, 9887200.00, 6921000.00, 1745000.00),
(N'A18E220', N'656/341', N'A', N'18', 18, N'2 Bed 2 Bath', 58.04, 9881250.00, 166500.00, 9663660.00, 6765000.00, 1741000.00),
(N'A18B1M21', N'656/342', N'A', N'18', 18, N'1 Bed M', 31.59, 4947500.00, 136500.00, 4312035.00, 3018000.00, 948000.00),
(N'A18B222', N'656/343', N'A', N'18', 18, N'1 Bed M', 31.94, 4980000.00, 136500.00, 4359810.00, 3052000.00, 958000.00),
(N'A18B2M23', N'656/344', N'A', N'18', 18, N'1 Bed M', 31.97, 5021250.00, 136500.00, 4363905.00, 3055000.00, 959000.00),
(N'A18B124', N'656/345', N'A', N'18', 18, N'1 Bed M', 32.16, 5021250.00, 136500.00, 4389840.00, 3073000.00, 965000.00),
(N'A18B1M25', N'656/346', N'A', N'18', 18, N'1 Bed M', 32.17, 4996250.00, 136500.00, 4391205.00, 3074000.00, 965000.00),
(N'A18C2M26', N'656/347', N'A', N'18', 18, N'1 Bed Plus', 35.03, 5526250.00, 150500.00, 5272015.00, 3690000.00, 1051000.00),
(N'A18C2M27', N'656/348', N'A', N'18', 18, N'1 Bed Plus', 35.06, 5526250.00, 150500.00, 5276530.00, 3694000.00, 1052000.00),
(N'A18A128', N'656/349', N'A', N'18', 18, N'1 Bed S', 30.00, 4716250.00, 146500.00, 4395000.00, 3077000.00, 900000.00),
(N'A18A1M29', N'656/350', N'A', N'18', 18, N'1 Bed S', 29.64, 4632500.00, 146500.00, 4342260.00, 3040000.00, 889000.00),
(N'A19A101', N'656/351', N'A', N'19', 19, N'1 Bed S', 29.63, 4755000.00, 148000.00, 4385240.00, 3070000.00, 889000.00),
(N'A19A202', N'656/352', N'A', N'19', 19, N'1 Bed S', 30.60, 4985000.00, 151000.00, 4620600.00, 3234000.00, 918000.00),
(N'A19D103', N'656/353', N'A', N'19', 19, N'2 Bed 1 Bath', 42.50, 6608750.00, 148000.00, 6290000.00, 4403000.00, 1275000.00),
(N'A19B2M04', N'656/354', N'A', N'19', 19, N'1 Bed M', 31.91, 4422500.00, 138000.00, 4403580.00, 3083000.00, 957000.00),
(N'A19B105', N'656/355', N'A', N'19', 19, N'1 Bed M', 32.16, 4795000.00, 138000.00, 4438080.00, 3107000.00, 965000.00),
(N'A19B1M06', N'656/356', N'A', N'19', 19, N'1 Bed M', 32.34, 4795000.00, 138000.00, 4462920.00, 3124000.00, 970000.00),
(N'A19D1M07', N'656/357', N'A', N'19', 19, N'2 Bed 1 Bath', 42.51, 6718750.00, 151500.00, 6440265.00, 4508000.00, 1275000.00),
(N'A19C208', N'656/358', N'A', N'19', 19, N'1 Bed Plus', 35.26, 5531250.00, 152000.00, 5359520.00, 3752000.00, 1058000.00),
(N'A19B109', N'656/359', N'A', N'19', 19, N'1 Bed M', 32.47, 5062500.00, 146500.00, 4756855.00, 3330000.00, 974000.00),
(N'A19B1M10', N'656/360', N'A', N'19', 19, N'1 Bed M', 32.39, 5123750.00, 146500.00, 4745135.00, 3322000.00, 972000.00),
(N'A19C111', N'656/361', N'A', N'19', 19, N'1 Bed L', 35.24, 5736250.00, 152000.00, 5356480.00, 3750000.00, 1057000.00),
(N'A19C112', N'656/362', N'A', N'19', 19, N'1 Bed L', 35.28, 5708750.00, 152000.00, 5362560.00, 3754000.00, 1058000.00),
(N'A19C213', N'656/363', N'A', N'19', 19, N'1 Bed Plus', 35.33, 5736250.00, 152000.00, 5370160.00, 3759000.00, 1060000.00),
(N'A19C214', N'656/364', N'A', N'19', 19, N'1 Bed Plus', 35.28, 5736250.00, 152000.00, 5362560.00, 3754000.00, 1058000.00),
(N'A19C215', N'656/365', N'A', N'19', 19, N'1 Bed Plus', 35.28, 5736250.00, 152000.00, 5362560.00, 3754000.00, 1058000.00),
(N'A19C216', N'656/366', N'A', N'19', 19, N'1 Bed Plus', 35.28, 5708750.00, 152000.00, 5362560.00, 3754000.00, 1058000.00),
(N'A19C217', N'656/367', N'A', N'19', 19, N'1 Bed Plus', 35.32, 5667500.00, 152000.00, 5368640.00, 3758000.00, 1060000.00),
(N'A19C218', N'656/368', N'A', N'19', 19, N'1 Bed Plus', 35.32, 5626250.00, 152000.00, 5368640.00, 3758000.00, 1060000.00),
(N'A19E119', N'656/369', N'A', N'19', 19, N'2 Bed 2 Bath', 58.16, 10096250.00, 171500.00, 9974440.00, 6982000.00, 1745000.00),
(N'A19E220', N'656/370', N'A', N'19', 19, N'2 Bed 2 Bath', 58.04, 10030000.00, 168000.00, 9750720.00, 6826000.00, 1741000.00),
(N'A19B1M21', N'656/371', N'A', N'19', 19, N'1 Bed M', 31.59, 5028750.00, 138000.00, 4359420.00, 3052000.00, 948000.00),
(N'A19B222', N'656/372', N'A', N'19', 19, N'1 Bed M', 31.94, 5062500.00, 138000.00, 4407720.00, 3085000.00, 958000.00),
(N'A19B2M23', N'656/373', N'A', N'19', 19, N'1 Bed M', 31.97, 5102500.00, 138000.00, 4411860.00, 3088000.00, 959000.00),
(N'A19B124', N'656/374', N'A', N'19', 19, N'1 Bed M', 32.16, 5102500.00, 138000.00, 4438080.00, 3107000.00, 965000.00),
(N'A19B1M25', N'656/375', N'A', N'19', 19, N'1 Bed M', 32.17, 5102500.00, 138000.00, 4439460.00, 3108000.00, 965000.00),
(N'A19C2M26', N'656/376', N'A', N'19', 19, N'1 Bed Plus', 35.03, 5645000.00, 152000.00, 5324560.00, 3727000.00, 1051000.00),
(N'A19C2M27', N'656/377', N'A', N'19', 19, N'1 Bed Plus', 35.06, 5645000.00, 152000.00, 5329120.00, 3730000.00, 1052000.00),
(N'A19A128', N'656/378', N'A', N'19', 19, N'1 Bed S', 30.00, 4793750.00, 148000.00, 4440000.00, 3108000.00, 900000.00),
(N'A19A1M29', N'656/379', N'A', N'19', 19, N'1 Bed S', 29.64, 4708750.00, 148000.00, 4386720.00, 3071000.00, 889000.00),
(N'A20A101', N'656/380', N'A', N'20', 20, N'1 Bed S', 29.63, 4808750.00, 149500.00, 4429685.00, 3101000.00, 889000.00),
(N'A20A202', N'656/381', N'A', N'20', 20, N'1 Bed S', 30.60, 5038750.00, 152500.00, 4666500.00, 3267000.00, 918000.00),
(N'A20D103', N'656/382', N'A', N'20', 20, N'2 Bed 1 Bath', 42.50, 6686250.00, 149500.00, 6353750.00, 4448000.00, 1275000.00),
(N'A20B2M04', N'656/383', N'A', N'20', 20, N'1 Bed M', 31.91, 4498750.00, 139500.00, 4451445.00, 3116000.00, 957000.00),
(N'A20B105', N'656/384', N'A', N'20', 20, N'1 Bed M', 32.16, 4852500.00, 139500.00, 4486320.00, 3140000.00, 965000.00),
(N'A20B1M06', N'656/385', N'A', N'20', 20, N'1 Bed M', 32.34, 4877500.00, 139500.00, 4511430.00, 3158000.00, 970000.00),
(N'A20D1M07', N'656/386', N'A', N'20', 20, N'2 Bed 1 Bath', 42.51, 6795000.00, 153000.00, 6504030.00, 4553000.00, 1275000.00),
(N'A20C208', N'656/387', N'A', N'20', 20, N'1 Bed Plus', 35.26, 5667500.00, 153500.00, 5412410.00, 3789000.00, 1058000.00),
(N'A20B109', N'656/388', N'A', N'20', 20, N'1 Bed M', 32.47, 5210000.00, 148000.00, 4805560.00, 3364000.00, 974000.00),
(N'A20B1M10', N'656/389', N'A', N'20', 20, N'1 Bed M', 32.39, 5246250.00, 148000.00, 4793720.00, 3356000.00, 972000.00),
(N'A20C111', N'656/390', N'A', N'20', 20, N'1 Bed L', 35.24, 5872500.00, 153500.00, 5409340.00, 3787000.00, 1057000.00),
(N'A20C112', N'656/391', N'A', N'20', 20, N'1 Bed L', 35.28, 5872500.00, 153500.00, 5415480.00, 3791000.00, 1058000.00),
(N'A20C213', N'656/392', N'A', N'20', 20, N'1 Bed Plus', 35.33, 5872500.00, 153500.00, 5423155.00, 3796000.00, 1060000.00),
(N'A20C214', N'656/393', N'A', N'20', 20, N'1 Bed Plus', 35.28, 5872500.00, 153500.00, 5415480.00, 3791000.00, 1058000.00),
(N'A20C215', N'656/394', N'A', N'20', 20, N'1 Bed Plus', 35.28, 5872500.00, 153500.00, 5415480.00, 3791000.00, 1058000.00),
(N'A20C216', N'656/395', N'A', N'20', 20, N'1 Bed Plus', 35.28, 5872500.00, 153500.00, 5415480.00, 3791000.00, 1058000.00),
(N'A20C217', N'656/396', N'A', N'20', 20, N'1 Bed Plus', 35.32, 5803750.00, 153500.00, 5421620.00, 3795000.00, 1060000.00),
(N'A20C218', N'656/397', N'A', N'20', 20, N'1 Bed Plus', 35.32, 5790000.00, 153500.00, 5421620.00, 3795000.00, 1060000.00),
(N'A20E119', N'656/398', N'A', N'20', 20, N'2 Bed 2 Bath', 58.16, 10363750.00, 173000.00, 10061680.00, 7043000.00, 1745000.00),
(N'A20E220', N'656/399', N'A', N'20', 20, N'2 Bed 2 Bath', 58.04, 10207500.00, 169500.00, 9837780.00, 6886000.00, 1741000.00),
(N'A20B1M21', N'656/400', N'A', N'20', 20, N'1 Bed M', 31.59, 5127500.00, 139500.00, 4406805.00, 3085000.00, 948000.00),
(N'A20B222', N'656/401', N'A', N'20', 20, N'1 Bed M', 31.94, 5160000.00, 139500.00, 4455630.00, 3119000.00, 958000.00),
(N'A20B2M23', N'656/402', N'A', N'20', 20, N'1 Bed M', 31.97, 5226250.00, 139500.00, 4459815.00, 3122000.00, 959000.00),
(N'A20B124', N'656/403', N'A', N'20', 20, N'1 Bed M', 32.16, 5201250.00, 139500.00, 4486320.00, 3140000.00, 965000.00),
(N'A20B1M25', N'656/404', N'A', N'20', 20, N'1 Bed M', 32.17, 5201250.00, 139500.00, 4487715.00, 3141000.00, 965000.00),
(N'A20C2M26', N'656/405', N'A', N'20', 20, N'1 Bed Plus', 35.03, 5753750.00, 153500.00, 5377105.00, 3764000.00, 1051000.00),
(N'A20C2M27', N'656/406', N'A', N'20', 20, N'1 Bed Plus', 35.06, 5753750.00, 153500.00, 5381710.00, 3767000.00, 1052000.00),
(N'A20A128', N'656/407', N'A', N'20', 20, N'1 Bed S', 30.00, 4908750.00, 149500.00, 4485000.00, 3140000.00, 900000.00),
(N'A20A1M29', N'656/408', N'A', N'20', 20, N'1 Bed S', 29.64, 4823750.00, 149500.00, 4431180.00, 3102000.00, 889000.00),
(N'A21A101', N'656/409', N'A', N'21', 21, N'1 Bed S', 29.63, 4886250.00, 151000.00, 4474130.00, 3132000.00, 889000.00),
(N'A21A202', N'656/410', N'A', N'21', 21, N'1 Bed S', 30.60, 5116250.00, 154000.00, 4712400.00, 3299000.00, 918000.00),
(N'A21D103', N'656/411', N'A', N'21', 21, N'2 Bed 1 Bath', 42.50, 6795000.00, 151000.00, 6417500.00, 4492000.00, 1275000.00),
(N'A21B2M04', N'656/412', N'A', N'21', 21, N'1 Bed M', 31.91, 4918750.00, 141000.00, 4499310.00, 3150000.00, 957000.00),
(N'A21B105', N'656/413', N'A', N'21', 21, N'1 Bed M', 32.16, 4960000.00, 141000.00, 4534560.00, 3174000.00, 965000.00),
(N'A21B1M06', N'656/414', N'A', N'21', 21, N'1 Bed M', 32.34, 4960000.00, 141000.00, 4559940.00, 3192000.00, 970000.00),
(N'A21D1M07', N'656/415', N'A', N'21', 21, N'2 Bed 1 Bath', 42.51, 6903750.00, 154500.00, 6567795.00, 4597000.00, 1275000.00),
(N'A21C208', N'656/416', N'A', N'21', 21, N'1 Bed Plus', 35.26, 5758750.00, 155000.00, 5465300.00, 3826000.00, 1058000.00),
(N'A21B109', N'656/417', N'A', N'21', 21, N'1 Bed M', 32.47, 5291250.00, 149500.00, 4854265.00, 3398000.00, 974000.00),
(N'A21B1M10', N'656/418', N'A', N'21', 21, N'1 Bed M', 32.39, 5328750.00, 149500.00, 4842305.00, 3390000.00, 972000.00),
(N'A21C111', N'656/419', N'A', N'21', 21, N'1 Bed L', 35.24, 5963750.00, 155000.00, 5462200.00, 3824000.00, 1057000.00),
(N'A21C112', N'656/420', N'A', N'21', 21, N'1 Bed L', 35.28, 5963750.00, 155000.00, 5468400.00, 3828000.00, 1058000.00),
(N'A21C213', N'656/421', N'A', N'21', 21, N'1 Bed Plus', 35.33, 5963750.00, 155000.00, 5476150.00, 3833000.00, 1060000.00),
(N'A21C214', N'656/422', N'A', N'21', 21, N'1 Bed Plus', 35.28, 5963750.00, 155000.00, 5468400.00, 3828000.00, 1058000.00),
(N'A21C215', N'656/423', N'A', N'21', 21, N'1 Bed Plus', 35.28, 5963750.00, 155000.00, 5468400.00, 3828000.00, 1058000.00),
(N'A21C216', N'656/424', N'A', N'21', 21, N'1 Bed Plus', 35.28, 5963750.00, 155000.00, 5468400.00, 3828000.00, 1058000.00),
(N'A21C217', N'656/425', N'A', N'21', 21, N'1 Bed Plus', 35.32, 5895000.00, 155000.00, 5474600.00, 3832000.00, 1060000.00),
(N'A21C218', N'656/426', N'A', N'21', 21, N'1 Bed Plus', 35.32, 5881250.00, 155000.00, 5474600.00, 3832000.00, 1060000.00),
(N'A21E119', N'656/427', N'A', N'21', 21, N'2 Bed 2 Bath', 58.16, 10512500.00, 174500.00, 10148920.00, 7104000.00, 1745000.00),
(N'A21E220', N'656/428', N'A', N'21', 21, N'2 Bed 2 Bath', 58.04, 10356250.00, 171000.00, 9924840.00, 6947000.00, 1741000.00),
(N'A21B1M21', N'656/429', N'A', N'21', 21, N'1 Bed M', 31.59, 5210000.00, 141000.00, 4454190.00, 3118000.00, 948000.00),
(N'A21B222', N'656/430', N'A', N'21', 21, N'1 Bed M', 31.94, 5242500.00, 141000.00, 4503540.00, 3152000.00, 958000.00),
(N'A21B2M23', N'656/431', N'A', N'21', 21, N'1 Bed M', 31.97, 5283750.00, 141000.00, 4507770.00, 3155000.00, 959000.00),
(N'A21B124', N'656/432', N'A', N'21', 21, N'1 Bed M', 32.16, 5283750.00, 141000.00, 4534560.00, 3174000.00, 965000.00),
(N'A21B1M25', N'656/433', N'A', N'21', 21, N'1 Bed M', 32.17, 5283750.00, 141000.00, 4535970.00, 3175000.00, 965000.00),
(N'A21C2M26', N'656/434', N'A', N'21', 21, N'1 Bed Plus', 35.03, 5845000.00, 155000.00, 5429650.00, 3801000.00, 1051000.00),
(N'A21C2M27', N'656/435', N'A', N'21', 21, N'1 Bed Plus', 35.06, 5845000.00, 155000.00, 5434300.00, 3804000.00, 1052000.00),
(N'A21A128', N'656/436', N'A', N'21', 21, N'1 Bed S', 30.00, 4962500.00, 151000.00, 4530000.00, 3171000.00, 900000.00),
(N'A21A1M29', N'656/437', N'A', N'21', 21, N'1 Bed S', 29.64, 4901250.00, 151000.00, 4475640.00, 3133000.00, 889000.00),
(N'A22A101', N'656/438', N'A', N'22', 22, N'1 Bed S', 29.63, 4985000.00, 152500.00, 4518575.00, 3163000.00, 889000.00),
(N'A22A202', N'656/439', N'A', N'22', 22, N'1 Bed S', 30.60, 5216250.00, 155500.00, 4758300.00, 3331000.00, 918000.00),
(N'A22D103', N'656/440', N'A', N'22', 22, N'2 Bed 1 Bath', 42.50, 6937500.00, 152500.00, 6481250.00, 4537000.00, 1275000.00),
(N'A22B2M04', N'656/441', N'A', N'22', 22, N'1 Bed M', 31.91, 5000000.00, 142500.00, 4547175.00, 3183000.00, 957000.00),
(N'A22B105', N'656/442', N'A', N'22', 22, N'1 Bed M', 32.16, 5041250.00, 142500.00, 4582800.00, 3208000.00, 965000.00),
(N'A22B1M06', N'656/443', N'A', N'22', 22, N'1 Bed M', 32.34, 5041250.00, 142500.00, 4608450.00, 3226000.00, 970000.00),
(N'A22D1M07', N'656/444', N'A', N'22', 22, N'2 Bed 1 Bath', 42.51, 7013750.00, 156000.00, 6631560.00, 4642000.00, 1275000.00),
(N'A22C208', N'656/445', N'A', N'22', 22, N'1 Bed Plus', 35.26, 5850000.00, 156500.00, 5518190.00, 3863000.00, 1058000.00),
(N'A22B109', N'656/446', N'A', N'22', 22, N'1 Bed M', 32.47, 5373750.00, 151000.00, 4902970.00, 3432000.00, 974000.00),
(N'A22B1M10', N'656/447', N'A', N'22', 22, N'1 Bed M', 32.39, 5435000.00, 151000.00, 4890890.00, 3424000.00, 972000.00),
(N'A22C111', N'656/448', N'A', N'22', 22, N'1 Bed L', 35.24, 6082500.00, 156500.00, 5515060.00, 3861000.00, 1057000.00),
(N'A22C112', N'656/449', N'A', N'22', 22, N'1 Bed L', 35.28, 6082500.00, 156500.00, 5521320.00, 3865000.00, 1058000.00),
(N'A22C213', N'656/450', N'A', N'22', 22, N'1 Bed Plus', 35.33, 6082500.00, 156500.00, 5529145.00, 3870000.00, 1060000.00),
(N'A22C214', N'656/451', N'A', N'22', 22, N'1 Bed Plus', 35.28, 6082500.00, 156500.00, 5521320.00, 3865000.00, 1058000.00),
(N'A22C215', N'656/452', N'A', N'22', 22, N'1 Bed Plus', 35.28, 6082500.00, 156500.00, 5521320.00, 3865000.00, 1058000.00),
(N'A22C216', N'656/453', N'A', N'22', 22, N'1 Bed Plus', 35.28, 6082500.00, 156500.00, 5521320.00, 3865000.00, 1058000.00),
(N'A22C217', N'656/454', N'A', N'22', 22, N'1 Bed Plus', 35.32, 6013750.00, 156500.00, 5527580.00, 3869000.00, 1060000.00),
(N'A22C218', N'656/455', N'A', N'22', 22, N'1 Bed Plus', 35.32, 5972500.00, 156500.00, 5527580.00, 3869000.00, 1060000.00),
(N'A22E119', N'656/456', N'A', N'22', 22, N'2 Bed 2 Bath', 58.16, 10660000.00, 176000.00, 10236160.00, 7165000.00, 1745000.00),
(N'A22E220', N'656/457', N'A', N'22', 22, N'2 Bed 2 Bath', 58.04, 10548750.00, 172500.00, 10011900.00, 7008000.00, 1741000.00),
(N'A22B1M21', N'656/458', N'A', N'22', 22, N'1 Bed M', 31.59, 5316250.00, 142500.00, 4501575.00, 3151000.00, 948000.00),
(N'A22B222', N'656/459', N'A', N'22', 22, N'1 Bed M', 31.94, 5348750.00, 142500.00, 4551450.00, 3186000.00, 958000.00),
(N'A22B2M23', N'656/460', N'A', N'22', 22, N'1 Bed M', 31.97, 5390000.00, 142500.00, 4555725.00, 3189000.00, 959000.00),
(N'A22B124', N'656/461', N'A', N'22', 22, N'1 Bed M', 32.16, 5390000.00, 142500.00, 4582800.00, 3208000.00, 965000.00),
(N'A22B1M25', N'656/462', N'A', N'22', 22, N'1 Bed M', 32.17, 5390000.00, 142500.00, 4584225.00, 3209000.00, 965000.00),
(N'A22C2M26', N'656/463', N'A', N'22', 22, N'1 Bed Plus', 35.03, 5963750.00, 156500.00, 5482195.00, 3838000.00, 1051000.00),
(N'A22C2M27', N'656/464', N'A', N'22', 22, N'1 Bed Plus', 35.06, 5963750.00, 156500.00, 5486890.00, 3841000.00, 1052000.00),
(N'A22A128', N'656/465', N'A', N'22', 22, N'1 Bed S', 30.00, 5062500.00, 152500.00, 4575000.00, 3203000.00, 900000.00),
(N'A22A1M29', N'656/466', N'A', N'22', 22, N'1 Bed S', 29.64, 4977500.00, 152500.00, 4520100.00, 3164000.00, 889000.00),
(N'A23A101', N'656/467', N'A', N'23', 23, N'1 Bed S', 29.63, 5062500.00, 154000.00, 4563020.00, 3194000.00, 889000.00),
(N'A23A202', N'656/468', N'A', N'23', 23, N'1 Bed S', 30.60, 5270000.00, 157000.00, 4804200.00, 3363000.00, 918000.00),
(N'A23D103', N'656/469', N'A', N'23', 23, N'2 Bed 1 Bath', 42.50, 7046250.00, 154000.00, 6545000.00, 4582000.00, 1275000.00),
(N'A23B2M04', N'656/470', N'A', N'23', 23, N'1 Bed M', 31.91, 5082500.00, 144000.00, 4595040.00, 3217000.00, 957000.00),
(N'A23B105', N'656/471', N'A', N'23', 23, N'1 Bed M', 32.16, 5123750.00, 144000.00, 4631040.00, 3242000.00, 965000.00),
(N'A23B1M06', N'656/472', N'A', N'23', 23, N'1 Bed M', 32.34, 5123750.00, 144000.00, 4656960.00, 3260000.00, 970000.00),
(N'A23D1M07', N'656/473', N'A', N'23', 23, N'2 Bed 1 Bath', 42.51, 7122500.00, 157500.00, 6695325.00, 4687000.00, 1275000.00),
(N'A23C208', N'656/474', N'A', N'23', 23, N'1 Bed Plus', 35.26, 5941250.00, 158000.00, 5571080.00, 3900000.00, 1058000.00),
(N'A23B109', N'656/475', N'A', N'23', 23, N'1 Bed M', 32.47, 5455000.00, 152500.00, 4951675.00, 3466000.00, 974000.00),
(N'A23B1M10', N'656/476', N'A', N'23', 23, N'1 Bed M', 32.39, 5492500.00, 152500.00, 4939475.00, 3458000.00, 972000.00),
(N'A23C111', N'656/477', N'A', N'23', 23, N'1 Bed L', 35.24, 6146250.00, 158000.00, 5567920.00, 3898000.00, 1057000.00),
(N'A23C112', N'656/478', N'A', N'23', 23, N'1 Bed L', 35.28, 6146250.00, 158000.00, 5574240.00, 3902000.00, 1058000.00),
(N'A23C213', N'656/479', N'A', N'23', 23, N'1 Bed Plus', 35.33, 6146250.00, 158000.00, 5582140.00, 3907000.00, 1060000.00),
(N'A23C214', N'656/480', N'A', N'23', 23, N'1 Bed Plus', 35.28, 6146250.00, 158000.00, 5574240.00, 3902000.00, 1058000.00),
(N'A23C215', N'656/481', N'A', N'23', 23, N'1 Bed Plus', 35.28, 6146250.00, 158000.00, 5574240.00, 3902000.00, 1058000.00),
(N'A23C216', N'656/482', N'A', N'23', 23, N'1 Bed Plus', 35.28, 6146250.00, 158000.00, 5574240.00, 3902000.00, 1058000.00),
(N'A23C217', N'656/483', N'A', N'23', 23, N'1 Bed Plus', 35.32, 6077500.00, 158000.00, 5580560.00, 3906000.00, 1060000.00),
(N'A23C218', N'656/484', N'A', N'23', 23, N'1 Bed Plus', 35.32, 6091250.00, 158000.00, 5580560.00, 3906000.00, 1060000.00),
(N'A23E119', N'656/485', N'A', N'23', 23, N'2 Bed 2 Bath', 58.16, 10808750.00, 177500.00, 10323400.00, 7226000.00, 1745000.00),
(N'A23E220', N'656/486', N'A', N'23', 23, N'2 Bed 2 Bath', 58.04, 10653750.00, 174000.00, 10098960.00, 7069000.00, 1741000.00),
(N'A23B1M21', N'656/487', N'A', N'23', 23, N'1 Bed M', 31.59, 5373750.00, 144000.00, 4548960.00, 3184000.00, 948000.00),
(N'A23B222', N'656/488', N'A', N'23', 23, N'1 Bed M', 31.94, 5406250.00, 144000.00, 4599360.00, 3220000.00, 958000.00),
(N'A23B2M23', N'656/489', N'A', N'23', 23, N'1 Bed M', 31.97, 5447500.00, 144000.00, 4603680.00, 3223000.00, 959000.00),
(N'A23B124', N'656/490', N'A', N'23', 23, N'1 Bed M', 32.16, 5447500.00, 144000.00, 4631040.00, 3242000.00, 965000.00),
(N'A23B1M25', N'656/491', N'A', N'23', 23, N'1 Bed M', 32.17, 5447500.00, 144000.00, 4632480.00, 3243000.00, 965000.00),
(N'A23C2M26', N'656/492', N'A', N'23', 23, N'1 Bed Plus', 35.03, 6027500.00, 158000.00, 5534740.00, 3874000.00, 1051000.00),
(N'A23C2M27', N'656/493', N'A', N'23', 23, N'1 Bed Plus', 35.06, 6055000.00, 158000.00, 5539480.00, 3878000.00, 1052000.00),
(N'A23A128', N'656/494', N'A', N'23', 23, N'1 Bed S', 30.00, 5138750.00, 154000.00, 4620000.00, 3234000.00, 900000.00),
(N'A23A1M29', N'656/495', N'A', N'23', 23, N'1 Bed S', 29.64, 5055000.00, 154000.00, 4564560.00, 3195000.00, 889000.00),
(N'A24A101', N'656/496', N'A', N'24', 24, N'1 Bed S', 29.63, 5116250.00, 155500.00, 4607465.00, 3225000.00, 889000.00),
(N'A24A202', N'656/497', N'A', N'24', 24, N'1 Bed S', 30.60, 5346250.00, 158500.00, 4850100.00, 3395000.00, 918000.00),
(N'A24D103', N'656/498', N'A', N'24', 24, N'2 Bed 1 Bath', 42.50, 7122500.00, 155500.00, 6608750.00, 4626000.00, 1275000.00),
(N'A24B2M04', N'656/499', N'A', N'24', 24, N'1 Bed M', 31.91, 5165000.00, 145500.00, 4642905.00, 3250000.00, 957000.00),
(N'A24B105', N'656/500', N'A', N'24', 24, N'1 Bed M', 32.16, 5205000.00, 145500.00, 4679280.00, 3275000.00, 965000.00),
(N'A24B1M06', N'656/501', N'A', N'24', 24, N'1 Bed M', 32.34, 5205000.00, 145500.00, 4705470.00, 3294000.00, 970000.00),
(N'A24D1M07', N'656/502', N'A', N'24', 24, N'2 Bed 1 Bath', 42.51, 7232500.00, 159000.00, 6759090.00, 4731000.00, 1275000.00),
(N'A24C208', N'656/503', N'A', N'24', 24, N'1 Bed Plus', 35.26, 6032500.00, 159500.00, 5623970.00, 3937000.00, 1058000.00),
(N'A24B109', N'656/504', N'A', N'24', 24, N'1 Bed M', 32.47, 5512500.00, 154000.00, 5000380.00, 3500000.00, 974000.00),
(N'A24B1M10', N'656/505', N'A', N'24', 24, N'1 Bed M', 32.39, 5598750.00, 154000.00, 4988060.00, 3492000.00, 972000.00),
(N'A24C111', N'656/506', N'A', N'24', 24, N'1 Bed L', 35.24, 6237500.00, 159500.00, 5620780.00, 3935000.00, 1057000.00),
(N'A24C112', N'656/507', N'A', N'24', 24, N'1 Bed L', 35.28, 6237500.00, 159500.00, 5627160.00, 3939000.00, 1058000.00),
(N'A24C213', N'656/508', N'A', N'24', 24, N'1 Bed Plus', 35.33, 6237500.00, 159500.00, 5635135.00, 3945000.00, 1060000.00),
(N'A24C214', N'656/509', N'A', N'24', 24, N'1 Bed Plus', 35.28, 6237500.00, 159500.00, 5627160.00, 3939000.00, 1058000.00),
(N'A24C215', N'656/510', N'A', N'24', 24, N'1 Bed Plus', 35.28, 6237500.00, 159500.00, 5627160.00, 3939000.00, 1058000.00),
(N'A24C216', N'656/511', N'A', N'24', 24, N'1 Bed Plus', 35.28, 6237500.00, 159500.00, 5627160.00, 3939000.00, 1058000.00),
(N'A24C217', N'656/512', N'A', N'24', 24, N'1 Bed Plus', 35.32, 6168750.00, 159500.00, 5633540.00, 3943000.00, 1060000.00),
(N'A24C218', N'656/513', N'A', N'24', 24, N'1 Bed Plus', 35.32, 6155000.00, 159500.00, 5633540.00, 3943000.00, 1060000.00),
(N'A24E119', N'656/514', N'A', N'24', 24, N'2 Bed 2 Bath', 58.16, 10957500.00, 179000.00, 10410640.00, 7287000.00, 1745000.00),
(N'A24E220', N'656/515', N'A', N'24', 24, N'2 Bed 2 Bath', 58.04, 10801250.00, 175500.00, 10186020.00, 7130000.00, 1741000.00),
(N'A24B1M21', N'656/516', N'A', N'24', 24, N'1 Bed M', 31.59, 5480000.00, 145500.00, 4596345.00, 3217000.00, 948000.00),
(N'A24B222', N'656/517', N'A', N'24', 24, N'1 Bed M', 31.94, 5512500.00, 145500.00, 4647270.00, 3253000.00, 958000.00),
(N'A24B2M23', N'656/518', N'A', N'24', 24, N'1 Bed M', 31.97, 5530000.00, 145500.00, 4651635.00, 3256000.00, 959000.00),
(N'A24B124', N'656/519', N'A', N'24', 24, N'1 Bed M', 32.16, 5553750.00, 145500.00, 4679280.00, 3275000.00, 965000.00),
(N'A24B1M25', N'656/520', N'A', N'24', 24, N'1 Bed M', 32.17, 5530000.00, 145500.00, 4680735.00, 3277000.00, 965000.00),
(N'A24C2M26', N'656/521', N'A', N'24', 24, N'1 Bed Plus', 35.03, 6118750.00, 159500.00, 5587285.00, 3911000.00, 1051000.00),
(N'A24C2M27', N'656/522', N'A', N'24', 24, N'1 Bed Plus', 35.06, 6118750.00, 159500.00, 5592070.00, 3914000.00, 1052000.00),
(N'A24A128', N'656/523', N'A', N'24', 24, N'1 Bed S', 30.00, 5216250.00, 155500.00, 4665000.00, 3266000.00, 900000.00),
(N'A24A1M29', N'656/524', N'A', N'24', 24, N'1 Bed S', 29.64, 5131250.00, 155500.00, 4609020.00, 3226000.00, 889000.00),
(N'A25F101', N'656/525', N'A', N'25', 25, N'1 Bed S', 29.63, 5292500.00, 166000.00, 4918580.00, 3443000.00, 889000.00),
(N'A25F202', N'656/526', N'A', N'25', 25, N'1 Bed S', 30.60, 5522500.00, 169000.00, 5171400.00, 3620000.00, 918000.00),
(N'A25I103', N'656/527', N'A', N'25', 25, N'2 Bed 1 Bath', 42.50, 7341250.00, 159000.00, 6757500.00, 4730000.00, 1275000.00),
(N'A25G2M04', N'656/528', N'A', N'25', 25, N'1 Bed M', 31.91, 5328750.00, 154000.00, 4914140.00, 3440000.00, 957000.00),
(N'A25G105', N'656/529', N'A', N'25', 25, N'1 Bed M', 32.16, 5370000.00, 154000.00, 4952640.00, 3467000.00, 965000.00),
(N'A25G1M06', N'656/530', N'A', N'25', 25, N'1 Bed M', 32.34, 5370000.00, 154000.00, 4980360.00, 3486000.00, 970000.00),
(N'A25I1M07', N'656/531', N'A', N'25', 25, N'2 Bed 1 Bath', 42.51, 7451250.00, 162500.00, 6907875.00, 4836000.00, 1275000.00),
(N'A25H208', N'656/532', N'A', N'25', 25, N'1 Bed Plus', 35.26, 6241250.00, 170000.00, 5994200.00, 4196000.00, 1058000.00),
(N'A25G109', N'656/533', N'A', N'25', 25, N'1 Bed M', 32.47, 5701250.00, 162500.00, 5276375.00, 3693000.00, 974000.00),
(N'A25G1M10', N'656/534', N'A', N'25', 25, N'1 Bed M', 32.39, 5762500.00, 162500.00, 5263375.00, 3684000.00, 972000.00),
(N'A25H111', N'656/535', N'A', N'25', 25, N'1 Bed L', 35.24, 6446250.00, 170000.00, 5990800.00, 4194000.00, 1057000.00),
(N'A25H112', N'656/536', N'A', N'25', 25, N'1 Bed L', 35.28, 6418750.00, 170000.00, 5997600.00, 4198000.00, 1058000.00),
(N'A25H213', N'656/537', N'A', N'25', 25, N'1 Bed Plus', 35.33, 6446250.00, 170000.00, 6006100.00, 4204000.00, 1060000.00),
(N'A25H214', N'656/538', N'A', N'25', 25, N'1 Bed Plus', 35.28, 6446250.00, 170000.00, 5997600.00, 4198000.00, 1058000.00),
(N'A25H215', N'656/539', N'A', N'25', 25, N'1 Bed Plus', 35.28, 6446250.00, 170000.00, 5997600.00, 4198000.00, 1058000.00),
(N'A25H216', N'656/540', N'A', N'25', 25, N'1 Bed Plus', 35.28, 6446250.00, 170000.00, 5997600.00, 4198000.00, 1058000.00),
(N'A25H217', N'656/541', N'A', N'25', 25, N'1 Bed Plus', 35.32, 6377500.00, 170000.00, 6004400.00, 4203000.00, 1060000.00),
(N'A25H218', N'656/542', N'A', N'25', 25, N'1 Bed Plus', 35.32, 6365000.00, 170000.00, 6004400.00, 4203000.00, 1060000.00),
(N'A25J119', N'656/543', N'A', N'25', 25, N'2 Bed 2 Bath', 58.16, 11253750.00, 187000.00, 10875920.00, 7613000.00, 1745000.00),
(N'A25J220', N'656/544', N'A', N'25', 25, N'2 Bed 2 Bath', 58.04, 11142500.00, 183500.00, 10650340.00, 7455000.00, 1741000.00),
(N'A25G1M21', N'656/545', N'A', N'25', 25, N'1 Bed M', 31.59, 5643750.00, 154000.00, 4864860.00, 3405000.00, 948000.00),
(N'A25G222', N'656/546', N'A', N'25', 25, N'1 Bed M', 31.94, 5677500.00, 154000.00, 4918760.00, 3443000.00, 958000.00),
(N'A25G2M23', N'656/547', N'A', N'25', 25, N'1 Bed M', 31.97, 5717500.00, 154000.00, 4923380.00, 3446000.00, 959000.00),
(N'A25G124', N'656/548', N'A', N'25', 25, N'1 Bed M', 32.16, 5717500.00, 154000.00, 4952640.00, 3467000.00, 965000.00),
(N'A25G1M25', N'656/549', N'A', N'25', 25, N'1 Bed M', 32.17, 5717500.00, 154000.00, 4954180.00, 3468000.00, 965000.00),
(N'A25H2M26', N'656/550', N'A', N'25', 25, N'1 Bed Plus', 35.03, 6327500.00, 170000.00, 5955100.00, 4169000.00, 1051000.00),
(N'A25H2M27', N'656/551', N'A', N'25', 25, N'1 Bed Plus', 35.06, 6327500.00, 170000.00, 5960200.00, 4172000.00, 1052000.00),
(N'A25F128', N'656/552', N'A', N'25', 25, N'1 Bed S', 30.00, 5370000.00, 166000.00, 4980000.00, 3486000.00, 900000.00),
(N'A25F1M29', N'656/553', N'A', N'25', 25, N'1 Bed S', 29.64, 5285000.00, 166000.00, 4920240.00, 3444000.00, 889000.00),
(N'A26F101', N'656/554', N'A', N'26', 26, N'1 Bed S', 29.63, 5331250.00, 167500.00, 4963025.00, 3474000.00, 889000.00),
(N'A26F202', N'656/555', N'A', N'26', 26, N'1 Bed S', 30.60, 5538750.00, 170500.00, 5217300.00, 3652000.00, 918000.00),
(N'A26I103', N'656/556', N'A', N'26', 26, N'2 Bed 1 Bath', 42.50, 7396250.00, 160500.00, 6821250.00, 4775000.00, 1275000.00),
(N'A26G2M04', N'656/557', N'A', N'26', 26, N'1 Bed M', 31.91, 5370000.00, 155500.00, 4962005.00, 3473000.00, 957000.00),
(N'A26G105', N'656/558', N'A', N'26', 26, N'1 Bed M', 32.16, 5410000.00, 155500.00, 5000880.00, 3501000.00, 965000.00),
(N'A26G1M06', N'656/559', N'A', N'26', 26, N'1 Bed M', 32.34, 5410000.00, 155500.00, 5028870.00, 3520000.00, 970000.00),
(N'A26I1M07', N'656/560', N'A', N'26', 26, N'2 Bed 1 Bath', 42.51, 7506250.00, 164000.00, 6971640.00, 4880000.00, 1275000.00),
(N'A26H208', N'656/561', N'A', N'26', 26, N'1 Bed Plus', 35.26, 6260000.00, 171500.00, 6047090.00, 4233000.00, 1058000.00),
(N'A26G109', N'656/562', N'A', N'26', 26, N'1 Bed M', 32.47, 5718750.00, 164000.00, 5325080.00, 3728000.00, 974000.00),
(N'A26G1M10', N'656/563', N'A', N'26', 26, N'1 Bed M', 32.39, 5780000.00, 164000.00, 5311960.00, 3718000.00, 972000.00),
(N'A26H111', N'656/564', N'A', N'26', 26, N'1 Bed L', 35.24, 6465000.00, 171500.00, 6043660.00, 4231000.00, 1057000.00),
(N'A26H112', N'656/565', N'A', N'26', 26, N'1 Bed L', 35.28, 6465000.00, 171500.00, 6050520.00, 4235000.00, 1058000.00),
(N'A26H213', N'656/566', N'A', N'26', 26, N'1 Bed Plus', 35.33, 6465000.00, 171500.00, 6059095.00, 4241000.00, 1060000.00),
(N'A26H214', N'656/567', N'A', N'26', 26, N'1 Bed Plus', 35.28, 6465000.00, 171500.00, 6050520.00, 4235000.00, 1058000.00),
(N'A26H215', N'656/568', N'A', N'26', 26, N'1 Bed Plus', 35.28, 6465000.00, 171500.00, 6050520.00, 4235000.00, 1058000.00),
(N'A26H216', N'656/569', N'A', N'26', 26, N'1 Bed Plus', 35.28, 6465000.00, 171500.00, 6050520.00, 4235000.00, 1058000.00),
(N'A26H217', N'656/570', N'A', N'26', 26, N'1 Bed Plus', 35.32, 6396250.00, 171500.00, 6057380.00, 4240000.00, 1060000.00),
(N'A26H218', N'656/571', N'A', N'26', 26, N'1 Bed Plus', 35.32, 6382500.00, 171500.00, 6057380.00, 4240000.00, 1060000.00),
(N'A26J119', N'656/572', N'A', N'26', 26, N'2 Bed 2 Bath', 58.16, 11328750.00, 188500.00, 10963160.00, 7674000.00, 1745000.00),
(N'A26J220', N'656/573', N'A', N'26', 26, N'2 Bed 2 Bath', 58.04, 11172500.00, 185000.00, 10737400.00, 7516000.00, 1741000.00),
(N'A26G1M21', N'656/574', N'A', N'26', 26, N'1 Bed M', 31.59, 5685000.00, 155500.00, 4912245.00, 3439000.00, 948000.00),
(N'A26G222', N'656/575', N'A', N'26', 26, N'1 Bed M', 31.94, 5717500.00, 155500.00, 4966670.00, 3477000.00, 958000.00),
(N'A26G2M23', N'656/576', N'A', N'26', 26, N'1 Bed M', 31.97, 5735000.00, 155500.00, 4971335.00, 3480000.00, 959000.00),
(N'A26G124', N'656/577', N'A', N'26', 26, N'1 Bed M', 32.16, 5735000.00, 155500.00, 5000880.00, 3501000.00, 965000.00),
(N'A26G1M25', N'656/578', N'A', N'26', 26, N'1 Bed M', 32.17, 5758750.00, 155500.00, 5002435.00, 3502000.00, 965000.00),
(N'A26H2M26', N'656/579', N'A', N'26', 26, N'1 Bed Plus', 35.03, 6346250.00, 171500.00, 6007645.00, 4205000.00, 1051000.00),
(N'A26H2M27', N'656/580', N'A', N'26', 26, N'1 Bed Plus', 35.06, 6346250.00, 171500.00, 6012790.00, 4209000.00, 1052000.00),
(N'A26F128', N'656/581', N'A', N'26', 26, N'1 Bed S', 30.00, 5407500.00, 167500.00, 5025000.00, 3518000.00, 900000.00),
(N'A26F1M29', N'656/582', N'A', N'26', 26, N'1 Bed S', 29.64, 5323750.00, 167500.00, 4964700.00, 3475000.00, 889000.00);

IF (SELECT COUNT(*) FROM #Src) <> 580
    THROW 50004, 'The staged workbook is not the expected row count. The VALUES block has been edited or truncated.', 1;

IF (SELECT COUNT(DISTINCT RoomNumber) FROM #Src) <> 580
   OR (SELECT COUNT(DISTINCT CondoRegistrationNumber) FROM #Src) <> 580
    THROW 50005, 'The staged workbook contains a duplicate room or registration number. Refusing to guess which price wins.', 1;

IF (SELECT SUM(AppraisalValue) FROM #Src) <> 3019811540.00
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
