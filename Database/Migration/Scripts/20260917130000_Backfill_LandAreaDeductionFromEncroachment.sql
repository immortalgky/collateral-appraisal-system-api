-- ============================================================
-- Backfill: carry the old single encroachment figure into the new deduction rows.
--
-- Until now appraisal.LandAppraisalDetails.EncroachmentArea was descriptive only — nothing ever
-- subtracted it. From this release the appraised area is registered area less the deductions, so a
-- property that already records an encroached area gets one row for it rather than silently losing
-- what the appraiser wrote.
--
-- Reason '99' (สาเหตุอื่น — this codebase's "other" convention) because the old field never
-- captured which kind of encroachment it was;
-- the original remark rides along so the detail is not lost. The appraiser can split it into
-- properly-typed rows the next time they open the property.
--
-- CONSEQUENCE, on purpose: an appraisal that carries an encroached area starts pricing on the
-- smaller net area from its next save. Existing saved prices are untouched — nothing is recomputed
-- here — but they will move when the property or its pricing is saved again. To ship without that
-- movement, skip this script; those properties then show zero deduction rows until someone enters
-- them by hand.
--
-- Ordering: DbUp runs after every EF migration, so appraisal.LandAreaDeductions already exists.
-- Idempotent: a property that already has any deduction row is left alone.
-- ============================================================

INSERT INTO appraisal.LandAreaDeductions (Id, LandAppraisalDetailId, ReasonCode, ReasonOther, AreaInSqWa, Remark)
SELECT NEWID(), lad.Id, N'99', NULL, lad.EncroachmentArea, lad.EncroachmentRemark
FROM   appraisal.LandAppraisalDetails lad
WHERE  lad.IsEncroached = 1
  AND  lad.EncroachmentArea IS NOT NULL
  AND  lad.EncroachmentArea > 0
  AND  NOT EXISTS (SELECT 1 FROM appraisal.LandAreaDeductions d
                   WHERE d.LandAppraisalDetailId = lad.Id);

-- Bring the stored per-property total in line with the rows just inserted. The domain maintains
-- this column on every write; this is the one time it has to be set from the outside.
UPDATE lad
SET    lad.DeductedAreaInSqWa = x.Total
FROM   appraisal.LandAppraisalDetails lad
JOIN   (SELECT d.LandAppraisalDetailId, SUM(ISNULL(d.AreaInSqWa, 0)) AS Total
        FROM   appraisal.LandAreaDeductions d
        GROUP BY d.LandAppraisalDetailId) x
       ON x.LandAppraisalDetailId = lad.Id
WHERE  ISNULL(lad.DeductedAreaInSqWa, -1) <> x.Total;

-- What moved, for the deployment log.
SELECT COUNT(*) AS PropertiesWithDeductions,
       SUM(ISNULL(lad.DeductedAreaInSqWa, 0)) AS TotalDeductedSqWa
FROM   appraisal.LandAppraisalDetails lad
WHERE  lad.DeductedAreaInSqWa > 0;
