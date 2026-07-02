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

    -- Engagement-time history fields (frozen at creation — historically accurate)
    e.AppraisedCollateralType,
    e.LandAreaInSqWa,
    e.AppraisalValue,

    -- Master metadata (denormalised for fast listing — no snapshot column here)
    m.CollateralType,
    m.OwnerName,

    -- Building type codes aggregated as CSV for FE display (without extra round-trip)
    -- Supports multi-building masters: "01,02" means Townhouse + Commercial on same title.
    (
        SELECT STRING_AGG(ceb.BuildingTypeCode, ',') WITHIN GROUP (ORDER BY ceb.Sequence)
        FROM collateral.CollateralEngagementBuildings ceb
        WHERE ceb.EngagementId = e.Id
    ) AS BuildingTypeCodes,

    -- Land identity for filtering (NULL when not a Land/LB type engagement)
    ld.Province         AS Land_Province,
    ld.District         AS Land_District,
    ld.SubDistrict      AS Land_SubDistrict,
    ld.TitleNumber      AS Land_TitleNumber,
    ld.GeoPoint         AS Land_GeoPoint,
    ld.Latitude         AS Land_Latitude,
    ld.Longitude        AS Land_Longitude,

    -- Condo identity for filtering (NULL when not a Condo type engagement)
    cd.Province         AS Condo_Province,
    cd.GeoPoint         AS Condo_GeoPoint,
    cd.Latitude         AS Condo_Latitude,
    cd.Longitude        AS Condo_Longitude,

    -- Leasehold identity for filtering (NULL when not a Leasehold type engagement)
    lhd.LeaseRegistrationNo AS Lh_LeaseRegistrationNo

FROM collateral.CollateralEngagements e
INNER JOIN collateral.CollateralMasters m ON m.Id = e.CollateralMasterId
LEFT JOIN  collateral.LandDetails       ld  ON ld.CollateralMasterId  = m.Id
LEFT JOIN  collateral.CondoDetails      cd  ON cd.CollateralMasterId  = m.Id
LEFT JOIN  collateral.LeaseholdDetails  lhd ON lhd.CollateralMasterId = m.Id
WHERE m.IsDeleted = 0
  AND m.IsMaster = 1   -- defense-in-depth: aliases never have engagements (runtime-guarded), but filter here to prevent leak from any future code path
