CREATE
OR ALTER
VIEW appraisal.vw_PropertyGroupDetail
AS
SELECT PG.AppraisalId,
       PG.Id                                                    AS PropertyGroupId,
       PG.GroupNumber,
       PG.GroupName,
       PG.Description,
       PA.Id                                                    AS PricingAnalysisId,
       PGI.Id                                                   AS PropertyGroupItemId,
       PGI.SequenceInGroup,
       AP.Id                                                    AS PropertyId,
       AP.PropertyType,
       COALESCE(L.Id, B.Id, C.Id)                               AS AppraisalDetailId,
       COALESCE(L.PropertyName, B.PropertyName, C.PropertyName) AS PropertyName,
       CASE
           WHEN AP.PropertyType IN ('L', 'LB') THEN LT.TotalSquareWa
           WHEN AP.PropertyType = 'U' THEN C.UsableArea
           WHEN AP.PropertyType = 'B' THEN B.TotalBuildingArea
           END                                                  AS Area,
       CONCAT_WS(',', SD.NameTh, DI.NameTh, PV.NameTh)          AS Location
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

    LEFT JOIN parameter.TitleProvinces PV ON PV.Code = ISNULL(L.Province, C.Province)
    LEFT JOIN parameter.TitleDistricts DI ON DI.Code = ISNULL(L.District, C.District)
    LEFT JOIN parameter.TitleSubDistricts SD ON SD.Code = ISNULL(L.SubDistrict, C.SubDistrict)