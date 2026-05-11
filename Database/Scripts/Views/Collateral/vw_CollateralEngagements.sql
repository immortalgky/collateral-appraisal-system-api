CREATE OR ALTER VIEW collateral.vw_CollateralEngagements AS
SELECT
    -- Engagement identity
    e.Id,
    e.CollateralMasterId,
    e.AppraisalId,
    e.AppraisalNumber,
    e.RequestId,
    e.RequestNumber,
    -- PropertyId dropped (PR-4): engagement is now per-appraisal. Members live in Snapshot.
    -- AppraisedValue dropped (PR-4): values live on master detail rows and in Snapshot.
    e.AppraisalType,
    e.AppraisalDate,
    e.AppraiserUserId,
    e.AppraisalCompanyId,
    e.AppraisalCompanyName,
    e.ConstructionInspectionFeeAmount,
    e.CreatedAt,

    -- Master metadata (denormalised for fast listing — no snapshot column here)
    m.CollateralType,
    m.OwnerName
FROM collateral.CollateralEngagements e
INNER JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
WHERE m.IsDeleted = 0
  AND m.IsMaster = 1   -- defense-in-depth: aliases never have engagements (runtime-guarded), but filter here to prevent leak from any future code path
