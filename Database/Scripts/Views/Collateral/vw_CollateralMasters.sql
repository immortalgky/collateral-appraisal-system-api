CREATE OR ALTER VIEW collateral.vw_CollateralMasters AS
SELECT
    -- Master identity
    m.Id,
    m.CollateralType,
    m.OwnerName,
    m.IsDeleted,
    m.CreatedOn,
    m.CreatedBy,
    m.UpdatedOn,
    m.UpdatedBy,

    -- Engagement aggregates (across all engagements for this master)
    agg.EngagementCount,
    agg.LastAppraisedDate,
    agg.LastAppraisedValue,

    -- Land-specific columns (NULL when not Land type)
    ld.LandOfficeCode          AS Land_LandOfficeCode,
    ld.Province                AS Land_Province,
    ld.Amphur                  AS Land_Amphur,
    ld.Tambon                  AS Land_Tambon,
    ld.TitleDeedType           AS Land_TitleDeedType,
    ld.TitleDeedNo             AS Land_TitleDeedNo,
    ld.SurveyOrParcelNo        AS Land_SurveyOrParcelNo,
    ld.Street                  AS Land_Street,
    ld.Village                 AS Land_Village,
    ld.PostalCode              AS Land_PostalCode,
    ld.Latitude                AS Land_Latitude,
    ld.Longitude               AS Land_Longitude,
    ld.LandShapeType           AS Land_LandShapeType,
    ld.LandZoneType            AS Land_LandZoneType,
    ld.UrbanPlanningType       AS Land_UrbanPlanningType,
    ld.AccessRoadWidth         AS Land_AccessRoadWidth,
    ld.RoadFrontage            AS Land_RoadFrontage,
    ld.LandArea                AS Land_LandArea,
    ld.IsUnderConstructionAtLastAppraisal,
    ld.OverallConstructionProgressPercent,
    ld.LastConstructionInspectionId AS Land_LastConstructionInspectionId,
    ld.LastAppraisalId         AS Land_LastAppraisalId,
    ld.LastAppraisalNumber     AS Land_LastAppraisalNumber,
    ld.LastAppraisedDate       AS Land_LastAppraisedDate,
    ld.LastAppraisedValue      AS Land_LastAppraisedValue,
    ld.LastTotalAppraisedValue AS Land_LastTotalAppraisedValue,

    -- Condo-specific columns (NULL when not Condo type)
    cd.LandOfficeCode          AS Condo_LandOfficeCode,
    cd.CondoRegistrationNumber AS Condo_CondoRegistrationNumber,
    cd.BuildingNumber          AS Condo_BuildingNumber,
    cd.FloorNumber             AS Condo_FloorNumber,
    cd.UnitNumber              AS Condo_UnitNumber,
    cd.TitleNumber             AS Condo_TitleNumber,
    cd.TitleType               AS Condo_TitleType,
    cd.CondoName               AS Condo_CondoName,
    cd.Province                AS Condo_Province,
    cd.UsableArea              AS Condo_UsableArea,
    cd.LocationType            AS Condo_LocationType,
    cd.BuildingAge             AS Condo_BuildingAge,
    cd.ConstructionYear        AS Condo_ConstructionYear,
    cd.ModelName               AS Condo_ModelName,
    cd.LastAppraisalId         AS Condo_LastAppraisalId,
    cd.LastAppraisalNumber     AS Condo_LastAppraisalNumber,
    cd.LastAppraisedDate       AS Condo_LastAppraisedDate,
    cd.LastAppraisedValue      AS Condo_LastAppraisedValue,

    -- Leasehold-specific columns (NULL when not Leasehold type)
    lhd.LeaseRegistrationNo    AS Lh_LeaseRegistrationNo,
    lhd.UnderlyingMasterId     AS Lh_UnderlyingMasterId,
    lhd.Lessor                 AS Lh_Lessor,
    lhd.Lessee                 AS Lh_Lessee,
    lhd.LeaseTermStart         AS Lh_LeaseTermStart,
    lhd.LeaseTermEnd           AS Lh_LeaseTermEnd,
    lhd.LeaseTermMonths        AS Lh_LeaseTermMonths,
    lhd.AnnualRent             AS Lh_AnnualRent,
    lhd.LeasePurpose           AS Lh_LeasePurpose,
    lhd.LastAppraisalId        AS Lh_LastAppraisalId,
    lhd.LastAppraisalNumber    AS Lh_LastAppraisalNumber,
    lhd.LastAppraisedDate      AS Lh_LastAppraisedDate,
    lhd.LastAppraisedValue     AS Lh_LastAppraisedValue,

    -- Machine-specific columns (NULL when not Machine type)
    md.MachineRegistrationNo   AS Machine_MachineRegistrationNo,
    md.SerialNo                AS Machine_SerialNo,
    md.Brand                   AS Machine_Brand,
    md.Model                   AS Machine_Model,
    md.Manufacturer            AS Machine_Manufacturer,
    md.EngineNo                AS Machine_EngineNo,
    md.ChassisNo               AS Machine_ChassisNo,
    md.YearOfManufacture       AS Machine_YearOfManufacture,
    md.MachineCondition        AS Machine_MachineCondition,
    md.MachineAge              AS Machine_MachineAge,
    md.LastAppraisalId         AS Machine_LastAppraisalId,
    md.LastAppraisalNumber     AS Machine_LastAppraisalNumber,
    md.LastAppraisedDate       AS Machine_LastAppraisedDate,
    md.LastAppraisedValue      AS Machine_LastAppraisedValue

FROM collateral.CollateralMasters m

-- Aggregate engagement metrics per master
LEFT JOIN (
    SELECT
        CollateralMasterId,
        COUNT(*)        AS EngagementCount,
        MAX(AppraisalDate) AS LastAppraisedDate,
        -- Last-appraised value taken from the engagement with the latest AppraisalDate
        (
            SELECT TOP 1 e2.AppraisedValue
            FROM collateral.CollateralEngagements e2
            WHERE e2.CollateralMasterId = e.CollateralMasterId
            ORDER BY e2.AppraisalDate DESC
        )               AS LastAppraisedValue
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
