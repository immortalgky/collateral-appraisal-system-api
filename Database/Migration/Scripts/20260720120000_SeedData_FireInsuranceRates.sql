-- ============================================================
-- Seed: Fire Insurance Coverage Rates
-- Table: parameter.PricingParameterFireInsuranceRates
--
-- Rates transcribed verbatim from the previous hardcoded lookup at
-- Appraisal.Domain.Projects.CoverageByCondition. Codes match the seeded
-- parameter.Parameters group 'FireInsuranceCondition' (codes '01'-'12').
--
-- Idempotent: skips the block if Code '01' already exists.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM parameter.PricingParameterFireInsuranceRates WHERE Code = '01')
BEGIN

    INSERT INTO parameter.PricingParameterFireInsuranceRates (Code, Condition, PropertyKind, RatePerSqm, DisplaySeq) VALUES
    -- Condo conditions
    ('01', 'LessThan8Floors',                  'Condo',           25000, 1),
    ('02', 'GreaterThan8Floors',                'Condo',           30000, 2),
    ('03', 'LessThan8FloorsWithMezzanine',      'Condo',           35000, 3),
    ('04', 'GreaterThan8FloorsWithMezzanine',   'Condo',           40000, 4),
    -- LandAndBuilding conditions
    ('05', 'OneTwoStoreyTownhouse',             'LandAndBuilding', 10000, 5),
    ('06', 'ThreeStoreyTownhouse',              'LandAndBuilding', 12000, 6),
    ('07', 'SemiDetachedHouse',                 'LandAndBuilding', 12000, 7),
    ('08', 'SingleHouseAreaLessThan150',        'LandAndBuilding', 15000, 8),
    ('09', 'SingleHouseArea150To200',           'LandAndBuilding', 17000, 9),
    ('10', 'SingleHouseArea200To400',           'LandAndBuilding', 19000, 10),
    ('11', 'SingleHouseArea400To500',           'LandAndBuilding', 25000, 11),
    ('12', 'SingleHouseAreaGreaterThan500',     'LandAndBuilding', 30000, 12);

END;
