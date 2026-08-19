-- Read model for the collateral master screens (catalog, lookup, get-by-id).
--
-- SOURCING RULE: anything that means "as at the latest appraisal" — the money figures, the
-- appraisal identifiers, the construction status — comes from the latest CollateralEngagement,
-- never from the xxxDetails rows.
--
-- Why: a detail row is a latest-WRITE-wins cache. CollateralBackfillJob walks appraisals
-- oldest-first, and an older appraisal that completes late overwrites a newer one's values with
-- no error and no signal. The engagement table keeps one immutable row per appraisal, so ordering
-- by AppraisalDate gives the genuinely latest figures. Both were populated from the same upstream
-- in the same transaction, so this is a change of source, not of value.
--
-- The per-type aliases (Land_*, Condo_*, Lh_*, Machine_*) are kept verbatim because the frontend
-- COALESCEs across all four (useReappraisalPrefill.ts, CollateralLookupBanner.tsx). Feeding the
-- same engagement-derived value into each is safe: a master carries exactly one detail type, and
-- the query handlers only build the DTO for the type whose detail row exists.
CREATE OR ALTER VIEW collateral.vw_CollateralMasters AS
WITH LatestEngagement AS (
    SELECT
        e.CollateralMasterId,
        e.AppraisalId,
        e.AppraisalNumber,
        e.AppraisalDate,
        e.AppraisalValue,
        e.IsUnderConstruction,
        e.ConstructionProgressPercent,
        COUNT(*) OVER (PARTITION BY e.CollateralMasterId) AS EngagementCount,
        -- CreatedAt then Id break ties so the row chosen is stable across runs when two
        -- appraisals share a date. Uses IX_CollateralEngagements_Master_Date directly.
        ROW_NUMBER() OVER (
            PARTITION BY e.CollateralMasterId
            ORDER BY     e.AppraisalDate DESC, e.CreatedAt DESC, e.Id DESC) AS rn
    FROM collateral.CollateralEngagements e
)
SELECT
    -- Master identity
    m.Id,
    m.CollateralType,
    m.OwnerName,
    m.IsDeleted,
    m.CreatedAt,
    m.CreatedBy,
    m.UpdatedAt,
    m.UpdatedBy,

    -- Engagement-derived (latest appraisal for this master)
    le.EngagementCount,
    le.AppraisalDate                    AS LastAppraisedDate,
    le.AppraisalValue                   AS LastAppraisedValue,

    -- Construction status. Value-weighted across every inspected building; the LandDetails columns
    -- these replace read a single property's inspection and were false/NULL on every dev row even
    -- where buildings were genuinely part-built.
    le.IsUnderConstruction              AS IsUnderConstructionAtLastAppraisal,
    le.ConstructionProgressPercent      AS OverallConstructionProgressPercent,

    -- AS400 hold state. Read straight off the master: AS400 keys collateral rather than appraisals,
    -- so this is the current state of the physical thing, maintained by the nightly
    -- HOST_COLLATERAL_LINK feed. Column names are unchanged from when the same answer was derived
    -- from the latest engagement, so no caller had to be touched.
    m.HostCollateralId                  AS CurrentHostCollateralId,
    CASE WHEN m.HostCollateralId IS NOT NULL AND m.IsRedeemed = 0
         THEN 1 ELSE 0 END              AS IsPledged,
    m.RedeemedDate,

    -- Land-specific columns (NULL when not Land type)
    ld.LandOfficeCode          AS Land_LandOfficeCode,
    ld.Province                AS Land_Province,
    ld.District                AS Land_District,
    ld.SubDistrict             AS Land_SubDistrict,
    ld.TitleType               AS Land_TitleType,
    ld.TitleNumber             AS Land_TitleNumber,
    ld.SurveyNumber            AS Land_SurveyNumber,
    ld.LandParcelNumber        AS Land_LandParcelNumber,
    ld.Street                  AS Land_Street,
    ld.Village                 AS Land_Village,
    ld.Latitude                AS Land_Latitude,
    ld.Longitude               AS Land_Longitude,
    ld.GeoPoint                AS Land_GeoPoint,
    ld.LandShapeType           AS Land_LandShapeType,
    ld.LandZoneType            AS Land_LandZoneType,
    ld.UrbanPlanningType       AS Land_UrbanPlanningType,
    ld.AccessRoadWidth         AS Land_AccessRoadWidth,
    ld.RoadFrontage            AS Land_RoadFrontage,
    ld.LandArea                AS Land_LandArea,
    le.AppraisalId             AS Land_LastAppraisalId,
    le.AppraisalNumber         AS Land_LastAppraisalNumber,
    le.AppraisalDate           AS Land_LastAppraisedDate,
    le.AppraisalValue          AS Land_AppraisalValue,

    -- Condo-specific columns (NULL when not Condo type)
    cd.LandOfficeCode          AS Condo_LandOfficeCode,
    cd.CondoRegistrationNumber AS Condo_CondoRegistrationNumber,
    cd.BuildingNumber          AS Condo_BuildingNumber,
    cd.FloorNumber             AS Condo_FloorNumber,
    cd.RoomNumber              AS Condo_RoomNumber,
    cd.CondoName               AS Condo_CondoName,
    cd.Province                AS Condo_Province,
    cd.District                AS Condo_District,
    cd.SubDistrict             AS Condo_SubDistrict,
    cd.UsableArea              AS Condo_UsableArea,
    cd.LocationType            AS Condo_LocationType,
    cd.BuildingAge             AS Condo_BuildingAge,
    cd.ConstructionYear        AS Condo_ConstructionYear,
    cd.ModelName               AS Condo_ModelName,
    cd.Latitude                AS Condo_Latitude,
    cd.Longitude               AS Condo_Longitude,
    cd.GeoPoint                AS Condo_GeoPoint,
    le.AppraisalId             AS Condo_LastAppraisalId,
    le.AppraisalNumber         AS Condo_LastAppraisalNumber,
    le.AppraisalDate           AS Condo_LastAppraisedDate,
    le.AppraisalValue          AS Condo_AppraisalValue,

    -- Leasehold-specific columns (NULL when not Leasehold type)
    lhd.LeaseRegistrationNo    AS Lh_LeaseRegistrationNo,
    lhd.UnderlyingMasterId     AS Lh_UnderlyingMasterId,
    lhd.Lessor                 AS Lh_Lessor,
    lhd.Lessee                 AS Lh_Lessee,
    lhd.LeaseTermStart         AS Lh_LeaseTermStart,
    lhd.LeaseTermEnd           AS Lh_LeaseTermEnd,
    lhd.LeaseTermMonths        AS Lh_LeaseTermMonths,
    le.AppraisalId             AS Lh_LastAppraisalId,
    le.AppraisalNumber         AS Lh_LastAppraisalNumber,
    le.AppraisalDate           AS Lh_LastAppraisedDate,

    -- Machine-specific columns (NULL when not Machine type)
    md.MachineRegistrationNo   AS Machine_MachineRegistrationNo,
    md.SerialNo                AS Machine_SerialNo,
    md.Brand                   AS Machine_Brand,
    md.Model                   AS Machine_Model,
    md.Manufacturer            AS Machine_Manufacturer,
    le.AppraisalId             AS Machine_LastAppraisalId,
    le.AppraisalNumber         AS Machine_LastAppraisalNumber,
    le.AppraisalDate           AS Machine_LastAppraisedDate

FROM collateral.CollateralMasters m

LEFT JOIN LatestEngagement le ON le.CollateralMasterId = m.Id AND le.rn = 1

-- Type-specific detail joins (1:1 per type, only one will be non-NULL per row)
LEFT JOIN collateral.LandDetails       ld  ON ld.CollateralMasterId  = m.Id
LEFT JOIN collateral.CondoDetails      cd  ON cd.CollateralMasterId  = m.Id
LEFT JOIN collateral.LeaseholdDetails  lhd ON lhd.CollateralMasterId = m.Id
LEFT JOIN collateral.MachineDetails    md  ON md.CollateralMasterId  = m.Id

WHERE m.IsDeleted = 0
  AND m.IsMaster = 1
