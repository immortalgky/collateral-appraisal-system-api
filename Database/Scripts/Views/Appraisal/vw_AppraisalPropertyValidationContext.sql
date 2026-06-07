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
        WHEN EXISTS (
            SELECT 1
            FROM appraisal.LandAppraisalDetails lad
                     JOIN appraisal.LandTitles lt ON lt.LandAppraisalDetailId = lad.Id
            WHERE lad.AppraisalPropertyId = p.Id
              AND lt.TitleNumber IS NOT NULL
              AND lt.TitleNumber <> ''
        ) THEN 1 ELSE 0
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

    -- ── Condo unit identity (from CondoAppraisalDetails; 0 for non-condo) ─
    CASE WHEN NULLIF(cad.RoomNumber, '')     IS NOT NULL THEN 1 ELSE 0 END   AS RoomNumber,
    CASE WHEN NULLIF(cad.BuildingNumber, '') IS NOT NULL THEN 1 ELSE 0 END   AS BuildingNumber,
    CASE WHEN NULLIF(cad.FloorNumber, '')    IS NOT NULL THEN 1 ELSE 0 END   AS FloorNumber

FROM appraisal.AppraisalProperties p
         LEFT JOIN appraisal.LandAppraisalDetails  lad ON lad.AppraisalPropertyId = p.Id
         LEFT JOIN appraisal.CondoAppraisalDetails cad ON cad.AppraisalPropertyId = p.Id
         JOIN  appraisal.Appraisals a ON a.Id = p.AppraisalId
WHERE a.IsDeleted = 0
