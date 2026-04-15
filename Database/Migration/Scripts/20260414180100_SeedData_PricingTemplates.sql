-- ============================================================
-- Seed: Income-Approach Pricing Templates
-- Tables: parameter.PricingTemplates
--         parameter.PricingTemplateSections
--         parameter.PricingTemplateCategories
--         parameter.PricingTemplateAssumptions
--
-- 5 templates ported verbatim from frontend dcfTemplates.ts:
--   dcf-hotel, dcf-apartment, dcf-office,
--   dcf-department-store, direct-apartment
--
-- All GUIDs are deterministic — safe to re-run (idempotent per Code).
-- ============================================================

-- ============================================================
-- TEMPLATE 1: DCF - Hotel  (dcf-hotel)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM parameter.PricingTemplates WHERE Code = 'dcf-hotel')
BEGIN

    INSERT INTO parameter.PricingTemplates
        (Id, Code, Name, TemplateType, Description, TotalNumberOfYears, TotalNumberOfDayInYear, CapitalizeRate, DiscountedRate, IsActive, DisplaySeq)
    VALUES
        ('A0000001-0000-0000-0000-000000000001', 'dcf-hotel', 'DCF - Hotel', 'DCF', NULL, 6, 365, 3.00, 5.00, 1, 1);

    -- Section: Income
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000001-0001-0000-0000-000000000001', 'A0000001-0000-0000-0000-000000000001', 'income', 'Income', 'positive', 0);

    -- Section: Income > Category: Operating Income
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000001-0001-0001-0000-000000000001', 'A0000001-0001-0000-0000-000000000001', 'income', 'Operating Income', 'positive', 0);

    -- Assumptions under Operating Income
    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0000-000000000001', 'I01', '', 'positive', 0, '01',
            '{"increaseRatePct":10,"increaseRateYrs":3,"occupancyRateFirstYearPct":80,"occupancyRatePct":5,"occupancyRateYrs":3}'),
        ('A0000001-0001-0001-0001-000000000002', 'A0000001-0001-0001-0000-000000000001', 'I04', '', 'positive', 1, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0001-0001-0001-000000000003', 'A0000001-0001-0001-0000-000000000001', 'I05', '', 'positive', 2, '14',
            '{}');

    -- Section: Expenses / Costs
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000001-0002-0000-0000-000000000001', 'A0000001-0000-0000-0000-000000000001', 'expenses', 'Expenses / Costs', 'negative', 1);

    -- Category: Direct Operating Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000001-0002-0001-0000-000000000001', 'A0000001-0002-0000-0000-000000000001', 'expenses', 'Direct Operating Expenses', 'expenses', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000001-0002-0001-0001-000000000001', 'A0000001-0002-0001-0000-000000000001', 'E15', '', 'negative', 0, '13',
            '{"proportionPct":15,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0001-0001-000000000002', 'A0000001-0002-0001-0000-000000000001', 'E07', '', 'negative', 1, '08',
            '{}'),
        ('A0000001-0002-0001-0001-000000000003', 'A0000001-0002-0001-0000-000000000001', 'E10', '', 'negative', 2, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}');

    -- Category: Administrative and Management Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000001-0002-0002-0000-000000000001', 'A0000001-0002-0000-0000-000000000001', 'expenses', 'Administrative and Management Expenses', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000001-0002-0002-0001-000000000001', 'A0000001-0002-0002-0000-000000000001', 'E09', '', 'negative', 0, '13',
            '{"proportionPct":12,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0002-0001-000000000002', 'A0000001-0002-0002-0000-000000000001', 'E17', '', 'negative', 1, '13',
            '{"proportionPct":3,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0002-0001-000000000003', 'A0000001-0002-0002-0000-000000000001', 'E13', '', 'negative', 2, '13',
            '{"proportionPct":2,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0002-0001-000000000004', 'A0000001-0002-0002-0000-000000000001', 'E03', '', 'negative', 3, '13',
            '{"proportionPct":2,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0002-0001-000000000005', 'A0000001-0002-0002-0000-000000000001', 'E14', '', 'negative', 4, '13',
            '{"proportionPct":2,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0002-0001-000000000006', 'A0000001-0002-0002-0000-000000000001', 'E18', '', 'negative', 5, '13',
            '{"proportionPct":2,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}');

    -- Category: GOP (no assumptions — computed category)
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000001-0002-0003-0000-000000000001', 'A0000001-0002-0000-0000-000000000001', 'gop', 'Gross Operating Profit (GOP)', 'gop', 2);

    -- Category: Fixed Charge
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000001-0002-0004-0000-000000000001', 'A0000001-0002-0000-0000-000000000001', 'fixedExps', 'Fixed Charge', 'positive', 3);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000001-0002-0004-0001-000000000001', 'A0000001-0002-0004-0000-000000000001', 'E20', '', 'positive', 0, '13',
            '{"proportionPct":2,"refTarget":{"kind":"assumption","dbId":"A0000001-0001-0001-0001-000000000001"}}'),
        ('A0000001-0002-0004-0001-000000000002', 'A0000001-0002-0004-0000-000000000001', 'E06', '', 'positive', 1, '12',
            '{"proportionPct":0.1,"increaseRatePct":2,"increaseRateYrs":1}'),
        ('A0000001-0002-0004-0001-000000000003', 'A0000001-0002-0004-0000-000000000001', 'E12', '', 'positive', 2, '10',
            '{}'),
        ('A0000001-0002-0004-0001-000000000004', 'A0000001-0002-0004-0000-000000000001', 'E00', '', 'positive', 3, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000001-0002-0003-0000-000000000001"}}'),
        ('A0000001-0002-0004-0001-000000000005', 'A0000001-0002-0004-0000-000000000001', 'E11', '', 'positive', 4, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000001-0002-0003-0000-000000000001"}}');

    -- Section: Summary DCF
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000001-0003-0000-0000-000000000001', 'A0000001-0000-0000-0000-000000000001', 'summaryDCF', 'Summary', 'empty', 2);

END;


-- ============================================================
-- TEMPLATE 2: DCF - Apartment  (dcf-apartment)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM parameter.PricingTemplates WHERE Code = 'dcf-apartment')
BEGIN

    INSERT INTO parameter.PricingTemplates
        (Id, Code, Name, TemplateType, Description, TotalNumberOfYears, TotalNumberOfDayInYear, CapitalizeRate, DiscountedRate, IsActive, DisplaySeq)
    VALUES
        ('A0000002-0000-0000-0000-000000000001', 'dcf-apartment', 'DCF - Apartment', 'DCF', NULL, 6, 365, 3.00, 5.00, 1, 2);

    -- Section: Income
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000002-0001-0000-0000-000000000001', 'A0000002-0000-0000-0000-000000000001', 'income', 'Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000002-0001-0001-0000-000000000001', 'A0000002-0001-0000-0000-000000000001', 'income', 'Operating Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000002-0001-0001-0001-000000000001', 'A0000002-0001-0001-0000-000000000001', 'I01', '', 'positive', 0, '01',
            '{"increaseRatePct":10,"increaseRateYrs":3,"occupancyRateFirstYearPct":80,"occupancyRatePct":5,"occupancyRateYrs":3}'),
        ('A0000002-0001-0001-0001-000000000002', 'A0000002-0001-0001-0000-000000000001', 'I06', '', 'positive', 1, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000002-0001-0001-0001-000000000001"}}');

    -- Section: Expenses
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000002-0002-0000-0000-000000000001', 'A0000002-0000-0000-0000-000000000001', 'expenses', 'Expenses / Costs', 'negative', 1);

    -- Category: Direct Operating Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000002-0002-0001-0000-000000000001', 'A0000002-0002-0000-0000-000000000001', 'expenses', 'Direct Operating Expenses', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000002-0002-0001-0001-000000000001', 'A0000002-0002-0001-0000-000000000001', 'E15', '', 'positive', 0, '13',
            '{"proportionPct":15,"refTarget":{"kind":"assumption","dbId":"A0000002-0001-0001-0001-000000000001"}}'),
        ('A0000002-0002-0001-0001-000000000002', 'A0000002-0002-0001-0000-000000000001', 'E10', '', 'positive', 1, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000002-0001-0001-0001-000000000001"}}');

    -- Category: Administrative and Management Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000002-0002-0002-0000-000000000001', 'A0000002-0002-0000-0000-000000000001', 'expenses', 'Administrative and Management Expenses', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000002-0002-0002-0001-000000000001', 'A0000002-0002-0002-0000-000000000001', 'E09', '', 'positive', 0, '09',
            '{}'),
        ('A0000002-0002-0002-0001-000000000002', 'A0000002-0002-0002-0000-000000000001', 'E17', '', 'positive', 1, '13',
            '{"proportionPct":3,"refTarget":{"kind":"assumption","dbId":"A0000002-0001-0001-0001-000000000001"}}'),
        ('A0000002-0002-0002-0001-000000000003', 'A0000002-0002-0002-0000-000000000001', 'E13', '', 'positive', 2, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000002-0001-0000-0000-000000000001"}}'),
        ('A0000002-0002-0002-0001-000000000004', 'A0000002-0002-0002-0000-000000000001', 'E18', '', 'positive', 3, '14',
            '{"increaseRatePct":10,"increaseRateYrs":3}'),
        ('A0000002-0002-0002-0001-000000000005', 'A0000002-0002-0002-0000-000000000001', 'E03', '', 'positive', 4, '13',
            '{"proportionPct":10,"refTarget":{"kind":"section","dbId":"A0000002-0001-0000-0000-000000000001"}}'),
        ('A0000002-0002-0002-0001-000000000006', 'A0000002-0002-0002-0000-000000000001', 'E14', '', 'positive', 5, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000002-0001-0000-0000-000000000001"}}');

    -- Category: GOP
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000002-0002-0003-0000-000000000001', 'A0000002-0002-0000-0000-000000000001', 'gop', 'Gross Operating Profit (GOP)', 'gop', 2);

    -- Category: Fixed Charge
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000002-0002-0004-0000-000000000001', 'A0000002-0002-0000-0000-000000000001', 'fixedExps', 'Fixed Charge', 'positive', 3);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000002-0002-0004-0001-000000000001', 'A0000002-0002-0004-0000-000000000001', 'E20', '', 'positive', 0, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000002-0001-0000-0000-000000000001"}}'),
        ('A0000002-0002-0004-0001-000000000002', 'A0000002-0002-0004-0000-000000000001', 'E06', '', 'positive', 1, '12',
            '{"proportionPct":0.1,"increaseRatePct":2,"increaseRateYrs":1}'),
        ('A0000002-0002-0004-0001-000000000003', 'A0000002-0002-0004-0000-000000000001', 'E12', '', 'positive', 2, '10',
            '{}'),
        ('A0000002-0002-0004-0001-000000000004', 'A0000002-0002-0004-0000-000000000001', 'E00', '', 'positive', 3, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000002-0002-0003-0000-000000000001"}}'),
        ('A0000002-0002-0004-0001-000000000005', 'A0000002-0002-0004-0000-000000000001', 'E11', '', 'positive', 4, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000002-0002-0003-0000-000000000001"}}');

    -- Section: Summary DCF
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000002-0003-0000-0000-000000000001', 'A0000002-0000-0000-0000-000000000001', 'summaryDCF', 'Summary', 'empty', 2);

END;


-- ============================================================
-- TEMPLATE 3: DCF - Office  (dcf-office)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM parameter.PricingTemplates WHERE Code = 'dcf-office')
BEGIN

    INSERT INTO parameter.PricingTemplates
        (Id, Code, Name, TemplateType, Description, TotalNumberOfYears, TotalNumberOfDayInYear, CapitalizeRate, DiscountedRate, IsActive, DisplaySeq)
    VALUES
        ('A0000003-0000-0000-0000-000000000001', 'dcf-office', 'DCF - Office', 'DCF', NULL, 6, 365, 3.00, 5.00, 1, 3);

    -- Section: Income
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000003-0001-0000-0000-000000000001', 'A0000003-0000-0000-0000-000000000001', 'income', 'Income', 'positive', 0);

    -- Category: Gross Income
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000003-0001-0001-0000-000000000001', 'A0000003-0001-0000-0000-000000000001', 'income', 'Gross Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000003-0001-0001-0001-000000000001', 'A0000003-0001-0001-0000-000000000001', 'I02', '', 'positive', 0, '06',
            '{"increaseRatePct":10,"increaseRateYrs":3,"occupancyRateFirstYearPct":80,"occupancyRatePct":5,"occupancyRateYrs":3}');

    -- Category: Other Income
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000003-0001-0002-0000-000000000001', 'A0000003-0001-0000-0000-000000000001', 'income', 'Other Income', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000003-0001-0002-0001-000000000001', 'A0000003-0001-0002-0000-000000000001', 'I03', '', 'positive', 0, '13',
            '{"proportionPct":2,"refTarget":{"kind":"category","dbId":"A0000003-0001-0001-0000-000000000001"}}'),
        ('A0000003-0001-0002-0001-000000000002', 'A0000003-0001-0002-0000-000000000001', 'I06', 'Other Income', 'positive', 1, '13',
            '{"proportionPct":2,"refTarget":{"kind":"category","dbId":"A0000003-0001-0001-0000-000000000001"}}');

    -- Section: Expenses
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000003-0002-0000-0000-000000000001', 'A0000003-0000-0000-0000-000000000001', 'expenses', 'Expenses / Costs', 'negative', 1);

    -- Category: Direct Operating Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000003-0002-0001-0000-000000000001', 'A0000003-0002-0000-0000-000000000001', 'expenses', 'Direct Operating Expenses', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000003-0002-0001-0001-000000000001', 'A0000003-0002-0001-0000-000000000001', 'E17', '', 'positive', 0, '13',
            '{"proportionPct":1,"refTarget":{"kind":"category","dbId":"A0000003-0001-0001-0000-000000000001"}}'),
        ('A0000003-0002-0001-0001-000000000002', 'A0000003-0002-0001-0000-000000000001', 'E05', '', 'positive', 1, '11',
            '{"energyCostIndex":30,"increaseRatePct":3,"increaseRateYrs":3}'),
        ('A0000003-0002-0001-0001-000000000003', 'A0000003-0002-0001-0000-000000000001', 'E10', '', 'positive', 2, '13',
            '{"proportionPct":1}'),
        ('A0000003-0002-0001-0001-000000000004', 'A0000003-0002-0001-0000-000000000001', 'M99', 'Sales and Marketing Expenses', 'positive', 3, '13',
            '{"proportionPct":1,"refTarget":{"kind":"category","dbId":"A0000003-0001-0001-0000-000000000001"}}');

    -- Category: Fixed Charge
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000003-0002-0002-0000-000000000001', 'A0000003-0002-0000-0000-000000000001', 'fixedExps', 'Fixed Charge', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000003-0002-0002-0001-000000000001', 'A0000003-0002-0002-0000-000000000001', 'E06', '', 'positive', 0, '12',
            '{"proportionPct":0.1,"increaseRatePct":2,"increaseRateYrs":1}'),
        ('A0000003-0002-0002-0001-000000000002', 'A0000003-0002-0002-0000-000000000001', 'E12', '', 'positive', 1, '10',
            '{}'),
        ('A0000003-0002-0002-0001-000000000003', 'A0000003-0002-0002-0000-000000000001', 'E14', '', 'positive', 2, '13',
            '{"proportionPct":10,"refTarget":{"kind":"section","dbId":"A0000003-0001-0000-0000-000000000001"}}');

    -- Section: Summary DCF
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000003-0003-0000-0000-000000000001', 'A0000003-0000-0000-0000-000000000001', 'summaryDCF', 'Summary', 'empty', 2);

END;


-- ============================================================
-- TEMPLATE 4: DCF - Department Store  (dcf-department-store)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM parameter.PricingTemplates WHERE Code = 'dcf-department-store')
BEGIN

    INSERT INTO parameter.PricingTemplates
        (Id, Code, Name, TemplateType, Description, TotalNumberOfYears, TotalNumberOfDayInYear, CapitalizeRate, DiscountedRate, IsActive, DisplaySeq)
    VALUES
        ('A0000004-0000-0000-0000-000000000001', 'dcf-department-store', 'DCF - Department Store', 'DCF', NULL, 6, 365, 3.00, 5.00, 1, 4);

    -- Section: Income
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000004-0001-0000-0000-000000000001', 'A0000004-0000-0000-0000-000000000001', 'income', 'Income', 'positive', 0);

    -- Category: Gross Income
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000004-0001-0001-0000-000000000001', 'A0000004-0001-0000-0000-000000000001', 'income', 'Gross Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000004-0001-0001-0001-000000000001', 'A0000004-0001-0001-0000-000000000001', 'I02', '', 'positive', 0, '06',
            '{"increaseRatePct":10,"increaseRateYrs":3,"occupancyRateFirstYearPct":80,"occupancyRatePct":5,"occupancyRateYrs":3}');

    -- Category: Other Income
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000004-0001-0002-0000-000000000001', 'A0000004-0001-0000-0000-000000000001', 'income', 'Other Income', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000004-0001-0002-0001-000000000001', 'A0000004-0001-0002-0000-000000000001', 'I04', '', 'positive', 0, '13',
            '{"proportionPct":20,"refTarget":{"kind":"category","dbId":"A0000004-0001-0001-0000-000000000001"}}'),
        ('A0000004-0001-0002-0001-000000000002', 'A0000004-0001-0002-0000-000000000001', 'I06', 'Other Income', 'positive', 1, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000004-0001-0001-0000-000000000001"}}');

    -- Section: Expenses
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000004-0002-0000-0000-000000000001', 'A0000004-0000-0000-0000-000000000001', 'expenses', 'Expenses / Costs', 'negative', 1);

    -- Category: Direct Operating Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000004-0002-0001-0000-000000000001', 'A0000004-0002-0000-0000-000000000001', 'expenses', 'Direct Operating Expenses', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000004-0002-0001-0001-000000000001', 'A0000004-0002-0001-0000-000000000001', 'E18', '', 'positive', 0, '13',
            '{"proportionPct":1,"refTarget":{"kind":"assumption","dbId":"A0000004-0001-0002-0001-000000000001"}}'),
        ('A0000004-0002-0001-0001-000000000002', 'A0000004-0002-0001-0000-000000000001', 'E15', '', 'positive', 1, '13',
            '{"proportionPct":15,"refTarget":{"kind":"section","dbId":"A0000004-0001-0000-0000-000000000001"}}'),
        ('A0000004-0002-0001-0001-000000000003', 'A0000004-0002-0001-0000-000000000001', 'E02', '', 'positive', 2, '13',
            '{"proportionPct":15,"refTarget":{"kind":"section","dbId":"A0000004-0001-0000-0000-000000000001"}}'),
        ('A0000004-0002-0001-0001-000000000004', 'A0000004-0002-0001-0000-000000000001', 'E02', '', 'positive', 3, '13',
            '{"proportionPct":10,"refTarget":{"kind":"section","dbId":"A0000004-0001-0000-0000-000000000001"}}'),
        ('A0000004-0002-0001-0001-000000000005', 'A0000004-0002-0001-0000-000000000001', 'E17', '', 'positive', 4, '13',
            '{"proportionPct":3,"refTarget":{"kind":"section","dbId":"A0000004-0001-0000-0000-000000000001"}}'),
        ('A0000004-0002-0001-0001-000000000006', 'A0000004-0002-0001-0000-000000000001', 'E10', '', 'positive', 5, '13',
            '{"proportionPct":20,"refTarget":{"kind":"assumption","dbId":"A0000004-0001-0002-0001-000000000002"}}');

    -- Category: Fixed Charge
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000004-0002-0002-0000-000000000001', 'A0000004-0002-0000-0000-000000000001', 'fixedExps', 'Fixed Charge', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000004-0002-0002-0001-000000000001', 'A0000004-0002-0002-0000-000000000001', 'E06', '', 'positive', 0, '12',
            '{"proportionPct":0.1,"increaseRatePct":2,"increaseRateYrs":1}'),
        ('A0000004-0002-0002-0001-000000000002', 'A0000004-0002-0002-0000-000000000001', 'E12', '', 'positive', 1, '10',
            '{}'),
        ('A0000004-0002-0002-0001-000000000003', 'A0000004-0002-0002-0000-000000000001', 'E14', '', 'positive', 2, '13',
            '{"proportionPct":10,"refTarget":{"kind":"section","dbId":"A0000004-0001-0000-0000-000000000001"}}');

    -- Section: Summary DCF
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000004-0003-0000-0000-000000000001', 'A0000004-0000-0000-0000-000000000001', 'summaryDCF', 'Summary', 'empty', 2);

END;


-- ============================================================
-- TEMPLATE 5: Direct - Apartment  (direct-apartment)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM parameter.PricingTemplates WHERE Code = 'direct-apartment')
BEGIN

    INSERT INTO parameter.PricingTemplates
        (Id, Code, Name, TemplateType, Description, TotalNumberOfYears, TotalNumberOfDayInYear, CapitalizeRate, DiscountedRate, IsActive, DisplaySeq)
    VALUES
        ('A0000005-0000-0000-0000-000000000001', 'direct-apartment', 'Direct - Apartment', 'Direct', NULL, 1, 365, 3.00, 0.00, 1, 5);

    -- Section: Income
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000005-0001-0000-0000-000000000001', 'A0000005-0000-0000-0000-000000000001', 'income', 'Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000005-0001-0001-0000-000000000001', 'A0000005-0001-0000-0000-000000000001', 'income', 'Operating Income', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000005-0001-0001-0001-000000000001', 'A0000005-0001-0001-0000-000000000001', 'I01', '', 'positive', 0, '01',
            '{"increaseRatePct":10,"increaseRateYrs":3,"occupancyRateFirstYearPct":80,"occupancyRatePct":5,"occupancyRateYrs":3}'),
        ('A0000005-0001-0001-0001-000000000002', 'A0000005-0001-0001-0000-000000000001', 'I06', '', 'positive', 1, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000005-0001-0001-0001-000000000001"}}');

    -- Section: Expenses
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000005-0002-0000-0000-000000000001', 'A0000005-0000-0000-0000-000000000001', 'expenses', 'Expenses / Costs', 'negative', 1);

    -- Category: Direct Operating Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000005-0002-0001-0000-000000000001', 'A0000005-0002-0000-0000-000000000001', 'expenses', 'Direct Operating Expenses', 'positive', 0);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000005-0002-0001-0001-000000000001', 'A0000005-0002-0001-0000-000000000001', 'E15', '', 'positive', 0, '13',
            '{"proportionPct":15,"refTarget":{"kind":"assumption","dbId":"A0000005-0001-0001-0001-000000000001"}}'),
        ('A0000005-0002-0001-0001-000000000002', 'A0000005-0002-0001-0000-000000000001', 'E10', '', 'positive', 1, '13',
            '{"proportionPct":10,"refTarget":{"kind":"assumption","dbId":"A0000005-0001-0001-0001-000000000001"}}');

    -- Category: Administrative and Management Expenses
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000005-0002-0002-0000-000000000001', 'A0000005-0002-0000-0000-000000000001', 'expenses', 'Administrative and Management Expenses', 'positive', 1);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000005-0002-0002-0001-000000000001', 'A0000005-0002-0002-0000-000000000001', 'E09', '', 'positive', 0, '09',
            '{}'),
        ('A0000005-0002-0002-0001-000000000002', 'A0000005-0002-0002-0000-000000000001', 'E17', '', 'positive', 1, '13',
            '{"proportionPct":3,"refTarget":{"kind":"assumption","dbId":"A0000005-0001-0001-0001-000000000001"}}'),
        ('A0000005-0002-0002-0001-000000000003', 'A0000005-0002-0002-0000-000000000001', 'E13', '', 'positive', 2, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000005-0001-0000-0000-000000000001"}}'),
        ('A0000005-0002-0002-0001-000000000004', 'A0000005-0002-0002-0000-000000000001', 'E18', '', 'positive', 3, '14',
            '{"increaseRatePct":10,"increaseRateYrs":3}'),
        ('A0000005-0002-0002-0001-000000000005', 'A0000005-0002-0002-0000-000000000001', 'E03', '', 'positive', 4, '13',
            '{"proportionPct":10,"refTarget":{"kind":"section","dbId":"A0000005-0001-0000-0000-000000000001"}}'),
        ('A0000005-0002-0002-0001-000000000006', 'A0000005-0002-0002-0000-000000000001', 'E14', '', 'positive', 5, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000005-0001-0000-0000-000000000001"}}');

    -- Category: GOP
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000005-0002-0003-0000-000000000001', 'A0000005-0002-0000-0000-000000000001', 'gop', 'Gross Operating Profit (GOP)', 'gop', 2);

    -- Category: Fixed Charge
    INSERT INTO parameter.PricingTemplateCategories (Id, PricingTemplateSectionId, CategoryType, CategoryName, Identifier, DisplaySeq) VALUES
        ('A0000005-0002-0004-0000-000000000001', 'A0000005-0002-0000-0000-000000000001', 'fixedExps', 'Fixed Charge', 'positive', 3);

    INSERT INTO parameter.PricingTemplateAssumptions (Id, PricingTemplateCategoryId, AssumptionType, AssumptionName, Identifier, DisplaySeq, MethodTypeCode, MethodDetailJson) VALUES
        ('A0000005-0002-0004-0001-000000000001', 'A0000005-0002-0004-0000-000000000001', 'E20', '', 'positive', 0, '13',
            '{"proportionPct":2,"refTarget":{"kind":"section","dbId":"A0000005-0001-0000-0000-000000000001"}}'),
        ('A0000005-0002-0004-0001-000000000002', 'A0000005-0002-0004-0000-000000000001', 'E06', '', 'positive', 1, '12',
            '{"proportionPct":0.1,"increaseRatePct":2,"increaseRateYrs":1}'),
        ('A0000005-0002-0004-0001-000000000003', 'A0000005-0002-0004-0000-000000000001', 'E12', '', 'positive', 2, '10',
            '{}'),
        ('A0000005-0002-0004-0001-000000000004', 'A0000005-0002-0004-0000-000000000001', 'E00', '', 'positive', 3, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000005-0002-0003-0000-000000000001"}}'),
        ('A0000005-0002-0004-0001-000000000005', 'A0000005-0002-0004-0000-000000000001', 'E11', '', 'positive', 4, '13',
            '{"proportionPct":5,"refTarget":{"kind":"category","dbId":"A0000005-0002-0003-0000-000000000001"}}');

    -- Section: Summary Direct (not DCF)
    INSERT INTO parameter.PricingTemplateSections (Id, PricingTemplateId, SectionType, SectionName, Identifier, DisplaySeq) VALUES
        ('A0000005-0003-0000-0000-000000000001', 'A0000005-0000-0000-0000-000000000001', 'summaryDirect', 'Summary', 'empty', 2);

END;
