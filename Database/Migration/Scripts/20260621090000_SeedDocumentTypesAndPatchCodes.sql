-- =============================================================================
-- Consolidate DocumentType onto the general Parameter seed
--
-- Background: parameter.DocumentTypes was previously seeded by a C# seeder
-- (DocumentRequirementDataSeed) using mnemonic codes (APP_FORM, TITLE_DEED, ...).
-- That C# seeder has been removed. The canonical source is now the general
-- parameter seed group 'DocumentType' (codes D001-D041), populated by
-- 20260317002600_SeedData_GeneralParameter.sql.
--
-- This script (runs AFTER that seed):
--   Section A  re-seeds parameter.DocumentTypes from the Parameters group.
--   Section B  patches stored mnemonic codes on document tables -> D0xx.
--
-- NOTE: DocumentRequirements re-seeding is intentionally NOT included here yet
-- (pending requirement details). Section A clears the legacy DocumentRequirements
-- rows (so the legacy DocumentTypes rows can be removed); the table is left empty
-- until a follow-up script seeds the new requirements against the D0xx codes.
--
-- The mnemonic -> D0xx mapping is BEST-EFFORT (the two schemes do not align
-- 1:1). Several mnemonics have no clean equivalent and fall to D041 (Others),
-- which also collapses some distinct requirements onto one code. Review the
-- @map / @req contents below before applying.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Pre-step: ensure D042/D043 exist in the Parameters 'DocumentType' group.
-- The base seed (20260317002600) now includes them for fresh installs, but on
-- databases where that script already ran (journaled) it will NOT re-run, so
-- insert any missing rows here. Idempotent via NOT EXISTS.
-- -----------------------------------------------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
SELECT v.[group], v.[country], v.[language], v.[code], v.[description], v.[isactive], v.[seqno]
FROM (VALUES
    (N'DocumentType', N'TH', N'EN', N'D042', N'Construction Progress Inspection Summary Report', 1, 42),
    (N'DocumentType', N'TH', N'TH', N'D042', N'รายงานสรุปผลการตรวจสอบงานก่อสร้าง', 1, 42),
    (N'DocumentType', N'TH', N'EN', N'D043', N'Property Valuation Summary Report', 1, 43),
    (N'DocumentType', N'TH', N'TH', N'D043', N'รายงานสรุปผลการประเมินราคาทรัพย์สิน', 1, 43)
) v ([group], [country], [language], [code], [description], [isactive], [seqno])
WHERE NOT EXISTS (
    SELECT 1 FROM parameter.Parameters p
    WHERE p.[group] = v.[group] AND p.[country] = v.[country]
      AND p.[language] = v.[language] AND p.[code] = v.[code]
);
GO

-- -----------------------------------------------------------------------------
-- Section A: re-seed parameter.DocumentTypes from the Parameters 'DocumentType'
-- group. Legacy mnemonic rows are removed first (cascade clears dependent
-- requirements). Guarded so a re-run is a no-op.
--
-- Category is derived from the DocumentGroupId of the source document master
-- (the codes are contiguous per group):
--   Group 1     D001                            -> 'VAL_REPORT'  Complete Valuation Report
--   Group 2 + 4 D002-D012, D039-D043            -> 'VAL_DOC'     Collateral Valuation Document
--   Group 3     D013-D038                       -> 'SUBMIT_DOC'  Submission Documents for Valuation
-- (D042/D043 belong to group 2 and are added to the Parameters seed above.)
-- (D042-D043 are not part of the Parameters 'DocumentType' seed yet; if added
--  later, extend the CASE below with their group.)
-- -----------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM parameter.DocumentTypes WHERE [Code] LIKE N'D[0-9][0-9][0-9]')
BEGIN
    DELETE FROM parameter.DocumentRequirements;
    DELETE FROM parameter.DocumentTypes;

    INSERT INTO parameter.DocumentTypes
        ([Id], [Code], [Name], [Description], [Category], [IsActive], [SortOrder], [CreatedAt], [CreatedBy])
    SELECT
        NEWID(),
        p.[code],
        p.[description],
        NULL,
        CASE
            WHEN p.[code] = N'D001'                      THEN N'VAL_REPORT'
            WHEN p.[code] BETWEEN N'D002' AND N'D012'    THEN N'VAL_DOC'
            WHEN p.[code] BETWEEN N'D013' AND N'D038'    THEN N'SUBMIT_DOC'
            WHEN p.[code] BETWEEN N'D039' AND N'D043'    THEN N'VAL_DOC'
            ELSE NULL
        END,
        1,
        p.[seqno],
        SYSUTCDATETIME(),
        N'SYSTEM'
    FROM parameter.Parameters p
    WHERE p.[group] = N'DocumentType' AND p.[language] = N'EN' AND p.[isactive] = 1;
END
GO

-- -----------------------------------------------------------------------------
-- Section B: patch stored mnemonic document-type codes -> D0xx.
--
-- PRE-CHECK before applying (confirm each table actually holds mnemonic values
-- so unrelated codes are not touched). The UPDATEs only match exact mnemonics
-- in @map, so a table without mnemonics is a safe no-op:
--   SELECT DISTINCT DocumentType FROM request.RequestDocuments;
--   SELECT DISTINCT DocumentType FROM request.RequestTitleDocuments;
--   SELECT DISTINCT DocumentType FROM document.Documents;
--   SELECT DISTINCT DocumentType FROM collateral.CollateralDocuments;
-- -----------------------------------------------------------------------------
DECLARE @map TABLE (OldCode nvarchar(50) NOT NULL PRIMARY KEY, NewCode nvarchar(20) NOT NULL);

INSERT INTO @map (OldCode, NewCode) VALUES
    -- Confident matches
    (N'ID_COPY',       N'D015'),  -- Identification Card
    (N'HOUSE_REG',     N'D019'),  -- House Registration (Homeowner)
    (N'TITLE_DEED',    N'D013'),  -- Original Size Ownership Document
    (N'AERIAL_PHOTO',  N'D005'),  -- Aerial Photograph Map
    (N'BLDG_PERMIT',   N'D017'),  -- Building Permit
    (N'VEH_REG',       N'D028'),  -- Vehicle Registration Manual
    (N'MAC_INVOICE',   N'D030'),  -- Purchase Order (Invoice)
    (N'SHIP_CERT',     N'D033'),  -- Boat Registration Certificate
    (N'LESSOR_ID',     N'D015'),  -- Identification Card (collides with ID_COPY)
    -- Weak / approximate matches
    (N'CONDO_TITLE',   N'D013'),  -- Original Size Ownership Document (collides with TITLE_DEED)
    (N'FLOOR_PLAN',    N'D009'),  -- Architectural Plans
    (N'SURVEY_MAP',    N'D007'),  -- Land Layout Plan
    (N'APP_FORM',      N'D035'),  -- Property Survey & Valuation Appointment Request
    (N'MAC_SPEC',      N'D031'),  -- Machinery Operation Manual
    -- No clean equivalent -> D041 (Others)
    (N'POA',           N'D041'),
    (N'COMPANY_REG',   N'D041'),
    (N'COMPANY_AFF',   N'D041'),
    (N'LAND_USE',      N'D041'),
    (N'GPS_COORD',     N'D041'),
    (N'BLDG_CERT',     N'D041'),
    (N'STRUCT_CALC',   N'D041'),
    (N'MEP_PLAN',      N'D041'),
    (N'CONDO_REG',     N'D041'),
    (N'JURISTIC_DOC',  N'D041'),
    (N'COMMON_FEE',    N'D041'),
    (N'CONDO_RULES',   N'D041'),
    (N'VEH_TAX',       N'D041'),
    (N'VEH_INSPECT',   N'D041'),
    (N'VEH_INSURANCE', N'D041'),
    (N'SHIP_CLASS',    N'D041'),
    (N'SHIP_SURVEY',   N'D041'),
    (N'SHIP_INSURANCE',N'D041'),
    (N'MAC_MAINT',     N'D041'),
    (N'MAC_CERT',      N'D041'),
    (N'MAC_WARRANTY',  N'D041'),
    (N'LEASE_AGR',     N'D041'),
    (N'LEASE_REG',     N'D041');

UPDATE x SET x.[DocumentType] = m.NewCode
FROM request.RequestDocuments x
INNER JOIN @map m ON x.[DocumentType] = m.OldCode;

UPDATE x SET x.[DocumentType] = m.NewCode
FROM request.RequestTitleDocuments x
INNER JOIN @map m ON x.[DocumentType] = m.OldCode;

UPDATE x SET x.[DocumentType] = m.NewCode
FROM document.Documents x
INNER JOIN @map m ON x.[DocumentType] = m.OldCode;

UPDATE x SET x.[DocumentType] = m.NewCode
FROM collateral.CollateralDocuments x
INNER JOIN @map m ON x.[DocumentType] = m.OldCode;
GO
