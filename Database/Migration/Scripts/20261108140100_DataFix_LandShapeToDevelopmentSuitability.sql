-- ============================================================
-- Data fix: LandShapeType used to hold the development-suitability options (codes 01-03) as well as the shapes.
-- Move them to the new DevelopmentSuitabilityType column (added by AddPropertyDetailBookFields; DbUp runs after
-- every EF migration) and tidy the legacy free-text values. Tables: appraisal.LandAppraisalDetails and
-- appraisal.ProjectLands.
--
--   LandShapeType 01 / 02 / 03                       -> DevelopmentSuitabilityType same code, LandShapeType NULL
--   'A shape with soil, space is appropriate for
--    development made a very beneficial' (legacy)    -> DevelopmentSuitabilityType '01', LandShapeType NULL
--   'Rectangular' (legacy)                           -> LandShapeType '05' (สี่เหลี่ยมผืนผ้า)
--   ''                                               -> NULL
--
-- Idempotent: after the first run no row matches any WHERE clause. One transaction.
-- ============================================================
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @landMoved int, @landLegacy int, @landRect int, @landBlank int,
        @projMoved int, @projBlank int;

BEGIN TRANSACTION;

-- Land / land-and-building
UPDATE appraisal.LandAppraisalDetails
SET    DevelopmentSuitabilityType = LandShapeType, LandShapeType = NULL
WHERE  LandShapeType IN (N'01', N'02', N'03');
SET @landMoved = @@ROWCOUNT;

UPDATE appraisal.LandAppraisalDetails
SET    DevelopmentSuitabilityType = N'01', LandShapeType = NULL
WHERE  LandShapeType = N'A shape with soil, space is appropriate for development made a very beneficial';
SET @landLegacy = @@ROWCOUNT;

UPDATE appraisal.LandAppraisalDetails SET LandShapeType = N'05' WHERE LandShapeType = N'Rectangular';
SET @landRect = @@ROWCOUNT;

UPDATE appraisal.LandAppraisalDetails SET LandShapeType = NULL WHERE LandShapeType = N'';
SET @landBlank = @@ROWCOUNT;

-- Project (block) land
UPDATE appraisal.ProjectLands
SET    DevelopmentSuitabilityType = LandShapeType, LandShapeType = NULL
WHERE  LandShapeType IN (N'01', N'02', N'03');
SET @projMoved = @@ROWCOUNT;

UPDATE appraisal.ProjectLands SET LandShapeType = NULL WHERE LandShapeType = N'';
SET @projBlank = @@ROWCOUNT;

COMMIT TRANSACTION;

-- What moved, for the deployment log.
SELECT @landMoved AS LandMoved01to03, @landLegacy AS LandLegacyTextToSuitability01, @landRect AS LandRectangularTo05,
       @landBlank AS LandBlankToNull, @projMoved AS ProjectLandMoved01to03, @projBlank AS ProjectLandBlankToNull;
GO
