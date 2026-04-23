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
       COALESCE(L.PropertyName, B.PropertyName, C.PropertyName, M.PropertyName) AS PropertyName,
       CASE
           WHEN AP.PropertyType IN ('L', 'LB') THEN LT.TotalSquareWa
           WHEN AP.PropertyType = 'U' THEN C.UsableArea
           WHEN AP.PropertyType = 'B' THEN B.TotalBuildingArea
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
           WHEN AP.PropertyType = 'MAC' THEN M.RegistrationNo
           END                                                       AS RegistrationNo,
       CASE
           WHEN AP.PropertyType = 'MAC'
               THEN CONCAT_WS(' x ', CAST(M.Width AS VARCHAR), CAST(M.Length AS VARCHAR), CAST(M.Height AS VARCHAR))
           END                                                       AS Dimension,
       CASE
           WHEN AP.PropertyType = 'MAC' THEN M.Location
           ELSE CONCAT_WS(',', SD.NameTh, DI.NameTh, PV.NameTh)
           END                                                       AS Location,
       COALESCE(L.Latitude,  C.Latitude)                             AS Latitude,
       COALESCE(L.Longitude, C.Longitude)                            AS Longitude
FROM appraisal.PropertyGroups PG
         LEFT JOIN appraisal.PricingAnalysis PA ON PA.PropertyGroupId = PG.Id
         LEFT JOIN appraisal.PropertyGroupItems PGI ON PGI.PropertyGroupId = PG.Id
         LEFT JOIN appraisal.AppraisalProperties AP ON AP.Id = PGI.AppraisalPropertyId
         LEFT JOIN appraisal.LandAppraisalDetails L ON L.AppraisalPropertyId = AP.Id
    OUTER APPLY (SELECT SUM(ISNULL(AreaRai, 0) * 400 + ISNULL(AreaNgan, 0) * 100 + AreaSquareWa) AS TotalSquareWa
                      FROM appraisal.LandTitles
                      WHERE LandAppraisalDetailId = L.Id) LT
         LEFT JOIN appraisal.BuildingAppraisalDetails B
ON B.AppraisalPropertyId = AP.Id
    LEFT JOIN appraisal.CondoAppraisalDetails C ON C.AppraisalPropertyId = AP.Id
    LEFT JOIN appraisal.MachineryAppraisalDetails M ON M.AppraisalPropertyId = AP.Id

    LEFT JOIN parameter.TitleProvinces PV ON PV.Code = ISNULL(L.Province, C.Province)
    LEFT JOIN parameter.TitleDistricts DI ON DI.Code = ISNULL(L.District, C.District)
    LEFT JOIN parameter.TitleSubDistricts SD ON SD.Code = ISNULL(L.SubDistrict, C.SubDistrict)