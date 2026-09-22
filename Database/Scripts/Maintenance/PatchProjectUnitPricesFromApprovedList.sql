-- ============================================================
-- appraisal.ProjectUnitPrices — patch the approved per-unit prices of ONE block condo
-- project, selected by its appraisal number.
-- Schema: appraisal
-- Run by hand. NOT a DbUp migration: it targets one project on one environment.
--
-- WHY THIS SCRIPT EXISTS
--
-- Nothing in CAS can import an approved unit-price table. The units Excel upload
-- (ProjectUnitExcelParser) carries Floor / Tower / Registration / Room / Model / Usable Area /
-- Selling Price and no appraised value at all, and the Unit Price tab's Calculate button
-- (Project.CalculateUnitPrices) derives its own figures from the per-model PricingAnalysis. The
-- committee-approved price list therefore has no way in except a direct write.
--
-- WHAT IS LOADED. Five columns per unit, all from the approved workbook:
--   StandardPrice              <- ราคา (บาท/ตร.ม.)
--   TotalAppraisalValue        <- ราคาประเมินที่เสนอขออนุมัติ
--   TotalAppraisalValueRounded <- the same figure; the workbook is already rounded to 1,000 baht
--   ForceSellingPrice          <- ราคาบังคับขาย
--   CoverageAmount             <- มูลค่าประกันอัคคีภัย
--
-- ALSO CREATED, under @CreateMissingUnits: a ProjectUnits row for a workbook room the project
-- does not hold. Off by default, and capped by @MaxUnitsToCreate — a handful missing is a data gap
-- worth filling, but hundreds missing means the room-number FORMAT differs and creating them would
-- silently duplicate the whole project alongside the units already there. A created unit is linked
-- to its ProjectTowers / ProjectModels rows by the same rule the aggregate uses
-- (Project.AutoCreateCondoTowersAndModels: tower by TowerName, model by (tower, ModelName)).
-- Those master rows are only ever RESOLVED, never invented from the spreadsheet — a unit whose
-- model cannot be resolved is created with a NULL ProjectModelId and counted in the report,
-- because such a unit contributes nothing to any total (see the INNER JOIN warning below).
--
-- ALSO REPAIRED, under @FixFloor: ProjectUnits.Floor. See FLOOR "12A" below — the Excel upload
-- drops any floor that is not a plain integer, so this is a hole the workbook can fill rather
-- than an opinion it is overriding.
--
-- NOT WRITTEN: every other ProjectUnits column. TowerName, ModelType, UsableArea and SellingPrice
-- are REPORTED as a diff against the workbook and left alone, so a mismatch is a decision for a
-- human rather than a silent overwrite. Location flags (IsCorner, IsEdge, IsPoolView, IsSouth,
-- IsOther, IsNearGarden), AdjustPriceLocation and PriceIncrementPerFloor on rows that already
-- exist are also left alone — they are the appraiser's input, not the workbook's. New price rows
-- get flags = 0 and those two amounts NULL.
--
-- SOURCE: "ศุภาลัย ปาร์ค เอกมัย - พัฒนาการ_Condo.xlsx", sheet 1, rows 2-1615 = 1614 units
-- (not in the repo — it is customer data). Expected totals once applied:
--   SUM(TotalAppraisalValueRounded) = 4,803,838,000.00
--   SUM(CoverageAmount)             = 1,955,720,000.00
--
-- FLOOR "12A", AND WHY THIS SCRIPT REPAIRS IT. The tower skips 13, so floor 12A is physically the
-- 13th, and the workbook prices it that way: +400/sq.m. per floor, continuous from 12 through 12A
-- to 14. This script therefore stages 12A as 13.
--
-- ProjectUnits.Floor is an int, and ProjectUnitExcelParser loads it like this:
--
--     int.TryParse(floorText, out var floor);        // "12A" -> false, floor stays 0
--     floor: floor != 0 ? floor : null,              // 0 -> null
--
-- The TryParse result is never checked, so a non-integer floor lands as NULL with no error, no
-- warning and no counter in the upload result — the row itself is still created, because the
-- parser only skips a row when BOTH floor and room number are blank. Nothing downstream ever
-- surfaces it either: BlockReappraisalMatcher compares Floor as null != null, so re-uploading the
-- same sheet reports no difference forever. Worse, CalculateCondoUnitPrices guards on
-- unit.Floor.HasValue before adding PriceIncrementPerFloor, so every NULL-floor unit silently
-- loses its entire floor premium the moment anyone presses Calculate.
--
-- That is a hole, not a human decision, so @FixFloor = 1 fills it — but ONLY where Floor is
-- currently NULL. A floor somebody actually keyed in is left alone and reported as a diff.
-- @FixFloor = 2 overwrites every mismatched floor; @FixFloor = 0 writes none.
--
-- ⚠ THE THREE WAYS THESE NUMBERS DIE
--   1. Pressing Calculate on the Unit Price tab. CalculateCondoUnitPrices rebuilds every value
--      from the per-model PricingAnalysis and adds PriceIncrementPerFloor as a flat baht amount
--      per unit. The workbook's increment is +400 PER SQUARE METRE per floor and differs between
--      towers A and B, so the calculator cannot reproduce it. It will overwrite, silently.
--   2. Re-uploading the units Excel. Project.ReplaceUnits() deletes the unit rows, and
--      ProjectUnitPrices cascade-deletes with them. The whole patch disappears.
--   3. A future run of this script with a stale workbook.
--
-- THE APPRAISAL-LEVEL SUMMARY, under @SyncValuationSummary. appraisal.ValuationAnalyses is
-- normally refreshed by AppraisalValuationSummaryService.RecomputeAsync, which for a block project
-- is reached only from CalculateProjectUnitPrices — the one command that would destroy this patch.
-- So the summary is rewritten here instead, reproducing that method's block branch exactly:
--
--   AppraisedValue  = SUM(TotalAppraisalValueRounded) over UNSOLD units that have BOTH a price row
--                     and a ProjectModelId — no rounding applied to the total itself
--   InsuranceValue  = SUM(CoverageAmount) over the same set, rounded to the nearest 1,000
--   ForcedSaleValue = AppraisedValue x rate / 100, rounded to the nearest 1,000, where the rate is
--                     resolved by ForceSaleRateResolver's chain: the appraisal's own
--                     ValuationAnalyses.ForceSaleRate override, else the project's
--                     ProjectPricingAssumptions.ForceSalePercentage, else SystemConfigurations
--                     'ForceSaleRateDefaultPct', else 70 — and anything outside (0,100] falls back
--                     to 70, exactly as the resolver does.
--                     @ForcedSaleFrom = 2 stores SUM(per-unit ForceSellingPrice) instead; see the
--                     block on the three figures further down, and read the dry-run before choosing
--   ValuationApproach = the single distinct selected ApproachType across the project's ProjectModel
--                     PricingAnalysis rows, or 'Combined' when there are zero or more than one
--
-- ⚠ That rollup INNER JOINs ProjectModels on ProjectUnits.ProjectModelId. A unit with a NULL
-- ProjectModelId contributes nothing to ValuationAnalyses OR to the Decision Summary insurance
-- total, however correct its price row is. The report counts them, and the total below excludes
-- them because that is what the application does — this script reproduces the app, it does not
-- correct it.
--
-- ⚠ ValuationDate IS NEVER OVERWRITTEN. It leads the printed book, both AS400 result feeds, the
-- 360 view, Decision Summary and History Search, and anchors the +5-year reappraisal clock in
-- vw_ReappraisalCandidates. An existing row keeps the date it has. If there is NO row yet, one is
-- created only when a non-cancelled appointment can supply a real date; otherwise the summary is
-- skipped with a message rather than stamping today's date on the appraisal.
--
-- ⚠ WHAT WRITING THIS FROM SQL STILL MISSES: AppraisalValueChangedIntegrationEvent. The app
-- publishes it on the outbox whenever AppraisedValue changes, and
-- Workflow.AppraisalValueChangedIntegrationEventConsumer writes 'appraisalValue' into
-- WorkflowInstance.Variables (the approval-tier switch: meeting vs. direct committee, and the
-- committee-selection activity) and refreshes AppraisalValue on live MeetingQueueItem rows.
-- Reaching into another module's workflow state from SQL is worse than leaving it alone, so the
-- report below prints the workflow's stored appraisalValue and any live queue rows next to the new
-- total. If the appraisal has not yet passed the approval-tier switch, that drift matters and
-- someone has to close it in the application.
--
-- ⚠ collateral.ProjectUnits.LastAppraisedValue is synced only by CollateralMasterUpsertService and
-- will still hold its old value after this runs.
--
-- Idempotent: re-running writes the same values again. @Apply = 0 touches nothing at all.
-- ============================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ── Inputs ────────────────────────────────────────────────────────────────────
DECLARE @AppraisalNumber nvarchar(50) = N'';   -- <<< fill in the appraisal number before running
DECLARE @Apply           bit          = 0;     -- 0 = report only (writes nothing), 1 = write
DECLARE @FixFloor        tinyint      = 1;     -- ProjectUnits.Floor: 0 = never write,
                                               -- 1 = fill only where it is NULL, 2 = overwrite mismatches
DECLARE @SyncValuationSummary bit     = 1;     -- 1 = also rewrite appraisal.ValuationAnalyses
DECLARE @CreateMissingUnits bit       = 0;     -- 1 = INSERT a ProjectUnits row for a workbook room
                                               -- the project does not have (0 = abort instead)
DECLARE @MaxUnitsToCreate int         = 50;    -- refuse to create more than this many; see 50011
DECLARE @ForcedSaleFrom  tinyint      = 1;     -- ForcedSaleValue: 1 = appraised total x rate (what
                                               -- the app does), 2 = SUM of the per-unit column

IF @AppraisalNumber IS NULL OR LTRIM(RTRIM(@AppraisalNumber)) = N''
    THROW 50001, 'Set @AppraisalNumber at the top of this script before running it.', 1;

PRINT CONCAT('Target appraisal number : ', @AppraisalNumber);
PRINT CONCAT('Mode                    : ', CASE WHEN @Apply = 1 THEN 'APPLY (writes)' ELSE 'REPORT ONLY (no writes)' END);
PRINT CONCAT('Missing units           : ', CASE WHEN @CreateMissingUnits = 1
                                               THEN CONCAT('create, up to ', @MaxUnitsToCreate)
                                               ELSE 'abort if any' END);
PRINT CONCAT('Valuation summary       : ', CASE WHEN @SyncValuationSummary = 1 THEN 'rewrite ValuationAnalyses' ELSE 'leave alone' END);
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
PRINT '';

-- ── 1. Resolve the project ────────────────────────────────────────────────────
-- The appraisal that names the project is not always the one that owns the appraisal.Projects
-- row: a unit's own appraisal chains back through PrevAppraisalId to the block PreAppraisal.
-- Walk that chain and take the nearest ancestor that owns a project. The Path string is a cycle
-- guard — a corrupted chain would otherwise recurse forever.
-- @ProjectAppraisalId, not the number the operator typed: RecomputeAsync summarises the appraisal
-- that OWNS the Projects row, and ValuationAnalyses hangs off that same appraisal. For a unit-level
-- appraisal pointing back at a block PreAppraisal the two are different rows.
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
                                          ELSE '  (an ANCESTOR of the number given)' END);
PRINT '';

-- ── 2. Stage the workbook ─────────────────────────────────────────────────────
-- COLLATE DATABASE_DEFAULT is load-bearing, not decoration. A temp table's character columns take
-- tempdb's collation - i.e. the instance collation, Thai_CI_AS on the bank's servers - while every
-- permanent column they are compared against carries the database's SQL_Latin1_General_CP1_CI_AS.
-- Without it the joins below do not even compile.
IF OBJECT_ID('tempdb..#Src')   IS NOT NULL DROP TABLE #Src;
IF OBJECT_ID('tempdb..#Match') IS NOT NULL DROP TABLE #Match;
IF OBJECT_ID('tempdb..#Post')  IS NOT NULL DROP TABLE #Post;

CREATE TABLE #Src
(
    RoomNumber        nvarchar(50)  COLLATE DATABASE_DEFAULT NOT NULL,
    TowerName         nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    FloorText         nvarchar(10)  COLLATE DATABASE_DEFAULT NULL,   -- as printed: '6', '12A', ...
    Floor             int                                    NULL,   -- 12A resolved to 13
    ModelType         nvarchar(200) COLLATE DATABASE_DEFAULT NULL,
    UsableArea        decimal(10,2)                          NULL,
    SellingPrice      decimal(18,2)                          NULL,
    PricePerSqm       decimal(18,2)                          NOT NULL,
    AppraisalValue    decimal(18,2)                          NOT NULL,
    ForceSellingPrice decimal(18,2)                          NOT NULL,
    CoverageAmount    decimal(18,2)                          NOT NULL
);

INSERT #Src (RoomNumber, TowerName, FloorText, Floor, ModelType, UsableArea,
            SellingPrice, PricePerSqm, AppraisalValue, ForceSellingPrice, CoverageAmount)
VALUES
(N'3079-0A00101', N'A', N'1', 1, N'SHOP', 53.50, 5390000.00, 0.00, 0.00, 0.00, 0.00),
(N'3079-0A00102', N'A', N'1', 1, N'SHOP', 41.00, 3590000.00, 0.00, 0.00, 0.00, 0.00),
(N'3079-0A00103', N'A', N'1', 1, N'SHOP', 45.50, 3790000.00, 0.00, 0.00, 0.00, 0.00),
(N'3079-0A00104', N'A', N'1', 1, N'SHOP', 44.00, 3690000.00, 0.00, 0.00, 0.00, 0.00),
(N'3079-0A00105', N'A', N'1', 1, N'SHOP', 36.00, 2590000.00, 0.00, 0.00, 0.00, 0.00),
(N'3079-0A00601', N'A', N'6', 6, N'2 BEDROOMS', 60.50, 4700000.00, 72000.00, 4356000.00, 3050000.00, 1820000.00),
(N'3079-0A00602', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.00, 3095000.00, 71000.00, 2840000.00, 1990000.00, 1200000.00),
(N'3079-0A00603', N'A', N'6', 6, N'1 BEDROOM PLUS', 46.00, 3164000.00, 71000.00, 3266000.00, 2290000.00, 1380000.00),
(N'3079-0A00604', N'A', N'6', 6, N'1 BEDROOM PLUS', 41.00, 2906000.00, 71000.00, 2911000.00, 2040000.00, 1230000.00),
(N'3079-0A00609', N'A', N'6', 6, N'1 BEDROOM PLUS', 41.00, 2906000.00, 71000.00, 2911000.00, 2040000.00, 1230000.00),
(N'3079-0A00610', N'A', N'6', 6, N'1 BEDROOM PLUS', 46.00, 3269000.00, 71000.00, 3266000.00, 2290000.00, 1380000.00),
(N'3079-0A00611', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.00, 2780000.00, 71000.00, 2840000.00, 1990000.00, 1200000.00),
(N'3079-0A00612', N'A', N'6', 6, N'2 BEDROOMS', 60.50, 4700000.00, 72000.00, 4356000.00, 3050000.00, 1820000.00),
(N'3079-0A00614', N'A', N'6', 6, N'2 BEDROOMS', 51.50, 4005000.00, 74000.00, 3811000.00, 2670000.00, 1550000.00),
(N'3079-0A00615', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.50, 2934000.00, 71000.00, 2876000.00, 2010000.00, 1220000.00),
(N'3079-0A00616', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.50, 2995000.00, 71000.00, 2876000.00, 2010000.00, 1220000.00),
(N'3079-0A00617', N'A', N'6', 6, N'2 BEDROOMS', 56.00, 3948000.00, 74000.00, 4144000.00, 2900000.00, 1680000.00),
(N'3079-0A00618', N'A', N'6', 6, N'STUDIO', 30.00, 2374000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00619', N'A', N'6', 6, N'1 BEDROOM', 34.00, 2692000.00, 71000.00, 2414000.00, 1690000.00, 1020000.00),
(N'3079-0A00620', N'A', N'6', 6, N'STUDIO', 30.00, 2428000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00621', N'A', N'6', 6, N'STUDIO', 30.00, 2428000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00622', N'A', N'6', 6, N'1 BEDROOM', 34.00, 2692000.00, 71000.00, 2414000.00, 1690000.00, 1020000.00),
(N'3079-0A00630', N'A', N'6', 6, N'1 BEDROOM', 34.00, 2692000.00, 71000.00, 2414000.00, 1690000.00, 1020000.00),
(N'3079-0A00631', N'A', N'6', 6, N'STUDIO', 30.00, 2428000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00632', N'A', N'6', 6, N'STUDIO', 30.00, 2428000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00633', N'A', N'6', 6, N'1 BEDROOM', 34.00, 2478000.00, 71000.00, 2414000.00, 1690000.00, 1020000.00),
(N'3079-0A00634', N'A', N'6', 6, N'STUDIO', 30.00, 2374000.00, 73000.00, 2190000.00, 1530000.00, 900000.00),
(N'3079-0A00635', N'A', N'6', 6, N'2 BEDROOMS', 56.00, 3948000.00, 74000.00, 4144000.00, 2900000.00, 1680000.00),
(N'3079-0A00636', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3349000.00, 71000.00, 2876000.00, 2010000.00, 1220000.00),
(N'3079-0A00637', N'A', N'6', 6, N'1 BEDROOM PLUS', 40.50, 2934000.00, 71000.00, 2876000.00, 2010000.00, 1220000.00),
(N'3079-0A00638', N'A', N'6', 6, N'2 BEDROOMS', 51.50, 4005000.00, 74000.00, 3811000.00, 2670000.00, 1550000.00),
(N'3079-0A00701', N'A', N'7', 7, N'2 BEDROOMS', 60.50, 4906000.00, 72400.00, 4380000.00, 3070000.00, 1820000.00),
(N'3079-0A00702', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.00, 2856000.00, 71400.00, 2856000.00, 2000000.00, 1200000.00),
(N'3079-0A00703', N'A', N'7', 7, N'1 BEDROOM PLUS', 46.00, 3825000.00, 71400.00, 3284000.00, 2300000.00, 1380000.00),
(N'3079-0A00704', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3337000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00705', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3375000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00706', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3265000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00707', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3192000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00708', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3265000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00709', N'A', N'7', 7, N'1 BEDROOM PLUS', 41.00, 3265000.00, 71400.00, 2927000.00, 2050000.00, 1230000.00),
(N'3079-0A00710', N'A', N'7', 7, N'1 BEDROOM PLUS', 46.00, 3745000.00, 71400.00, 3284000.00, 2300000.00, 1380000.00),
(N'3079-0A00711', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.00, 2796000.00, 71400.00, 2856000.00, 2000000.00, 1200000.00),
(N'3079-0A00712', N'A', N'7', 7, N'2 BEDROOMS', 60.50, 5624000.00, 72400.00, 4380000.00, 3070000.00, 1820000.00),
(N'3079-0A00714', N'A', N'7', 7, N'2 BEDROOMS', 51.50, 4586000.00, 74400.00, 3832000.00, 2680000.00, 1550000.00),
(N'3079-0A00715', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3297000.00, 71400.00, 2892000.00, 2020000.00, 1220000.00),
(N'3079-0A00716', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3441000.00, 71400.00, 2892000.00, 2020000.00, 1220000.00),
(N'3079-0A00717', N'A', N'7', 7, N'2 BEDROOMS', 56.00, 4459000.00, 74400.00, 4166000.00, 2920000.00, 1680000.00),
(N'3079-0A00718', N'A', N'7', 7, N'STUDIO', 30.00, 2442000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00719', N'A', N'7', 7, N'1 BEDROOM', 34.00, 2768000.00, 71400.00, 2428000.00, 1700000.00, 1020000.00),
(N'3079-0A00720', N'A', N'7', 7, N'STUDIO', 30.00, 2495000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00721', N'A', N'7', 7, N'STUDIO', 30.00, 2495000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00722', N'A', N'7', 7, N'1 BEDROOM', 34.00, 2829000.00, 71400.00, 2428000.00, 1700000.00, 1020000.00),
(N'3079-0A00723', N'A', N'7', 7, N'1 BEDROOM', 35.50, 3016000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00724', N'A', N'7', 7, N'1 BEDROOM', 35.50, 3016000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00725', N'A', N'7', 7, N'1 BEDROOM', 35.50, 2889000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00726', N'A', N'7', 7, N'1 BEDROOM', 35.50, 2954000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00727', N'A', N'7', 7, N'1 BEDROOM', 35.50, 2954000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00728', N'A', N'7', 7, N'1 BEDROOM', 35.50, 3016000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00729', N'A', N'7', 7, N'1 BEDROOM', 35.50, 3016000.00, 71400.00, 2535000.00, 1770000.00, 1070000.00),
(N'3079-0A00730', N'A', N'7', 7, N'1 BEDROOM', 34.00, 2829000.00, 71400.00, 2428000.00, 1700000.00, 1020000.00),
(N'3079-0A00731', N'A', N'7', 7, N'STUDIO', 30.00, 2495000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00732', N'A', N'7', 7, N'STUDIO', 30.00, 2495000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00733', N'A', N'7', 7, N'1 BEDROOM', 34.00, 2768000.00, 71400.00, 2428000.00, 1700000.00, 1020000.00),
(N'3079-0A00734', N'A', N'7', 7, N'STUDIO', 30.00, 2442000.00, 73400.00, 2202000.00, 1540000.00, 900000.00),
(N'3079-0A00735', N'A', N'7', 7, N'2 BEDROOMS', 56.00, 4459000.00, 74400.00, 4166000.00, 2920000.00, 1680000.00),
(N'3079-0A00736', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3441000.00, 71400.00, 2892000.00, 2020000.00, 1220000.00),
(N'3079-0A00737', N'A', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3441000.00, 71400.00, 2892000.00, 2020000.00, 1220000.00),
(N'3079-0A00738', N'A', N'7', 7, N'2 BEDROOMS', 51.50, 4767000.00, 74400.00, 3832000.00, 2680000.00, 1550000.00),
(N'3079-0A00801', N'A', N'8', 8, N'2 BEDROOMS', 60.50, 4749000.00, 72800.00, 4404000.00, 3080000.00, 1820000.00),
(N'3079-0A00802', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.00, 2812000.00, 71800.00, 2872000.00, 2010000.00, 1200000.00),
(N'3079-0A00803', N'A', N'8', 8, N'1 BEDROOM PLUS', 46.00, 3201000.00, 71800.00, 3303000.00, 2310000.00, 1380000.00),
(N'3079-0A00804', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3000000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00805', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3284000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00806', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3284000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00807', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3211000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00808', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3000000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00809', N'A', N'8', 8, N'1 BEDROOM PLUS', 41.00, 3000000.00, 71800.00, 2944000.00, 2060000.00, 1230000.00),
(N'3079-0A00810', N'A', N'8', 8, N'1 BEDROOM PLUS', 46.00, 3521000.00, 71800.00, 3303000.00, 2310000.00, 1380000.00),
(N'3079-0A00811', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.00, 3062000.00, 71800.00, 2872000.00, 2010000.00, 1200000.00),
(N'3079-0A00812', N'A', N'8', 8, N'2 BEDROOMS', 60.50, 4749000.00, 72800.00, 4404000.00, 3080000.00, 1820000.00),
(N'3079-0A00814', N'A', N'8', 8, N'2 BEDROOMS', 51.50, 4124000.00, 74800.00, 3852000.00, 2700000.00, 1550000.00),
(N'3079-0A00815', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3316000.00, 71800.00, 2908000.00, 2040000.00, 1220000.00),
(N'3079-0A00816', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3088000.00, 71800.00, 2908000.00, 2040000.00, 1220000.00),
(N'3079-0A00817', N'A', N'8', 8, N'2 BEDROOMS', 56.00, 4077000.00, 74800.00, 4189000.00, 2930000.00, 1680000.00),
(N'3079-0A00818', N'A', N'8', 8, N'STUDIO', 30.00, 2219000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00819', N'A', N'8', 8, N'1 BEDROOM', 34.00, 2784000.00, 71800.00, 2441000.00, 1710000.00, 1020000.00),
(N'3079-0A00820', N'A', N'8', 8, N'STUDIO', 30.00, 2509000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00821', N'A', N'8', 8, N'STUDIO', 30.00, 2264000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00822', N'A', N'8', 8, N'1 BEDROOM', 34.00, 2845000.00, 71800.00, 2441000.00, 1710000.00, 1020000.00),
(N'3079-0A00823', N'A', N'8', 8, N'1 BEDROOM', 35.50, 3032000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00824', N'A', N'8', 8, N'1 BEDROOM', 35.50, 3032000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00825', N'A', N'8', 8, N'1 BEDROOM', 35.50, 2907000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00826', N'A', N'8', 8, N'1 BEDROOM', 35.50, 2970000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00827', N'A', N'8', 8, N'1 BEDROOM', 35.50, 2970000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00828', N'A', N'8', 8, N'1 BEDROOM', 35.50, 3032000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00829', N'A', N'8', 8, N'1 BEDROOM', 35.50, 2767000.00, 71800.00, 2549000.00, 1780000.00, 1070000.00),
(N'3079-0A00830', N'A', N'8', 8, N'1 BEDROOM', 34.00, 2845000.00, 71800.00, 2441000.00, 1710000.00, 1020000.00),
(N'3079-0A00831', N'A', N'8', 8, N'STUDIO', 30.00, 2509000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00832', N'A', N'8', 8, N'STUDIO', 30.00, 2509000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00833', N'A', N'8', 8, N'1 BEDROOM', 34.00, 2784000.00, 71800.00, 2441000.00, 1710000.00, 1020000.00),
(N'3079-0A00834', N'A', N'8', 8, N'STUDIO', 30.00, 2456000.00, 73800.00, 2214000.00, 1550000.00, 900000.00),
(N'3079-0A00835', N'A', N'8', 8, N'2 BEDROOMS', 56.00, 4484000.00, 74800.00, 4189000.00, 2930000.00, 1680000.00),
(N'3079-0A00836', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3088000.00, 71800.00, 2908000.00, 2040000.00, 1220000.00),
(N'3079-0A00837', N'A', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3027000.00, 71800.00, 2908000.00, 2040000.00, 1220000.00),
(N'3079-0A00838', N'A', N'8', 8, N'2 BEDROOMS', 51.50, 4124000.00, 74800.00, 3852000.00, 2700000.00, 1550000.00),
(N'3079-0A00901', N'A', N'9', 9, N'2 BEDROOMS', 60.50, 4864000.00, 73200.00, 4429000.00, 3100000.00, 1820000.00),
(N'3079-0A00902', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.00, 2828000.00, 72200.00, 2888000.00, 2020000.00, 1200000.00),
(N'3079-0A00903', N'A', N'9', 9, N'1 BEDROOM PLUS', 46.00, 3870000.00, 72200.00, 3321000.00, 2320000.00, 1380000.00),
(N'3079-0A00904', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3304000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00905', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3304000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00906', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3304000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00907', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3230000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00908', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3304000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00909', N'A', N'9', 9, N'1 BEDROOM PLUS', 41.00, 3304000.00, 72200.00, 2960000.00, 2070000.00, 1230000.00),
(N'3079-0A00910', N'A', N'9', 9, N'1 BEDROOM PLUS', 46.00, 3220000.00, 72200.00, 3321000.00, 2320000.00, 1380000.00),
(N'3079-0A00911', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.00, 3081000.00, 72200.00, 2888000.00, 2020000.00, 1200000.00),
(N'3079-0A00912', N'A', N'9', 9, N'2 BEDROOMS', 60.50, 4864000.00, 73200.00, 4429000.00, 3100000.00, 1820000.00),
(N'3079-0A00914', N'A', N'9', 9, N'2 BEDROOMS', 51.50, 4145000.00, 75200.00, 3873000.00, 2710000.00, 1550000.00),
(N'3079-0A00915', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.50, 3043000.00, 72200.00, 2924000.00, 2050000.00, 1220000.00),
(N'3079-0A00916', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.50, 3104000.00, 72200.00, 2924000.00, 2050000.00, 1220000.00),
(N'3079-0A00917', N'A', N'9', 9, N'2 BEDROOMS', 56.00, 4511000.00, 75200.00, 4211000.00, 2950000.00, 1680000.00),
(N'3079-0A00918', N'A', N'9', 9, N'STUDIO', 30.00, 2471000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00919', N'A', N'9', 9, N'1 BEDROOM', 34.00, 2799000.00, 72200.00, 2455000.00, 1720000.00, 1020000.00),
(N'3079-0A00920', N'A', N'9', 9, N'STUDIO', 30.00, 2523000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00921', N'A', N'9', 9, N'STUDIO', 30.00, 2523000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00922', N'A', N'9', 9, N'1 BEDROOM', 34.00, 2860000.00, 72200.00, 2455000.00, 1720000.00, 1020000.00),
(N'3079-0A00923', N'A', N'9', 9, N'1 BEDROOM', 35.50, 2781000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00924', N'A', N'9', 9, N'1 BEDROOM', 35.50, 3049000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00925', N'A', N'9', 9, N'1 BEDROOM', 35.50, 2923000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00926', N'A', N'9', 9, N'1 BEDROOM', 35.50, 2986000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00927', N'A', N'9', 9, N'1 BEDROOM', 35.50, 2986000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00928', N'A', N'9', 9, N'1 BEDROOM', 35.50, 3049000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00929', N'A', N'9', 9, N'1 BEDROOM', 35.50, 2781000.00, 72200.00, 2563000.00, 1790000.00, 1070000.00),
(N'3079-0A00930', N'A', N'9', 9, N'1 BEDROOM', 34.00, 2860000.00, 72200.00, 2455000.00, 1720000.00, 1020000.00),
(N'3079-0A00931', N'A', N'9', 9, N'STUDIO', 30.00, 2523000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00932', N'A', N'9', 9, N'STUDIO', 30.00, 2523000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00933', N'A', N'9', 9, N'1 BEDROOM', 34.00, 2799000.00, 72200.00, 2455000.00, 1720000.00, 1020000.00),
(N'3079-0A00934', N'A', N'9', 9, N'STUDIO', 30.00, 2471000.00, 74200.00, 2226000.00, 1560000.00, 900000.00),
(N'3079-0A00935', N'A', N'9', 9, N'2 BEDROOMS', 56.00, 4511000.00, 75200.00, 4211000.00, 2950000.00, 1680000.00),
(N'3079-0A00936', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.50, 3104000.00, 72200.00, 2924000.00, 2050000.00, 1220000.00),
(N'3079-0A00937', N'A', N'9', 9, N'1 BEDROOM PLUS', 40.50, 3043000.00, 72200.00, 2924000.00, 2050000.00, 1220000.00),
(N'3079-0A00938', N'A', N'9', 9, N'2 BEDROOMS', 51.50, 4145000.00, 75200.00, 3873000.00, 2710000.00, 1550000.00),
(N'3079-0A01001', N'A', N'10', 10, N'2 BEDROOMS', 60.50, 4797000.00, 73600.00, 4453000.00, 3120000.00, 1820000.00),
(N'3079-0A01002', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.00, 2844000.00, 72600.00, 2904000.00, 2030000.00, 1200000.00),
(N'3079-0A01003', N'A', N'10', 10, N'1 BEDROOM PLUS', 46.00, 3238000.00, 72600.00, 3340000.00, 2340000.00, 1380000.00),
(N'3079-0A01004', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3396000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01005', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3322000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01006', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3138000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01007', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3250000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01008', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3322000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01009', N'A', N'10', 10, N'1 BEDROOM PLUS', 41.00, 3322000.00, 72600.00, 2977000.00, 2080000.00, 1230000.00),
(N'3079-0A01010', N'A', N'10', 10, N'1 BEDROOM PLUS', 46.00, 3238000.00, 72600.00, 3340000.00, 2340000.00, 1380000.00),
(N'3079-0A01011', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.00, 2844000.00, 72600.00, 2904000.00, 2030000.00, 1200000.00),
(N'3079-0A01012', N'A', N'10', 10, N'2 BEDROOMS', 60.50, 4797000.00, 73600.00, 4453000.00, 3120000.00, 1820000.00),
(N'3079-0A01014', N'A', N'10', 10, N'2 BEDROOMS', 51.50, 4165000.00, 75600.00, 3893000.00, 2730000.00, 1550000.00),
(N'3079-0A01015', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.50, 3354000.00, 72600.00, 2940000.00, 2060000.00, 1220000.00),
(N'3079-0A01016', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.50, 3497000.00, 72600.00, 2940000.00, 2060000.00, 1220000.00),
(N'3079-0A01017', N'A', N'10', 10, N'2 BEDROOMS', 56.00, 4537000.00, 75600.00, 4234000.00, 2960000.00, 1680000.00),
(N'3079-0A01018', N'A', N'10', 10, N'STUDIO', 30.00, 2243000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01019', N'A', N'10', 10, N'1 BEDROOM', 34.00, 2817000.00, 72600.00, 2468000.00, 1730000.00, 1020000.00),
(N'3079-0A01020', N'A', N'10', 10, N'STUDIO', 30.00, 2537000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01021', N'A', N'10', 10, N'STUDIO', 30.00, 2537000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01022', N'A', N'10', 10, N'1 BEDROOM', 34.00, 2877000.00, 72600.00, 2468000.00, 1730000.00, 1020000.00),
(N'3079-0A01023', N'A', N'10', 10, N'1 BEDROOM', 35.50, 3067000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01024', N'A', N'10', 10, N'1 BEDROOM', 35.50, 3067000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01025', N'A', N'10', 10, N'1 BEDROOM', 35.50, 2941000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01026', N'A', N'10', 10, N'1 BEDROOM', 35.50, 3003000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01027', N'A', N'10', 10, N'1 BEDROOM', 35.50, 3003000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01028', N'A', N'10', 10, N'1 BEDROOM', 35.50, 2796000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01029', N'A', N'10', 10, N'1 BEDROOM', 35.50, 2796000.00, 72600.00, 2577000.00, 1800000.00, 1070000.00),
(N'3079-0A01030', N'A', N'10', 10, N'1 BEDROOM', 34.00, 2877000.00, 72600.00, 2468000.00, 1730000.00, 1020000.00),
(N'3079-0A01031', N'A', N'10', 10, N'STUDIO', 30.00, 2537000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01032', N'A', N'10', 10, N'STUDIO', 30.00, 2288000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01033', N'A', N'10', 10, N'1 BEDROOM', 34.00, 2817000.00, 72600.00, 2468000.00, 1730000.00, 1020000.00),
(N'3079-0A01034', N'A', N'10', 10, N'STUDIO', 30.00, 2484000.00, 74600.00, 2238000.00, 1570000.00, 900000.00),
(N'3079-0A01035', N'A', N'10', 10, N'2 BEDROOMS', 56.00, 4537000.00, 75600.00, 4234000.00, 2960000.00, 1680000.00),
(N'3079-0A01036', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.50, 3120000.00, 72600.00, 2940000.00, 2060000.00, 1220000.00),
(N'3079-0A01037', N'A', N'10', 10, N'1 BEDROOM PLUS', 40.50, 3060000.00, 72600.00, 2940000.00, 2060000.00, 1220000.00),
(N'3079-0A01038', N'A', N'10', 10, N'2 BEDROOMS', 51.50, 4165000.00, 75600.00, 3893000.00, 2730000.00, 1550000.00),
(N'3079-0A01101', N'A', N'11', 11, N'2 BEDROOMS', 60.50, 4912000.00, 74000.00, 4477000.00, 3130000.00, 1820000.00),
(N'3079-0A01102', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.00, 3190000.00, 73000.00, 2920000.00, 2040000.00, 1200000.00),
(N'3079-0A01103', N'A', N'11', 11, N'1 BEDROOM PLUS', 46.00, 3668000.00, 73000.00, 3358000.00, 2350000.00, 1380000.00),
(N'3079-0A01104', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3342000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01105', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3342000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01106', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3342000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01107', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3269000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01108', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3342000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01109', N'A', N'11', 11, N'1 BEDROOM PLUS', 41.00, 3342000.00, 73000.00, 2993000.00, 2100000.00, 1230000.00),
(N'3079-0A01110', N'A', N'11', 11, N'1 BEDROOM PLUS', 46.00, 3585000.00, 73000.00, 3358000.00, 2350000.00, 1380000.00),
(N'3079-0A01111', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.00, 3118000.00, 73000.00, 2920000.00, 2040000.00, 1200000.00),
(N'3079-0A01112', N'A', N'11', 11, N'2 BEDROOMS', 60.50, 4912000.00, 74000.00, 4477000.00, 3130000.00, 1820000.00),
(N'3079-0A01114', N'A', N'11', 11, N'2 BEDROOMS', 51.50, 4684000.00, 76000.00, 3914000.00, 2740000.00, 1550000.00),
(N'3079-0A01115', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3373000.00, 73000.00, 2957000.00, 2070000.00, 1220000.00),
(N'3079-0A01116', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3444000.00, 73000.00, 2957000.00, 2070000.00, 1220000.00),
(N'3079-0A01117', N'A', N'11', 11, N'2 BEDROOMS', 56.00, 4564000.00, 76000.00, 4256000.00, 2980000.00, 1680000.00),
(N'3079-0A01118', N'A', N'11', 11, N'STUDIO', 30.00, 2498000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01119', N'A', N'11', 11, N'1 BEDROOM', 34.00, 2832000.00, 73000.00, 2482000.00, 1740000.00, 1020000.00),
(N'3079-0A01120', N'A', N'11', 11, N'STUDIO', 30.00, 2552000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01121', N'A', N'11', 11, N'STUDIO', 30.00, 2552000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01122', N'A', N'11', 11, N'1 BEDROOM', 34.00, 2892000.00, 73000.00, 2482000.00, 1740000.00, 1020000.00),
(N'3079-0A01123', N'A', N'11', 11, N'1 BEDROOM', 35.50, 3083000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01124', N'A', N'11', 11, N'1 BEDROOM', 35.50, 3083000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01125', N'A', N'11', 11, N'1 BEDROOM', 35.50, 2957000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01126', N'A', N'11', 11, N'1 BEDROOM', 35.50, 3019000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01127', N'A', N'11', 11, N'1 BEDROOM', 35.50, 3019000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01128', N'A', N'11', 11, N'1 BEDROOM', 35.50, 2810000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01129', N'A', N'11', 11, N'1 BEDROOM', 35.50, 3083000.00, 73000.00, 2592000.00, 1810000.00, 1070000.00),
(N'3079-0A01130', N'A', N'11', 11, N'1 BEDROOM', 34.00, 2892000.00, 73000.00, 2482000.00, 1740000.00, 1020000.00),
(N'3079-0A01131', N'A', N'11', 11, N'STUDIO', 30.00, 2552000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01132', N'A', N'11', 11, N'STUDIO', 30.00, 2552000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01133', N'A', N'11', 11, N'1 BEDROOM', 34.00, 2832000.00, 73000.00, 2482000.00, 1740000.00, 1020000.00),
(N'3079-0A01134', N'A', N'11', 11, N'STUDIO', 30.00, 2498000.00, 75000.00, 2250000.00, 1580000.00, 900000.00),
(N'3079-0A01135', N'A', N'11', 11, N'2 BEDROOMS', 56.00, 4564000.00, 76000.00, 4256000.00, 2980000.00, 1680000.00),
(N'3079-0A01136', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3136000.00, 73000.00, 2957000.00, 2070000.00, 1220000.00),
(N'3079-0A01137', N'A', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3076000.00, 73000.00, 2957000.00, 2070000.00, 1220000.00),
(N'3079-0A01138', N'A', N'11', 11, N'2 BEDROOMS', 51.50, 4186000.00, 76000.00, 3914000.00, 2740000.00, 1550000.00),
(N'3079-0A01201', N'A', N'12', 12, N'2 BEDROOMS', 60.50, 4846000.00, 74400.00, 4501000.00, 3150000.00, 1820000.00),
(N'3079-0A01202', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.00, 3280000.00, 73400.00, 2936000.00, 2060000.00, 1200000.00),
(N'3079-0A01203', N'A', N'12', 12, N'1 BEDROOM PLUS', 46.00, 3275000.00, 73400.00, 3376000.00, 2360000.00, 1380000.00),
(N'3079-0A01204', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3435000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01205', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3361000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01206', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3361000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01207', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3289000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01208', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3361000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01209', N'A', N'12', 12, N'1 BEDROOM PLUS', 41.00, 3361000.00, 73400.00, 3009000.00, 2110000.00, 1230000.00),
(N'3079-0A01210', N'A', N'12', 12, N'1 BEDROOM PLUS', 46.00, 3608000.00, 73400.00, 3376000.00, 2360000.00, 1380000.00),
(N'3079-0A01211', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.00, 3136000.00, 73400.00, 2936000.00, 2060000.00, 1200000.00),
(N'3079-0A01212', N'A', N'12', 12, N'2 BEDROOMS', 60.50, 4846000.00, 74400.00, 4501000.00, 3150000.00, 1820000.00),
(N'3079-0A01214', N'A', N'12', 12, N'2 BEDROOMS', 51.50, 4707000.00, 76400.00, 3935000.00, 2750000.00, 1550000.00),
(N'3079-0A01215', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3393000.00, 73400.00, 2973000.00, 2080000.00, 1220000.00),
(N'3079-0A01216', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3153000.00, 73400.00, 2973000.00, 2080000.00, 1220000.00),
(N'3079-0A01217', N'A', N'12', 12, N'2 BEDROOMS', 56.00, 4591000.00, 76400.00, 4278000.00, 2990000.00, 1680000.00),
(N'3079-0A01218', N'A', N'12', 12, N'STUDIO', 30.00, 2512000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01219', N'A', N'12', 12, N'1 BEDROOM', 34.00, 2611000.00, 73400.00, 2496000.00, 1750000.00, 1020000.00),
(N'3079-0A01220', N'A', N'12', 12, N'STUDIO', 30.00, 2566000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01221', N'A', N'12', 12, N'STUDIO', 30.00, 2566000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01222', N'A', N'12', 12, N'1 BEDROOM', 34.00, 2908000.00, 73400.00, 2496000.00, 1750000.00, 1020000.00),
(N'3079-0A01223', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3099000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01224', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3099000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01225', N'A', N'12', 12, N'1 BEDROOM', 35.50, 2973000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01226', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3036000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01227', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3036000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01228', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3099000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01229', N'A', N'12', 12, N'1 BEDROOM', 35.50, 3099000.00, 73400.00, 2606000.00, 1820000.00, 1070000.00),
(N'3079-0A01230', N'A', N'12', 12, N'1 BEDROOM', 34.00, 2908000.00, 73400.00, 2496000.00, 1750000.00, 1020000.00),
(N'3079-0A01231', N'A', N'12', 12, N'STUDIO', 30.00, 2566000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01232', N'A', N'12', 12, N'STUDIO', 30.00, 2566000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01233', N'A', N'12', 12, N'1 BEDROOM', 34.00, 2848000.00, 73400.00, 2496000.00, 1750000.00, 1020000.00),
(N'3079-0A01234', N'A', N'12', 12, N'STUDIO', 30.00, 2512000.00, 75400.00, 2262000.00, 1580000.00, 900000.00),
(N'3079-0A01235', N'A', N'12', 12, N'2 BEDROOMS', 56.00, 4591000.00, 76400.00, 4278000.00, 2990000.00, 1680000.00),
(N'3079-0A01236', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3153000.00, 73400.00, 2973000.00, 2080000.00, 1220000.00),
(N'3079-0A01237', N'A', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3092000.00, 73400.00, 2973000.00, 2080000.00, 1220000.00),
(N'3079-0A01238', N'A', N'12', 12, N'2 BEDROOMS', 51.50, 4206000.00, 76400.00, 3935000.00, 2750000.00, 1550000.00),
(N'3079-0A12A01', N'A', N'12A', 13, N'2 BEDROOMS', 60.50, 5051000.00, 74800.00, 4525000.00, 3170000.00, 1820000.00),
(N'3079-0A12A02', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.00, 3298000.00, 73800.00, 2952000.00, 2070000.00, 1200000.00),
(N'3079-0A12A03', N'A', N'12A', 13, N'1 BEDROOM PLUS', 46.00, 3955000.00, 73800.00, 3395000.00, 2380000.00, 1380000.00),
(N'3079-0A12A04', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3454000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A05', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3380000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A06', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3380000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A07', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3309000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A08', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3380000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A09', N'A', N'12A', 13, N'1 BEDROOM PLUS', 41.00, 3380000.00, 73800.00, 3026000.00, 2120000.00, 1230000.00),
(N'3079-0A12A10', N'A', N'12A', 13, N'1 BEDROOM PLUS', 46.00, 3874000.00, 73800.00, 3395000.00, 2380000.00, 1380000.00),
(N'3079-0A12A11', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.00, 3156000.00, 73800.00, 2952000.00, 2070000.00, 1200000.00),
(N'3079-0A12A12', N'A', N'12A', 13, N'2 BEDROOMS', 60.50, 5051000.00, 74800.00, 4525000.00, 3170000.00, 1820000.00),
(N'3079-0A12A14', N'A', N'12A', 13, N'2 BEDROOMS', 51.50, 4611000.00, 76800.00, 3955000.00, 2770000.00, 1550000.00),
(N'3079-0A12A15', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3411000.00, 73800.00, 2989000.00, 2090000.00, 1220000.00),
(N'3079-0A12A16', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3555000.00, 73800.00, 2989000.00, 2090000.00, 1220000.00),
(N'3079-0A12A17', N'A', N'12A', 13, N'2 BEDROOMS', 56.00, 4617000.00, 76800.00, 4301000.00, 3010000.00, 1680000.00),
(N'3079-0A12A18', N'A', N'12A', 13, N'STUDIO', 30.00, 2527000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A19', N'A', N'12A', 13, N'1 BEDROOM', 34.00, 2865000.00, 73800.00, 2509000.00, 1760000.00, 1020000.00),
(N'3079-0A12A20', N'A', N'12A', 13, N'STUDIO', 30.00, 2581000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A21', N'A', N'12A', 13, N'STUDIO', 30.00, 2581000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A22', N'A', N'12A', 13, N'1 BEDROOM', 34.00, 2925000.00, 73800.00, 2509000.00, 1760000.00, 1020000.00),
(N'3079-0A12A23', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3116000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A24', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3116000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A25', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 2991000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A26', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3054000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A27', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3054000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A28', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3116000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A29', N'A', N'12A', 13, N'1 BEDROOM', 35.50, 3116000.00, 73800.00, 2620000.00, 1830000.00, 1070000.00),
(N'3079-0A12A30', N'A', N'12A', 13, N'1 BEDROOM', 34.00, 2925000.00, 73800.00, 2509000.00, 1760000.00, 1020000.00),
(N'3079-0A12A31', N'A', N'12A', 13, N'STUDIO', 30.00, 2581000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A32', N'A', N'12A', 13, N'STUDIO', 30.00, 2581000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A33', N'A', N'12A', 13, N'1 BEDROOM', 34.00, 2865000.00, 73800.00, 2509000.00, 1760000.00, 1020000.00),
(N'3079-0A12A34', N'A', N'12A', 13, N'STUDIO', 30.00, 2527000.00, 75800.00, 2274000.00, 1590000.00, 900000.00),
(N'3079-0A12A35', N'A', N'12A', 13, N'2 BEDROOMS', 56.00, 4617000.00, 76800.00, 4301000.00, 3010000.00, 1680000.00),
(N'3079-0A12A36', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3555000.00, 73800.00, 2989000.00, 2090000.00, 1220000.00),
(N'3079-0A12A37', N'A', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3555000.00, 73800.00, 2989000.00, 2090000.00, 1220000.00),
(N'3079-0A12A38', N'A', N'12A', 13, N'2 BEDROOMS', 51.50, 4793000.00, 76800.00, 3955000.00, 2770000.00, 1550000.00),
(N'3079-0A01401', N'A', N'14', 14, N'2 BEDROOMS', 60.50, 4894000.00, 75200.00, 4550000.00, 3190000.00, 1820000.00),
(N'3079-0A01402', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.00, 3317000.00, 74200.00, 2968000.00, 2080000.00, 1200000.00),
(N'3079-0A01403', N'A', N'14', 14, N'1 BEDROOM PLUS', 46.00, 3815000.00, 74200.00, 3413000.00, 2390000.00, 1380000.00),
(N'3079-0A01404', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3473000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01405', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3400000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01406', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3400000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01407', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3328000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01408', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3400000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01409', N'A', N'14', 14, N'1 BEDROOM PLUS', 41.00, 3400000.00, 74200.00, 3042000.00, 2130000.00, 1230000.00),
(N'3079-0A01410', N'A', N'14', 14, N'1 BEDROOM PLUS', 46.00, 3312000.00, 74200.00, 3413000.00, 2390000.00, 1380000.00),
(N'3079-0A01411', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.00, 3175000.00, 74200.00, 2968000.00, 2080000.00, 1200000.00),
(N'3079-0A01412', N'A', N'14', 14, N'2 BEDROOMS', 60.50, 4894000.00, 75200.00, 4550000.00, 3190000.00, 1820000.00),
(N'3079-0A01414', N'A', N'14', 14, N'2 BEDROOMS', 51.50, 4248000.00, 77200.00, 3976000.00, 2780000.00, 1550000.00),
(N'3079-0A01415', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3540000.00, 74200.00, 3005000.00, 2100000.00, 1220000.00),
(N'3079-0A01416', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3185000.00, 74200.00, 3005000.00, 2100000.00, 1220000.00),
(N'3079-0A01417', N'A', N'14', 14, N'2 BEDROOMS', 56.00, 4643000.00, 77200.00, 4323000.00, 3030000.00, 1680000.00),
(N'3079-0A01418', N'A', N'14', 14, N'STUDIO', 30.00, 2541000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01419', N'A', N'14', 14, N'1 BEDROOM', 34.00, 2880000.00, 74200.00, 2523000.00, 1770000.00, 1020000.00),
(N'3079-0A01420', N'A', N'14', 14, N'STUDIO', 30.00, 2595000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01421', N'A', N'14', 14, N'STUDIO', 30.00, 2595000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01422', N'A', N'14', 14, N'1 BEDROOM', 34.00, 2941000.00, 74200.00, 2523000.00, 1770000.00, 1020000.00),
(N'3079-0A01423', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3132000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01424', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3132000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01425', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3007000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01426', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3070000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01427', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3070000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01428', N'A', N'14', 14, N'1 BEDROOM', 35.50, 3132000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01429', N'A', N'14', 14, N'1 BEDROOM', 35.50, 2852000.00, 74200.00, 2634000.00, 1840000.00, 1070000.00),
(N'3079-0A01430', N'A', N'14', 14, N'1 BEDROOM', 34.00, 2689000.00, 74200.00, 2523000.00, 1770000.00, 1020000.00),
(N'3079-0A01431', N'A', N'14', 14, N'STUDIO', 30.00, 2595000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01432', N'A', N'14', 14, N'STUDIO', 30.00, 2595000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01433', N'A', N'14', 14, N'1 BEDROOM', 34.00, 2880000.00, 74200.00, 2523000.00, 1770000.00, 1020000.00),
(N'3079-0A01434', N'A', N'14', 14, N'STUDIO', 30.00, 2541000.00, 76200.00, 2286000.00, 1600000.00, 900000.00),
(N'3079-0A01435', N'A', N'14', 14, N'2 BEDROOMS', 56.00, 4643000.00, 77200.00, 4323000.00, 3030000.00, 1680000.00),
(N'3079-0A01436', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3574000.00, 74200.00, 3005000.00, 2100000.00, 1220000.00),
(N'3079-0A01437', N'A', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3124000.00, 74200.00, 3005000.00, 2100000.00, 1220000.00),
(N'3079-0A01438', N'A', N'14', 14, N'2 BEDROOMS', 51.50, 4248000.00, 77200.00, 3976000.00, 2780000.00, 1550000.00),
(N'3079-0A01501', N'A', N'15', 15, N'2 BEDROOMS', 60.50, 4918000.00, 75600.00, 4574000.00, 3200000.00, 1820000.00),
(N'3079-0A01502', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.00, 2924000.00, 74600.00, 2984000.00, 2090000.00, 1200000.00),
(N'3079-0A01503', N'A', N'15', 15, N'1 BEDROOM PLUS', 46.00, 3330000.00, 74600.00, 3432000.00, 2400000.00, 1380000.00),
(N'3079-0A01504', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3115000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01505', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3115000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01506', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3420000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01507', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3347000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01508', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3420000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01509', N'A', N'15', 15, N'1 BEDROOM PLUS', 41.00, 3420000.00, 74600.00, 3059000.00, 2140000.00, 1230000.00),
(N'3079-0A01510', N'A', N'15', 15, N'1 BEDROOM PLUS', 46.00, 3330000.00, 74600.00, 3432000.00, 2400000.00, 1380000.00),
(N'3079-0A01511', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.00, 2924000.00, 74600.00, 2984000.00, 2090000.00, 1200000.00),
(N'3079-0A01512', N'A', N'15', 15, N'2 BEDROOMS', 60.50, 4918000.00, 75600.00, 4574000.00, 3200000.00, 1820000.00),
(N'3079-0A01514', N'A', N'15', 15, N'2 BEDROOMS', 51.50, 4268000.00, 77600.00, 3996000.00, 2800000.00, 1550000.00),
(N'3079-0A01515', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3450000.00, 74600.00, 3021000.00, 2110000.00, 1220000.00),
(N'3079-0A01516', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3593000.00, 74600.00, 3021000.00, 2110000.00, 1220000.00),
(N'3079-0A01517', N'A', N'15', 15, N'2 BEDROOMS', 56.00, 4234000.00, 77600.00, 4346000.00, 3040000.00, 1680000.00),
(N'3079-0A01518', N'A', N'15', 15, N'STUDIO', 30.00, 2303000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01519', N'A', N'15', 15, N'1 BEDROOM', 34.00, 2652000.00, 74600.00, 2536000.00, 1780000.00, 1020000.00),
(N'3079-0A01520', N'A', N'15', 15, N'STUDIO', 30.00, 2608000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01521', N'A', N'15', 15, N'STUDIO', 30.00, 2608000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01522', N'A', N'15', 15, N'1 BEDROOM', 34.00, 2957000.00, 74600.00, 2536000.00, 1780000.00, 1020000.00),
(N'3079-0A01523', N'A', N'15', 15, N'1 BEDROOM', 35.50, 2867000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01524', N'A', N'15', 15, N'1 BEDROOM', 35.50, 2867000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01525', N'A', N'15', 15, N'1 BEDROOM', 35.50, 3023000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01526', N'A', N'15', 15, N'1 BEDROOM', 35.50, 3086000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01527', N'A', N'15', 15, N'1 BEDROOM', 35.50, 3086000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01528', N'A', N'15', 15, N'1 BEDROOM', 35.50, 2867000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01529', N'A', N'15', 15, N'1 BEDROOM', 35.50, 2867000.00, 74600.00, 2648000.00, 1850000.00, 1070000.00),
(N'3079-0A01530', N'A', N'15', 15, N'1 BEDROOM', 34.00, 2957000.00, 74600.00, 2536000.00, 1780000.00, 1020000.00),
(N'3079-0A01531', N'A', N'15', 15, N'STUDIO', 30.00, 2608000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01532', N'A', N'15', 15, N'STUDIO', 30.00, 2608000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01533', N'A', N'15', 15, N'1 BEDROOM', 34.00, 2652000.00, 74600.00, 2536000.00, 1780000.00, 1020000.00),
(N'3079-0A01534', N'A', N'15', 15, N'STUDIO', 30.00, 2303000.00, 76600.00, 2298000.00, 1610000.00, 900000.00),
(N'3079-0A01535', N'A', N'15', 15, N'2 BEDROOMS', 56.00, 4669000.00, 77600.00, 4346000.00, 3040000.00, 1680000.00),
(N'3079-0A01536', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3201000.00, 74600.00, 3021000.00, 2110000.00, 1220000.00),
(N'3079-0A01537', N'A', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3141000.00, 74600.00, 3021000.00, 2110000.00, 1220000.00),
(N'3079-0A01538', N'A', N'15', 15, N'2 BEDROOMS', 51.50, 4268000.00, 77600.00, 3996000.00, 2800000.00, 1550000.00),
(N'3079-0A01601', N'A', N'16', 16, N'2 BEDROOMS', 60.50, 4942000.00, 76000.00, 4598000.00, 3220000.00, 1820000.00),
(N'3079-0A01602', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.00, 2940000.00, 75000.00, 3000000.00, 2100000.00, 1200000.00),
(N'3079-0A01603', N'A', N'16', 16, N'1 BEDROOM PLUS', 46.00, 3348000.00, 75000.00, 3450000.00, 2420000.00, 1380000.00),
(N'3079-0A01604', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3131000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01605', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3439000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01606', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3439000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01607', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3366000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01608', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3439000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01609', N'A', N'16', 16, N'1 BEDROOM PLUS', 41.00, 3439000.00, 75000.00, 3075000.00, 2150000.00, 1230000.00),
(N'3079-0A01610', N'A', N'16', 16, N'1 BEDROOM PLUS', 46.00, 3348000.00, 75000.00, 3450000.00, 2420000.00, 1380000.00),
(N'3079-0A01611', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.00, 3212000.00, 75000.00, 3000000.00, 2100000.00, 1200000.00),
(N'3079-0A01612', N'A', N'16', 16, N'2 BEDROOMS', 60.50, 4942000.00, 76000.00, 4598000.00, 3220000.00, 1820000.00),
(N'3079-0A01614', N'A', N'16', 16, N'2 BEDROOMS', 51.50, 4805000.00, 78000.00, 4017000.00, 2810000.00, 1550000.00),
(N'3079-0A01615', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3469000.00, 75000.00, 3038000.00, 2130000.00, 1220000.00),
(N'3079-0A01616', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3217000.00, 75000.00, 3038000.00, 2130000.00, 1220000.00),
(N'3079-0A01617', N'A', N'16', 16, N'2 BEDROOMS', 56.00, 4696000.00, 78000.00, 4368000.00, 3060000.00, 1680000.00),
(N'3079-0A01618', N'A', N'16', 16, N'STUDIO', 30.00, 2569000.00, 77000.00, 2310000.00, 1620000.00, 900000.00),
(N'3079-0A01619', N'A', N'16', 16, N'2 BEDROOMS', 64.00, 5593000.00, 75000.00, 4800000.00, 3360000.00, 1920000.00),
(N'3079-0A01621', N'A', N'16', 16, N'STUDIO', 30.00, 2622000.00, 77000.00, 2310000.00, 1620000.00, 900000.00),
(N'3079-0A01622', N'A', N'16', 16, N'1 BEDROOM', 34.00, 2972000.00, 75000.00, 2550000.00, 1790000.00, 1020000.00),
(N'3079-0A01623', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3167000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01624', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3167000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01625', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3040000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01626', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3103000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01627', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3103000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01628', N'A', N'16', 16, N'1 BEDROOM', 35.50, 2881000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01629', N'A', N'16', 16, N'1 BEDROOM', 35.50, 3167000.00, 75000.00, 2663000.00, 1860000.00, 1070000.00),
(N'3079-0A01630', N'A', N'16', 16, N'1 BEDROOM', 34.00, 2972000.00, 75000.00, 2550000.00, 1790000.00, 1020000.00),
(N'3079-0A01631', N'A', N'16', 16, N'STUDIO', 30.00, 2622000.00, 77000.00, 2310000.00, 1620000.00, 900000.00),
(N'3079-0A01632', N'A', N'16', 16, N'2 BEDROOMS', 64.00, 5593000.00, 75000.00, 4800000.00, 3360000.00, 1920000.00),
(N'3079-0A01634', N'A', N'16', 16, N'STUDIO', 30.00, 2569000.00, 77000.00, 2310000.00, 1620000.00, 900000.00),
(N'3079-0A01635', N'A', N'16', 16, N'2 BEDROOMS', 56.00, 4696000.00, 78000.00, 4368000.00, 3060000.00, 1680000.00),
(N'3079-0A01636', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3611000.00, 75000.00, 3038000.00, 2130000.00, 1220000.00),
(N'3079-0A01637', N'A', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3469000.00, 75000.00, 3038000.00, 2130000.00, 1220000.00),
(N'3079-0A01638', N'A', N'16', 16, N'2 BEDROOMS', 51.50, 4289000.00, 78000.00, 4017000.00, 2810000.00, 1550000.00),
(N'3079-0A01701', N'A', N'17', 17, N'2 BEDROOMS', 60.50, 5148000.00, 76400.00, 4622000.00, 3240000.00, 1820000.00),
(N'3079-0A01702', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.00, 3373000.00, 75400.00, 3016000.00, 2110000.00, 1200000.00),
(N'3079-0A01703', N'A', N'17', 17, N'1 BEDROOM PLUS', 46.00, 3798000.00, 75400.00, 3468000.00, 2430000.00, 1380000.00),
(N'3079-0A01704', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3532000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01705', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3458000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01706', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3458000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01707', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3384000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01708', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3458000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01709', N'A', N'17', 17, N'1 BEDROOM PLUS', 41.00, 3458000.00, 75400.00, 3091000.00, 2160000.00, 1230000.00),
(N'3079-0A01710', N'A', N'17', 17, N'1 BEDROOM PLUS', 46.00, 3717000.00, 75400.00, 3468000.00, 2430000.00, 1380000.00),
(N'3079-0A01711', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.00, 3232000.00, 75400.00, 3016000.00, 2110000.00, 1200000.00),
(N'3079-0A01712', N'A', N'17', 17, N'2 BEDROOMS', 60.50, 5910000.00, 76400.00, 4622000.00, 3240000.00, 1820000.00),
(N'3079-0A01714', N'A', N'17', 17, N'2 BEDROOMS', 51.50, 4737000.00, 78400.00, 4038000.00, 2830000.00, 1550000.00),
(N'3079-0A01715', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3487000.00, 75400.00, 3054000.00, 2140000.00, 1220000.00),
(N'3079-0A01716', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3632000.00, 75400.00, 3054000.00, 2140000.00, 1220000.00),
(N'3079-0A01717', N'A', N'17', 17, N'2 BEDROOMS', 56.00, 4723000.00, 78400.00, 4390000.00, 3070000.00, 1680000.00),
(N'3079-0A01718', N'A', N'17', 17, N'STUDIO', 30.00, 2584000.00, 77400.00, 2322000.00, 1630000.00, 900000.00),
(N'3079-0A01719', N'A', N'17', 17, N'2 BEDROOMS', 64.00, 5624000.00, 75400.00, 4826000.00, 3380000.00, 1920000.00),
(N'3079-0A01721', N'A', N'17', 17, N'STUDIO', 30.00, 2636000.00, 77400.00, 2322000.00, 1630000.00, 900000.00),
(N'3079-0A01722', N'A', N'17', 17, N'1 BEDROOM', 34.00, 2989000.00, 75400.00, 2564000.00, 1790000.00, 1020000.00),
(N'3079-0A01723', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3183000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01724', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3183000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01725', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3057000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01726', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3120000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01727', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3120000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01728', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3183000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01729', N'A', N'17', 17, N'1 BEDROOM', 35.50, 3183000.00, 75400.00, 2677000.00, 1870000.00, 1070000.00),
(N'3079-0A01730', N'A', N'17', 17, N'1 BEDROOM', 34.00, 2989000.00, 75400.00, 2564000.00, 1790000.00, 1020000.00),
(N'3079-0A01731', N'A', N'17', 17, N'STUDIO', 30.00, 2636000.00, 77400.00, 2322000.00, 1630000.00, 900000.00),
(N'3079-0A01732', N'A', N'17', 17, N'2 BEDROOMS', 64.00, 5624000.00, 75400.00, 4826000.00, 3380000.00, 1920000.00),
(N'3079-0A01734', N'A', N'17', 17, N'STUDIO', 30.00, 2584000.00, 77400.00, 2322000.00, 1630000.00, 900000.00),
(N'3079-0A01735', N'A', N'17', 17, N'2 BEDROOMS', 56.00, 4723000.00, 78400.00, 4390000.00, 3070000.00, 1680000.00),
(N'3079-0A01736', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3560000.00, 75400.00, 3054000.00, 2140000.00, 1220000.00),
(N'3079-0A01737', N'A', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3487000.00, 75400.00, 3054000.00, 2140000.00, 1220000.00),
(N'3079-0A01738', N'A', N'17', 17, N'2 BEDROOMS', 51.50, 4737000.00, 78400.00, 4038000.00, 2830000.00, 1550000.00),
(N'3079-0A01801', N'A', N'18', 18, N'2 BEDROOMS', 60.50, 4991000.00, 76800.00, 4646000.00, 3250000.00, 1820000.00),
(N'3079-0A01802', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.00, 3393000.00, 75800.00, 3032000.00, 2120000.00, 1200000.00),
(N'3079-0A01803', N'A', N'18', 18, N'1 BEDROOM PLUS', 46.00, 3385000.00, 75800.00, 3487000.00, 2440000.00, 1380000.00),
(N'3079-0A01804', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3550000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01805', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3477000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01806', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3477000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01807', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3406000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01808', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3477000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01809', N'A', N'18', 18, N'1 BEDROOM PLUS', 41.00, 3477000.00, 75800.00, 3108000.00, 2180000.00, 1230000.00),
(N'3079-0A01810', N'A', N'18', 18, N'1 BEDROOM PLUS', 46.00, 3385000.00, 75800.00, 3487000.00, 2440000.00, 1380000.00),
(N'3079-0A01811', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.00, 2972000.00, 75800.00, 3032000.00, 2120000.00, 1200000.00),
(N'3079-0A01812', N'A', N'18', 18, N'2 BEDROOMS', 60.50, 4991000.00, 76800.00, 4646000.00, 3250000.00, 1820000.00),
(N'3079-0A01814', N'A', N'18', 18, N'2 BEDROOMS', 51.50, 4330000.00, 78800.00, 4058000.00, 2840000.00, 1550000.00),
(N'3079-0A01815', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3506000.00, 75800.00, 3070000.00, 2150000.00, 1220000.00),
(N'3079-0A01816', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3579000.00, 75800.00, 3070000.00, 2150000.00, 1220000.00),
(N'3079-0A01817', N'A', N'18', 18, N'2 BEDROOMS', 56.00, 4301000.00, 78800.00, 4413000.00, 3090000.00, 1680000.00),
(N'3079-0A01818', N'A', N'18', 18, N'STUDIO', 30.00, 2598000.00, 77800.00, 2334000.00, 1630000.00, 900000.00),
(N'3079-0A01819', N'A', N'18', 18, N'2 BEDROOMS', 64.00, 5088000.00, 75800.00, 4851000.00, 3400000.00, 1920000.00),
(N'3079-0A01821', N'A', N'18', 18, N'STUDIO', 30.00, 2650000.00, 77800.00, 2334000.00, 1630000.00, 900000.00),
(N'3079-0A01822', N'A', N'18', 18, N'1 BEDROOM', 34.00, 3005000.00, 75800.00, 2577000.00, 1800000.00, 1020000.00),
(N'3079-0A01823', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3200000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01824', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3200000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01825', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3074000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01826', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3136000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01827', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3136000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01828', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3200000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01829', N'A', N'18', 18, N'1 BEDROOM', 35.50, 3200000.00, 75800.00, 2691000.00, 1880000.00, 1070000.00),
(N'3079-0A01830', N'A', N'18', 18, N'1 BEDROOM', 34.00, 3005000.00, 75800.00, 2577000.00, 1800000.00, 1020000.00),
(N'3079-0A01831', N'A', N'18', 18, N'STUDIO', 30.00, 2650000.00, 77800.00, 2334000.00, 1630000.00, 900000.00),
(N'3079-0A01832', N'A', N'18', 18, N'2 BEDROOMS', 64.00, 5655000.00, 75800.00, 4851000.00, 3400000.00, 1920000.00),
(N'3079-0A01834', N'A', N'18', 18, N'STUDIO', 30.00, 2598000.00, 77800.00, 2334000.00, 1630000.00, 900000.00),
(N'3079-0A01835', N'A', N'18', 18, N'2 BEDROOMS', 56.00, 4301000.00, 78800.00, 4413000.00, 3090000.00, 1680000.00),
(N'3079-0A01836', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3652000.00, 75800.00, 3070000.00, 2150000.00, 1220000.00),
(N'3079-0A01837', N'A', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3506000.00, 75800.00, 3070000.00, 2150000.00, 1220000.00),
(N'3079-0A01838', N'A', N'18', 18, N'2 BEDROOMS', 51.50, 4330000.00, 78800.00, 4058000.00, 2840000.00, 1550000.00),
(N'3079-0A01901', N'A', N'19', 19, N'2 BEDROOMS', 60.50, 5197000.00, 77200.00, 4671000.00, 3270000.00, 1820000.00),
(N'3079-0A01902', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.00, 3411000.00, 76200.00, 3048000.00, 2130000.00, 1200000.00),
(N'3079-0A01903', N'A', N'19', 19, N'1 BEDROOM PLUS', 46.00, 4086000.00, 76200.00, 3505000.00, 2450000.00, 1380000.00),
(N'3079-0A01904', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3497000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01905', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3497000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01906', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3497000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01907', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3424000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01908', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3497000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01909', N'A', N'19', 19, N'1 BEDROOM PLUS', 41.00, 3497000.00, 76200.00, 3124000.00, 2190000.00, 1230000.00),
(N'3079-0A01910', N'A', N'19', 19, N'1 BEDROOM PLUS', 46.00, 3761000.00, 76200.00, 3505000.00, 2450000.00, 1380000.00),
(N'3079-0A01911', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.00, 3269000.00, 76200.00, 3048000.00, 2130000.00, 1200000.00),
(N'3079-0A01912', N'A', N'19', 19, N'2 BEDROOMS', 60.50, 5197000.00, 77200.00, 4671000.00, 3270000.00, 1820000.00),
(N'3079-0A01914', N'A', N'19', 19, N'2 BEDROOMS', 51.50, 4786000.00, 79200.00, 4079000.00, 2860000.00, 1550000.00),
(N'3079-0A01915', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3527000.00, 76200.00, 3086000.00, 2160000.00, 1220000.00),
(N'3079-0A01916', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3670000.00, 76200.00, 3086000.00, 2160000.00, 1220000.00),
(N'3079-0A01917', N'A', N'19', 19, N'2 BEDROOMS', 56.00, 4775000.00, 79200.00, 4435000.00, 3100000.00, 1680000.00),
(N'3079-0A01918', N'A', N'19', 19, N'STUDIO', 30.00, 2612000.00, 78200.00, 2346000.00, 1640000.00, 900000.00),
(N'3079-0A01919', N'A', N'19', 19, N'2 BEDROOMS', 64.00, 5797000.00, 76200.00, 4877000.00, 3410000.00, 1920000.00),
(N'3079-0A01921', N'A', N'19', 19, N'STUDIO', 30.00, 2665000.00, 78200.00, 2346000.00, 1640000.00, 900000.00),
(N'3079-0A01922', N'A', N'19', 19, N'1 BEDROOM', 34.00, 3020000.00, 76200.00, 2591000.00, 1810000.00, 1020000.00),
(N'3079-0A01923', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3217000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01924', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3217000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01925', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3092000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01926', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3153000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01927', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3153000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01928', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3217000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01929', N'A', N'19', 19, N'1 BEDROOM', 35.50, 3217000.00, 76200.00, 2705000.00, 1890000.00, 1070000.00),
(N'3079-0A01930', N'A', N'19', 19, N'1 BEDROOM', 34.00, 3020000.00, 76200.00, 2591000.00, 1810000.00, 1020000.00),
(N'3079-0A01931', N'A', N'19', 19, N'STUDIO', 30.00, 2665000.00, 78200.00, 2346000.00, 1640000.00, 900000.00),
(N'3079-0A01932', N'A', N'19', 19, N'2 BEDROOMS', 64.00, 5683000.00, 76200.00, 4877000.00, 3410000.00, 1920000.00),
(N'3079-0A01934', N'A', N'19', 19, N'STUDIO', 30.00, 2612000.00, 78200.00, 2346000.00, 1640000.00, 900000.00),
(N'3079-0A01935', N'A', N'19', 19, N'2 BEDROOMS', 56.00, 4775000.00, 79200.00, 4435000.00, 3100000.00, 1680000.00),
(N'3079-0A01936', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3670000.00, 76200.00, 3086000.00, 2160000.00, 1220000.00),
(N'3079-0A01937', N'A', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3670000.00, 76200.00, 3086000.00, 2160000.00, 1220000.00),
(N'3079-0A01938', N'A', N'19', 19, N'2 BEDROOMS', 51.50, 4878000.00, 79200.00, 4079000.00, 2860000.00, 1550000.00),
(N'3079-0A02001', N'A', N'20', 20, N'2 BEDROOMS', 60.50, 5039000.00, 77600.00, 4695000.00, 3290000.00, 1820000.00),
(N'3079-0A02002', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.00, 3430000.00, 76600.00, 3064000.00, 2140000.00, 1200000.00),
(N'3079-0A02003', N'A', N'20', 20, N'1 BEDROOM PLUS', 46.00, 3422000.00, 76600.00, 3524000.00, 2470000.00, 1380000.00),
(N'3079-0A02004', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3197000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02005', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3197000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02006', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3517000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02007', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3444000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02008', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3517000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02009', N'A', N'20', 20, N'1 BEDROOM PLUS', 41.00, 3517000.00, 76600.00, 3141000.00, 2200000.00, 1230000.00),
(N'3079-0A02010', N'A', N'20', 20, N'1 BEDROOM PLUS', 46.00, 3422000.00, 76600.00, 3524000.00, 2470000.00, 1380000.00),
(N'3079-0A02011', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.00, 3289000.00, 76600.00, 3064000.00, 2140000.00, 1200000.00),
(N'3079-0A02012', N'A', N'20', 20, N'2 BEDROOMS', 60.50, 5039000.00, 77600.00, 4695000.00, 3290000.00, 1820000.00),
(N'3079-0A02014', N'A', N'20', 20, N'2 BEDROOMS', 51.50, 4371000.00, 79600.00, 4099000.00, 2870000.00, 1550000.00),
(N'3079-0A02015', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3546000.00, 76600.00, 3102000.00, 2170000.00, 1220000.00),
(N'3079-0A02016', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3689000.00, 76600.00, 3102000.00, 2170000.00, 1220000.00),
(N'3079-0A02017', N'A', N'20', 20, N'2 BEDROOMS', 56.00, 4346000.00, 79600.00, 4458000.00, 3120000.00, 1680000.00),
(N'3079-0A02018', N'A', N'20', 20, N'STUDIO', 30.00, 2627000.00, 78600.00, 2358000.00, 1650000.00, 900000.00),
(N'3079-0A02019', N'A', N'20', 20, N'2 BEDROOMS', 64.00, 5714000.00, 76600.00, 4902000.00, 3430000.00, 1920000.00),
(N'3079-0A02021', N'A', N'20', 20, N'STUDIO', 30.00, 2679000.00, 78600.00, 2358000.00, 1650000.00, 900000.00),
(N'3079-0A02022', N'A', N'20', 20, N'1 BEDROOM', 34.00, 3036000.00, 76600.00, 2604000.00, 1820000.00, 1020000.00),
(N'3079-0A02023', N'A', N'20', 20, N'1 BEDROOM', 35.50, 3234000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02024', N'A', N'20', 20, N'1 BEDROOM', 35.50, 3234000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02025', N'A', N'20', 20, N'1 BEDROOM', 35.50, 3108000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02026', N'A', N'20', 20, N'1 BEDROOM', 35.50, 3171000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02027', N'A', N'20', 20, N'1 BEDROOM', 35.50, 3171000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02028', N'A', N'20', 20, N'1 BEDROOM', 35.50, 2938000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02029', N'A', N'20', 20, N'1 BEDROOM', 35.50, 2938000.00, 76600.00, 2719000.00, 1900000.00, 1070000.00),
(N'3079-0A02030', N'A', N'20', 20, N'1 BEDROOM', 34.00, 3036000.00, 76600.00, 2604000.00, 1820000.00, 1020000.00),
(N'3079-0A02031', N'A', N'20', 20, N'STUDIO', 30.00, 2679000.00, 78600.00, 2358000.00, 1650000.00, 900000.00),
(N'3079-0A02032', N'A', N'20', 20, N'2 BEDROOMS', 64.00, 5714000.00, 76600.00, 4902000.00, 3430000.00, 1920000.00),
(N'3079-0A02034', N'A', N'20', 20, N'STUDIO', 30.00, 2627000.00, 78600.00, 2358000.00, 1650000.00, 900000.00),
(N'3079-0A02035', N'A', N'20', 20, N'2 BEDROOMS', 56.00, 4803000.00, 79600.00, 4458000.00, 3120000.00, 1680000.00),
(N'3079-0A02036', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3689000.00, 76600.00, 3102000.00, 2170000.00, 1220000.00),
(N'3079-0A02037', N'A', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3222000.00, 76600.00, 3102000.00, 2170000.00, 1220000.00),
(N'3079-0A02038', N'A', N'20', 20, N'2 BEDROOMS', 51.50, 4371000.00, 79600.00, 4099000.00, 2870000.00, 1550000.00),
(N'3079-0A02101', N'A', N'21', 21, N'2 BEDROOMS', 60.50, 5245000.00, 78000.00, 4719000.00, 3300000.00, 1820000.00),
(N'3079-0A02102', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.00, 3449000.00, 77000.00, 3080000.00, 2160000.00, 1200000.00),
(N'3079-0A02103', N'A', N'21', 21, N'1 BEDROOM PLUS', 46.00, 4129000.00, 77000.00, 3542000.00, 2480000.00, 1380000.00),
(N'3079-0A02104', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3608000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02105', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3535000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02106', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3535000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02107', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3462000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02108', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3535000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02109', N'A', N'21', 21, N'1 BEDROOM PLUS', 41.00, 3535000.00, 77000.00, 3157000.00, 2210000.00, 1230000.00),
(N'3079-0A02110', N'A', N'21', 21, N'1 BEDROOM PLUS', 46.00, 4047000.00, 77000.00, 3542000.00, 2480000.00, 1380000.00),
(N'3079-0A02111', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.00, 3307000.00, 77000.00, 3080000.00, 2160000.00, 1200000.00),
(N'3079-0A02112', N'A', N'21', 21, N'2 BEDROOMS', 60.50, 5200000.00, 78000.00, 4719000.00, 3300000.00, 1820000.00),
(N'3079-0A02114', N'A', N'21', 21, N'2 BEDROOMS', 51.50, 4807000.00, 80000.00, 4120000.00, 2880000.00, 1550000.00),
(N'3079-0A02115', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3565000.00, 77000.00, 3119000.00, 2180000.00, 1220000.00),
(N'3079-0A02116', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3707000.00, 77000.00, 3119000.00, 2180000.00, 1220000.00),
(N'3079-0A02117', N'A', N'21', 21, N'2 BEDROOMS', 56.00, 4828000.00, 80000.00, 4480000.00, 3140000.00, 1680000.00),
(N'3079-0A02118', N'A', N'21', 21, N'STUDIO', 30.00, 2640000.00, 79000.00, 2370000.00, 1660000.00, 900000.00),
(N'3079-0A02119', N'A', N'21', 21, N'2 BEDROOMS', 64.00, 5971000.00, 77000.00, 4928000.00, 3450000.00, 1920000.00),
(N'3079-0A02121', N'A', N'21', 21, N'STUDIO', 30.00, 2694000.00, 79000.00, 2370000.00, 1660000.00, 900000.00),
(N'3079-0A02122', N'A', N'21', 21, N'1 BEDROOM', 34.00, 3053000.00, 77000.00, 2618000.00, 1830000.00, 1020000.00),
(N'3079-0A02123', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3250000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02124', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3250000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02125', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3124000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02126', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3187000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02127', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3187000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02128', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3250000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02129', N'A', N'21', 21, N'1 BEDROOM', 35.50, 3250000.00, 77000.00, 2734000.00, 1910000.00, 1070000.00),
(N'3079-0A02130', N'A', N'21', 21, N'1 BEDROOM', 34.00, 3053000.00, 77000.00, 2618000.00, 1830000.00, 1020000.00),
(N'3079-0A02131', N'A', N'21', 21, N'STUDIO', 30.00, 2694000.00, 79000.00, 2370000.00, 1660000.00, 900000.00),
(N'3079-0A02132', N'A', N'21', 21, N'2 BEDROOMS', 64.00, 5971000.00, 77000.00, 4928000.00, 3450000.00, 1920000.00),
(N'3079-0A02134', N'A', N'21', 21, N'STUDIO', 30.00, 2640000.00, 79000.00, 2370000.00, 1660000.00, 900000.00),
(N'3079-0A02135', N'A', N'21', 21, N'2 BEDROOMS', 56.00, 4828000.00, 80000.00, 4480000.00, 3140000.00, 1680000.00),
(N'3079-0A02136', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3707000.00, 77000.00, 3119000.00, 2180000.00, 1220000.00),
(N'3079-0A02137', N'A', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3708000.00, 77000.00, 3119000.00, 2180000.00, 1220000.00),
(N'3079-0A02138', N'A', N'21', 21, N'2 BEDROOMS', 51.50, 5109000.00, 80000.00, 4120000.00, 2880000.00, 1550000.00),
(N'3079-0A02201', N'A', N'22', 22, N'2 BEDROOMS', 60.50, 5088000.00, 78400.00, 4743000.00, 3320000.00, 1820000.00),
(N'3079-0A02202', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.00, 3468000.00, 77400.00, 3096000.00, 2170000.00, 1200000.00),
(N'3079-0A02203', N'A', N'22', 22, N'1 BEDROOM PLUS', 46.00, 3564000.00, 77400.00, 3560000.00, 2490000.00, 1380000.00),
(N'3079-0A02204', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3629000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02205', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3555000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02206', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3555000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02207', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3482000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02208', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3555000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02209', N'A', N'22', 22, N'1 BEDROOM PLUS', 41.00, 3230000.00, 77400.00, 3173000.00, 2220000.00, 1230000.00),
(N'3079-0A02210', N'A', N'22', 22, N'1 BEDROOM PLUS', 46.00, 3459000.00, 77400.00, 3560000.00, 2490000.00, 1380000.00),
(N'3079-0A02211', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.00, 3327000.00, 77400.00, 3096000.00, 2170000.00, 1200000.00),
(N'3079-0A02212', N'A', N'22', 22, N'2 BEDROOMS', 60.50, 5088000.00, 78400.00, 4743000.00, 3320000.00, 1820000.00),
(N'3079-0A02214', N'A', N'22', 22, N'2 BEDROOMS', 51.50, 4831000.00, 80400.00, 4141000.00, 2900000.00, 1550000.00),
(N'3079-0A02215', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3254000.00, 77400.00, 3135000.00, 2190000.00, 1220000.00),
(N'3079-0A02216', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3315000.00, 77400.00, 3135000.00, 2190000.00, 1220000.00),
(N'3079-0A02217', N'A', N'22', 22, N'2 BEDROOMS', 56.00, 4855000.00, 80400.00, 4502000.00, 3150000.00, 1680000.00),
(N'3079-0A02218', N'A', N'22', 22, N'STUDIO', 30.00, 2654000.00, 79400.00, 2382000.00, 1670000.00, 900000.00),
(N'3079-0A02219', N'A', N'22', 22, N'2 BEDROOMS', 64.00, 5774000.00, 77400.00, 4954000.00, 3470000.00, 1920000.00),
(N'3079-0A02221', N'A', N'22', 22, N'STUDIO', 30.00, 2708000.00, 79400.00, 2382000.00, 1670000.00, 900000.00),
(N'3079-0A02222', N'A', N'22', 22, N'1 BEDROOM', 34.00, 3069000.00, 77400.00, 2632000.00, 1840000.00, 1020000.00),
(N'3079-0A02223', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3267000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02224', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3267000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02225', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3141000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02226', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3205000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02227', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3205000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02228', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3267000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02229', N'A', N'22', 22, N'1 BEDROOM', 35.50, 3267000.00, 77400.00, 2748000.00, 1920000.00, 1070000.00),
(N'3079-0A02230', N'A', N'22', 22, N'1 BEDROOM', 34.00, 3069000.00, 77400.00, 2632000.00, 1840000.00, 1020000.00),
(N'3079-0A02231', N'A', N'22', 22, N'STUDIO', 30.00, 2708000.00, 79400.00, 2382000.00, 1670000.00, 900000.00),
(N'3079-0A02232', N'A', N'22', 22, N'2 BEDROOMS', 64.00, 5774000.00, 77400.00, 4954000.00, 3470000.00, 1920000.00),
(N'3079-0A02234', N'A', N'22', 22, N'STUDIO', 30.00, 2654000.00, 79400.00, 2382000.00, 1670000.00, 900000.00),
(N'3079-0A02235', N'A', N'22', 22, N'2 BEDROOMS', 56.00, 4855000.00, 80400.00, 4502000.00, 3150000.00, 1680000.00),
(N'3079-0A02236', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3727000.00, 77400.00, 3135000.00, 2190000.00, 1220000.00),
(N'3079-0A02237', N'A', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3583000.00, 77400.00, 3135000.00, 2190000.00, 1220000.00),
(N'3079-0A02238', N'A', N'22', 22, N'2 BEDROOMS', 51.50, 4859000.00, 80400.00, 4141000.00, 2900000.00, 1550000.00),
(N'3079-0A02301', N'A', N'23', 23, N'2 BEDROOMS', 60.50, 5293000.00, 78800.00, 4767000.00, 3340000.00, 1820000.00),
(N'3079-0A02302', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.00, 3486000.00, 77800.00, 3112000.00, 2180000.00, 1200000.00),
(N'3079-0A02303', N'A', N'23', 23, N'1 BEDROOM PLUS', 46.00, 4172000.00, 77800.00, 3579000.00, 2510000.00, 1380000.00),
(N'3079-0A02304', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3647000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02305', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3574000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02306', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3574000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02307', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3502000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02308', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3574000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02309', N'A', N'23', 23, N'1 BEDROOM PLUS', 41.00, 3574000.00, 77800.00, 3190000.00, 2230000.00, 1230000.00),
(N'3079-0A02310', N'A', N'23', 23, N'1 BEDROOM PLUS', 46.00, 4091000.00, 77800.00, 3579000.00, 2510000.00, 1380000.00),
(N'3079-0A02311', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.00, 3345000.00, 77800.00, 3112000.00, 2180000.00, 1200000.00),
(N'3079-0A02312', N'A', N'23', 23, N'2 BEDROOMS', 60.50, 5867000.00, 78800.00, 4767000.00, 3340000.00, 1820000.00),
(N'3079-0A02314', N'A', N'23', 23, N'2 BEDROOMS', 51.50, 4855000.00, 80800.00, 4161000.00, 2910000.00, 1550000.00),
(N'3079-0A02315', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3602000.00, 77800.00, 3151000.00, 2210000.00, 1220000.00),
(N'3079-0A02316', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3747000.00, 77800.00, 3151000.00, 2210000.00, 1220000.00),
(N'3079-0A02317', N'A', N'23', 23, N'2 BEDROOMS', 56.00, 4881000.00, 80800.00, 4525000.00, 3170000.00, 1680000.00),
(N'3079-0A02318', N'A', N'23', 23, N'STUDIO', 30.00, 2668000.00, 79800.00, 2394000.00, 1680000.00, 900000.00),
(N'3079-0A02319', N'A', N'23', 23, N'2 BEDROOMS', 64.00, 6032000.00, 77800.00, 4979000.00, 3490000.00, 1920000.00),
(N'3079-0A02321', N'A', N'23', 23, N'STUDIO', 30.00, 2722000.00, 79800.00, 2394000.00, 1680000.00, 900000.00),
(N'3079-0A02322', N'A', N'23', 23, N'1 BEDROOM', 34.00, 3085000.00, 77800.00, 2645000.00, 1850000.00, 1020000.00),
(N'3079-0A02323', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3284000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02324', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3284000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02325', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3159000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02326', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3221000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02327', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3221000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02328', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3284000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02329', N'A', N'23', 23, N'1 BEDROOM', 35.50, 3284000.00, 77800.00, 2762000.00, 1930000.00, 1070000.00),
(N'3079-0A02330', N'A', N'23', 23, N'1 BEDROOM', 34.00, 3085000.00, 77800.00, 2645000.00, 1850000.00, 1020000.00),
(N'3079-0A02331', N'A', N'23', 23, N'STUDIO', 30.00, 2722000.00, 79800.00, 2394000.00, 1680000.00, 900000.00),
(N'3079-0A02332', N'A', N'23', 23, N'2 BEDROOMS', 64.00, 6152000.00, 77800.00, 4979000.00, 3490000.00, 1920000.00),
(N'3079-0A02334', N'A', N'23', 23, N'STUDIO', 30.00, 2668000.00, 79800.00, 2394000.00, 1680000.00, 900000.00),
(N'3079-0A02335', N'A', N'23', 23, N'2 BEDROOMS', 56.00, 4881000.00, 80800.00, 4525000.00, 3170000.00, 1680000.00),
(N'3079-0A02336', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3747000.00, 77800.00, 3151000.00, 2210000.00, 1220000.00),
(N'3079-0A02337', N'A', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3747000.00, 77800.00, 3151000.00, 2210000.00, 1220000.00),
(N'3079-0A02338', N'A', N'23', 23, N'2 BEDROOMS', 51.50, 5037000.00, 80800.00, 4161000.00, 2910000.00, 1550000.00),
(N'3079-0A02401', N'A', N'24', 24, N'2 BEDROOMS', 60.50, 5136000.00, 79200.00, 4792000.00, 3350000.00, 1820000.00),
(N'3079-0A02402', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.00, 3505000.00, 78200.00, 3128000.00, 2190000.00, 1200000.00),
(N'3079-0A02403', N'A', N'24', 24, N'1 BEDROOM PLUS', 46.00, 3496000.00, 78200.00, 3597000.00, 2520000.00, 1380000.00),
(N'3079-0A02404', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3668000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02405', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3594000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02406', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3594000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02407', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3521000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02408', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3594000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02409', N'A', N'24', 24, N'1 BEDROOM PLUS', 41.00, 3594000.00, 78200.00, 3206000.00, 2240000.00, 1230000.00),
(N'3079-0A02410', N'A', N'24', 24, N'1 BEDROOM PLUS', 46.00, 3496000.00, 78200.00, 3597000.00, 2520000.00, 1380000.00),
(N'3079-0A02411', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.00, 3364000.00, 78200.00, 3128000.00, 2190000.00, 1200000.00),
(N'3079-0A02412', N'A', N'24', 24, N'2 BEDROOMS', 60.50, 5136000.00, 79200.00, 4792000.00, 3350000.00, 1820000.00),
(N'3079-0A02414', N'A', N'24', 24, N'2 BEDROOMS', 51.50, 4880000.00, 81200.00, 4182000.00, 2930000.00, 1550000.00),
(N'3079-0A02415', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3621000.00, 78200.00, 3167000.00, 2220000.00, 1220000.00),
(N'3079-0A02416', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3693000.00, 78200.00, 3167000.00, 2220000.00, 1220000.00),
(N'3079-0A02417', N'A', N'24', 24, N'2 BEDROOMS', 56.00, 4907000.00, 81200.00, 4547000.00, 3180000.00, 1680000.00),
(N'3079-0A02418', N'A', N'24', 24, N'STUDIO', 30.00, 2682000.00, 80200.00, 2406000.00, 1680000.00, 900000.00),
(N'3079-0A02419', N'A', N'24', 24, N'2 BEDROOMS', 64.00, 5834000.00, 78200.00, 5005000.00, 3500000.00, 1920000.00),
(N'3079-0A02421', N'A', N'24', 24, N'STUDIO', 30.00, 2736000.00, 80200.00, 2406000.00, 1680000.00, 900000.00),
(N'3079-0A02422', N'A', N'24', 24, N'1 BEDROOM', 34.00, 3100000.00, 78200.00, 2659000.00, 1860000.00, 1020000.00),
(N'3079-0A02423', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3300000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02424', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3300000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02425', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3175000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02426', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3237000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02427', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3237000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02428', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3300000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02429', N'A', N'24', 24, N'1 BEDROOM', 35.50, 3300000.00, 78200.00, 2776000.00, 1940000.00, 1070000.00),
(N'3079-0A02430', N'A', N'24', 24, N'1 BEDROOM', 34.00, 3100000.00, 78200.00, 2659000.00, 1860000.00, 1020000.00),
(N'3079-0A02431', N'A', N'24', 24, N'STUDIO', 30.00, 2736000.00, 80200.00, 2406000.00, 1680000.00, 900000.00),
(N'3079-0A02432', N'A', N'24', 24, N'2 BEDROOMS', 64.00, 5834000.00, 78200.00, 5005000.00, 3500000.00, 1920000.00),
(N'3079-0A02434', N'A', N'24', 24, N'STUDIO', 30.00, 2682000.00, 80200.00, 2406000.00, 1680000.00, 900000.00),
(N'3079-0A02435', N'A', N'24', 24, N'2 BEDROOMS', 56.00, 4436000.00, 81200.00, 4547000.00, 3180000.00, 1680000.00),
(N'3079-0A02436', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3766000.00, 78200.00, 3167000.00, 2220000.00, 1220000.00),
(N'3079-0A02437', N'A', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3766000.00, 78200.00, 3167000.00, 2220000.00, 1220000.00),
(N'3079-0A02438', N'A', N'24', 24, N'2 BEDROOMS', 51.50, 4907000.00, 81200.00, 4182000.00, 2930000.00, 1550000.00),
(N'3079-0A02501', N'A', N'25', 25, N'2 BEDROOMS', 60.50, 5342000.00, 79600.00, 4816000.00, 3370000.00, 1820000.00),
(N'3079-0A02502', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.00, 3524000.00, 78600.00, 3144000.00, 2200000.00, 1200000.00),
(N'3079-0A02503', N'A', N'25', 25, N'1 BEDROOM PLUS', 46.00, 4217000.00, 78600.00, 3616000.00, 2530000.00, 1380000.00),
(N'3079-0A02504', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3686000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02505', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3612000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02506', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3612000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02507', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3541000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02508', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3612000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02509', N'A', N'25', 25, N'1 BEDROOM PLUS', 41.00, 3612000.00, 78600.00, 3223000.00, 2260000.00, 1230000.00),
(N'3079-0A02510', N'A', N'25', 25, N'1 BEDROOM PLUS', 46.00, 4135000.00, 78600.00, 3616000.00, 2530000.00, 1380000.00),
(N'3079-0A02511', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.00, 3382000.00, 78600.00, 3144000.00, 2200000.00, 1200000.00),
(N'3079-0A02512', N'A', N'25', 25, N'2 BEDROOMS', 60.50, 5342000.00, 79600.00, 4816000.00, 3370000.00, 1820000.00),
(N'3079-0A02514', N'A', N'25', 25, N'2 BEDROOMS', 51.50, 4903000.00, 81600.00, 4202000.00, 2940000.00, 1550000.00),
(N'3079-0A02515', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3642000.00, 78600.00, 3183000.00, 2230000.00, 1220000.00),
(N'3079-0A02516', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3784000.00, 78600.00, 3183000.00, 2230000.00, 1220000.00),
(N'3079-0A02517', N'A', N'25', 25, N'2 BEDROOMS', 56.00, 4934000.00, 81600.00, 4570000.00, 3200000.00, 1680000.00),
(N'3079-0A02518', N'A', N'25', 25, N'STUDIO', 30.00, 2697000.00, 80600.00, 2418000.00, 1690000.00, 900000.00),
(N'3079-0A02519', N'A', N'25', 25, N'2 BEDROOMS', 64.00, 5979000.00, 78600.00, 5030000.00, 3520000.00, 1920000.00),
(N'3079-0A02521', N'A', N'25', 25, N'STUDIO', 30.00, 2750000.00, 80600.00, 2418000.00, 1690000.00, 900000.00),
(N'3079-0A02522', N'A', N'25', 25, N'1 BEDROOM', 34.00, 3117000.00, 78600.00, 2672000.00, 1870000.00, 1020000.00),
(N'3079-0A02523', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3318000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02524', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3318000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02525', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3192000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02526', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3254000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02527', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3254000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02528', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3318000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02529', N'A', N'25', 25, N'1 BEDROOM', 35.50, 3318000.00, 78600.00, 2790000.00, 1950000.00, 1070000.00),
(N'3079-0A02530', N'A', N'25', 25, N'1 BEDROOM', 34.00, 3117000.00, 78600.00, 2672000.00, 1870000.00, 1020000.00),
(N'3079-0A02531', N'A', N'25', 25, N'STUDIO', 30.00, 2750000.00, 80600.00, 2418000.00, 1690000.00, 900000.00),
(N'3079-0A02532', N'A', N'25', 25, N'2 BEDROOMS', 64.00, 6093000.00, 78600.00, 5030000.00, 3520000.00, 1920000.00),
(N'3079-0A02534', N'A', N'25', 25, N'STUDIO', 30.00, 2697000.00, 80600.00, 2418000.00, 1690000.00, 900000.00),
(N'3079-0A02535', N'A', N'25', 25, N'2 BEDROOMS', 56.00, 4934000.00, 81600.00, 4570000.00, 3200000.00, 1680000.00),
(N'3079-0A02536', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3784000.00, 78600.00, 3183000.00, 2230000.00, 1220000.00),
(N'3079-0A02537', N'A', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3785000.00, 78600.00, 3183000.00, 2230000.00, 1220000.00),
(N'3079-0A02538', N'A', N'25', 25, N'2 BEDROOMS', 51.50, 5205000.00, 81600.00, 4202000.00, 2940000.00, 1550000.00),
(N'3079-0A02601', N'A', N'26', 26, N'2 BEDROOMS', 60.50, 5184000.00, 80000.00, 4840000.00, 3390000.00, 1820000.00),
(N'3079-0A02602', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.00, 3544000.00, 79000.00, 3160000.00, 2210000.00, 1200000.00),
(N'3079-0A02603', N'A', N'26', 26, N'1 BEDROOM PLUS', 46.00, 3532000.00, 79000.00, 3634000.00, 2540000.00, 1380000.00),
(N'3079-0A02604', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3632000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02605', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3295000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02606', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3400000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02607', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3560000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02608', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3632000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02609', N'A', N'26', 26, N'1 BEDROOM PLUS', 41.00, 3632000.00, 79000.00, 3239000.00, 2270000.00, 1230000.00),
(N'3079-0A02610', N'A', N'26', 26, N'1 BEDROOM PLUS', 46.00, 3532000.00, 79000.00, 3634000.00, 2540000.00, 1380000.00),
(N'3079-0A02611', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.00, 3403000.00, 79000.00, 3160000.00, 2210000.00, 1200000.00),
(N'3079-0A02612', N'A', N'26', 26, N'2 BEDROOMS', 60.50, 5184000.00, 80000.00, 4840000.00, 3390000.00, 1820000.00),
(N'3079-0A02614', N'A', N'26', 26, N'2 BEDROOMS', 51.50, 4929000.00, 82000.00, 4223000.00, 2960000.00, 1550000.00),
(N'3079-0A02615', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3319000.00, 79000.00, 3200000.00, 2240000.00, 1220000.00),
(N'3079-0A02616', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3731000.00, 79000.00, 3200000.00, 2240000.00, 1220000.00),
(N'3079-0A02617', N'A', N'26', 26, N'2 BEDROOMS', 56.00, 4960000.00, 82000.00, 4592000.00, 3210000.00, 1680000.00),
(N'3079-0A02618', N'A', N'26', 26, N'STUDIO', 30.00, 2711000.00, 81000.00, 2430000.00, 1700000.00, 900000.00),
(N'3079-0A02619', N'A', N'26', 26, N'2 BEDROOMS', 64.00, 5292000.00, 79000.00, 5056000.00, 3540000.00, 1920000.00),
(N'3079-0A02621', N'A', N'26', 26, N'STUDIO', 30.00, 2764000.00, 81000.00, 2430000.00, 1700000.00, 900000.00),
(N'3079-0A02622', N'A', N'26', 26, N'1 BEDROOM', 34.00, 3132000.00, 79000.00, 2686000.00, 1880000.00, 1020000.00),
(N'3079-0A02623', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3334000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02624', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3334000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02625', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3208000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02626', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3270000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02627', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3270000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02628', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3334000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02629', N'A', N'26', 26, N'1 BEDROOM', 35.50, 3334000.00, 79000.00, 2805000.00, 1960000.00, 1070000.00),
(N'3079-0A02630', N'A', N'26', 26, N'1 BEDROOM', 34.00, 3132000.00, 79000.00, 2686000.00, 1880000.00, 1020000.00),
(N'3079-0A02631', N'A', N'26', 26, N'STUDIO', 30.00, 2764000.00, 81000.00, 2430000.00, 1700000.00, 900000.00),
(N'3079-0A02632', N'A', N'26', 26, N'2 BEDROOMS', 64.00, 5895000.00, 79000.00, 5056000.00, 3540000.00, 1920000.00),
(N'3079-0A02634', N'A', N'26', 26, N'STUDIO', 30.00, 2711000.00, 81000.00, 2430000.00, 1700000.00, 900000.00),
(N'3079-0A02635', N'A', N'26', 26, N'2 BEDROOMS', 56.00, 4480000.00, 82000.00, 4592000.00, 3210000.00, 1680000.00),
(N'3079-0A02636', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3803000.00, 79000.00, 3200000.00, 2240000.00, 1220000.00),
(N'3079-0A02637', N'A', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3804000.00, 79000.00, 3200000.00, 2240000.00, 1220000.00),
(N'3079-0A02638', N'A', N'26', 26, N'2 BEDROOMS', 51.50, 4956000.00, 82000.00, 4223000.00, 2960000.00, 1550000.00),
(N'3079-0A02701', N'A', N'27', 27, N'2 BEDROOMS', 60.50, 6196000.00, 80400.00, 4864000.00, 3400000.00, 1820000.00),
(N'3079-0A02702', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.00, 3562000.00, 79400.00, 3176000.00, 2220000.00, 1200000.00),
(N'3079-0A02703', N'A', N'27', 27, N'1 BEDROOM PLUS', 46.00, 4260000.00, 79400.00, 3652000.00, 2560000.00, 1380000.00),
(N'3079-0A02704', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3724000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02705', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3653000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02706', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3653000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02707', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3579000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02708', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3653000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02709', N'A', N'27', 27, N'1 BEDROOM PLUS', 41.00, 3653000.00, 79400.00, 3255000.00, 2280000.00, 1230000.00),
(N'3079-0A02710', N'A', N'27', 27, N'1 BEDROOM PLUS', 46.00, 4179000.00, 79400.00, 3652000.00, 2560000.00, 1380000.00),
(N'3079-0A02711', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.00, 3116000.00, 79400.00, 3176000.00, 2220000.00, 1200000.00),
(N'3079-0A02712', N'A', N'27', 27, N'2 BEDROOMS', 60.50, 5390000.00, 80400.00, 4864000.00, 3400000.00, 1820000.00),
(N'3079-0A02714', N'A', N'27', 27, N'2 BEDROOMS', 51.50, 4952000.00, 82400.00, 4244000.00, 2970000.00, 1550000.00),
(N'3079-0A02715', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.50, 3679000.00, 79400.00, 3216000.00, 2250000.00, 1220000.00),
(N'3079-0A02716', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.50, 3823000.00, 79400.00, 3216000.00, 2250000.00, 1220000.00),
(N'3079-0A02717', N'A', N'27', 27, N'2 BEDROOMS', 56.00, 4987000.00, 82400.00, 4614000.00, 3230000.00, 1680000.00),
(N'3079-0A02718', N'A', N'27', 27, N'STUDIO', 30.00, 2725000.00, 81400.00, 2442000.00, 1710000.00, 900000.00),
(N'3079-0A02719', N'A', N'27', 27, N'2 BEDROOMS', 64.00, 6153000.00, 79400.00, 5082000.00, 3560000.00, 1920000.00),
(N'3079-0A02721', N'A', N'27', 27, N'STUDIO', 30.00, 2778000.00, 81400.00, 2442000.00, 1710000.00, 900000.00),
(N'3079-0A02722', N'A', N'27', 27, N'1 BEDROOM', 34.00, 3148000.00, 79400.00, 2700000.00, 1890000.00, 1020000.00),
(N'3079-0A02723', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3350000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02724', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3350000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02725', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3224000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02726', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3289000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02727', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3289000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02728', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3350000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02729', N'A', N'27', 27, N'1 BEDROOM', 35.50, 3350000.00, 79400.00, 2819000.00, 1970000.00, 1070000.00),
(N'3079-0A02730', N'A', N'27', 27, N'1 BEDROOM', 34.00, 3148000.00, 79400.00, 2700000.00, 1890000.00, 1020000.00),
(N'3079-0A02731', N'A', N'27', 27, N'STUDIO', 30.00, 2778000.00, 81400.00, 2442000.00, 1710000.00, 900000.00),
(N'3079-0A02732', N'A', N'27', 27, N'2 BEDROOMS', 64.00, 6039000.00, 79400.00, 5082000.00, 3560000.00, 1920000.00),
(N'3079-0A02734', N'A', N'27', 27, N'STUDIO', 30.00, 2725000.00, 81400.00, 2442000.00, 1710000.00, 900000.00),
(N'3079-0A02735', N'A', N'27', 27, N'2 BEDROOMS', 56.00, 4987000.00, 82400.00, 4614000.00, 3230000.00, 1680000.00),
(N'3079-0A02736', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.50, 3823000.00, 79400.00, 3216000.00, 2250000.00, 1220000.00),
(N'3079-0A02737', N'A', N'27', 27, N'1 BEDROOM PLUS', 40.50, 3823000.00, 79400.00, 3216000.00, 2250000.00, 1220000.00),
(N'3079-0A02738', N'A', N'27', 27, N'2 BEDROOMS', 51.50, 5133000.00, 82400.00, 4244000.00, 2970000.00, 1550000.00),
(N'3079-0A02801', N'A', N'28', 28, N'2 BEDROOMS', 60.50, 5233000.00, 80800.00, 4888000.00, 3420000.00, 1820000.00),
(N'3079-0A02802', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.00, 3581000.00, 79800.00, 3192000.00, 2230000.00, 1200000.00),
(N'3079-0A02803', N'A', N'28', 28, N'1 BEDROOM PLUS', 46.00, 3569000.00, 79800.00, 3671000.00, 2570000.00, 1380000.00),
(N'3079-0A02804', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3671000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02805', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3328000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02806', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3671000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02807', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3598000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02808', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3671000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02809', N'A', N'28', 28, N'1 BEDROOM PLUS', 41.00, 3671000.00, 79800.00, 3272000.00, 2290000.00, 1230000.00),
(N'3079-0A02810', N'A', N'28', 28, N'1 BEDROOM PLUS', 46.00, 3569000.00, 79800.00, 3671000.00, 2570000.00, 1380000.00),
(N'3079-0A02811', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.00, 3440000.00, 79800.00, 3192000.00, 2230000.00, 1200000.00),
(N'3079-0A02812', N'A', N'28', 28, N'2 BEDROOMS', 60.50, 5233000.00, 80800.00, 4888000.00, 3420000.00, 1820000.00),
(N'3079-0A02814', N'A', N'28', 28, N'2 BEDROOMS', 51.50, 4536000.00, 82800.00, 4264000.00, 2980000.00, 1550000.00),
(N'3079-0A02815', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.50, 3351000.00, 79800.00, 3232000.00, 2260000.00, 1220000.00),
(N'3079-0A02816', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.50, 3842000.00, 79800.00, 3232000.00, 2260000.00, 1220000.00),
(N'3079-0A02817', N'A', N'28', 28, N'2 BEDROOMS', 56.00, 5012000.00, 82800.00, 4637000.00, 3250000.00, 1680000.00),
(N'3079-0A02818', N'A', N'28', 28, N'STUDIO', 30.00, 2740000.00, 81800.00, 2454000.00, 1720000.00, 900000.00),
(N'3079-0A02819', N'A', N'28', 28, N'2 BEDROOMS', 64.00, 5956000.00, 79800.00, 5107000.00, 3570000.00, 1920000.00),
(N'3079-0A02821', N'A', N'28', 28, N'STUDIO', 30.00, 2792000.00, 81800.00, 2454000.00, 1720000.00, 900000.00),
(N'3079-0A02822', N'A', N'28', 28, N'1 BEDROOM', 34.00, 3166000.00, 79800.00, 2713000.00, 1900000.00, 1020000.00),
(N'3079-0A02823', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3367000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02824', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3367000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02825', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3242000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02826', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3305000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02827', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3305000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02828', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3367000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02829', N'A', N'28', 28, N'1 BEDROOM', 35.50, 3367000.00, 79800.00, 2833000.00, 1980000.00, 1070000.00),
(N'3079-0A02830', N'A', N'28', 28, N'1 BEDROOM', 34.00, 3166000.00, 79800.00, 2713000.00, 1900000.00, 1020000.00),
(N'3079-0A02831', N'A', N'28', 28, N'STUDIO', 30.00, 2792000.00, 81800.00, 2454000.00, 1720000.00, 900000.00),
(N'3079-0A02832', N'A', N'28', 28, N'2 BEDROOMS', 64.00, 5344000.00, 79800.00, 5107000.00, 3570000.00, 1920000.00),
(N'3079-0A02834', N'A', N'28', 28, N'STUDIO', 30.00, 2740000.00, 81800.00, 2454000.00, 1720000.00, 900000.00),
(N'3079-0A02835', N'A', N'28', 28, N'2 BEDROOMS', 56.00, 4525000.00, 82800.00, 4637000.00, 3250000.00, 1680000.00),
(N'3079-0A02836', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.50, 3842000.00, 79800.00, 3232000.00, 2260000.00, 1220000.00),
(N'3079-0A02837', N'A', N'28', 28, N'1 BEDROOM PLUS', 40.50, 3351000.00, 79800.00, 3232000.00, 2260000.00, 1220000.00),
(N'3079-0A02838', N'A', N'28', 28, N'2 BEDROOMS', 51.50, 5159000.00, 82800.00, 4264000.00, 2980000.00, 1550000.00),
(N'3079-0A02901', N'A', N'29', 29, N'2 BEDROOMS', 60.50, 5439000.00, 81200.00, 4913000.00, 3440000.00, 1820000.00),
(N'3079-0A02902', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.00, 3208000.00, 80200.00, 3208000.00, 2250000.00, 1200000.00),
(N'3079-0A02903', N'A', N'29', 29, N'1 BEDROOM PLUS', 46.00, 4141000.00, 80200.00, 3689000.00, 2580000.00, 1380000.00),
(N'3079-0A02904', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3765000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02905', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3691000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02906', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3691000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02907', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3617000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02908', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3691000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02909', N'A', N'29', 29, N'1 BEDROOM PLUS', 41.00, 3691000.00, 80200.00, 3288000.00, 2300000.00, 1230000.00),
(N'3079-0A02910', N'A', N'29', 29, N'1 BEDROOM PLUS', 46.00, 3979000.00, 80200.00, 3689000.00, 2580000.00, 1380000.00),
(N'3079-0A02911', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.00, 3458000.00, 80200.00, 3208000.00, 2250000.00, 1200000.00),
(N'3079-0A02912', N'A', N'29', 29, N'2 BEDROOMS', 60.50, 5439000.00, 81200.00, 4913000.00, 3440000.00, 1820000.00),
(N'3079-0A02914', N'A', N'29', 29, N'2 BEDROOMS', 51.50, 5000000.00, 83200.00, 4285000.00, 3000000.00, 1550000.00),
(N'3079-0A02915', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.50, 3717000.00, 80200.00, 3248000.00, 2270000.00, 1220000.00),
(N'3079-0A02916', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.50, 3789000.00, 80200.00, 3248000.00, 2270000.00, 1220000.00),
(N'3079-0A02917', N'A', N'29', 29, N'2 BEDROOMS', 56.00, 5041000.00, 83200.00, 4659000.00, 3260000.00, 1680000.00),
(N'3079-0A02918', N'A', N'29', 29, N'STUDIO', 30.00, 2754000.00, 82200.00, 2466000.00, 1730000.00, 900000.00),
(N'3079-0A02919', N'A', N'29', 29, N'2 BEDROOMS', 64.00, 5986000.00, 80200.00, 5133000.00, 3590000.00, 1920000.00),
(N'3079-0A02921', N'A', N'29', 29, N'STUDIO', 30.00, 2806000.00, 82200.00, 2466000.00, 1730000.00, 900000.00),
(N'3079-0A02922', N'A', N'29', 29, N'1 BEDROOM', 34.00, 3181000.00, 80200.00, 2727000.00, 1910000.00, 1020000.00),
(N'3079-0A02923', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3383000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02924', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3383000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02925', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3258000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02926', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3321000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02927', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3321000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02928', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3383000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02929', N'A', N'29', 29, N'1 BEDROOM', 35.50, 3383000.00, 80200.00, 2847000.00, 1990000.00, 1070000.00),
(N'3079-0A02930', N'A', N'29', 29, N'1 BEDROOM', 34.00, 3181000.00, 80200.00, 2727000.00, 1910000.00, 1020000.00),
(N'3079-0A02931', N'A', N'29', 29, N'STUDIO', 30.00, 2806000.00, 82200.00, 2466000.00, 1730000.00, 900000.00),
(N'3079-0A02932', N'A', N'29', 29, N'2 BEDROOMS', 64.00, 5986000.00, 80200.00, 5133000.00, 3590000.00, 1920000.00),
(N'3079-0A02934', N'A', N'29', 29, N'STUDIO', 30.00, 2754000.00, 82200.00, 2466000.00, 1730000.00, 900000.00),
(N'3079-0A02935', N'A', N'29', 29, N'2 BEDROOMS', 56.00, 5041000.00, 83200.00, 4659000.00, 3260000.00, 1680000.00),
(N'3079-0A02936', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.50, 3861000.00, 80200.00, 3248000.00, 2270000.00, 1220000.00),
(N'3079-0A02937', N'A', N'29', 29, N'1 BEDROOM PLUS', 40.50, 3861000.00, 80200.00, 3248000.00, 2270000.00, 1220000.00),
(N'3079-0A02938', N'A', N'29', 29, N'2 BEDROOMS', 51.50, 5029000.00, 83200.00, 4285000.00, 3000000.00, 1550000.00),
(N'3079-0A03001', N'A', N'30', 30, N'3 BEDROOMS', 100.00, 8275000.00, 74000.00, 7400000.00, 5180000.00, 3000000.00),
(N'3079-0A03002', N'A', N'30', 30, N'1 BEDROOM PLUS', 46.00, 3675000.00, 80600.00, 3708000.00, 2600000.00, 1380000.00),
(N'3079-0A03003', N'A', N'30', 30, N'2 BEDROOMS', 66.50, 5394000.00, 80600.00, 5360000.00, 3750000.00, 2000000.00),
(N'3079-0A03004', N'A', N'30', 30, N'2 BEDROOMS', 55.50, 4532000.00, 80600.00, 4473000.00, 3130000.00, 1670000.00),
(N'3079-0A03005', N'A', N'30', 30, N'2 BEDROOMS', 74.50, 6007000.00, 80600.00, 6005000.00, 4200000.00, 2240000.00),
(N'3079-0A03006', N'A', N'30', 30, N'2 BEDROOMS', 64.00, 5299000.00, 80600.00, 5158000.00, 3610000.00, 1920000.00),
(N'3079-0A03007', N'A', N'30', 30, N'2 BEDROOMS', 64.00, 5299000.00, 80600.00, 5158000.00, 3610000.00, 1920000.00),
(N'3079-0A03008', N'A', N'30', 30, N'3 BEDROOMS', 85.50, 6955000.00, 76000.00, 6498000.00, 4550000.00, 2570000.00),
(N'3079-0A03009', N'A', N'30', 30, N'3 BEDROOMS', 80.50, 6708000.00, 73000.00, 5877000.00, 4110000.00, 2420000.00),
(N'3079-0A03010', N'A', N'30', 30, N'2 BEDROOMS', 52.50, 5215000.00, 83600.00, 4389000.00, 3070000.00, 1580000.00),
(N'3079-0B00101', N'B', N'1', 1, N'SHOP', 38.00, 2690000.00, 0.00, 0.00, 0.00, 1140000.00),
(N'3079-0B00102', N'B', N'1', 1, N'SHOP', 35.00, 2390000.00, 0.00, 0.00, 0.00, 1050000.00),
(N'3079-0B00501', N'B', N'5', 5, N'2 BEDROOMS', 55.50, 4176000.00, 66000.00, 3663000.00, 2560000.00, 1670000.00),
(N'3079-0B00502', N'B', N'5', 5, N'1 BEDROOM PLUS', 42.00, 3007000.00, 66000.00, 2772000.00, 1940000.00, 1260000.00),
(N'3079-0B00503', N'B', N'5', 5, N'1 BEDROOM', 34.50, 2349000.00, 66000.00, 2277000.00, 1590000.00, 1040000.00),
(N'3079-0B00504', N'B', N'5', 5, N'1 BEDROOM', 34.00, 2261000.00, 66000.00, 2244000.00, 1570000.00, 1020000.00),
(N'3079-0B00505', N'B', N'5', 5, N'1 BEDROOM', 35.00, 2269000.00, 66000.00, 2310000.00, 1620000.00, 1050000.00),
(N'3079-0B00508', N'B', N'5', 5, N'1 BEDROOM', 35.00, 2269000.00, 66000.00, 2310000.00, 1620000.00, 1050000.00),
(N'3079-0B00509', N'B', N'5', 5, N'1 BEDROOM', 34.00, 2261000.00, 66000.00, 2244000.00, 1570000.00, 1020000.00),
(N'3079-0B00510', N'B', N'5', 5, N'1 BEDROOM', 34.50, 2188000.00, 66000.00, 2277000.00, 1590000.00, 1040000.00),
(N'3079-0B00511', N'B', N'5', 5, N'1 BEDROOM PLUS', 42.00, 2766000.00, 66000.00, 2772000.00, 1940000.00, 1260000.00),
(N'3079-0B00512', N'B', N'5', 5, N'2 BEDROOMS', 55.50, 4170000.00, 66000.00, 3663000.00, 2560000.00, 1670000.00),
(N'3079-0B00514', N'B', N'5', 5, N'2 BEDROOMS', 50.00, 3460000.00, 68000.00, 3400000.00, 2380000.00, 1500000.00),
(N'3079-0B00515', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2972000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00516', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2675000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00517', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2972000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00518', N'B', N'5', 5, N'2 BEDROOMS', 46.00, 3344000.00, 68000.00, 3128000.00, 2190000.00, 1380000.00),
(N'3079-0B00519', N'B', N'5', 5, N'1 BEDROOM PLUS', 45.50, 3257000.00, 66000.00, 3003000.00, 2100000.00, 1370000.00),
(N'3079-0B00520', N'B', N'5', 5, N'STUDIO', 30.00, 2003000.00, 68000.00, 2040000.00, 1430000.00, 900000.00),
(N'3079-0B00521', N'B', N'5', 5, N'1 BEDROOM', 34.00, 2312000.00, 66000.00, 2244000.00, 1570000.00, 1020000.00),
(N'3079-0B00528', N'B', N'5', 5, N'1 BEDROOM', 34.00, 2312000.00, 66000.00, 2244000.00, 1570000.00, 1020000.00),
(N'3079-0B00529', N'B', N'5', 5, N'STUDIO', 30.00, 2030000.00, 68000.00, 2040000.00, 1430000.00, 900000.00),
(N'3079-0B00530', N'B', N'5', 5, N'1 BEDROOM PLUS', 45.50, 3257000.00, 66000.00, 3003000.00, 2100000.00, 1370000.00),
(N'3079-0B00531', N'B', N'5', 5, N'2 BEDROOMS', 46.00, 3344000.00, 68000.00, 3128000.00, 2190000.00, 1380000.00),
(N'3079-0B00532', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2736000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00533', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2675000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00534', N'B', N'5', 5, N'1 BEDROOM PLUS', 40.50, 2736000.00, 66000.00, 2673000.00, 1870000.00, 1220000.00),
(N'3079-0B00535', N'B', N'5', 5, N'2 BEDROOMS', 50.00, 3565000.00, 68000.00, 3400000.00, 2380000.00, 1500000.00),
(N'3079-0B00601', N'B', N'6', 6, N'2 BEDROOMS', 55.50, 4301000.00, 66400.00, 3685000.00, 2580000.00, 1670000.00),
(N'3079-0B00602', N'B', N'6', 6, N'1 BEDROOM PLUS', 42.00, 3175000.00, 66400.00, 2789000.00, 1950000.00, 1260000.00),
(N'3079-0B00603', N'B', N'6', 6, N'1 BEDROOM', 34.50, 2487000.00, 66400.00, 2291000.00, 1600000.00, 1040000.00),
(N'3079-0B00604', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2570000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00605', N'B', N'6', 6, N'1 BEDROOM', 35.00, 2646000.00, 66400.00, 2324000.00, 1630000.00, 1050000.00),
(N'3079-0B00606', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2510000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00607', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2510000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00608', N'B', N'6', 6, N'1 BEDROOM', 35.00, 2646000.00, 66400.00, 2324000.00, 1630000.00, 1050000.00),
(N'3079-0B00609', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2570000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00610', N'B', N'6', 6, N'1 BEDROOM', 34.50, 2425000.00, 66400.00, 2291000.00, 1600000.00, 1040000.00),
(N'3079-0B00611', N'B', N'6', 6, N'1 BEDROOM PLUS', 42.00, 3100000.00, 66400.00, 2789000.00, 1950000.00, 1260000.00),
(N'3079-0B00612', N'B', N'6', 6, N'2 BEDROOMS', 55.50, 4346000.00, 66400.00, 3685000.00, 2580000.00, 1670000.00),
(N'3079-0B00614', N'B', N'6', 6, N'2 BEDROOMS', 50.00, 3869000.00, 68400.00, 3420000.00, 2390000.00, 1500000.00),
(N'3079-0B00615', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3279000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00616', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3064000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00617', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3062000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00618', N'B', N'6', 6, N'2 BEDROOMS', 46.00, 4211000.00, 68400.00, 3146000.00, 2200000.00, 1380000.00),
(N'3079-0B00619', N'B', N'6', 6, N'1 BEDROOM PLUS', 45.50, 3359000.00, 66400.00, 3021000.00, 2110000.00, 1370000.00),
(N'3079-0B00620', N'B', N'6', 6, N'STUDIO', 30.00, 2269000.00, 68400.00, 2052000.00, 1440000.00, 900000.00),
(N'3079-0B00621', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2570000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00622', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2570000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00623', N'B', N'6', 6, N'STUDIO', 30.00, 2269000.00, 68400.00, 2052000.00, 1440000.00, 900000.00),
(N'3079-0B00624', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2510000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00625', N'B', N'6', 6, N'STUDIO', 30.00, 2285000.00, 68400.00, 2052000.00, 1440000.00, 900000.00),
(N'3079-0B00626', N'B', N'6', 6, N'STUDIO', 30.00, 2269000.00, 68400.00, 2052000.00, 1440000.00, 900000.00),
(N'3079-0B00627', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2570000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00628', N'B', N'6', 6, N'1 BEDROOM', 34.00, 2631000.00, 66400.00, 2258000.00, 1580000.00, 1020000.00),
(N'3079-0B00629', N'B', N'6', 6, N'STUDIO', 30.00, 2269000.00, 68400.00, 2052000.00, 1440000.00, 900000.00),
(N'3079-0B00630', N'B', N'6', 6, N'1 BEDROOM PLUS', 45.50, 3359000.00, 66400.00, 3021000.00, 2110000.00, 1370000.00),
(N'3079-0B00631', N'B', N'6', 6, N'2 BEDROOMS', 46.00, 4041000.00, 68400.00, 3146000.00, 2200000.00, 1380000.00),
(N'3079-0B00632', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3062000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00633', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3134000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00634', N'B', N'6', 6, N'1 BEDROOM PLUS', 40.50, 3133000.00, 66400.00, 2689000.00, 1880000.00, 1220000.00),
(N'3079-0B00635', N'B', N'6', 6, N'2 BEDROOMS', 50.00, 3974000.00, 68400.00, 3420000.00, 2390000.00, 1500000.00),
(N'3079-0B00701', N'B', N'7', 7, N'2 BEDROOMS', 55.50, 4228000.00, 66800.00, 3707000.00, 2590000.00, 1670000.00),
(N'3079-0B00702', N'B', N'7', 7, N'1 BEDROOM PLUS', 42.00, 2799000.00, 66800.00, 2806000.00, 1960000.00, 1260000.00),
(N'3079-0B00703', N'B', N'7', 7, N'1 BEDROOM', 34.50, 2215000.00, 66800.00, 2305000.00, 1610000.00, 1040000.00),
(N'3079-0B00704', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2288000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00705', N'B', N'7', 7, N'1 BEDROOM', 35.00, 2539000.00, 66800.00, 2338000.00, 1640000.00, 1050000.00),
(N'3079-0B00706', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2237000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00707', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2237000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00708', N'B', N'7', 7, N'1 BEDROOM', 35.00, 2539000.00, 66800.00, 2338000.00, 1640000.00, 1050000.00),
(N'3079-0B00709', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2288000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00710', N'B', N'7', 7, N'1 BEDROOM', 34.50, 2215000.00, 66800.00, 2305000.00, 1610000.00, 1040000.00),
(N'3079-0B00711', N'B', N'7', 7, N'1 BEDROOM PLUS', 42.00, 3047000.00, 66800.00, 2806000.00, 1960000.00, 1260000.00),
(N'3079-0B00712', N'B', N'7', 7, N'2 BEDROOMS', 55.50, 3921000.00, 66800.00, 3707000.00, 2590000.00, 1670000.00),
(N'3079-0B00714', N'B', N'7', 7, N'2 BEDROOMS', 50.00, 3575000.00, 68800.00, 3440000.00, 2410000.00, 1500000.00),
(N'3079-0B00715', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 2829000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00716', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 2768000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00717', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3082000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00718', N'B', N'7', 7, N'2 BEDROOMS', 46.00, 3989000.00, 68800.00, 3165000.00, 2220000.00, 1380000.00),
(N'3079-0B00719', N'B', N'7', 7, N'1 BEDROOM PLUS', 45.50, 3380000.00, 66800.00, 3039000.00, 2130000.00, 1370000.00),
(N'3079-0B00720', N'B', N'7', 7, N'STUDIO', 30.00, 2072000.00, 68800.00, 2064000.00, 1440000.00, 900000.00),
(N'3079-0B00721', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2587000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00722', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2587000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00723', N'B', N'7', 7, N'STUDIO', 30.00, 2282000.00, 68800.00, 2064000.00, 1440000.00, 900000.00),
(N'3079-0B00724', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2527000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00725', N'B', N'7', 7, N'STUDIO', 30.00, 2030000.00, 68800.00, 2064000.00, 1440000.00, 900000.00),
(N'3079-0B00726', N'B', N'7', 7, N'STUDIO', 30.00, 2282000.00, 68800.00, 2064000.00, 1440000.00, 900000.00),
(N'3079-0B00727', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2587000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00728', N'B', N'7', 7, N'1 BEDROOM', 34.00, 2587000.00, 66800.00, 2271000.00, 1590000.00, 1020000.00),
(N'3079-0B00729', N'B', N'7', 7, N'STUDIO', 30.00, 2072000.00, 68800.00, 2064000.00, 1440000.00, 900000.00),
(N'3079-0B00730', N'B', N'7', 7, N'1 BEDROOM PLUS', 45.50, 3380000.00, 66800.00, 3039000.00, 2130000.00, 1370000.00),
(N'3079-0B00731', N'B', N'7', 7, N'2 BEDROOMS', 46.00, 3525000.00, 68800.00, 3165000.00, 2220000.00, 1380000.00),
(N'3079-0B00732', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 3082000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00733', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 2768000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00734', N'B', N'7', 7, N'1 BEDROOM PLUS', 40.50, 2829000.00, 66800.00, 2705000.00, 1890000.00, 1220000.00),
(N'3079-0B00735', N'B', N'7', 7, N'2 BEDROOMS', 50.00, 3997000.00, 68800.00, 3440000.00, 2410000.00, 1500000.00),
(N'3079-0B00801', N'B', N'8', 8, N'2 BEDROOMS', 55.50, 4255000.00, 67200.00, 3730000.00, 2610000.00, 1670000.00),
(N'3079-0B00802', N'B', N'8', 8, N'1 BEDROOM PLUS', 42.00, 2816000.00, 67200.00, 2822000.00, 1980000.00, 1260000.00),
(N'3079-0B00803', N'B', N'8', 8, N'1 BEDROOM', 34.50, 2397000.00, 67200.00, 2318000.00, 1620000.00, 1040000.00),
(N'3079-0B00804', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2251000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00805', N'B', N'8', 8, N'1 BEDROOM', 35.00, 2363000.00, 67200.00, 2352000.00, 1650000.00, 1050000.00),
(N'3079-0B00806', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2422000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00807', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2251000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00808', N'B', N'8', 8, N'1 BEDROOM', 35.00, 2555000.00, 67200.00, 2352000.00, 1650000.00, 1050000.00),
(N'3079-0B00809', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2302000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00810', N'B', N'8', 8, N'1 BEDROOM', 34.50, 2229000.00, 67200.00, 2318000.00, 1620000.00, 1040000.00),
(N'3079-0B00811', N'B', N'8', 8, N'1 BEDROOM PLUS', 42.00, 2816000.00, 67200.00, 2822000.00, 1980000.00, 1260000.00),
(N'3079-0B00812', N'B', N'8', 8, N'2 BEDROOMS', 55.50, 4300000.00, 67200.00, 3730000.00, 2610000.00, 1670000.00),
(N'3079-0B00814', N'B', N'8', 8, N'2 BEDROOMS', 50.00, 3916000.00, 69200.00, 3460000.00, 2420000.00, 1500000.00),
(N'3079-0B00815', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 2845000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00816', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3028000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00817', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3100000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00818', N'B', N'8', 8, N'2 BEDROOMS', 46.00, 3468000.00, 69200.00, 3183000.00, 2230000.00, 1380000.00),
(N'3079-0B00819', N'B', N'8', 8, N'1 BEDROOM PLUS', 45.50, 3404000.00, 67200.00, 3058000.00, 2140000.00, 1370000.00),
(N'3079-0B00820', N'B', N'8', 8, N'STUDIO', 30.00, 2296000.00, 69200.00, 2076000.00, 1450000.00, 900000.00),
(N'3079-0B00821', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2604000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00822', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2604000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00823', N'B', N'8', 8, N'STUDIO', 30.00, 2296000.00, 69200.00, 2076000.00, 1450000.00, 900000.00),
(N'3079-0B00824', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2543000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00825', N'B', N'8', 8, N'STUDIO', 30.00, 2244000.00, 69200.00, 2076000.00, 1450000.00, 900000.00),
(N'3079-0B00826', N'B', N'8', 8, N'STUDIO', 30.00, 2296000.00, 69200.00, 2076000.00, 1450000.00, 900000.00),
(N'3079-0B00827', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2604000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00828', N'B', N'8', 8, N'1 BEDROOM', 34.00, 2404000.00, 67200.00, 2285000.00, 1600000.00, 1020000.00),
(N'3079-0B00829', N'B', N'8', 8, N'STUDIO', 30.00, 2296000.00, 69200.00, 2076000.00, 1450000.00, 900000.00),
(N'3079-0B00830', N'B', N'8', 8, N'1 BEDROOM PLUS', 45.50, 3404000.00, 67200.00, 3058000.00, 2140000.00, 1370000.00),
(N'3079-0B00831', N'B', N'8', 8, N'2 BEDROOMS', 46.00, 3543000.00, 69200.00, 3183000.00, 2230000.00, 1380000.00),
(N'3079-0B00832', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 3100000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00833', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 2784000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00834', N'B', N'8', 8, N'1 BEDROOM PLUS', 40.50, 2845000.00, 67200.00, 2722000.00, 1910000.00, 1220000.00),
(N'3079-0B00835', N'B', N'8', 8, N'2 BEDROOMS', 50.00, 3916000.00, 69200.00, 3460000.00, 2420000.00, 1500000.00),
(N'3079-0B00901', N'B', N'9', 9, N'2 BEDROOMS', 55.50, 4280000.00, 67600.00, 3752000.00, 2630000.00, 1670000.00),
(N'3079-0B00902', N'B', N'9', 9, N'1 BEDROOM PLUS', 42.00, 2833000.00, 67600.00, 2839000.00, 1990000.00, 1260000.00),
(N'3079-0B00903', N'B', N'9', 9, N'1 BEDROOM', 34.50, 2243000.00, 67600.00, 2332000.00, 1630000.00, 1040000.00),
(N'3079-0B00904', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2315000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00905', N'B', N'9', 9, N'1 BEDROOM', 35.00, 2377000.00, 67600.00, 2366000.00, 1660000.00, 1050000.00),
(N'3079-0B00906', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2264000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00);

INSERT #Src (RoomNumber, TowerName, FloorText, Floor, ModelType, UsableArea,
            SellingPrice, PricePerSqm, AppraisalValue, ForceSellingPrice, CoverageAmount)
VALUES
(N'3079-0B00907', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2437000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00908', N'B', N'9', 9, N'1 BEDROOM', 35.00, 2571000.00, 67600.00, 2366000.00, 1660000.00, 1050000.00),
(N'3079-0B00909', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2315000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00910', N'B', N'9', 9, N'1 BEDROOM', 34.50, 2243000.00, 67600.00, 2332000.00, 1630000.00, 1040000.00),
(N'3079-0B00911', N'B', N'9', 9, N'1 BEDROOM PLUS', 42.00, 2833000.00, 67600.00, 2839000.00, 1990000.00, 1260000.00),
(N'3079-0B00912', N'B', N'9', 9, N'2 BEDROOMS', 55.50, 4425000.00, 67600.00, 3752000.00, 2630000.00, 1670000.00),
(N'3079-0B00914', N'B', N'9', 9, N'2 BEDROOMS', 50.00, 3615000.00, 69600.00, 3480000.00, 2440000.00, 1500000.00),
(N'3079-0B00915', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 2861000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00916', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 2800000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00917', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 2861000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00918', N'B', N'9', 9, N'2 BEDROOMS', 46.00, 3487000.00, 69600.00, 3202000.00, 2240000.00, 1380000.00),
(N'3079-0B00919', N'B', N'9', 9, N'1 BEDROOM PLUS', 45.50, 3424000.00, 67600.00, 3076000.00, 2150000.00, 1370000.00),
(N'3079-0B00920', N'B', N'9', 9, N'STUDIO', 30.00, 2096000.00, 69600.00, 2088000.00, 1460000.00, 900000.00),
(N'3079-0B00921', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2619000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00922', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2417000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00923', N'B', N'9', 9, N'STUDIO', 30.00, 2310000.00, 69600.00, 2088000.00, 1460000.00, 900000.00),
(N'3079-0B00924', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2558000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00925', N'B', N'9', 9, N'STUDIO', 30.00, 2258000.00, 69600.00, 2088000.00, 1460000.00, 900000.00),
(N'3079-0B00926', N'B', N'9', 9, N'STUDIO', 30.00, 2310000.00, 69600.00, 2088000.00, 1460000.00, 900000.00),
(N'3079-0B00927', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2417000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00928', N'B', N'9', 9, N'1 BEDROOM', 34.00, 2417000.00, 67600.00, 2298000.00, 1610000.00, 1020000.00),
(N'3079-0B00929', N'B', N'9', 9, N'STUDIO', 30.00, 2096000.00, 69600.00, 2088000.00, 1460000.00, 900000.00),
(N'3079-0B00930', N'B', N'9', 9, N'1 BEDROOM PLUS', 45.50, 3119000.00, 67600.00, 3076000.00, 2150000.00, 1370000.00),
(N'3079-0B00931', N'B', N'9', 9, N'2 BEDROOMS', 46.00, 3562000.00, 69600.00, 3202000.00, 2240000.00, 1380000.00),
(N'3079-0B00932', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 3119000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00933', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 2800000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00934', N'B', N'9', 9, N'1 BEDROOM PLUS', 40.50, 2861000.00, 67600.00, 2738000.00, 1920000.00, 1220000.00),
(N'3079-0B00935', N'B', N'9', 9, N'2 BEDROOMS', 50.00, 3720000.00, 69600.00, 3480000.00, 2440000.00, 1500000.00),
(N'3079-0B01001', N'B', N'10', 10, N'2 BEDROOMS', 55.50, 3943000.00, 68000.00, 3774000.00, 2640000.00, 1670000.00),
(N'3079-0B01002', N'B', N'10', 10, N'1 BEDROOM PLUS', 42.00, 2850000.00, 68000.00, 2856000.00, 2000000.00, 1260000.00),
(N'3079-0B01003', N'B', N'10', 10, N'1 BEDROOM', 34.50, 2257000.00, 68000.00, 2346000.00, 1640000.00, 1040000.00),
(N'3079-0B01004', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2278000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01005', N'B', N'10', 10, N'1 BEDROOM', 35.00, 2391000.00, 68000.00, 2380000.00, 1670000.00, 1050000.00),
(N'3079-0B01006', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2278000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01007', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2454000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01008', N'B', N'10', 10, N'1 BEDROOM', 35.00, 2391000.00, 68000.00, 2380000.00, 1670000.00, 1050000.00),
(N'3079-0B01009', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2635000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01010', N'B', N'10', 10, N'1 BEDROOM', 34.50, 2257000.00, 68000.00, 2346000.00, 1640000.00, 1040000.00),
(N'3079-0B01011', N'B', N'10', 10, N'1 BEDROOM PLUS', 42.00, 3106000.00, 68000.00, 2856000.00, 2000000.00, 1260000.00),
(N'3079-0B01012', N'B', N'10', 10, N'2 BEDROOMS', 55.50, 4300000.00, 68000.00, 3774000.00, 2640000.00, 1670000.00),
(N'3079-0B01014', N'B', N'10', 10, N'2 BEDROOMS', 50.00, 3962000.00, 70000.00, 3500000.00, 2450000.00, 1500000.00),
(N'3079-0B01015', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 2877000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01016', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 2817000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01017', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 3139000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01018', N'B', N'10', 10, N'2 BEDROOMS', 46.00, 3436000.00, 70000.00, 3220000.00, 2250000.00, 1380000.00),
(N'3079-0B01019', N'B', N'10', 10, N'1 BEDROOM PLUS', 45.50, 3445000.00, 68000.00, 3094000.00, 2170000.00, 1370000.00),
(N'3079-0B01020', N'B', N'10', 10, N'STUDIO', 30.00, 2324000.00, 70000.00, 2100000.00, 1470000.00, 900000.00),
(N'3079-0B01021', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2431000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01022', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2431000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01023', N'B', N'10', 10, N'STUDIO', 30.00, 2108000.00, 70000.00, 2100000.00, 1470000.00, 900000.00),
(N'3079-0B01024', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2575000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01025', N'B', N'10', 10, N'STUDIO', 30.00, 2063000.00, 70000.00, 2100000.00, 1470000.00, 900000.00),
(N'3079-0B01026', N'B', N'10', 10, N'STUDIO', 30.00, 2108000.00, 70000.00, 2100000.00, 1470000.00, 900000.00),
(N'3079-0B01027', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2431000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01028', N'B', N'10', 10, N'1 BEDROOM', 34.00, 2431000.00, 68000.00, 2312000.00, 1620000.00, 1020000.00),
(N'3079-0B01029', N'B', N'10', 10, N'STUDIO', 30.00, 2108000.00, 70000.00, 2100000.00, 1470000.00, 900000.00),
(N'3079-0B01030', N'B', N'10', 10, N'1 BEDROOM PLUS', 45.50, 3445000.00, 68000.00, 3094000.00, 2170000.00, 1370000.00),
(N'3079-0B01031', N'B', N'10', 10, N'2 BEDROOMS', 46.00, 3511000.00, 70000.00, 3220000.00, 2250000.00, 1380000.00),
(N'3079-0B01032', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 2877000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01033', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 2817000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01034', N'B', N'10', 10, N'1 BEDROOM PLUS', 40.50, 2877000.00, 68000.00, 2754000.00, 1930000.00, 1220000.00),
(N'3079-0B01035', N'B', N'10', 10, N'2 BEDROOMS', 50.00, 4067000.00, 70000.00, 3500000.00, 2450000.00, 1500000.00),
(N'3079-0B01101', N'B', N'11', 11, N'2 BEDROOMS', 55.50, 4433000.00, 68400.00, 3796000.00, 2660000.00, 1670000.00),
(N'3079-0B01102', N'B', N'11', 11, N'1 BEDROOM PLUS', 42.00, 3125000.00, 68400.00, 2873000.00, 2010000.00, 1260000.00),
(N'3079-0B01103', N'B', N'11', 11, N'1 BEDROOM', 34.50, 2270000.00, 68400.00, 2360000.00, 1650000.00, 1040000.00),
(N'3079-0B01104', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2342000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01105', N'B', N'11', 11, N'1 BEDROOM', 35.00, 2405000.00, 68400.00, 2394000.00, 1680000.00, 1050000.00),
(N'3079-0B01106', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2471000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01107', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2291000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01108', N'B', N'11', 11, N'1 BEDROOM', 35.00, 2405000.00, 68400.00, 2394000.00, 1680000.00, 1050000.00),
(N'3079-0B01109', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2342000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01110', N'B', N'11', 11, N'1 BEDROOM', 34.50, 2445000.00, 68400.00, 2360000.00, 1650000.00, 1040000.00),
(N'3079-0B01111', N'B', N'11', 11, N'1 BEDROOM PLUS', 42.00, 3125000.00, 68400.00, 2873000.00, 2010000.00, 1260000.00),
(N'3079-0B01112', N'B', N'11', 11, N'2 BEDROOMS', 55.50, 4010000.00, 68400.00, 3796000.00, 2660000.00, 1670000.00),
(N'3079-0B01114', N'B', N'11', 11, N'2 BEDROOMS', 50.00, 3986000.00, 70400.00, 3520000.00, 2460000.00, 1500000.00),
(N'3079-0B01115', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3373000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01116', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3159000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01117', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3157000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01118', N'B', N'11', 11, N'2 BEDROOMS', 46.00, 3523000.00, 70400.00, 3238000.00, 2270000.00, 1380000.00),
(N'3079-0B01119', N'B', N'11', 11, N'1 BEDROOM PLUS', 45.50, 3467000.00, 68400.00, 3112000.00, 2180000.00, 1370000.00),
(N'3079-0B01120', N'B', N'11', 11, N'STUDIO', 30.00, 2339000.00, 70400.00, 2112000.00, 1480000.00, 900000.00),
(N'3079-0B01121', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2650000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01122', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2650000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01123', N'B', N'11', 11, N'STUDIO', 30.00, 2339000.00, 70400.00, 2112000.00, 1480000.00, 900000.00),
(N'3079-0B01124', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2591000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01125', N'B', N'11', 11, N'STUDIO', 30.00, 2286000.00, 70400.00, 2112000.00, 1480000.00, 900000.00),
(N'3079-0B01126', N'B', N'11', 11, N'STUDIO', 30.00, 2339000.00, 70400.00, 2112000.00, 1480000.00, 900000.00),
(N'3079-0B01127', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2650000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01128', N'B', N'11', 11, N'1 BEDROOM', 34.00, 2444000.00, 68400.00, 2326000.00, 1630000.00, 1020000.00),
(N'3079-0B01129', N'B', N'11', 11, N'STUDIO', 30.00, 2339000.00, 70400.00, 2112000.00, 1480000.00, 900000.00),
(N'3079-0B01130', N'B', N'11', 11, N'1 BEDROOM PLUS', 45.50, 3467000.00, 68400.00, 3112000.00, 2180000.00, 1370000.00),
(N'3079-0B01131', N'B', N'11', 11, N'2 BEDROOMS', 46.00, 3598000.00, 70400.00, 3238000.00, 2270000.00, 1380000.00),
(N'3079-0B01132', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 3157000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01133', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 2833000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01134', N'B', N'11', 11, N'1 BEDROOM PLUS', 40.50, 2893000.00, 68400.00, 2770000.00, 1940000.00, 1220000.00),
(N'3079-0B01135', N'B', N'11', 11, N'2 BEDROOMS', 50.00, 3760000.00, 70400.00, 3520000.00, 2460000.00, 1500000.00),
(N'3079-0B01201', N'B', N'12', 12, N'2 BEDROOMS', 55.50, 4459000.00, 68800.00, 3818000.00, 2670000.00, 1670000.00),
(N'3079-0B01202', N'B', N'12', 12, N'1 BEDROOM PLUS', 42.00, 3295000.00, 68800.00, 2890000.00, 2020000.00, 1260000.00),
(N'3079-0B01203', N'B', N'12', 12, N'1 BEDROOM', 34.50, 2585000.00, 68800.00, 2374000.00, 1660000.00, 1040000.00),
(N'3079-0B01204', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2667000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01205', N'B', N'12', 12, N'1 BEDROOM', 35.00, 2745000.00, 68800.00, 2408000.00, 1690000.00, 1050000.00),
(N'3079-0B01206', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2607000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01207', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2607000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01208', N'B', N'12', 12, N'1 BEDROOM', 35.00, 2745000.00, 68800.00, 2408000.00, 1690000.00, 1050000.00),
(N'3079-0B01209', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2667000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01210', N'B', N'12', 12, N'1 BEDROOM', 34.50, 2523000.00, 68800.00, 2374000.00, 1660000.00, 1040000.00),
(N'3079-0B01211', N'B', N'12', 12, N'1 BEDROOM PLUS', 42.00, 3220000.00, 68800.00, 2890000.00, 2020000.00, 1260000.00),
(N'3079-0B01212', N'B', N'12', 12, N'2 BEDROOMS', 55.50, 4354000.00, 68800.00, 3818000.00, 2670000.00, 1670000.00),
(N'3079-0B01214', N'B', N'12', 12, N'2 BEDROOMS', 50.00, 4010000.00, 70800.00, 3540000.00, 2480000.00, 1500000.00),
(N'3079-0B01215', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3394000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01216', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3178000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01217', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3178000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01218', N'B', N'12', 12, N'2 BEDROOMS', 46.00, 4247000.00, 70800.00, 3257000.00, 2280000.00, 1380000.00),
(N'3079-0B01219', N'B', N'12', 12, N'1 BEDROOM PLUS', 45.50, 3487000.00, 68800.00, 3130000.00, 2190000.00, 1370000.00),
(N'3079-0B01220', N'B', N'12', 12, N'STUDIO', 30.00, 2354000.00, 70800.00, 2124000.00, 1490000.00, 900000.00),
(N'3079-0B01221', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2667000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01222', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2667000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01223', N'B', N'12', 12, N'STUDIO', 30.00, 2354000.00, 70800.00, 2124000.00, 1490000.00, 900000.00),
(N'3079-0B01224', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2607000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01225', N'B', N'12', 12, N'STUDIO', 30.00, 2300000.00, 70800.00, 2124000.00, 1490000.00, 900000.00),
(N'3079-0B01226', N'B', N'12', 12, N'STUDIO', 30.00, 2354000.00, 70800.00, 2124000.00, 1490000.00, 900000.00),
(N'3079-0B01227', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2667000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01228', N'B', N'12', 12, N'1 BEDROOM', 34.00, 2728000.00, 68800.00, 2339000.00, 1640000.00, 1020000.00),
(N'3079-0B01229', N'B', N'12', 12, N'STUDIO', 30.00, 2354000.00, 70800.00, 2124000.00, 1490000.00, 900000.00),
(N'3079-0B01230', N'B', N'12', 12, N'1 BEDROOM PLUS', 45.50, 3487000.00, 68800.00, 3130000.00, 2190000.00, 1370000.00),
(N'3079-0B01231', N'B', N'12', 12, N'2 BEDROOMS', 46.00, 4417000.00, 70800.00, 3257000.00, 2280000.00, 1380000.00),
(N'3079-0B01232', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3178000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01233', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3249000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01234', N'B', N'12', 12, N'1 BEDROOM PLUS', 40.50, 3249000.00, 68800.00, 2786000.00, 1950000.00, 1220000.00),
(N'3079-0B01235', N'B', N'12', 12, N'2 BEDROOMS', 50.00, 4115000.00, 70800.00, 3540000.00, 2480000.00, 1500000.00),
(N'3079-0B12A01', N'B', N'12A', 13, N'2 BEDROOMS', 55.50, 4486000.00, 69200.00, 3841000.00, 2690000.00, 1670000.00),
(N'3079-0B12A02', N'B', N'12A', 13, N'1 BEDROOM PLUS', 42.00, 2900000.00, 69200.00, 2906000.00, 2030000.00, 1260000.00),
(N'3079-0B12A03', N'B', N'12A', 13, N'1 BEDROOM', 34.50, 2602000.00, 69200.00, 2387000.00, 1670000.00, 1040000.00),
(N'3079-0B12A04', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2370000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A05', N'B', N'12A', 13, N'1 BEDROOM', 35.00, 2761000.00, 69200.00, 2422000.00, 1700000.00, 1050000.00),
(N'3079-0B12A06', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2504000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A07', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2504000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A08', N'B', N'12A', 13, N'1 BEDROOM', 35.00, 2637000.00, 69200.00, 2422000.00, 1700000.00, 1050000.00),
(N'3079-0B12A09', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2370000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A10', N'B', N'12A', 13, N'1 BEDROOM', 34.50, 2479000.00, 69200.00, 2387000.00, 1670000.00, 1040000.00),
(N'3079-0B12A11', N'B', N'12A', 13, N'1 BEDROOM PLUS', 42.00, 3166000.00, 69200.00, 2906000.00, 2030000.00, 1260000.00),
(N'3079-0B12A12', N'B', N'12A', 13, N'2 BEDROOMS', 55.50, 4381000.00, 69200.00, 3841000.00, 2690000.00, 1670000.00),
(N'3079-0B12A14', N'B', N'12A', 13, N'2 BEDROOMS', 50.00, 4033000.00, 71200.00, 3560000.00, 2490000.00, 1500000.00),
(N'3079-0B12A15', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3412000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A16', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3196000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A17', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3196000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A18', N'B', N'12A', 13, N'2 BEDROOMS', 46.00, 3560000.00, 71200.00, 3275000.00, 2290000.00, 1380000.00),
(N'3079-0B12A19', N'B', N'12A', 13, N'1 BEDROOM PLUS', 45.50, 3511000.00, 69200.00, 3149000.00, 2200000.00, 1370000.00),
(N'3079-0B12A20', N'B', N'12A', 13, N'STUDIO', 30.00, 2368000.00, 71200.00, 2136000.00, 1500000.00, 900000.00),
(N'3079-0B12A21', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2683000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A22', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2683000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A23', N'B', N'12A', 13, N'STUDIO', 30.00, 2368000.00, 71200.00, 2136000.00, 1500000.00, 900000.00),
(N'3079-0B12A24', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2623000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A25', N'B', N'12A', 13, N'STUDIO', 30.00, 2314000.00, 71200.00, 2136000.00, 1500000.00, 900000.00),
(N'3079-0B12A26', N'B', N'12A', 13, N'STUDIO', 30.00, 2368000.00, 71200.00, 2136000.00, 1500000.00, 900000.00),
(N'3079-0B12A27', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2683000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A28', N'B', N'12A', 13, N'1 BEDROOM', 34.00, 2744000.00, 69200.00, 2353000.00, 1650000.00, 1020000.00),
(N'3079-0B12A29', N'B', N'12A', 13, N'STUDIO', 30.00, 2368000.00, 71200.00, 2136000.00, 1500000.00, 900000.00),
(N'3079-0B12A30', N'B', N'12A', 13, N'1 BEDROOM PLUS', 45.50, 3511000.00, 69200.00, 3149000.00, 2200000.00, 1370000.00),
(N'3079-0B12A31', N'B', N'12A', 13, N'2 BEDROOMS', 46.00, 3635000.00, 71200.00, 3275000.00, 2290000.00, 1380000.00),
(N'3079-0B12A32', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3196000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A33', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 3124000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A34', N'B', N'12A', 13, N'1 BEDROOM PLUS', 40.50, 2926000.00, 69200.00, 2803000.00, 1960000.00, 1220000.00),
(N'3079-0B12A35', N'B', N'12A', 13, N'2 BEDROOMS', 50.00, 4033000.00, 71200.00, 3560000.00, 2490000.00, 1500000.00),
(N'3079-0B01401', N'B', N'14', 14, N'2 BEDROOMS', 55.50, 4412000.00, 69600.00, 3863000.00, 2700000.00, 1670000.00),
(N'3079-0B01402', N'B', N'14', 14, N'1 BEDROOM PLUS', 42.00, 3186000.00, 69600.00, 2923000.00, 2050000.00, 1260000.00),
(N'3079-0B01403', N'B', N'14', 14, N'1 BEDROOM', 34.50, 2618000.00, 69600.00, 2401000.00, 1680000.00, 1040000.00),
(N'3079-0B01404', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2383000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01405', N'B', N'14', 14, N'1 BEDROOM', 35.00, 2654000.00, 69600.00, 2436000.00, 1710000.00, 1050000.00),
(N'3079-0B01406', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2519000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01407', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2519000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01408', N'B', N'14', 14, N'1 BEDROOM', 35.00, 2654000.00, 69600.00, 2436000.00, 1710000.00, 1050000.00),
(N'3079-0B01409', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2332000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01410', N'B', N'14', 14, N'1 BEDROOM', 34.50, 2495000.00, 69600.00, 2401000.00, 1680000.00, 1040000.00),
(N'3079-0B01411', N'B', N'14', 14, N'1 BEDROOM PLUS', 42.00, 3186000.00, 69600.00, 2923000.00, 2050000.00, 1260000.00),
(N'3079-0B01412', N'B', N'14', 14, N'2 BEDROOMS', 55.50, 4457000.00, 69600.00, 3863000.00, 2700000.00, 1670000.00),
(N'3079-0B01414', N'B', N'14', 14, N'2 BEDROOMS', 50.00, 4057000.00, 71600.00, 3580000.00, 2510000.00, 1500000.00),
(N'3079-0B01415', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3215000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01416', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3215000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01417', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3215000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01418', N'B', N'14', 14, N'2 BEDROOMS', 46.00, 3579000.00, 71600.00, 3294000.00, 2310000.00, 1380000.00),
(N'3079-0B01419', N'B', N'14', 14, N'1 BEDROOM PLUS', 45.50, 3532000.00, 69600.00, 3167000.00, 2220000.00, 1370000.00),
(N'3079-0B01420', N'B', N'14', 14, N'STUDIO', 30.00, 2382000.00, 71600.00, 2148000.00, 1500000.00, 900000.00),
(N'3079-0B01421', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2699000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01422', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2699000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01423', N'B', N'14', 14, N'STUDIO', 30.00, 2382000.00, 71600.00, 2148000.00, 1500000.00, 900000.00),
(N'3079-0B01424', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2639000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01425', N'B', N'14', 14, N'STUDIO', 30.00, 2328000.00, 71600.00, 2148000.00, 1500000.00, 900000.00),
(N'3079-0B01426', N'B', N'14', 14, N'STUDIO', 30.00, 2382000.00, 71600.00, 2148000.00, 1500000.00, 900000.00),
(N'3079-0B01427', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2699000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01428', N'B', N'14', 14, N'1 BEDROOM', 34.00, 2759000.00, 69600.00, 2366000.00, 1660000.00, 1020000.00),
(N'3079-0B01429', N'B', N'14', 14, N'STUDIO', 30.00, 2382000.00, 71600.00, 2148000.00, 1500000.00, 900000.00),
(N'3079-0B01430', N'B', N'14', 14, N'1 BEDROOM PLUS', 45.50, 3532000.00, 69600.00, 3167000.00, 2220000.00, 1370000.00),
(N'3079-0B01431', N'B', N'14', 14, N'2 BEDROOMS', 46.00, 3654000.00, 71600.00, 3294000.00, 2310000.00, 1380000.00),
(N'3079-0B01432', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3215000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01433', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3287000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01434', N'B', N'14', 14, N'1 BEDROOM PLUS', 40.50, 3215000.00, 69600.00, 2819000.00, 1970000.00, 1220000.00),
(N'3079-0B01435', N'B', N'14', 14, N'2 BEDROOMS', 50.00, 4057000.00, 71600.00, 3580000.00, 2510000.00, 1500000.00),
(N'3079-0B01501', N'B', N'15', 15, N'2 BEDROOMS', 55.50, 4537000.00, 70000.00, 3885000.00, 2720000.00, 1670000.00),
(N'3079-0B01502', N'B', N'15', 15, N'1 BEDROOM PLUS', 42.00, 2934000.00, 70000.00, 2940000.00, 2060000.00, 1260000.00),
(N'3079-0B01503', N'B', N'15', 15, N'1 BEDROOM', 34.50, 2326000.00, 70000.00, 2415000.00, 1690000.00, 1040000.00),
(N'3079-0B01504', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2346000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01505', N'B', N'15', 15, N'1 BEDROOM', 35.00, 2461000.00, 70000.00, 2450000.00, 1720000.00, 1050000.00),
(N'3079-0B01506', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2535000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01507', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2346000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01508', N'B', N'15', 15, N'1 BEDROOM', 35.00, 2461000.00, 70000.00, 2450000.00, 1720000.00, 1050000.00),
(N'3079-0B01509', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2346000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01510', N'B', N'15', 15, N'1 BEDROOM', 34.50, 2326000.00, 70000.00, 2415000.00, 1690000.00, 1040000.00),
(N'3079-0B01511', N'B', N'15', 15, N'1 BEDROOM PLUS', 42.00, 3206000.00, 70000.00, 2940000.00, 2060000.00, 1260000.00),
(N'3079-0B01512', N'B', N'15', 15, N'2 BEDROOMS', 55.50, 4484000.00, 70000.00, 3885000.00, 2720000.00, 1670000.00),
(N'3079-0B01514', N'B', N'15', 15, N'2 BEDROOMS', 50.00, 3735000.00, 72000.00, 3600000.00, 2520000.00, 1500000.00),
(N'3079-0B01515', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 2958000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01516', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 2898000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01517', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3234000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01518', N'B', N'15', 15, N'2 BEDROOMS', 46.00, 3528000.00, 72000.00, 3312000.00, 2320000.00, 1380000.00),
(N'3079-0B01519', N'B', N'15', 15, N'1 BEDROOM PLUS', 45.50, 3228000.00, 70000.00, 3185000.00, 2230000.00, 1370000.00),
(N'3079-0B01520', N'B', N'15', 15, N'STUDIO', 30.00, 2168000.00, 72000.00, 2160000.00, 1510000.00, 900000.00),
(N'3079-0B01521', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2499000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01522', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2716000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01523', N'B', N'15', 15, N'STUDIO', 30.00, 2396000.00, 72000.00, 2160000.00, 1510000.00, 900000.00),
(N'3079-0B01524', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2655000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01525', N'B', N'15', 15, N'STUDIO', 30.00, 2343000.00, 72000.00, 2160000.00, 1510000.00, 900000.00),
(N'3079-0B01526', N'B', N'15', 15, N'STUDIO', 30.00, 2396000.00, 72000.00, 2160000.00, 1510000.00, 900000.00),
(N'3079-0B01527', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2716000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01528', N'B', N'15', 15, N'1 BEDROOM', 34.00, 2716000.00, 70000.00, 2380000.00, 1670000.00, 1020000.00),
(N'3079-0B01529', N'B', N'15', 15, N'STUDIO', 30.00, 2168000.00, 72000.00, 2160000.00, 1510000.00, 900000.00),
(N'3079-0B01530', N'B', N'15', 15, N'1 BEDROOM PLUS', 45.50, 3228000.00, 70000.00, 3185000.00, 2230000.00, 1370000.00),
(N'3079-0B01531', N'B', N'15', 15, N'2 BEDROOMS', 46.00, 3603000.00, 72000.00, 3312000.00, 2320000.00, 1380000.00),
(N'3079-0B01532', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 3234000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01533', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 2898000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01534', N'B', N'15', 15, N'1 BEDROOM PLUS', 40.50, 2958000.00, 70000.00, 2835000.00, 1980000.00, 1220000.00),
(N'3079-0B01535', N'B', N'15', 15, N'2 BEDROOMS', 50.00, 4080000.00, 72000.00, 3600000.00, 2520000.00, 1500000.00),
(N'3079-0B01601', N'B', N'16', 16, N'2 BEDROOMS', 55.50, 4564000.00, 70400.00, 3907000.00, 2730000.00, 1670000.00),
(N'3079-0B01602', N'B', N'16', 16, N'1 BEDROOM PLUS', 42.00, 3224000.00, 70400.00, 2957000.00, 2070000.00, 1260000.00),
(N'3079-0B01603', N'B', N'16', 16, N'1 BEDROOM', 34.50, 2527000.00, 70400.00, 2429000.00, 1700000.00, 1040000.00),
(N'3079-0B01604', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2611000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01605', N'B', N'16', 16, N'1 BEDROOM', 35.00, 2686000.00, 70400.00, 2464000.00, 1720000.00, 1050000.00),
(N'3079-0B01606', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2550000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01607', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2550000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01608', N'B', N'16', 16, N'1 BEDROOM', 35.00, 2686000.00, 70400.00, 2464000.00, 1720000.00, 1050000.00),
(N'3079-0B01609', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2731000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01610', N'B', N'16', 16, N'1 BEDROOM', 34.50, 2527000.00, 70400.00, 2429000.00, 1700000.00, 1040000.00),
(N'3079-0B01611', N'B', N'16', 16, N'1 BEDROOM PLUS', 42.00, 3224000.00, 70400.00, 2957000.00, 2070000.00, 1260000.00),
(N'3079-0B01612', N'B', N'16', 16, N'2 BEDROOMS', 55.50, 4459000.00, 70400.00, 3907000.00, 2730000.00, 1670000.00),
(N'3079-0B01614', N'B', N'16', 16, N'2 BEDROOMS', 50.00, 4105000.00, 72400.00, 3620000.00, 2530000.00, 1500000.00),
(N'3079-0B01615', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3253000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01616', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3182000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01617', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3253000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01618', N'B', N'16', 16, N'2 BEDROOMS', 46.00, 4183000.00, 72400.00, 3330000.00, 2330000.00, 1380000.00),
(N'3079-0B01619', N'B', N'16', 16, N'1 BEDROOM PLUS', 45.50, 3574000.00, 70400.00, 3203000.00, 2240000.00, 1370000.00),
(N'3079-0B01620', N'B', N'16', 16, N'STUDIO', 30.00, 2410000.00, 72400.00, 2172000.00, 1520000.00, 900000.00),
(N'3079-0B01621', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2731000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01622', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2731000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01623', N'B', N'16', 16, N'STUDIO', 30.00, 2410000.00, 72400.00, 2172000.00, 1520000.00, 900000.00),
(N'3079-0B01624', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2670000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01625', N'B', N'16', 16, N'STUDIO', 30.00, 2357000.00, 72400.00, 2172000.00, 1520000.00, 900000.00),
(N'3079-0B01626', N'B', N'16', 16, N'STUDIO', 30.00, 2410000.00, 72400.00, 2172000.00, 1520000.00, 900000.00),
(N'3079-0B01627', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2731000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01628', N'B', N'16', 16, N'1 BEDROOM', 34.00, 2731000.00, 70400.00, 2394000.00, 1680000.00, 1020000.00),
(N'3079-0B01629', N'B', N'16', 16, N'STUDIO', 30.00, 2410000.00, 72400.00, 2172000.00, 1520000.00, 900000.00),
(N'3079-0B01630', N'B', N'16', 16, N'1 BEDROOM PLUS', 45.50, 3574000.00, 70400.00, 3203000.00, 2240000.00, 1370000.00),
(N'3079-0B01631', N'B', N'16', 16, N'2 BEDROOMS', 46.00, 4428000.00, 72400.00, 3330000.00, 2330000.00, 1380000.00),
(N'3079-0B01632', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3253000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01633', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3182000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01634', N'B', N'16', 16, N'1 BEDROOM PLUS', 40.50, 3253000.00, 70400.00, 2851000.00, 2000000.00, 1220000.00),
(N'3079-0B01635', N'B', N'16', 16, N'2 BEDROOMS', 50.00, 4105000.00, 72400.00, 3620000.00, 2530000.00, 1500000.00),
(N'3079-0B01701', N'B', N'17', 17, N'2 BEDROOMS', 55.50, 4589000.00, 70800.00, 3929000.00, 2750000.00, 1670000.00),
(N'3079-0B01702', N'B', N'17', 17, N'1 BEDROOM PLUS', 42.00, 3244000.00, 70800.00, 2974000.00, 2080000.00, 1260000.00),
(N'3079-0B01703', N'B', N'17', 17, N'1 BEDROOM', 34.50, 2666000.00, 70800.00, 2443000.00, 1710000.00, 1040000.00),
(N'3079-0B01704', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2424000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01705', N'B', N'17', 17, N'1 BEDROOM', 35.00, 2489000.00, 70800.00, 2478000.00, 1730000.00, 1050000.00),
(N'3079-0B01706', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2567000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01707', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2567000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01708', N'B', N'17', 17, N'1 BEDROOM', 35.00, 2489000.00, 70800.00, 2478000.00, 1730000.00, 1050000.00),
(N'3079-0B01709', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2424000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01710', N'B', N'17', 17, N'1 BEDROOM', 34.50, 2543000.00, 70800.00, 2443000.00, 1710000.00, 1040000.00),
(N'3079-0B01711', N'B', N'17', 17, N'1 BEDROOM PLUS', 42.00, 2967000.00, 70800.00, 2974000.00, 2080000.00, 1260000.00),
(N'3079-0B01712', N'B', N'17', 17, N'2 BEDROOMS', 55.50, 4634000.00, 70800.00, 3929000.00, 2750000.00, 1670000.00),
(N'3079-0B01714', N'B', N'17', 17, N'2 BEDROOMS', 50.00, 3775000.00, 72800.00, 3640000.00, 2550000.00, 1500000.00),
(N'3079-0B01715', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 2991000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01716', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 2930000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01717', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3272000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01718', N'B', N'17', 17, N'2 BEDROOMS', 46.00, 3634000.00, 72800.00, 3349000.00, 2340000.00, 1380000.00),
(N'3079-0B01719', N'B', N'17', 17, N'1 BEDROOM PLUS', 45.50, 3264000.00, 70800.00, 3221000.00, 2250000.00, 1370000.00),
(N'3079-0B01720', N'B', N'17', 17, N'STUDIO', 30.00, 2424000.00, 72800.00, 2184000.00, 1530000.00, 900000.00),
(N'3079-0B01721', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2747000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01722', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2747000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01723', N'B', N'17', 17, N'STUDIO', 30.00, 2424000.00, 72800.00, 2184000.00, 1530000.00, 900000.00),
(N'3079-0B01724', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2686000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01725', N'B', N'17', 17, N'STUDIO', 30.00, 2371000.00, 72800.00, 2184000.00, 1530000.00, 900000.00),
(N'3079-0B01726', N'B', N'17', 17, N'STUDIO', 30.00, 2424000.00, 72800.00, 2184000.00, 1530000.00, 900000.00),
(N'3079-0B01727', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2747000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01728', N'B', N'17', 17, N'1 BEDROOM', 34.00, 2808000.00, 70800.00, 2407000.00, 1680000.00, 1020000.00),
(N'3079-0B01729', N'B', N'17', 17, N'STUDIO', 30.00, 2424000.00, 72800.00, 2184000.00, 1530000.00, 900000.00),
(N'3079-0B01730', N'B', N'17', 17, N'1 BEDROOM PLUS', 45.50, 3595000.00, 70800.00, 3221000.00, 2250000.00, 1370000.00),
(N'3079-0B01731', N'B', N'17', 17, N'2 BEDROOMS', 46.00, 3709000.00, 72800.00, 3349000.00, 2340000.00, 1380000.00),
(N'3079-0B01732', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3382000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01733', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3345000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01734', N'B', N'17', 17, N'1 BEDROOM PLUS', 40.50, 3455000.00, 70800.00, 2867000.00, 2010000.00, 1220000.00),
(N'3079-0B01735', N'B', N'17', 17, N'2 BEDROOMS', 50.00, 3880000.00, 72800.00, 3640000.00, 2550000.00, 1500000.00),
(N'3079-0B01801', N'B', N'18', 18, N'2 BEDROOMS', 55.50, 4517000.00, 71200.00, 3952000.00, 2770000.00, 1670000.00),
(N'3079-0B01802', N'B', N'18', 18, N'1 BEDROOM PLUS', 42.00, 3265000.00, 71200.00, 2990000.00, 2090000.00, 1260000.00),
(N'3079-0B01803', N'B', N'18', 18, N'1 BEDROOM', 34.50, 2682000.00, 71200.00, 2456000.00, 1720000.00, 1040000.00),
(N'3079-0B01804', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2644000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01805', N'B', N'18', 18, N'1 BEDROOM', 35.00, 2721000.00, 71200.00, 2492000.00, 1740000.00, 1050000.00),
(N'3079-0B01806', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2584000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01807', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2584000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01808', N'B', N'18', 18, N'1 BEDROOM', 35.00, 2721000.00, 71200.00, 2492000.00, 1740000.00, 1050000.00),
(N'3079-0B01809', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2644000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01810', N'B', N'18', 18, N'1 BEDROOM', 34.50, 2559000.00, 71200.00, 2456000.00, 1720000.00, 1040000.00),
(N'3079-0B01811', N'B', N'18', 18, N'1 BEDROOM PLUS', 42.00, 3265000.00, 71200.00, 2990000.00, 2090000.00, 1260000.00),
(N'3079-0B01812', N'B', N'18', 18, N'2 BEDROOMS', 55.50, 4511000.00, 71200.00, 3952000.00, 2770000.00, 1670000.00),
(N'3079-0B01814', N'B', N'18', 18, N'2 BEDROOMS', 50.00, 4152000.00, 73200.00, 3660000.00, 2560000.00, 1500000.00),
(N'3079-0B01815', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3507000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01816', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3292000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01817', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3292000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01818', N'B', N'18', 18, N'2 BEDROOMS', 46.00, 4228000.00, 73200.00, 3367000.00, 2360000.00, 1380000.00),
(N'3079-0B01819', N'B', N'18', 18, N'1 BEDROOM PLUS', 45.50, 3617000.00, 71200.00, 3240000.00, 2270000.00, 1370000.00),
(N'3079-0B01820', N'B', N'18', 18, N'STUDIO', 30.00, 2437000.00, 73200.00, 2196000.00, 1540000.00, 900000.00),
(N'3079-0B01821', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2764000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01822', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2764000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01823', N'B', N'18', 18, N'STUDIO', 30.00, 2437000.00, 73200.00, 2196000.00, 1540000.00, 900000.00),
(N'3079-0B01824', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2705000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01825', N'B', N'18', 18, N'STUDIO', 30.00, 2385000.00, 73200.00, 2196000.00, 1540000.00, 900000.00),
(N'3079-0B01826', N'B', N'18', 18, N'STUDIO', 30.00, 2437000.00, 73200.00, 2196000.00, 1540000.00, 900000.00),
(N'3079-0B01827', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2764000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01828', N'B', N'18', 18, N'1 BEDROOM', 34.00, 2764000.00, 71200.00, 2421000.00, 1690000.00, 1020000.00),
(N'3079-0B01829', N'B', N'18', 18, N'STUDIO', 30.00, 2437000.00, 73200.00, 2196000.00, 1540000.00, 900000.00),
(N'3079-0B01830', N'B', N'18', 18, N'1 BEDROOM PLUS', 45.50, 3617000.00, 71200.00, 3240000.00, 2270000.00, 1370000.00),
(N'3079-0B01831', N'B', N'18', 18, N'2 BEDROOMS', 46.00, 4471000.00, 73200.00, 3367000.00, 2360000.00, 1380000.00),
(N'3079-0B01832', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3292000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01833', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3220000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01834', N'B', N'18', 18, N'1 BEDROOM PLUS', 40.50, 3292000.00, 71200.00, 2884000.00, 2020000.00, 1220000.00),
(N'3079-0B01835', N'B', N'18', 18, N'2 BEDROOMS', 50.00, 4152000.00, 73200.00, 3660000.00, 2560000.00, 1500000.00),
(N'3079-0B01901', N'B', N'19', 19, N'2 BEDROOMS', 55.50, 4143000.00, 71600.00, 3974000.00, 2780000.00, 1670000.00),
(N'3079-0B01902', N'B', N'19', 19, N'1 BEDROOM PLUS', 42.00, 3285000.00, 71600.00, 3007000.00, 2100000.00, 1260000.00),
(N'3079-0B01903', N'B', N'19', 19, N'1 BEDROOM', 34.50, 2577000.00, 71600.00, 2470000.00, 1730000.00, 1040000.00),
(N'3079-0B01904', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2451000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01905', N'B', N'19', 19, N'1 BEDROOM', 35.00, 2517000.00, 71600.00, 2506000.00, 1750000.00, 1050000.00),
(N'3079-0B01906', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2400000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01907', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2599000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01908', N'B', N'19', 19, N'1 BEDROOM', 35.00, 2861000.00, 71600.00, 2506000.00, 1750000.00, 1050000.00),
(N'3079-0B01909', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2451000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01910', N'B', N'19', 19, N'1 BEDROOM', 34.50, 2637000.00, 71600.00, 2470000.00, 1730000.00, 1040000.00),
(N'3079-0B01911', N'B', N'19', 19, N'1 BEDROOM PLUS', 42.00, 3359000.00, 71600.00, 3007000.00, 2100000.00, 1260000.00),
(N'3079-0B01912', N'B', N'19', 19, N'2 BEDROOMS', 55.50, 4537000.00, 71600.00, 3974000.00, 2780000.00, 1670000.00),
(N'3079-0B01914', N'B', N'19', 19, N'2 BEDROOMS', 50.00, 3815000.00, 73600.00, 3680000.00, 2580000.00, 1500000.00),
(N'3079-0B01915', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3023000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01916', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 2962000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01917', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3023000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01918', N'B', N'19', 19, N'2 BEDROOMS', 46.00, 3671000.00, 73600.00, 3386000.00, 2370000.00, 1380000.00),
(N'3079-0B01919', N'B', N'19', 19, N'1 BEDROOM PLUS', 45.50, 3301000.00, 71600.00, 3258000.00, 2280000.00, 1370000.00),
(N'3079-0B01920', N'B', N'19', 19, N'STUDIO', 30.00, 2452000.00, 73600.00, 2208000.00, 1550000.00, 900000.00),
(N'3079-0B01921', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2779000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01922', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2779000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01923', N'B', N'19', 19, N'STUDIO', 30.00, 2452000.00, 73600.00, 2208000.00, 1550000.00, 900000.00),
(N'3079-0B01924', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2720000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01925', N'B', N'19', 19, N'STUDIO', 30.00, 2399000.00, 73600.00, 2208000.00, 1550000.00, 900000.00),
(N'3079-0B01926', N'B', N'19', 19, N'STUDIO', 30.00, 2452000.00, 73600.00, 2208000.00, 1550000.00, 900000.00),
(N'3079-0B01927', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2779000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01928', N'B', N'19', 19, N'1 BEDROOM', 34.00, 2553000.00, 71600.00, 2434000.00, 1700000.00, 1020000.00),
(N'3079-0B01929', N'B', N'19', 19, N'STUDIO', 30.00, 2216000.00, 73600.00, 2208000.00, 1550000.00, 900000.00),
(N'3079-0B01930', N'B', N'19', 19, N'1 BEDROOM PLUS', 45.50, 3301000.00, 71600.00, 3258000.00, 2280000.00, 1370000.00),
(N'3079-0B01931', N'B', N'19', 19, N'2 BEDROOMS', 46.00, 4494000.00, 73600.00, 3386000.00, 2370000.00, 1380000.00),
(N'3079-0B01932', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3311000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01933', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 2962000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01934', N'B', N'19', 19, N'1 BEDROOM PLUS', 40.50, 3023000.00, 71600.00, 2900000.00, 2030000.00, 1220000.00),
(N'3079-0B01935', N'B', N'19', 19, N'2 BEDROOMS', 50.00, 3920000.00, 73600.00, 3680000.00, 2580000.00, 1500000.00),
(N'3079-0B02001', N'B', N'20', 20, N'2 BEDROOMS', 55.50, 4564000.00, 72000.00, 3996000.00, 2800000.00, 1670000.00),
(N'3079-0B02002', N'B', N'20', 20, N'1 BEDROOM PLUS', 42.00, 3454000.00, 72000.00, 3024000.00, 2120000.00, 1260000.00),
(N'3079-0B02003', N'B', N'20', 20, N'1 BEDROOM', 34.50, 2716000.00, 72000.00, 2484000.00, 1740000.00, 1040000.00),
(N'3079-0B02004', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2795000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02005', N'B', N'20', 20, N'1 BEDROOM', 35.00, 2878000.00, 72000.00, 2520000.00, 1760000.00, 1050000.00),
(N'3079-0B02006', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2736000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02007', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2736000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02008', N'B', N'20', 20, N'1 BEDROOM', 35.00, 2878000.00, 72000.00, 2520000.00, 1760000.00, 1050000.00),
(N'3079-0B02009', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2795000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02010', N'B', N'20', 20, N'1 BEDROOM', 34.50, 2654000.00, 72000.00, 2484000.00, 1740000.00, 1040000.00),
(N'3079-0B02011', N'B', N'20', 20, N'1 BEDROOM PLUS', 42.00, 3379000.00, 72000.00, 3024000.00, 2120000.00, 1260000.00),
(N'3079-0B02012', N'B', N'20', 20, N'2 BEDROOMS', 55.50, 4564000.00, 72000.00, 3996000.00, 2800000.00, 1670000.00),
(N'3079-0B02014', N'B', N'20', 20, N'2 BEDROOMS', 50.00, 4198000.00, 74000.00, 3700000.00, 2590000.00, 1500000.00),
(N'3079-0B02015', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3546000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02016', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3331000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02017', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3330000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02018', N'B', N'20', 20, N'2 BEDROOMS', 46.00, 4270000.00, 74000.00, 3404000.00, 2380000.00, 1380000.00),
(N'3079-0B02019', N'B', N'20', 20, N'1 BEDROOM PLUS', 45.50, 3660000.00, 72000.00, 3276000.00, 2290000.00, 1370000.00),
(N'3079-0B02020', N'B', N'20', 20, N'STUDIO', 30.00, 2467000.00, 74000.00, 2220000.00, 1550000.00, 900000.00),
(N'3079-0B02021', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2795000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02022', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2795000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02023', N'B', N'20', 20, N'STUDIO', 30.00, 2467000.00, 74000.00, 2220000.00, 1550000.00, 900000.00),
(N'3079-0B02024', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2736000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02025', N'B', N'20', 20, N'STUDIO', 30.00, 2414000.00, 74000.00, 2220000.00, 1550000.00, 900000.00),
(N'3079-0B02026', N'B', N'20', 20, N'STUDIO', 30.00, 2467000.00, 74000.00, 2220000.00, 1550000.00, 900000.00),
(N'3079-0B02027', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2795000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02028', N'B', N'20', 20, N'1 BEDROOM', 34.00, 2856000.00, 72000.00, 2448000.00, 1710000.00, 1020000.00),
(N'3079-0B02029', N'B', N'20', 20, N'STUDIO', 30.00, 2467000.00, 74000.00, 2220000.00, 1550000.00, 900000.00),
(N'3079-0B02030', N'B', N'20', 20, N'1 BEDROOM PLUS', 45.50, 3660000.00, 72000.00, 3276000.00, 2290000.00, 1370000.00),
(N'3079-0B02031', N'B', N'20', 20, N'2 BEDROOMS', 46.00, 4515000.00, 74000.00, 3404000.00, 2380000.00, 1380000.00),
(N'3079-0B02032', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3330000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02033', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3404000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02034', N'B', N'20', 20, N'1 BEDROOM PLUS', 40.50, 3403000.00, 72000.00, 2916000.00, 2040000.00, 1220000.00),
(N'3079-0B02035', N'B', N'20', 20, N'2 BEDROOMS', 50.00, 4198000.00, 74000.00, 3700000.00, 2590000.00, 1500000.00),
(N'3079-0B02101', N'B', N'21', 21, N'2 BEDROOMS', 55.50, 4187000.00, 72400.00, 4018000.00, 2810000.00, 1670000.00),
(N'3079-0B02102', N'B', N'21', 21, N'1 BEDROOM PLUS', 42.00, 3323000.00, 72400.00, 3041000.00, 2130000.00, 1260000.00),
(N'3079-0B02103', N'B', N'21', 21, N'1 BEDROOM', 34.50, 2608000.00, 72400.00, 2498000.00, 1750000.00, 1040000.00),
(N'3079-0B02104', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2478000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02105', N'B', N'21', 21, N'1 BEDROOM', 35.00, 2545000.00, 72400.00, 2534000.00, 1770000.00, 1050000.00),
(N'3079-0B02106', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2752000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02107', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2631000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02108', N'B', N'21', 21, N'1 BEDROOM', 35.00, 2770000.00, 72400.00, 2534000.00, 1770000.00, 1050000.00),
(N'3079-0B02109', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2478000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02110', N'B', N'21', 21, N'1 BEDROOM', 34.50, 2669000.00, 72400.00, 2498000.00, 1750000.00, 1040000.00),
(N'3079-0B02111', N'B', N'21', 21, N'1 BEDROOM PLUS', 42.00, 3398000.00, 72400.00, 3041000.00, 2130000.00, 1260000.00),
(N'3079-0B02112', N'B', N'21', 21, N'2 BEDROOMS', 55.50, 4641000.00, 72400.00, 4018000.00, 2810000.00, 1670000.00),
(N'3079-0B02114', N'B', N'21', 21, N'2 BEDROOMS', 50.00, 4222000.00, 74400.00, 3720000.00, 2600000.00, 1500000.00),
(N'3079-0B02115', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3055000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02116', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3349000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02117', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3055000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02118', N'B', N'21', 21, N'2 BEDROOMS', 46.00, 3707000.00, 74400.00, 3422000.00, 2400000.00, 1380000.00),
(N'3079-0B02119', N'B', N'21', 21, N'1 BEDROOM PLUS', 45.50, 3682000.00, 72400.00, 3294000.00, 2310000.00, 1370000.00),
(N'3079-0B02120', N'B', N'21', 21, N'STUDIO', 30.00, 2481000.00, 74400.00, 2232000.00, 1560000.00, 900000.00),
(N'3079-0B02121', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2812000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02122', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2812000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02123', N'B', N'21', 21, N'STUDIO', 30.00, 2481000.00, 74400.00, 2232000.00, 1560000.00, 900000.00),
(N'3079-0B02124', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2752000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02125', N'B', N'21', 21, N'STUDIO', 30.00, 2428000.00, 74400.00, 2232000.00, 1560000.00, 900000.00),
(N'3079-0B02126', N'B', N'21', 21, N'STUDIO', 30.00, 2481000.00, 74400.00, 2232000.00, 1560000.00, 900000.00),
(N'3079-0B02127', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2812000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02128', N'B', N'21', 21, N'1 BEDROOM', 34.00, 2871000.00, 72400.00, 2462000.00, 1720000.00, 1020000.00),
(N'3079-0B02129', N'B', N'21', 21, N'STUDIO', 30.00, 2481000.00, 74400.00, 2232000.00, 1560000.00, 900000.00),
(N'3079-0B02130', N'B', N'21', 21, N'1 BEDROOM PLUS', 45.50, 3682000.00, 72400.00, 3294000.00, 2310000.00, 1370000.00),
(N'3079-0B02131', N'B', N'21', 21, N'2 BEDROOMS', 46.00, 4611000.00, 74400.00, 3422000.00, 2400000.00, 1380000.00),
(N'3079-0B02132', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3348000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02133', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 2995000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02134', N'B', N'21', 21, N'1 BEDROOM PLUS', 40.50, 3421000.00, 72400.00, 2932000.00, 2050000.00, 1220000.00),
(N'3079-0B02135', N'B', N'21', 21, N'2 BEDROOMS', 50.00, 4222000.00, 74400.00, 3720000.00, 2600000.00, 1500000.00),
(N'3079-0B02201', N'B', N'22', 22, N'2 BEDROOMS', 55.50, 4616000.00, 72800.00, 4040000.00, 2830000.00, 1670000.00),
(N'3079-0B02202', N'B', N'22', 22, N'1 BEDROOM PLUS', 42.00, 3492000.00, 72800.00, 3058000.00, 2140000.00, 1260000.00),
(N'3079-0B02203', N'B', N'22', 22, N'1 BEDROOM', 34.50, 2747000.00, 72800.00, 2512000.00, 1760000.00, 1040000.00),
(N'3079-0B02204', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2829000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02205', N'B', N'22', 22, N'1 BEDROOM', 35.00, 2910000.00, 72800.00, 2548000.00, 1780000.00, 1050000.00),
(N'3079-0B02206', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2768000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02207', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2768000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02208', N'B', N'22', 22, N'1 BEDROOM', 35.00, 2910000.00, 72800.00, 2548000.00, 1780000.00, 1050000.00),
(N'3079-0B02209', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2829000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02210', N'B', N'22', 22, N'1 BEDROOM', 34.50, 2685000.00, 72800.00, 2512000.00, 1760000.00, 1040000.00),
(N'3079-0B02211', N'B', N'22', 22, N'1 BEDROOM PLUS', 42.00, 3419000.00, 72800.00, 3058000.00, 2140000.00, 1260000.00),
(N'3079-0B02212', N'B', N'22', 22, N'2 BEDROOMS', 55.50, 4616000.00, 72800.00, 4040000.00, 2830000.00, 1670000.00),
(N'3079-0B02214', N'B', N'22', 22, N'2 BEDROOMS', 50.00, 4246000.00, 74800.00, 3740000.00, 2620000.00, 1500000.00),
(N'3079-0B02215', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3584000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02216', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3368000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02217', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3368000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02218', N'B', N'22', 22, N'2 BEDROOMS', 46.00, 4314000.00, 74800.00, 3441000.00, 2410000.00, 1380000.00),
(N'3079-0B02219', N'B', N'22', 22, N'1 BEDROOM PLUS', 45.50, 3703000.00, 72800.00, 3312000.00, 2320000.00, 1370000.00),
(N'3079-0B02220', N'B', N'22', 22, N'STUDIO', 30.00, 2495000.00, 74800.00, 2244000.00, 1570000.00, 900000.00),
(N'3079-0B02221', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2829000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02222', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2829000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02223', N'B', N'22', 22, N'STUDIO', 30.00, 2495000.00, 74800.00, 2244000.00, 1570000.00, 900000.00),
(N'3079-0B02224', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2768000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02225', N'B', N'22', 22, N'STUDIO', 30.00, 2442000.00, 74800.00, 2244000.00, 1570000.00, 900000.00),
(N'3079-0B02226', N'B', N'22', 22, N'STUDIO', 30.00, 2495000.00, 74800.00, 2244000.00, 1570000.00, 900000.00),
(N'3079-0B02227', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2829000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02228', N'B', N'22', 22, N'1 BEDROOM', 34.00, 2887000.00, 72800.00, 2475000.00, 1730000.00, 1020000.00),
(N'3079-0B02229', N'B', N'22', 22, N'STUDIO', 30.00, 2495000.00, 74800.00, 2244000.00, 1570000.00, 900000.00),
(N'3079-0B02230', N'B', N'22', 22, N'1 BEDROOM PLUS', 45.50, 3703000.00, 72800.00, 3312000.00, 2320000.00, 1370000.00),
(N'3079-0B02231', N'B', N'22', 22, N'2 BEDROOMS', 46.00, 4558000.00, 74800.00, 3441000.00, 2410000.00, 1380000.00),
(N'3079-0B02232', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3368000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02233', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3441000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02234', N'B', N'22', 22, N'1 BEDROOM PLUS', 40.50, 3441000.00, 72800.00, 2948000.00, 2060000.00, 1220000.00),
(N'3079-0B02235', N'B', N'22', 22, N'2 BEDROOMS', 50.00, 4246000.00, 74800.00, 3740000.00, 2620000.00, 1500000.00),
(N'3079-0B02301', N'B', N'23', 23, N'2 BEDROOMS', 55.50, 4232000.00, 73200.00, 4063000.00, 2840000.00, 1670000.00),
(N'3079-0B02302', N'B', N'23', 23, N'1 BEDROOM PLUS', 42.00, 3364000.00, 73200.00, 3074000.00, 2150000.00, 1260000.00),
(N'3079-0B02303', N'B', N'23', 23, N'1 BEDROOM', 34.50, 2642000.00, 73200.00, 2525000.00, 1770000.00, 1040000.00),
(N'3079-0B02304', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2506000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02305', N'B', N'23', 23, N'1 BEDROOM', 35.00, 2803000.00, 73200.00, 2562000.00, 1790000.00, 1050000.00),
(N'3079-0B02306', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2664000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02307', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2664000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02308', N'B', N'23', 23, N'1 BEDROOM', 35.00, 2803000.00, 73200.00, 2562000.00, 1790000.00, 1050000.00),
(N'3079-0B02309', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2506000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02310', N'B', N'23', 23, N'1 BEDROOM', 34.50, 2642000.00, 73200.00, 2525000.00, 1770000.00, 1040000.00),
(N'3079-0B02311', N'B', N'23', 23, N'1 BEDROOM PLUS', 42.00, 3439000.00, 73200.00, 3074000.00, 2150000.00, 1260000.00),
(N'3079-0B02312', N'B', N'23', 23, N'2 BEDROOMS', 55.50, 4277000.00, 73200.00, 4063000.00, 2840000.00, 1670000.00),
(N'3079-0B02314', N'B', N'23', 23, N'2 BEDROOMS', 50.00, 3895000.00, 75200.00, 3760000.00, 2630000.00, 1500000.00),
(N'3079-0B02315', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3387000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02316', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3387000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02317', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3387000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02318', N'B', N'23', 23, N'2 BEDROOMS', 46.00, 4580000.00, 75200.00, 3459000.00, 2420000.00, 1380000.00),
(N'3079-0B02319', N'B', N'23', 23, N'1 BEDROOM PLUS', 45.50, 3724000.00, 73200.00, 3331000.00, 2330000.00, 1370000.00),
(N'3079-0B02320', N'B', N'23', 23, N'STUDIO', 30.00, 2509000.00, 75200.00, 2256000.00, 1580000.00, 900000.00),
(N'3079-0B02321', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2845000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02322', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2845000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02323', N'B', N'23', 23, N'STUDIO', 30.00, 2509000.00, 75200.00, 2256000.00, 1580000.00, 900000.00),
(N'3079-0B02324', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2784000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02325', N'B', N'23', 23, N'STUDIO', 30.00, 2456000.00, 75200.00, 2256000.00, 1580000.00, 900000.00),
(N'3079-0B02326', N'B', N'23', 23, N'STUDIO', 30.00, 2509000.00, 75200.00, 2256000.00, 1580000.00, 900000.00),
(N'3079-0B02327', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2845000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02328', N'B', N'23', 23, N'1 BEDROOM', 34.00, 2904000.00, 73200.00, 2489000.00, 1740000.00, 1020000.00),
(N'3079-0B02329', N'B', N'23', 23, N'STUDIO', 30.00, 2509000.00, 75200.00, 2256000.00, 1580000.00, 900000.00),
(N'3079-0B02330', N'B', N'23', 23, N'1 BEDROOM PLUS', 45.50, 3724000.00, 73200.00, 3331000.00, 2330000.00, 1370000.00),
(N'3079-0B02331', N'B', N'23', 23, N'2 BEDROOMS', 46.00, 3819000.00, 75200.00, 3459000.00, 2420000.00, 1380000.00),
(N'3079-0B02332', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3088000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02333', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3459000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02334', N'B', N'23', 23, N'1 BEDROOM PLUS', 40.50, 3459000.00, 73200.00, 2965000.00, 2080000.00, 1220000.00),
(N'3079-0B02335', N'B', N'23', 23, N'2 BEDROOMS', 50.00, 4269000.00, 75200.00, 3760000.00, 2630000.00, 1500000.00),
(N'3079-0B02401', N'B', N'24', 24, N'2 BEDROOMS', 55.50, 4675000.00, 73600.00, 4085000.00, 2860000.00, 1670000.00),
(N'3079-0B02402', N'B', N'24', 24, N'1 BEDROOM PLUS', 42.00, 3533000.00, 73600.00, 3091000.00, 2160000.00, 1260000.00),
(N'3079-0B02403', N'B', N'24', 24, N'1 BEDROOM', 34.50, 2780000.00, 73600.00, 2539000.00, 1780000.00, 1040000.00),
(N'3079-0B02404', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2740000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02405', N'B', N'24', 24, N'1 BEDROOM', 35.00, 2944000.00, 73600.00, 2576000.00, 1800000.00, 1050000.00),
(N'3079-0B02406', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2799000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02407', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2799000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02408', N'B', N'24', 24, N'1 BEDROOM', 35.00, 2944000.00, 73600.00, 2576000.00, 1800000.00, 1050000.00),
(N'3079-0B02409', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2740000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02410', N'B', N'24', 24, N'1 BEDROOM', 34.50, 2820000.00, 73600.00, 2539000.00, 1780000.00, 1040000.00),
(N'3079-0B02411', N'B', N'24', 24, N'1 BEDROOM PLUS', 42.00, 3458000.00, 73600.00, 3091000.00, 2160000.00, 1260000.00),
(N'3079-0B02412', N'B', N'24', 24, N'2 BEDROOMS', 55.50, 4818000.00, 73600.00, 4085000.00, 2860000.00, 1670000.00),
(N'3079-0B02414', N'B', N'24', 24, N'2 BEDROOMS', 50.00, 4293000.00, 75600.00, 3780000.00, 2650000.00, 1500000.00),
(N'3079-0B02415', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3407000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02416', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3334000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02417', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3517000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02418', N'B', N'24', 24, N'2 BEDROOMS', 46.00, 4603000.00, 75600.00, 3478000.00, 2430000.00, 1380000.00),
(N'3079-0B02419', N'B', N'24', 24, N'1 BEDROOM PLUS', 45.50, 3747000.00, 73600.00, 3349000.00, 2340000.00, 1370000.00),
(N'3079-0B02420', N'B', N'24', 24, N'STUDIO', 30.00, 2523000.00, 75600.00, 2268000.00, 1590000.00, 900000.00),
(N'3079-0B02421', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2860000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02422', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2860000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02423', N'B', N'24', 24, N'STUDIO', 30.00, 2523000.00, 75600.00, 2268000.00, 1590000.00, 900000.00),
(N'3079-0B02424', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2799000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02425', N'B', N'24', 24, N'STUDIO', 30.00, 2471000.00, 75600.00, 2268000.00, 1590000.00, 900000.00),
(N'3079-0B02426', N'B', N'24', 24, N'STUDIO', 30.00, 2523000.00, 75600.00, 2268000.00, 1590000.00, 900000.00),
(N'3079-0B02427', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2860000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02428', N'B', N'24', 24, N'1 BEDROOM', 34.00, 2919000.00, 73600.00, 2502000.00, 1750000.00, 1020000.00),
(N'3079-0B02429', N'B', N'24', 24, N'STUDIO', 30.00, 2523000.00, 75600.00, 2268000.00, 1590000.00, 900000.00),
(N'3079-0B02430', N'B', N'24', 24, N'1 BEDROOM PLUS', 45.50, 3747000.00, 73600.00, 3349000.00, 2340000.00, 1370000.00),
(N'3079-0B02431', N'B', N'24', 24, N'2 BEDROOMS', 46.00, 4678000.00, 75600.00, 3478000.00, 2430000.00, 1380000.00),
(N'3079-0B02432', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3407000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02433', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3588000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02434', N'B', N'24', 24, N'1 BEDROOM PLUS', 40.50, 3478000.00, 73600.00, 2981000.00, 2090000.00, 1220000.00),
(N'3079-0B02435', N'B', N'24', 24, N'2 BEDROOMS', 50.00, 4293000.00, 75600.00, 3780000.00, 2650000.00, 1500000.00),
(N'3079-0B02501', N'B', N'25', 25, N'2 BEDROOMS', 55.50, 4276000.00, 74000.00, 4107000.00, 2870000.00, 1670000.00),
(N'3079-0B02502', N'B', N'25', 25, N'1 BEDROOM PLUS', 42.00, 3553000.00, 74000.00, 3108000.00, 2180000.00, 1260000.00),
(N'3079-0B02503', N'B', N'25', 25, N'1 BEDROOM', 34.50, 2674000.00, 74000.00, 2553000.00, 1790000.00, 1040000.00),
(N'3079-0B02504', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2533000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02505', N'B', N'25', 25, N'1 BEDROOM', 35.00, 2601000.00, 74000.00, 2590000.00, 1810000.00, 1050000.00),
(N'3079-0B02506', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2817000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02507', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2482000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02508', N'B', N'25', 25, N'1 BEDROOM', 35.00, 2601000.00, 74000.00, 2590000.00, 1810000.00, 1050000.00),
(N'3079-0B02509', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2533000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02510', N'B', N'25', 25, N'1 BEDROOM', 34.50, 2736000.00, 74000.00, 2553000.00, 1790000.00, 1040000.00),
(N'3079-0B02511', N'B', N'25', 25, N'1 BEDROOM PLUS', 42.00, 3102000.00, 74000.00, 3108000.00, 2180000.00, 1260000.00),
(N'3079-0B02512', N'B', N'25', 25, N'2 BEDROOMS', 55.50, 4321000.00, 74000.00, 4107000.00, 2870000.00, 1670000.00),
(N'3079-0B02514', N'B', N'25', 25, N'2 BEDROOMS', 50.00, 4316000.00, 76000.00, 3800000.00, 2660000.00, 1500000.00),
(N'3079-0B02515', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3120000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02516', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3427000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02517', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3425000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02518', N'B', N'25', 25, N'2 BEDROOMS', 46.00, 3781000.00, 76000.00, 3496000.00, 2450000.00, 1380000.00),
(N'3079-0B02519', N'B', N'25', 25, N'1 BEDROOM PLUS', 45.50, 3768000.00, 74000.00, 3367000.00, 2360000.00, 1370000.00),
(N'3079-0B02520', N'B', N'25', 25, N'STUDIO', 30.00, 2537000.00, 76000.00, 2280000.00, 1600000.00, 900000.00),
(N'3079-0B02521', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2877000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02522', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2877000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02523', N'B', N'25', 25, N'STUDIO', 30.00, 2537000.00, 76000.00, 2280000.00, 1600000.00, 900000.00),
(N'3079-0B02524', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2817000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02525', N'B', N'25', 25, N'STUDIO', 30.00, 2484000.00, 76000.00, 2280000.00, 1600000.00, 900000.00),
(N'3079-0B02526', N'B', N'25', 25, N'STUDIO', 30.00, 2537000.00, 76000.00, 2280000.00, 1600000.00, 900000.00),
(N'3079-0B02527', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2877000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02528', N'B', N'25', 25, N'1 BEDROOM', 34.00, 2937000.00, 74000.00, 2516000.00, 1760000.00, 1020000.00),
(N'3079-0B02529', N'B', N'25', 25, N'STUDIO', 30.00, 2537000.00, 76000.00, 2280000.00, 1600000.00, 900000.00),
(N'3079-0B02530', N'B', N'25', 25, N'1 BEDROOM PLUS', 45.50, 3768000.00, 74000.00, 3367000.00, 2360000.00, 1370000.00),
(N'3079-0B02531', N'B', N'25', 25, N'2 BEDROOMS', 46.00, 3856000.00, 76000.00, 3496000.00, 2450000.00, 1380000.00),
(N'3079-0B02532', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3425000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02533', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3498000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02534', N'B', N'25', 25, N'1 BEDROOM PLUS', 40.50, 3497000.00, 74000.00, 2997000.00, 2100000.00, 1220000.00),
(N'3079-0B02535', N'B', N'25', 25, N'2 BEDROOMS', 50.00, 4421000.00, 76000.00, 3800000.00, 2660000.00, 1500000.00),
(N'3079-0B02601', N'B', N'26', 26, N'2 BEDROOMS', 55.50, 4720000.00, 74400.00, 4129000.00, 2890000.00, 1670000.00),
(N'3079-0B02602', N'B', N'26', 26, N'1 BEDROOM PLUS', 42.00, 3571000.00, 74400.00, 3125000.00, 2190000.00, 1260000.00),
(N'3079-0B02603', N'B', N'26', 26, N'1 BEDROOM', 34.50, 2814000.00, 74400.00, 2567000.00, 1800000.00, 1040000.00),
(N'3079-0B02604', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2892000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02605', N'B', N'26', 26, N'1 BEDROOM', 35.00, 2977000.00, 74400.00, 2604000.00, 1820000.00, 1050000.00),
(N'3079-0B02606', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2832000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02607', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2832000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02608', N'B', N'26', 26, N'1 BEDROOM', 35.00, 2977000.00, 74400.00, 2604000.00, 1820000.00, 1050000.00),
(N'3079-0B02609', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2892000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02610', N'B', N'26', 26, N'1 BEDROOM', 34.50, 2752000.00, 74400.00, 2567000.00, 1800000.00, 1040000.00),
(N'3079-0B02611', N'B', N'26', 26, N'1 BEDROOM PLUS', 42.00, 3497000.00, 74400.00, 3125000.00, 2190000.00, 1260000.00),
(N'3079-0B02612', N'B', N'26', 26, N'2 BEDROOMS', 55.50, 4720000.00, 74400.00, 4129000.00, 2890000.00, 1670000.00),
(N'3079-0B02614', N'B', N'26', 26, N'2 BEDROOMS', 50.00, 4341000.00, 76400.00, 3820000.00, 2670000.00, 1500000.00),
(N'3079-0B02615', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3660000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02616', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3445000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02617', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3444000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02618', N'B', N'26', 26, N'2 BEDROOMS', 46.00, 4645000.00, 76400.00, 3514000.00, 2460000.00, 1380000.00),
(N'3079-0B02619', N'B', N'26', 26, N'1 BEDROOM PLUS', 45.50, 3789000.00, 74400.00, 3385000.00, 2370000.00, 1370000.00),
(N'3079-0B02620', N'B', N'26', 26, N'STUDIO', 30.00, 2552000.00, 76400.00, 2292000.00, 1600000.00, 900000.00),
(N'3079-0B02621', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2892000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02622', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2892000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02623', N'B', N'26', 26, N'STUDIO', 30.00, 2552000.00, 76400.00, 2292000.00, 1600000.00, 900000.00),
(N'3079-0B02624', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2832000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02625', N'B', N'26', 26, N'STUDIO', 30.00, 2498000.00, 76400.00, 2292000.00, 1600000.00, 900000.00),
(N'3079-0B02626', N'B', N'26', 26, N'STUDIO', 30.00, 2552000.00, 76400.00, 2292000.00, 1600000.00, 900000.00),
(N'3079-0B02627', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2892000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02628', N'B', N'26', 26, N'1 BEDROOM', 34.00, 2953000.00, 74400.00, 2530000.00, 1770000.00, 1020000.00),
(N'3079-0B02629', N'B', N'26', 26, N'STUDIO', 30.00, 2552000.00, 76400.00, 2292000.00, 1600000.00, 900000.00),
(N'3079-0B02630', N'B', N'26', 26, N'1 BEDROOM PLUS', 45.50, 3789000.00, 74400.00, 3385000.00, 2370000.00, 1370000.00),
(N'3079-0B02631', N'B', N'26', 26, N'2 BEDROOMS', 46.00, 4645000.00, 76400.00, 3514000.00, 2460000.00, 1380000.00),
(N'3079-0B02632', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3444000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02633', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3518000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02634', N'B', N'26', 26, N'1 BEDROOM PLUS', 40.50, 3517000.00, 74400.00, 3013000.00, 2110000.00, 1220000.00),
(N'3079-0B02635', N'B', N'26', 26, N'2 BEDROOMS', 50.00, 4341000.00, 76400.00, 3820000.00, 2670000.00, 1500000.00),
(N'3079-0B02701', N'B', N'27', 27, N'2 BEDROOMS', 51.00, 3743000.00, 73800.00, 3764000.00, 2630000.00, 1530000.00),
(N'3079-0B02702', N'B', N'27', 27, N'2 BEDROOMS', 78.00, 5480000.00, 73800.00, 5756000.00, 4030000.00, 2340000.00),
(N'3079-0B02703', N'B', N'27', 27, N'2 BEDROOMS', 56.00, 4167000.00, 74800.00, 4189000.00, 2930000.00, 1680000.00),
(N'3079-0B02704', N'B', N'27', 27, N'3 BEDROOMS', 91.00, 6889000.00, 71000.00, 6461000.00, 4520000.00, 2730000.00),
(N'3079-0B02705', N'B', N'27', 27, N'3 BEDROOMS', 81.50, 6229000.00, 68000.00, 5542000.00, 3880000.00, 2450000.00),
(N'3079-0B02706', N'B', N'27', 27, N'2 BEDROOMS', 46.00, 3680000.00, 76800.00, 3533000.00, 2470000.00, 1380000.00),
(N'3079-0B02707', N'B', N'27', 27, N'1 BEDROOM PLUS', 45.50, 3446000.00, 74800.00, 3403000.00, 2380000.00, 1370000.00),
(N'3079-0B02708', N'B', N'27', 27, N'2 BEDROOMS', 64.00, 4838000.00, 73800.00, 4723000.00, 3310000.00, 1920000.00);

IF (SELECT COUNT(*) FROM #Src) <> 1614
    THROW 50004, 'The staged workbook is not the expected row count. The VALUES block has been edited or truncated.', 1;

IF (SELECT COUNT(DISTINCT RoomNumber) FROM #Src) <> 1614
    THROW 50005, 'The staged workbook contains duplicate room numbers. Refusing to guess which price wins.', 1;

-- ── 3. Match workbook rows to units ───────────────────────────────────────────
-- RoomNumber is key rank 1 in appraisal.vw_ProjectUnitKeys; CondoRegistrationNumber (rank 0) is
-- tried as a fallback because a project loaded from the legacy system may carry the code there.
--
-- LEFT JOIN, not JOIN: a workbook room with no unit has to survive into #Match so that
-- @CreateMissingUnits can mint one for it. IsNew separates the two populations everywhere below —
-- an attribute diff or a floor repair is meaningless for a row that does not exist yet.
SELECT s.*,
       ProjectUnitId  = pu.Id,
       IsNew          = CONVERT(bit, CASE WHEN pu.Id IS NULL THEN 1 ELSE 0 END),
       pu.ProjectModelId,
       ProjectTowerId = CONVERT(uniqueidentifier, NULL),   -- filled in for new rows only
       SequenceNumber = CONVERT(int, NULL),                --            "
       IsSold         = ISNULL(pu.IsSold, CONVERT(bit, 0)),
       DbFloor        = pu.Floor,      DbTowerName    = pu.TowerName, DbModelType = pu.ModelType,
       DbUsableArea   = pu.UsableArea, DbSellingPrice = pu.SellingPrice
INTO #Match
FROM #Src s
LEFT JOIN appraisal.ProjectUnits pu
  ON pu.ProjectId = @ProjectId
 AND (pu.RoomNumber = s.RoomNumber OR pu.CondoRegistrationNumber = s.RoomNumber);

DECLARE @Supplied       int = (SELECT COUNT(*) FROM #Src);
DECLARE @Matched        int = (SELECT COUNT(DISTINCT RoomNumber) FROM #Match WHERE IsNew = 0);
DECLARE @Unmatched      int;
DECLARE @Ambiguous      int = (SELECT COUNT(*) FROM (SELECT RoomNumber FROM #Match WHERE IsNew = 0
                                                     GROUP BY RoomNumber HAVING COUNT(*) > 1) x);
-- Declared here rather than in the report below: the guards abort before the report runs, and
-- "1,609 of 1,614 matched" only means something next to how many units the project actually holds.
DECLARE @UnitsInProject int = (SELECT COUNT(*) FROM appraisal.ProjectUnits WHERE ProjectId = @ProjectId);
SET @Unmatched = @Supplied - @Matched;

PRINT CONCAT('Workbook rows           : ', @Supplied);
PRINT CONCAT('Units in this project   : ', @UnitsInProject);
PRINT CONCAT('Matched to a unit       : ', @Matched);
PRINT CONCAT('Unmatched               : ', @Unmatched);
PRINT CONCAT('Ambiguous (>1 unit)     : ', @Ambiguous);
PRINT '';

-- Both guards below PRINT their offending rooms as well as SELECTing them. An aborted batch
-- leaves the grids sitting in SSMS's Results tab while the operator is reading the error in
-- Messages, so the Messages half has to be able to stand on its own.
IF @Ambiguous > 0
BEGIN
    DECLARE @AmbiguousList nvarchar(max) = (
        SELECT STRING_AGG(CAST(x.RoomNumber AS nvarchar(max)), N', ') WITHIN GROUP (ORDER BY x.RoomNumber)
        FROM (SELECT TOP (20) RoomNumber FROM #Match WHERE IsNew = 0
              GROUP BY RoomNumber HAVING COUNT(*) > 1 ORDER BY RoomNumber) x);
    PRINT CONCAT('  rooms matching more than one unit: ', @AmbiguousList,
                 CASE WHEN @Ambiguous > 20 THEN CONCAT('  ...and ', @Ambiguous - 20, ' more') ELSE '' END);
    SELECT RoomNumber, UnitsMatched = COUNT(*)
    FROM #Match WHERE IsNew = 0 GROUP BY RoomNumber HAVING COUNT(*) > 1 ORDER BY RoomNumber;
    THROW 50006, 'A workbook row matched more than one project unit. Resolve the duplicates before loading.', 1;
END

IF @Unmatched > 0
BEGIN
    DECLARE @UnmatchedList nvarchar(max) = (
        SELECT STRING_AGG(CAST(x.RoomNumber AS nvarchar(max)), N', ') WITHIN GROUP (ORDER BY x.RoomNumber)
        FROM (SELECT TOP (20) RoomNumber FROM #Match WHERE IsNew = 1 ORDER BY RoomNumber) x);
    PRINT CONCAT('  workbook rooms with no unit: ', @UnmatchedList,
                 CASE WHEN @Unmatched > 20 THEN CONCAT('  ...and ', @Unmatched - 20, ' more') ELSE '' END);

    -- A handful unmatched out of thousands is a data question about those specific rooms; nearly
    -- all unmatched means the room-number FORMAT differs, so show what the project actually holds.
    DECLARE @HeldSample nvarchar(max) = (
        SELECT STRING_AGG(CAST(x.RoomNumber AS nvarchar(max)), N', ')
        FROM (SELECT TOP (20) RoomNumber = ISNULL(pu.RoomNumber, N'(null)')
              FROM appraisal.ProjectUnits pu
              WHERE pu.ProjectId = @ProjectId ORDER BY pu.SequenceNumber) x);
    PRINT CONCAT('  rooms this project holds   : ', @HeldSample);
    PRINT '';

    SELECT TOP (20) m.RoomNumber, m.TowerName, m.FloorText, m.ModelType, m.UsableArea
    FROM #Match m WHERE m.IsNew = 1 ORDER BY m.RoomNumber;

    SELECT TOP (20) pu.RoomNumber, pu.CondoRegistrationNumber, pu.TowerName, pu.Floor, pu.ModelType
    FROM appraisal.ProjectUnits pu
    WHERE pu.ProjectId = @ProjectId
    ORDER BY pu.SequenceNumber;

    IF @CreateMissingUnits = 0
        THROW 50007, 'Not every workbook row matched a unit, and @CreateMissingUnits is 0. Nothing was written. The unmatched rooms are named above (Messages), with fuller detail in the Results tab.', 1;

    -- The ceiling is the whole point of the switch. Five missing rooms out of 1,614 is inventory
    -- the project never had loaded; 1,614 missing means the room numbers are written differently
    -- and creating them all would leave the project holding two parallel sets of every unit.
    IF @Unmatched > @MaxUnitsToCreate
        THROW 50011, 'More workbook rooms are missing than @MaxUnitsToCreate allows. That is a room-number format mismatch, not a data gap — creating them would duplicate the project. Nothing was written.', 1;

    -- Mint the ids, then number the rows on from the highest sequence the project already uses.
    UPDATE #Match SET ProjectUnitId = NEWID() WHERE IsNew = 1;

    DECLARE @MaxSeq int =
        ISNULL((SELECT MAX(SequenceNumber) FROM appraisal.ProjectUnits WHERE ProjectId = @ProjectId), 0);

    ;WITH Numbered AS (
        SELECT SequenceNumber, NewSeq = @MaxSeq + ROW_NUMBER() OVER (ORDER BY RoomNumber)
        FROM #Match WHERE IsNew = 1
    )
    UPDATE Numbered SET SequenceNumber = NewSeq;

    -- Resolve tower and model the way Project.AutoCreateCondoTowersAndModels does: tower by
    -- TowerName, model by (tower, ModelName). RESOLVE ONLY — a missing master row leaves the FK
    -- null and is reported, because inventing ProjectTowers/ProjectModels from a spreadsheet is
    -- worse data than none.
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

-- A room the workbook prices at zero but still insures. Contradictory on its face — an unappraised
-- unit has no appraised value to insure — and it lands in the Decision Summary building-insurance
-- total (SUM of CoverageAmount) while contributing nothing to the appraised total, so the two
-- numbers stop reconciling. Surfaced rather than corrected: only the appraiser knows which of the
-- two figures is the wrong one.
DECLARE @ZeroValueInsured int = (SELECT COUNT(*) FROM #Match WHERE AppraisalValue = 0 AND CoverageAmount > 0);
DECLARE @ZeroValueCover decimal(18,2) = (SELECT ISNULL(SUM(CoverageAmount), 0) FROM #Match WHERE AppraisalValue = 0 AND CoverageAmount > 0);
IF @ZeroValueInsured > 0
BEGIN
    PRINT CONCAT('WARN rooms priced at 0 that still carry fire cover : ', @ZeroValueInsured,
                 ', totalling ', @ZeroValueCover);
    SELECT m.RoomNumber, m.TowerName, m.ModelType, m.UsableArea,
           AppraisedInFile = m.AppraisalValue, FireCoverInFile = m.CoverageAmount,
           CountsTowardInsurance = CASE WHEN m.ProjectModelId IS NOT NULL AND m.IsSold = 0
                                        THEN 'yes' ELSE 'no (no model, or sold)' END
    FROM #Match m WHERE m.AppraisalValue = 0 AND m.CoverageAmount > 0 ORDER BY m.RoomNumber;
END
PRINT '';

IF @NotInWorkbook > 0
BEGIN
    PRINT 'Units in the project that the workbook does not price (first 20):';
    SELECT TOP (20) pu.SequenceNumber, pu.RoomNumber, pu.TowerName, pu.Floor, pu.ModelType, pu.IsSold
    FROM appraisal.ProjectUnits pu
    WHERE pu.ProjectId = @ProjectId
      AND NOT EXISTS (SELECT 1 FROM #Match m WHERE m.ProjectUnitId = pu.Id)
    ORDER BY pu.SequenceNumber;
END

-- Floor gap vs floor disagreement. The first is the parser's silent NULL and is what @FixFloor
-- repairs; the second is a value somebody put there on purpose and needs a human either way.
-- IsNew rows are excluded throughout: they are inserted with the workbook's floor already, so
-- there is nothing to repair and nothing to diff.
DECLARE @FloorNull     int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 0 AND DbFloor IS NULL AND Floor IS NOT NULL);
DECLARE @FloorMismatch int = (SELECT COUNT(*) FROM #Match WHERE IsNew = 0 AND DbFloor IS NOT NULL AND Floor IS NOT NULL AND DbFloor <> Floor);
DECLARE @FloorToWrite  int = CASE @FixFloor WHEN 1 THEN @FloorNull
                                            WHEN 2 THEN @FloorNull + @FloorMismatch
                                            ELSE 0 END;

PRINT CONCAT('Units with Floor NULL that the workbook can fill : ', @FloorNull);
PRINT CONCAT('Units whose stored Floor disagrees with the file : ', @FloorMismatch);
PRINT CONCAT('Floor values this run would write               : ', @FloorToWrite);
IF @FloorNull > 0
BEGIN
    PRINT '  NULL floors the workbook would fill (grouped by the floor as printed in the sheet):';
    SELECT FloorInSheet = m.FloorText, WouldBecome = m.Floor, Units = COUNT(*)
    FROM #Match m WHERE m.IsNew = 0 AND m.DbFloor IS NULL AND m.Floor IS NOT NULL
    GROUP BY m.FloorText, m.Floor ORDER BY m.Floor;
END
IF @FloorMismatch > 0
BEGIN
    PRINT '  Stored floors that disagree with the sheet (written only when @FixFloor = 2):';
    SELECT TOP (50) m.RoomNumber, FloorDb = m.DbFloor, FloorFile = m.Floor, FloorInSheet = m.FloorText
    FROM #Match m WHERE m.IsNew = 0 AND m.DbFloor IS NOT NULL AND m.Floor IS NOT NULL AND m.DbFloor <> m.Floor
    ORDER BY m.RoomNumber;
END
PRINT '';

-- Attribute diff. NOT written by this script — a mismatch here means the units were loaded from a
-- different revision of the price list, and a price is only meaningful against the area it was
-- computed from, so this is a stop-and-ask signal rather than something to fix automatically.
DECLARE @Diffs int = (
    SELECT COUNT(*) FROM #Match m
    WHERE m.IsNew = 0
      AND (ISNULL(m.DbFloor, -1)           <> ISNULL(m.Floor, -1)
       OR ISNULL(m.DbTowerName, N'~')      <> ISNULL(m.TowerName, N'~')
       OR ISNULL(m.DbModelType, N'~')      <> ISNULL(m.ModelType, N'~')
       OR ISNULL(m.DbUsableArea, -1)       <> ISNULL(m.UsableArea, -1)
       OR ISNULL(m.DbSellingPrice, -1)     <> ISNULL(m.SellingPrice, -1)));

PRINT CONCAT('Units whose attributes differ from the workbook : ', @Diffs);
IF @Diffs > 0
BEGIN
    PRINT '  (TowerName / ModelType / UsableArea / SellingPrice — reported only, not written.';
    PRINT '   Floor is listed here too, but is the one column @FixFloor may repair.)';
    SELECT TOP (50)
        m.RoomNumber,
        FloorDb = m.DbFloor,          FloorFile = m.Floor,      FloorFileText = m.FloorText,
        TowerDb = m.DbTowerName,      TowerFile = m.TowerName,
        ModelDb = m.DbModelType,      ModelFile = m.ModelType,
        AreaDb  = m.DbUsableArea,     AreaFile  = m.UsableArea,
        SellDb  = m.DbSellingPrice,   SellFile  = m.SellingPrice
    FROM #Match m
    WHERE m.IsNew = 0
      AND (ISNULL(m.DbFloor, -1)       <> ISNULL(m.Floor, -1)
       OR ISNULL(m.DbTowerName, N'~')  <> ISNULL(m.TowerName, N'~')
       OR ISNULL(m.DbModelType, N'~')  <> ISNULL(m.ModelType, N'~')
       OR ISNULL(m.DbUsableArea, -1)   <> ISNULL(m.UsableArea, -1)
       OR ISNULL(m.DbSellingPrice, -1) <> ISNULL(m.SellingPrice, -1))
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
BEGIN
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
END
PRINT '';

-- ── The appraisal-level summary ───────────────────────────────────────────────
-- Reproduces AppraisalValuationSummaryService.RecomputeAsync's block branch against the state the
-- database WILL be in once this run's price writes land: a unit contributes its workbook figure if
-- the workbook prices it, otherwise whatever its existing price row holds. The membership rules are
-- the app's, not ours — unsold only, ProjectModelId required (INNER JOIN in the original), and a
-- price row required (also an INNER JOIN), which after this run means "in the workbook OR already
-- priced". Computing it this way makes @Apply = 0 predict exactly what @Apply = 1 will store.
-- #Post is the post-patch state of every unit the rollup will count. Two arms, no overlap:
--   1. every workbook row (matched units take the workbook's figures; rows to be created take them
--      too, which is why this cannot be expressed as a query over ProjectUnits alone — those rows
--      do not exist yet at report time)
--   2. the project's other units, at whatever their existing price row holds
-- The app's INNER JOINs are reproduced as membership rules: unsold, a model that belongs to this
-- appraisal's project, and a price row (arm 1 always gets one from this run; arm 2 must already
-- have one). Building it this way is what lets @Apply = 0 predict exactly what @Apply = 1 stores.
SELECT ProjectUnitId    = m.ProjectUnitId,
       ProjectModelId   = m.ProjectModelId,
       AppraisalValue   = m.AppraisalValue,
       CoverageAmount   = m.CoverageAmount,
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

-- ForceSaleRateResolver's chain, in its order. @RateSource is only for the printout.
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
    ORDER BY ppa.Id;                    -- 1:1 in practice; ordered so the pick is deterministic
    IF @ForceSaleRate IS NOT NULL SET @RateSource = 'project pricing assumption';
END

IF @ForceSaleRate IS NULL
BEGIN
    SELECT @ForceSaleRate = TRY_CONVERT(decimal(18,6), sc.Value)
    FROM common.SystemConfigurations sc
    WHERE sc.[Key] = N'ForceSaleRateDefaultPct' AND sc.IsActive = 1;
    IF @ForceSaleRate IS NOT NULL SET @RateSource = 'SystemConfigurations default';
END

-- The resolver treats an out-of-range rate as a config error (an admin typing 0.7 for 70%).
IF @ForceSaleRate IS NULL OR @ForceSaleRate <= 0 OR @ForceSaleRate > 100
BEGIN
    SET @ForceSaleRate = 70;
    SET @RateSource = 'hardcoded 70 fallback';
END

DECLARE @NewAppraised   decimal(18,2)  = @RollupValue;                                -- not rounded, as in UpdateSummary
DECLARE @NewInsurance   decimal(18,2)  = ROUND(@RollupInsurance / 1000.0, 0) * 1000;  -- ROUND() is half-away-from-zero,
                                                                                     -- matching MidpointRounding.AwayFromZero

-- THREE FORCED-SALE FIGURES, and they do not agree. Nothing in the application ever sums
-- ProjectUnitPrices.ForceSellingPrice — the calculator writes it per unit for the Unit Price tab
-- and every total re-derives from "appraised x rate" instead — so the workbook's own column and
-- the app's arithmetic drift apart by whatever the per-unit rounding cost.
--
--   @FsvFromRate    ROUND(grand total x rate / 1000) x 1000
--                   What UpdateSummary stores, and what UpdateForceSaleRateCommandHandler and
--                   SaveDecisionSummaryCommandHandler will re-derive on their next write. Pick this
--                   (@ForcedSaleFrom = 1) and the value is stable.
--   @FsvFromUnitSum SUM of the per-unit ForceSellingPrice
--                   The workbook's own column, rounded per unit by whoever approved it. Truer to the
--                   approved document, but the first touch of the force-sale rate on the Decision
--                   Summary screen silently converts it to @FsvFromRate.
--   @FsvFromModels  SUM over ProjectModels of ROUND(model total x rate / 1000) x 1000
--                   Not selectable — this is what the Decision Summary SCREEN computes and displays
--                   (GetDecisionSummaryQueryHandler's block branch). Printed so the operator can see
--                   what the committee will be looking at next to what is being stored.
DECLARE @FsvFromRate    decimal(18,2) =
    ROUND(CAST(@RollupValue AS decimal(38,6)) * @ForceSaleRate / 100.0 / 1000.0, 0) * 1000;

DECLARE @FsvFromUnitSum decimal(18,2) = (SELECT ISNULL(SUM(ForceSellingPrice), 0) FROM #Post);

DECLARE @FsvFromModels decimal(18,2);
SELECT @FsvFromModels = ISNULL(SUM(ROUND(CAST(mt.ModelTotal AS decimal(38,6)) * @ForceSaleRate / 100.0 / 1000.0, 0) * 1000), 0)
FROM (SELECT ProjectModelId, ModelTotal = SUM(AppraisalValue) FROM #Post GROUP BY ProjectModelId) mt;

DECLARE @NewForcedSale decimal(18,2) =
    CASE @ForcedSaleFrom WHEN 2 THEN @FsvFromUnitSum ELSE @FsvFromRate END;

-- ValuationApproach: one distinct selected approach across the project's ProjectModel analyses,
-- else 'Combined'. SubjectType 1 = PricingAnalysisSubjectType.ProjectModel (stored as int).
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

-- ValuationDate. Never overwritten on an existing row. Only invented for a new row when a real
-- appointment can supply it — never SYSDATETIME().
DECLARE @VaId            uniqueidentifier;
DECLARE @VaDate          datetime2;
DECLARE @SeedDate        datetime2;
DECLARE @SummaryAction   varchar(20);

SELECT @VaId = va.Id, @VaDate = va.ValuationDate
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
PRINT CONCAT('  force-sale rate              : ', @ForceSaleRate, '%  from ', @RateSource);
PRINT CONCAT('  forced sale, total x rate    : ', @FsvFromRate,
             CASE WHEN @ForcedSaleFrom = 1 THEN '   <-- storing this' ELSE '' END);
PRINT CONCAT('  forced sale, SUM of units    : ', @FsvFromUnitSum,
             CASE WHEN @ForcedSaleFrom = 2 THEN '   <-- storing this' ELSE '' END);
PRINT CONCAT('  forced sale, Decision Summary screen shows : ', @FsvFromModels);
PRINT CONCAT('  spread between them          : ', ABS(@FsvFromRate - @FsvFromUnitSum),
             '  (', CONVERT(decimal(10,5), CASE WHEN @RollupValue = 0 THEN 0
                  ELSE ABS(@FsvFromRate - @FsvFromUnitSum) * 100.0 / @RollupValue END), '% of the appraised total)');
PRINT CONCAT('  valuation approach           : ', @Approach);
PRINT CONCAT('  action                       : ',
             CASE WHEN @SyncValuationSummary = 0 THEN 'disabled (@SyncValuationSummary = 0)' ELSE @SummaryAction END);
IF @SummaryAction = 'SKIP (no date)'
    PRINT '  ^ no ValuationAnalyses row and no non-cancelled appointment to date one from. The summary'
        + ' will be left alone rather than stamping today on the appraisal; prices still load.';

-- LEFT JOIN from a one-row derived table so the "after" figures still print when there is no
-- ValuationAnalyses row yet — that is exactly the run where the operator needs to see them.
SELECT
    AppraisedValueBefore  = va.AppraisedValue,    AppraisedValueAfter  = @NewAppraised,
    ForcedSaleValueBefore = va.ForcedSaleValue,   ForcedSaleValueAfter = @NewForcedSale,
    InsuranceValueBefore  = va.InsuranceValue,    InsuranceValueAfter  = @NewInsurance,
    ApproachBefore        = va.ValuationApproach, ApproachAfter        = @Approach,
    ValuationDateUsed     = COALESCE(va.ValuationDate, @SeedDate),
    WorkbookTotalAllUnits = (SELECT SUM(AppraisalValue) FROM #Src)
FROM (SELECT 1 AS OneRow) d
LEFT JOIN appraisal.ValuationAnalyses va ON va.AppraisalId = @ProjectAppraisalId;

-- The one thing a SQL write cannot do: republish AppraisalValueChangedIntegrationEvent. Show the
-- workflow state that the event would have refreshed so the drift is a decision, not a surprise.
PRINT '  Workflow state this script does NOT touch:';
SELECT WorkflowAppraisalValue = TRY_CONVERT(decimal(18,2), JSON_VALUE(wi.Variables, '$.appraisalValue')),
       NewAppraisedValue      = @NewAppraised,
       WorkflowStatus         = wi.Status,
       CurrentActivity        = wi.CurrentActivityId
FROM appraisal.Appraisals a
JOIN workflow.WorkflowInstances wi ON wi.CorrelationId = CAST(a.RequestId AS nvarchar(50))
WHERE a.Id = @ProjectAppraisalId;

SELECT LiveMeetingQueueItem = mqi.Id, mqi.Status, QueuedAppraisalValue = mqi.AppraisalValue,
       NewAppraisedValue = @NewAppraised
FROM workflow.MeetingQueueItems mqi
WHERE mqi.AppraisalId = @ProjectAppraisalId AND mqi.Status <> 'Released';
PRINT '';

-- ── 5. Write ──────────────────────────────────────────────────────────────────
IF @Apply = 0
BEGIN
    PRINT 'REPORT ONLY — nothing was written. Set @Apply = 1 to apply.';
    RETURN;
END

BEGIN TRAN;

-- Units first: the price rows below FK to them, and a unit's floor and area are part of what its
-- price means, so if any of this fails none of it should land.
-- UploadBatchId gets the same system sentinel the bank-file load used, so these rows are
-- identifiable later as script-loaded rather than uploaded. There is no FK on that column.
DECLARE @UnitsCreated int = 0;
IF @CreateMissingUnits = 1 AND EXISTS (SELECT 1 FROM #Match WHERE IsNew = 1)
BEGIN
    INSERT appraisal.ProjectUnits
        (Id, ProjectId, UploadBatchId, SequenceNumber, RoomNumber, TowerName, ModelType,
         Floor, UsableArea, SellingPrice, ProjectTowerId, ProjectModelId, IsSold,
         CreatedAt, CreatedBy)
    SELECT m.ProjectUnitId, @ProjectId, '00000000-0000-0000-0000-0000CA5B0001', m.SequenceNumber,
           m.RoomNumber, m.TowerName, m.ModelType,
           m.Floor, m.UsableArea, m.SellingPrice, m.ProjectTowerId, m.ProjectModelId, 0,
           SYSDATETIME(), 'SYSTEM'
    FROM #Match m
    WHERE m.IsNew = 1;

    SET @UnitsCreated = @@ROWCOUNT;
END

-- Floor repair, for units that already existed. Only ever widens what is known — @FixFloor = 1
-- touches NULLs alone.
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

-- Flags default to 0 and AdjustPriceLocation / PriceIncrementPerFloor / LandIncreaseDecreaseAmount
-- stay NULL: the workbook prices the unit outright, it does not decompose the figure.
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

-- Re-read the rollup from the tables now that the prices have landed. It must equal the projection
-- the report printed; if it does not, the projection logic is wrong and this run should not be
-- allowed to store a number nobody was shown. XACT_ABORT rolls the whole thing back.
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
    -- ValuationDate is deliberately absent from this SET list. See the header.
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
    -- Id is omitted on purpose: the column carries a newsequentialid() default, unlike
    -- ProjectUnitPrices.Id which has none and is supplied above.
    INSERT appraisal.ValuationAnalyses
        (AppraisalId, ValuationApproach, ValuationDate, AppraisedValue, ForcedSaleValue,
         InsuranceValue, Currency, CreatedAt, CreatedBy)
    VALUES
        (@ProjectAppraisalId, @Approach, @SeedDate, @NewAppraised, @NewForcedSale,
         @NewInsurance, N'THB', SYSDATETIME(), 'SYSTEM');

    SET @SummaryWritten = 'inserted';
END

COMMIT;

PRINT CONCAT('Applied: ', @UnitsCreated, ' units created, ', @Updated, ' price rows updated, ',
             @Inserted, ' price rows inserted, ', @FloorWritten, ' unit floors repaired.');
PRINT CONCAT('ValuationAnalyses: ', @SummaryWritten,
             CASE WHEN @SummaryWritten = 'not written' THEN '' ELSE CONCAT(
                  '  appraised=', @NewAppraised,
                  '  forcedSale=', @NewForcedSale,
                  '  insurance=', @NewInsurance) END);
PRINT 'Workflow was NOT notified — appraisalValue in WorkflowInstance.Variables and any live'
    + ' MeetingQueueItem still hold the old figure. See the report above.';
PRINT 'Reminder: do NOT press Calculate on the Unit Price tab, and do NOT re-upload the units Excel.';

DROP TABLE #Post;
DROP TABLE #Match;
DROP TABLE #Src;
