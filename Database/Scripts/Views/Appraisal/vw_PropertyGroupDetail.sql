CREATE
OR ALTER
VIEW appraisal.vw_PropertyGroupDetail
AS
SELECT PG.AppraisalId,
       PG.Id                                                         AS PropertyGroupId,
       PG.GroupNumber,
       PG.GroupName,
       PG.Description,
       PA.Id                                                         AS PricingAnalysisId,
       PGI.Id                                                        AS PropertyGroupItemId,
       PGI.SequenceInGroup,
       AP.Id                                                         AS PropertyId,
       AP.PropertyType,
       COALESCE(L.Id, B.Id, C.Id, M.Id)                              AS AppraisalDetailId,
       -- Machinery falls back to MachineName: the form used to have both a property name and a
       -- machine name and now writes only the first, so a row filled in through the old form can
       -- carry its name in the other column. Same rule the machinery report applies.
       COALESCE(L.PropertyName, B.PropertyName, C.PropertyName,
                NULLIF(M.PropertyName, ''), NULLIF(M.MachineName, '')) AS PropertyName,
       -- The lease types are the same four things held under a lease agreement, and they write to
       -- the same detail tables as their freehold twins: LSL/LS to land, LSB to building, LSU to
       -- condominium. Listing only the freehold codes here left every leased property with no
       -- area at all on screen and out of the group totals.
       CASE
           WHEN AP.PropertyType IN ('L', 'LB', 'LSL', 'LS') THEN LT.TotalSquareWa
           WHEN AP.PropertyType IN ('U', 'LSU') THEN C.UsableArea
           WHEN AP.PropertyType IN ('B', 'LSB') THEN B.TotalBuildingArea
           END                                                       AS Area,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.MachineName
           END                                                       AS MachineName,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.Brand
           END                                                       AS Brand,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.Model
           END                                                       AS Model,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.RegistrationNumber
           END                                                       AS RegistrationNumber,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.RegistrationStatus
           END                                                       AS RegistrationStatus,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.IsPriceCertified
           END                                                       AS IsPriceCertified,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.ConditionUse
           END                                                       AS ConditionUse,
       CASE
           WHEN AP.PropertyType = 'MAC'
               THEN CONCAT_WS(' x ', CAST(M.Width AS VARCHAR), CAST(M.Length AS VARCHAR), CAST(M.Height AS VARCHAR))
           END                                                       AS Dimension,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.Location
           ELSE CONCAT_WS(',', SD.NameTh, DI.NameTh, PV.NameTh)
           END                                                       AS Location,
       -- A building has no address of its own: the tambon/amphoe/province live on the land it
       -- stands on, and a building row's Location above is therefore always blank. Its type is
       -- what the list can say about it instead. BuildingType parameter code; older rows hold
       -- free text ('SingleHouse'), which the client falls back to printing as-is.
       CASE
           WHEN AP.PropertyType IN ('B', 'LSB') THEN NULLIF(B.BuildingType, '')
           END                                                       AS BuildingType,
       -- The appraiser's own words when the type is '99' (other) — "ถังน้ำ" says far more
       -- than the master's "อื่นๆ" does.
       CASE
           WHEN AP.PropertyType IN ('B', 'LSB') THEN NULLIF(B.BuildingTypeOther, '')
           END                                                       AS BuildingTypeOther,
       CASE
           WHEN AP.PropertyType IN ('B', 'LSB') THEN B.NumberOfFloors
           END                                                       AS NumberOfFloors,
       COALESCE(L.Latitude,  C.Latitude)                             AS Latitude,
       COALESCE(L.Longitude, C.Longitude)                            AS Longitude,
       CASE
           WHEN AP.PropertyType IN ('L', 'LB', 'LSL', 'LS') THEN LTN.TitleNumbers
           WHEN AP.PropertyType IN ('U', 'LSU') THEN C.TitleNumber
           END                                                       AS TitleNo,
       L.IsRentedOut                                                 AS IsRentedOut
FROM appraisal.PropertyGroups PG
         LEFT JOIN appraisal.PricingAnalysis PA ON PA.AnchorId = PG.Id AND PA.SubjectType = 0
         LEFT JOIN appraisal.PropertyGroupItems PGI ON PGI.PropertyGroupId = PG.Id
         LEFT JOIN appraisal.AppraisalProperties AP ON AP.Id = PGI.AppraisalPropertyId
         LEFT JOIN appraisal.LandAppraisalDetails L ON L.AppraisalPropertyId = AP.Id
    OUTER APPLY (SELECT SUM(ISNULL(AreaRai, 0) * 400 + ISNULL(AreaNgan, 0) * 100 + ISNULL(AreaSquareWa, 0)) AS TotalSquareWa
                      FROM appraisal.LandTitles
                      WHERE LandAppraisalDetailId = L.Id) LT
         OUTER APPLY (SELECT STRING_AGG(TitleNumber, ', ') WITHIN GROUP (ORDER BY TitleNumber) AS TitleNumbers
                      FROM appraisal.LandTitles
                      WHERE LandAppraisalDetailId = L.Id) LTN
         LEFT JOIN appraisal.BuildingAppraisalDetails B
ON B.AppraisalPropertyId = AP.Id
    LEFT JOIN appraisal.CondoAppraisalDetails C ON C.AppraisalPropertyId = AP.Id
    LEFT JOIN appraisal.MachineryAppraisalDetails M ON M.AppraisalPropertyId = AP.Id

    LEFT JOIN parameter.TitleProvinces PV ON PV.Code = ISNULL(L.Province, C.Province)
    LEFT JOIN parameter.TitleDistricts DI ON DI.Code = ISNULL(L.District, C.District)
    LEFT JOIN parameter.TitleSubDistricts SD ON SD.Code = ISNULL(L.SubDistrict, C.SubDistrict)