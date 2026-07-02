-- =============================================================================
-- Organization reference data (AS400 / Silverlake Teller)
--   auth.Departments  <- LP4PAR4 (Department Code Parameters)
--   auth.CostCenters  <- GLPAR7  (G/L Cost Center Record Format)
--   auth.Officers     <- SSOFFR  (Officer Parameter File)
--
-- IDEMPOTENT upsert-by-code (MERGE). This is the SHAPE the future periodic AS400
-- sync should use, but it is NOT a complete sync: the real sync must additionally
-- set IsActive (to deactivate rows dropped by AS400) and LastSyncedAt in both the
-- WHEN MATCHED and WHEN NOT MATCHED branches. This seed leaves both untouched on
-- update on purpose. Run order: parents (Departments, CostCenters) first.
--
-- NOTE: the rows below are PLACEHOLDER examples only. The screenshots are AS400
-- field layouts, not data. Replace these VALUES with the real AS400 extract
-- (codes are zero-padded fixed-width strings, e.g. N'001').
-- =============================================================================

-- ---------------------------------------------------------------------------
-- Departments (LP4COD, LP4DVC, LP4DSC)
-- ---------------------------------------------------------------------------
MERGE auth.Departments AS target
USING (VALUES
    (N'001', N'001', N'Example Department 001'),
    (N'002', N'001', N'Example Department 002')
) AS source (Code, DivisionCode, Description)
ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET DivisionCode = source.DivisionCode,
               Description  = source.Description
WHEN NOT MATCHED THEN
    INSERT (Code, DivisionCode, Description, IsActive)
    VALUES (source.Code, source.DivisionCode, source.Description, 1);
GO

-- ---------------------------------------------------------------------------
-- Cost Centers (G7CNTR, G7DESC, G7TEXT)
-- ---------------------------------------------------------------------------
MERGE auth.CostCenters AS target
USING (VALUES
    (N'001', N'Example Cost Center 001', NULL),
    (N'002', N'Example Cost Center 002', NULL)
) AS source (Code, Description, [Text])
ON target.Code = source.Code
WHEN MATCHED THEN
    UPDATE SET Description = source.Description,
               [Text]      = source.[Text]
WHEN NOT MATCHED THEN
    INSERT (Code, Description, [Text], IsActive)
    VALUES (source.Code, source.Description, source.[Text], 1);
GO

-- ---------------------------------------------------------------------------
-- Officers (SSOOFF, SSOBRN, SSOIDN, SSONAM, SSOSNA, SSONTH, SSDDEPT)
--   CostCenterCode (SSONTH) is 8-digit in this file; reconcile against the
--   3-digit CostCenters master before treating them as the same key.
-- ---------------------------------------------------------------------------
-- The example CostCenterCode below (N'00000001', 8-digit SSONTH form) intentionally
-- does NOT match the 3-digit CostCenters master (N'001'/N'002') — it demonstrates the
-- documented width mismatch, not a data bug.
MERGE auth.Officers AS target
USING (VALUES
    (N'001', N'001', N'EMP0000001', N'Example Officer One', N'Officer One', N'00000001', N'001')
) AS source (OfficerCode, BranchNumber, OfficerId, Name, ShortName, CostCenterCode, DepartmentCode)
ON target.OfficerCode = source.OfficerCode
WHEN MATCHED THEN
    UPDATE SET BranchNumber   = source.BranchNumber,
               OfficerId      = source.OfficerId,
               Name           = source.Name,
               ShortName      = source.ShortName,
               CostCenterCode = source.CostCenterCode,
               DepartmentCode = source.DepartmentCode
WHEN NOT MATCHED THEN
    INSERT (OfficerCode, BranchNumber, OfficerId, Name, ShortName, CostCenterCode, DepartmentCode, IsActive)
    VALUES (source.OfficerCode, source.BranchNumber, source.OfficerId, source.Name,
            source.ShortName, source.CostCenterCode, source.DepartmentCode, 1);
GO
