-- ============================================================
-- Fire-insurance rates now live in the Appraisal module: appraisal.FireInsuranceRates.
--
-- Every consumer of these rates — condo property detail, block project models, the
-- unit-price calculation — is appraisal work, so the rows moved out of the Parameter
-- module and the cross-module query that reached them is gone.
--
-- WHY THIS SEEDS INSTEAD OF COPYING from parameter.PricingParameterFireInsuranceRates:
-- that table is dropped by an EF migration in this same release, and `migrate` runs EVERY
-- EF migration BEFORE any of these scripts. By the time this file runs the source table is
-- already gone, so a copy could never succeed — and merely naming a missing table risks
-- taking the whole migrate run down with "Invalid object name".
--
-- Seeding loses nothing in practice: no screen has ever been able to edit these rates
-- (parameter maintenance does not cover this table), so the seeded figures below are the
-- same values every database holds. If an operator ever hand-edited a rate with SQL, that
-- edit is NOT carried over — re-apply it against appraisal.FireInsuranceRates afterwards.
--
-- Codes match the seeded parameter.Parameters group 'FireInsuranceCondition' ('01'-'12'),
-- which stays where it is and keeps supplying the Thai/English labels.
--
-- Idempotent: inserts only the codes that are not present yet.
-- ============================================================

INSERT INTO appraisal.FireInsuranceRates (Code, Condition, PropertyKind, RatePerSqm, DisplaySeq)
SELECT src.Code, src.Condition, src.PropertyKind, src.RatePerSqm, src.DisplaySeq
FROM (VALUES
    -- Condo conditions
    ('01', 'LessThan8Floors',                  'Condo',           CAST(25000 AS DECIMAL(18,2)),  1),
    ('02', 'GreaterThan8Floors',               'Condo',           CAST(30000 AS DECIMAL(18,2)),  2),
    ('03', 'LessThan8FloorsWithMezzanine',     'Condo',           CAST(35000 AS DECIMAL(18,2)),  3),
    ('04', 'GreaterThan8FloorsWithMezzanine',  'Condo',           CAST(40000 AS DECIMAL(18,2)),  4),
    -- LandAndBuilding conditions
    ('05', 'OneTwoStoreyTownhouse',            'LandAndBuilding', CAST(10000 AS DECIMAL(18,2)),  5),
    ('06', 'ThreeStoreyTownhouse',             'LandAndBuilding', CAST(12000 AS DECIMAL(18,2)),  6),
    ('07', 'SemiDetachedHouse',                'LandAndBuilding', CAST(12000 AS DECIMAL(18,2)),  7),
    ('08', 'SingleHouseAreaLessThan150',       'LandAndBuilding', CAST(15000 AS DECIMAL(18,2)),  8),
    ('09', 'SingleHouseArea150To200',          'LandAndBuilding', CAST(17000 AS DECIMAL(18,2)),  9),
    ('10', 'SingleHouseArea200To400',          'LandAndBuilding', CAST(19000 AS DECIMAL(18,2)), 10),
    ('11', 'SingleHouseArea400To500',          'LandAndBuilding', CAST(25000 AS DECIMAL(18,2)), 11),
    ('12', 'SingleHouseAreaGreaterThan500',    'LandAndBuilding', CAST(30000 AS DECIMAL(18,2)), 12)
) AS src (Code, Condition, PropertyKind, RatePerSqm, DisplaySeq)
WHERE NOT EXISTS (
    SELECT 1 FROM appraisal.FireInsuranceRates dst WHERE dst.Code = src.Code);
