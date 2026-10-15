-- ============================================================
-- Data fix: a figure whose toggle says it does not apply is blank, not 0.
--
-- The property forms used 0 as the stand-in for "Distance from electricity" while the plot is not far
-- from electricity, and for "Encroaching area" while the building does not encroach. Opening a record
-- with the figure blank put that 0 in the form, and the next save stored it — so most records hold a 0
-- nobody entered. The forms now use a blank stand-in; this clears the zeros already written, so the
-- screens, Data Correction and reports read the same blank everywhere.
--
-- Only an explicit No (= 0) and an exact 0. A NULL toggle is unknown, and a non-zero figure under No is
-- something someone typed; both are left as they are.
--
-- Not written to the Data Correction history: like the other data fixes, this is a one-time cleanup of
-- a value the screens put there, not a correction someone made.
-- Idempotent: a second run changes nothing.
-- ============================================================

DECLARE @Land INT, @Building INT, @ProjectLand INT, @ProjectModel INT;

UPDATE [appraisal].[LandAppraisalDetails]
SET    [ElectricityDistance] = NULL
WHERE  [HasElectricity] = 0 AND [ElectricityDistance] = 0;
SET @Land = @@ROWCOUNT;

UPDATE [appraisal].[BuildingAppraisalDetails]
SET    [EncroachingOthersArea] = NULL
WHERE  [IsEncroachingOthers] = 0 AND [EncroachingOthersArea] = 0;
SET @Building = @@ROWCOUNT;

-- Block projects render the same two fields.
UPDATE [appraisal].[ProjectLands]
SET    [ElectricityDistance] = NULL
WHERE  [HasElectricity] = 0 AND [ElectricityDistance] = 0;
SET @ProjectLand = @@ROWCOUNT;

UPDATE [appraisal].[ProjectModels]
SET    [EncroachingOthersArea] = NULL
WHERE  [IsEncroachingOthers] = 0 AND [EncroachingOthersArea] = 0;
SET @ProjectModel = @@ROWCOUNT;

-- What moved, for the deployment log.
SELECT @Land AS LandCleared, @Building AS BuildingCleared, @ProjectLand AS ProjectLandCleared,
       @ProjectModel AS ProjectModelCleared;
