CREATE OR ALTER VIEW collateral.vw_CollateralMasters AS
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

    -- Engagement aggregates (across all engagements for this master)
    agg.EngagementCount,
    agg.LastAppraisedDate,
    -- PR-4: LastAppraisedValue now sourced from master detail AppraisalValue (IsMaster only).
    -- AppraisedValue column dropped from CollateralEngagements in PR-4.
    -- All master detail types carry the appraisal-level AppraisalValue (ValuationAnalyses total).
    COALESCE(ld.AppraisalValue, cd.AppraisalValue, md.AppraisalValue, lhd.AppraisalValue) AS LastAppraisedValue,

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
    ld.IsUnderConstructionAtLastAppraisal,
    ld.OverallConstructionProgressPercent,
    -- PR-5: LastConstructionInspectionId removed from LandDetails — CI list is in the engagement snapshot.
    ld.LastAppraisalId         AS Land_LastAppraisalId,
    ld.LastAppraisalNumber     AS Land_LastAppraisalNumber,
    ld.LastAppraisedDate       AS Land_LastAppraisedDate,
    ld.UnitPrice               AS Land_UnitPrice,
    ld.BuildingValue           AS Land_BuildingValue,
    ld.AppraisalValue          AS Land_AppraisalValue,

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
    cd.LastAppraisalId         AS Condo_LastAppraisalId,
    cd.LastAppraisalNumber     AS Condo_LastAppraisalNumber,
    cd.LastAppraisedDate       AS Condo_LastAppraisedDate,
    cd.UnitPrice               AS Condo_UnitPrice,
    cd.BuildingValue           AS Condo_BuildingValue,
    cd.AppraisalValue          AS Condo_AppraisalValue,

    -- Leasehold-specific columns (NULL when not Leasehold type)
    lhd.LeaseRegistrationNo    AS Lh_LeaseRegistrationNo,
    lhd.UnderlyingMasterId     AS Lh_UnderlyingMasterId,
    lhd.Lessor                 AS Lh_Lessor,
    lhd.Lessee                 AS Lh_Lessee,
    lhd.LeaseTermStart         AS Lh_LeaseTermStart,
    lhd.LeaseTermEnd           AS Lh_LeaseTermEnd,
    lhd.LeaseTermMonths        AS Lh_LeaseTermMonths,
    lhd.LastAppraisalId        AS Lh_LastAppraisalId,
    lhd.LastAppraisalNumber    AS Lh_LastAppraisalNumber,
    lhd.LastAppraisedDate      AS Lh_LastAppraisedDate,

    -- Machine-specific columns (NULL when not Machine type)
    md.MachineRegistrationNo   AS Machine_MachineRegistrationNo,
    md.SerialNo                AS Machine_SerialNo,
    md.Brand                   AS Machine_Brand,
    md.Model                   AS Machine_Model,
    md.Manufacturer            AS Machine_Manufacturer,
    md.LastAppraisalId         AS Machine_LastAppraisalId,
    md.LastAppraisalNumber     AS Machine_LastAppraisalNumber,
    md.LastAppraisedDate       AS Machine_LastAppraisedDate

FROM collateral.CollateralMasters m

-- Aggregate engagement metrics per master
LEFT JOIN (
    SELECT
        CollateralMasterId,
        COUNT(*)           AS EngagementCount,
        MAX(AppraisalDate) AS LastAppraisedDate
    FROM collateral.CollateralEngagements e
    GROUP BY CollateralMasterId
) agg ON agg.CollateralMasterId = m.Id

-- Type-specific detail joins (1:1 per type, only one will be non-NULL per row)
LEFT JOIN collateral.LandDetails       ld  ON ld.CollateralMasterId  = m.Id
LEFT JOIN collateral.CondoDetails      cd  ON cd.CollateralMasterId  = m.Id
LEFT JOIN collateral.LeaseholdDetails  lhd ON lhd.CollateralMasterId = m.Id
LEFT JOIN collateral.MachineDetails    md  ON md.CollateralMasterId  = m.Id

WHERE m.IsDeleted = 0
  AND m.IsMaster = 1
