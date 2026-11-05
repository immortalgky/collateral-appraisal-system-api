CREATE
OR ALTER
VIEW appraisal.vw_AppraisalPropertyValidationContext AS
-- One row per property. Key columns: AppraisalId, SequenceNumber, PropertyType.
-- Presence-flag columns (1 = present/ok, 0 = missing): column name == fieldKey.
-- Adding a new validatable field: add one CASE…AS <FieldKey> column here.
-- The step reads rows via `SELECT * … WHERE AppraisalId = @id` and looks up
-- each required fieldKey by column name, so no C# redeploy is needed.
SELECT
    -- ── Identity / meta ───────────────────────────────────────────────────
    p.AppraisalId,
    p.SequenceNumber,
    p.PropertyType,

    -- ── TitleNumber: 1 when the property's LandAppraisalDetail has at least
    --    one LandTitles row with a non-empty TitleNumber. 0 otherwise.
    --    FK chain: AppraisalProperties.Id → LandAppraisalDetails.AppraisalPropertyId
    --             → LandTitles.LandAppraisalDetailId
    CASE
        WHEN EXISTS (SELECT 1 FROM appraisal.LandAppraisalDetails lad2
                    JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad2.Id
                    WHERE lad2.AppraisalPropertyId = p.Id AND NULLIF(lt.TitleNumber, '') IS NOT NULL)
        OR NULLIF(cad.TitleNumber, '') IS NOT NULL
        THEN 1 ELSE 0
    END                                                                     AS TitleNumber,

    -- ── LandOffice: 1 when land or condo Address.LandOffice is non-empty ──
    CASE
        WHEN NULLIF(COALESCE(lad.LandOffice, cad.LandOffice), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS LandOffice,

    -- ── Province: 1 when land or condo Address.Province is non-empty ──────
    CASE
        WHEN NULLIF(COALESCE(lad.Province, cad.Province), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS Province,

    -- ── District: 1 when land or condo Address.District is non-empty ──────
    CASE
        WHEN NULLIF(COALESCE(lad.District, cad.District), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS District,

    -- ── SubDistrict: 1 when land or condo Address.SubDistrict is non-empty
    CASE
        WHEN NULLIF(COALESCE(lad.SubDistrict, cad.SubDistrict), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS SubDistrict,

    -- ── DopaProvince: 1 when land or condo Address.DopaProvince is non-empty ──────
    CASE
        WHEN NULLIF(COALESCE(lad.DopaProvince, cad.DopaProvince), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS DopaProvince,

    -- ── DopaDistrict: 1 when land or condo Address.DopaDistrict is non-empty ──────
    CASE
        WHEN NULLIF(COALESCE(lad.DopaDistrict, cad.DopaDistrict), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS DopaDistrict,

    -- ── DopaSubDistrict: 1 when land or condo Address.DopaSubDistrict is non-empty
    CASE
        WHEN NULLIF(COALESCE(lad.DopaSubDistrict, cad.DopaSubDistrict), '') IS NOT NULL
            THEN 1 ELSE 0
    END                                                                     AS DopaSubDistrict,

    -- ── Building number when building or condo BuildingNumber is non-empty ─
    CASE 
        WHEN NULLIF(COALESCE(cad.BuildingNumber, bad.BuildingNumber), '') IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS BuildingNumber,

    -- ── ModelName when building or condo ModelName is non-empty ─
    CASE 
        WHEN NULLIF(COALESCE(cad.ModelName, bad.ModelName), '') IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS ModelName,

    -- ── DecorationType when building or condo DecorationType is non-empty ─
    CASE 
        WHEN NULLIF(COALESCE(cad.DecorationType, bad.DecorationType), '') IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS DecorationType,

    -- ── OwnerName when building, land, machine or condo OwnerName is non-empty ─
    CASE 
        WHEN NULLIF(COALESCE(cad.OwnerName, bad.OwnerName, mad.OwnerName, lad.OwnerName), '') IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS OwnerName,

    -- ── Latitude number when land or condo Latitude is non-empty ─
    CASE 
        WHEN COALESCE(cad.Latitude, lad.Latitude) IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS Latitude,

    -- ── Longitude number when land or condo Longitude is non-empty ─
    CASE 
        WHEN COALESCE(cad.Longitude, lad.Longitude) IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS Longitude,

    -- ── NumberOfFloors number when building or condo NumberOfFloors is non-empty ─
    CASE 
        WHEN COALESCE(cad.NumberOfFloors, bad.NumberOfFloors) IS NOT NULL
            THEN 1 ELSE 0 
    END                                                                     AS NumberOfFloors,

    -- ── Land identity (from LandAppraisalDetails; 0 for non-condo) ─
    CASE WHEN NULLIF(lad.LandDescription, '')     IS NOT NULL THEN 1 ELSE 0 END   AS LandDescription,
    CASE WHEN NULLIF(lad.Village, '')     IS NOT NULL THEN 1 ELSE 0 END   AS Village,
    CASE WHEN NULLIF(lad.Street, '')     IS NOT NULL THEN 1 ELSE 0 END   AS Street,

    -- ── Condo unit identity (from CondoAppraisalDetails; 0 for non-condo) ─
    CASE WHEN NULLIF(cad.CondoName, '')     IS NOT NULL THEN 1 ELSE 0 END   AS CondoName,
    CASE WHEN NULLIF(cad.RoomNumber, '')     IS NOT NULL THEN 1 ELSE 0 END   AS RoomNumber,
    CASE WHEN NULLIF(cad.FloorNumber, '')    IS NOT NULL THEN 1 ELSE 0 END   AS FloorNumber,
    CASE WHEN NULLIF(cad.CondoRegistrationNumber, '')    IS NOT NULL THEN 1 ELSE 0 END   AS CondoRegistrationNumber,
    CASE WHEN cad.UsableArea                 IS NOT NULL THEN 1 ELSE 0 END   AS UsableArea,

    -- ── Building identity (from BuildingAppraisalDetails; 0 for non-condo) ─
    CASE WHEN NULLIF(bad.HouseNumber, '')    IS NOT NULL THEN 1 ELSE 0 END   AS HouseNumber,
    CASE WHEN NULLIF(bad.BuiltOnTitleNumber, '')    IS NOT NULL THEN 1 ELSE 0 END   AS BuiltOnTitleNumber,
    CASE WHEN bad.TotalBuildingArea IS NOT NULL THEN 1 ELSE 0 END   AS TotalBuildingArea,
    CASE WHEN NULLIF(bad.BuildingType, '')   IS NOT NULL THEN 1 ELSE 0 END   AS BuildingType

FROM appraisal.AppraisalProperties p
         LEFT JOIN appraisal.LandAppraisalDetails  lad ON lad.AppraisalPropertyId = p.Id
         LEFT JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = p.Id
         LEFT JOIN appraisal.BuildingAppraisalDetails bad ON bad.AppraisalPropertyId = p.Id
         LEFT JOIN appraisal.MachineryAppraisalDetails mad ON mad.AppraisalPropertyId = p.Id
         JOIN  appraisal.Appraisals a ON a.Id = p.AppraisalId
WHERE a.IsDeleted = 0
