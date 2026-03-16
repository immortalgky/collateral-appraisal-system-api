-- ============================================================
-- Seed data for Market Comparable tables
-- Source: LHB Parameter Listing Nov 13 2025.xlsx
-- Sheet: MarketSurveyFactor
-- ============================================================

SET NOCOUNT ON;
GO

-- ============================================================
-- 1. MarketComparableFactors (74 factors)
-- ============================================================

INSERT INTO appraisal.MarketComparableFactors
    (FactorCode, FieldName, DataType, FieldLength, FieldDecimal, ParameterGroup, IsActive, CreatedAt, CreatedBy)
VALUES
    (N'01', N'SqWa', N'Numeric', 15, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'02', N'LandFillCondition', N'Radio', 10, NULL, N'Landfill', 1, GETDATE(), N'SYSTEM'),
    (N'03', N'LandLevel', N'Numeric', 5, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'04', N'WideFrontageOfLandAdjacentToTheRoad', N'Numeric', 5, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'05', N'NumberOfSidesFacingTheRoad', N'Numeric', 1, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'06', N'LandShape', N'Dropdown', 10, NULL, N'LandShape', 1, GETDATE(), N'SYSTEM'),
    (N'07', N'BuildingName', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'08', N'BuildingModelName', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'09', N'BuildingDetails', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'10', N'BuildingCondition', N'Radio', 10, NULL, N'BuildingCondition', 1, GETDATE(), N'SYSTEM'),
    (N'11', N'BuildingWidth', N'Numeric', 7, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'12', N'BuildingAge', N'Numeric', 3, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'13', N'BuildingAreaUseableArea', N'Numeric', 5, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'14', N'DivisionOfInteriorSpace', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'15', N'BuildingExtension', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'16', N'AddressLocation', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'17', N'ProjectNameVillageName', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'18', N'RoomTypes', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'19', N'TotalRoom', N'Numeric', 5, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'20', N'OccupancyRate', N'Numeric', 5, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'21', N'RentalPrice', N'Numeric', 13, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'22', N'WaterAndElectricityRatesBahtMonth', N'Numeric', 13, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'23', N'RoomSize', N'Numeric', 7, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'24', N'RoadSurface', N'Radio', 10, NULL, N'RoadSurface', 1, GETDATE(), N'SYSTEM'),
    (N'25', N'EntryAndExitRights', N'Radio', 10, NULL, N'LandEntranceExit', 1, GETDATE(), N'SYSTEM'),
    (N'26', N'MaximumUtilization', N'Radio', 10, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'27', N'CurrentUtilizationLandUse', N'Dropdown', 10, NULL, N'LandUse', 1, GETDATE(), N'SYSTEM'),
    (N'28', N'TypesOfUrbanPlanning', N'Dropdown', 200, NULL, N'TypeOfUrbanPlanning', 1, GETDATE(), N'SYSTEM'),
    (N'29', N'Utilities', N'CheckboxGroup', 200, NULL, N'PublicUtility', 1, GETDATE(), N'SYSTEM'),
    (N'30', N'Facility', N'CheckboxGroup', 200, NULL, N'Facilities', 1, GETDATE(), N'SYSTEM'),
    (N'31', N'OfferingPriceConditionTheBankHasAcceptedThePrice', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'32', N'OtherDetails', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'33', N'Latitude', N'Numeric', 9, 6, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'34', N'Longtitude', N'Numeric', 9, 6, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'35', N'TotalFloor', N'Numeric', 3, 1, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'36', N'TotalBuilding', N'Numeric', 5, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'37', N'TotalUnit', N'Numeric', 5, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'38', N'LandAreaInRai', N'Numeric', 5, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'39', N'LandAreaInNgan', N'Numeric', 1, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'40', N'LandAreaInWah', N'Numeric', 5, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'41', N'RoomLayout', N'Dropdown', 2, NULL, N'RoomLayout', 1, GETDATE(), N'SYSTEM'),
    (N'42', N'RoomFloor', N'Numeric', 3, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'43', N'PlotLocation', N'CheckboxGroup', 200, NULL, N'PlotLocation', 1, GETDATE(), N'SYSTEM'),
    (N'44', N'DeveloperReputation', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'45', N'BuildingStyle', N'Radio', 10, NULL, N'BuildingStyle', 1, GETDATE(), N'SYSTEM'),
    (N'46', N'Environment', N'CheckboxGroup', 10, NULL, N'Environment', 1, GETDATE(), N'SYSTEM'),
    (N'47', N'MarketDemand', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'48', N'RoomCondition', N'Radio', NULL, NULL, N'CondoCondition', 1, GETDATE(), N'SYSTEM'),
    (N'49', N'LocationView', N'CheckboxGroup', 200, NULL, N'LocationView', 1, GETDATE(), N'SYSTEM'),
    (N'50', N'UnitTypeDetail', N'Text', 1000, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'51', N'RoadPassingInFrontOfTheLand', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'52', N'Liquidity', N'Dropdown', 10, NULL, N'Liquidity', 1, GETDATE(), N'SYSTEM'),
    (N'53', N'LocalDevelopmentDevelopmentInTheArea', N'Dropdown', 10, NULL, N'LocalDev', 1, GETDATE(), N'SYSTEM'),
    (N'54', N'Location', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'55', N'Series', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'56', N'Brand', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'57', N'EngineNumber', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'58', N'MachineCapacity', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'59', N'MachineNameAndDetails', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'60', N'MachineWidth', N'Numeric', 7, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'61', N'MachineLength', N'Numeric', 7, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'62', N'MachineHeight', N'Numeric', 7, 2, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'63', N'Manufacturer', N'Dropdown', 3, NULL, N'Bank to provide Country parameter', 1, GETDATE(), N'SYSTEM'),
    (N'64', N'MachineModel', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'65', N'EnergyUsed', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'66', N'UsageCondition', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'67', N'UtilizationUsagePurpose', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'68', N'YearOfUse', N'Numeric', 3, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'69', N'MachineryEfficiency', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'70', N'MachineryTechnology', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'71', N'MachineryMaterialAssembly', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'72', N'MachineryCondition', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'73', N'MachineryAgeN', N'Numeric', 3, 1, NULL, 1, GETDATE(), N'SYSTEM'),
    (N'74', N'RulesLaws', N'Text', 200, NULL, NULL, 1, GETDATE(), N'SYSTEM');
GO

-- ============================================================
-- 2. MarketComparableTemplates (5 templates)
-- ============================================================

INSERT INTO appraisal.MarketComparableTemplates
    (TemplateCode, TemplateName, PropertyType, Description, IsActive, CreatedAt, CreatedBy)
VALUES
    (N'LAND_BUILDING_TEMPLATE', N'Survey Template for Land & Building', N'LB', N'Survey Template for Land & Building', 1, GETDATE(), N'SYSTEM'),
    (N'CONDO_TEMPLATE', N'Survey Template for Condo', N'U', N'Survey Template for Condo', 1, GETDATE(), N'SYSTEM'),
    (N'LAND_TEMPLATE', N'Survey Template for Land', N'L', N'Survey Template for Land', 1, GETDATE(), N'SYSTEM'),
    (N'MACHINE_TEMPLATE', N'Survey Template for Machine', N'MAC', N'Survey Template for Machine', 1, GETDATE(), N'SYSTEM'),
    (N'LEASE_AGREEMENT_TEMPLATE', N'Survey Template for Lease Agreement', N'LS', N'Survey Template for Lease Agreement', 1, GETDATE(), N'SYSTEM');
GO

-- ============================================================
-- 3. MarketComparableTemplateFactors
-- NOTE: Factor codes remapped from old numbering to new Excel numbering.
-- Factors removed in new Excel: PropertyType(old 01), OfferingPrice(old 25),
-- InformationDate(old 35), SourceOfInformation(old 36), Remark(old 37),
-- PurchasePrice(old 46), SellingPrice(old 47), AdjustOfferingPrice%(old 48),
-- AdjustOfferingPriceBaht(old 49), BuysellDate(old 84)
-- ============================================================

-- Template: LAND_BUILDING_TEMPLATE
    -- NOTE: Old FactorCode N'01' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'25' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'46' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'36' was removed from the new MarketSurveyFactor list
INSERT INTO appraisal.MarketComparableTemplateFactors
    (TemplateId, FactorId, DisplaySequence, IsMandatory, CreatedAt, CreatedBy)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'33'), 1, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'34'), 2, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'49'), 3, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'17'), 4, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'45'), 5, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'43'), 6, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'10'), 7, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'01'), 8, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'13'), 9, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_BUILDING_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'30'), 10, 0, GETDATE(), N'SYSTEM');
GO
-- Template: CONDO_TEMPLATE
    -- NOTE: Old FactorCode N'01' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'25' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'46' was removed from the new MarketSurveyFactor list
INSERT INTO appraisal.MarketComparableTemplateFactors
    (TemplateId, FactorId, DisplaySequence, IsMandatory, CreatedAt, CreatedBy)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'16'), 1, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'37'), 2, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'17'), 3, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'45'), 4, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'30'), 5, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'48'), 6, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'49'), 7, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'42'), 8, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'CONDO_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'23'), 9, 0, GETDATE(), N'SYSTEM');
GO
-- Template: LAND_TEMPLATE
    -- NOTE: Old FactorCode N'01' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'25' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'46' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'84' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'36' was removed from the new MarketSurveyFactor list
INSERT INTO appraisal.MarketComparableTemplateFactors
    (TemplateId, FactorId, DisplaySequence, IsMandatory, CreatedAt, CreatedBy)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'43'), 1, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'16'), 2, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'46'), 3, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'33'), 4, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'34'), 5, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'01'), 6, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'06'), 7, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'04'), 8, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'02'), 9, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'51'), 10, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'74'), 11, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'26'), 12, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LAND_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'29'), 13, 0, GETDATE(), N'SYSTEM');
GO
-- Template: MACHINE_TEMPLATE
    -- NOTE: Old FactorCode N'25' was removed from the new MarketSurveyFactor list
    -- NOTE: Old FactorCode N'46' was removed from the new MarketSurveyFactor list
INSERT INTO appraisal.MarketComparableTemplateFactors
    (TemplateId, FactorId, DisplaySequence, IsMandatory, CreatedAt, CreatedBy)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'69'), 1, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'70'), 2, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'71'), 3, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'72'), 4, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'73'), 5, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'MACHINE_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'63'), 6, 0, GETDATE(), N'SYSTEM');
GO
-- Template: LEASE_AGREEMENT_TEMPLATE
    -- NOTE: Old FactorCode N'01' was removed from the new MarketSurveyFactor list
INSERT INTO appraisal.MarketComparableTemplateFactors
    (TemplateId, FactorId, DisplaySequence, IsMandatory, CreatedAt, CreatedBy)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'16'), 1, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'51'), 2, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'30'), 3, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'74'), 4, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'46'), 5, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'04'), 6, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'06'), 7, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'01'), 8, 0, GETDATE(), N'SYSTEM'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableTemplates WHERE TemplateCode = N'LEASE_AGREEMENT_TEMPLATE'), (SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'29'), 9, 0, GETDATE(), N'SYSTEM');
GO

-- ============================================================
-- 4. MarketComparableFactorTranslations (74 EN + 74 TH)
-- ============================================================

-- English translations
INSERT INTO appraisal.MarketComparableFactorTranslations
    (MarketComparableFactorId, Language, FactorName)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'01'), N'en', N'Sq. Wa'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'02'), N'en', N'Land Fill Condition'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'03'), N'en', N'Land Level'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'04'), N'en', N'Wide frontage of land adjacent to the road'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'05'), N'en', N'Number of sides facing the road'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'06'), N'en', N'Land shape'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'07'), N'en', N'Building Name'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'08'), N'en', N'Building Model Name'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'09'), N'en', N'Building Details'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'10'), N'en', N'Building Condition'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'11'), N'en', N'Building width'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'12'), N'en', N'Building Age'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'13'), N'en', N'Building Area/Useable Area'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'14'), N'en', N'Division of interior space'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'15'), N'en', N'Building extension'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'16'), N'en', N'Address/Location'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'17'), N'en', N'Project Name/Village name'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'18'), N'en', N'Room types'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'19'), N'en', N'Total Room'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'20'), N'en', N'Occupancy rate'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'21'), N'en', N'Rental Price'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'22'), N'en', N'Water and electricity rates (baht/month)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'23'), N'en', N'Room Size'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'24'), N'en', N'Road surface'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'25'), N'en', N'Entry and exit rights'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'26'), N'en', N'Maximum Utilization'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'27'), N'en', N'Current Utilization/ Land Use'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'28'), N'en', N'Types of urban planning'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'29'), N'en', N'Utilities'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'30'), N'en', N'Facility'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'31'), N'en', N'Offering Price (Condition The bank has accepted the price)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'32'), N'en', N'Other Details'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'33'), N'en', N'Latitude'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'34'), N'en', N'Longtitude'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'35'), N'en', N'Total Floor'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'36'), N'en', N'Total Building'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'37'), N'en', N'Total Unit'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'38'), N'en', N'Land Area (in Rai)  Rai, Ngan, Wah either one field must have value.'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'39'), N'en', N'Land Area (in Ngan)  Rai, Ngan, Wah either one field must have value.'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'40'), N'en', N'Land Area (in Wah)  Rai, Ngan, Wah either one field must have value.'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'41'), N'en', N'Room Layout'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'42'), N'en', N'Room Floor'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'43'), N'en', N'Plot Location'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'44'), N'en', N'Developer Reputation'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'45'), N'en', N'Building Style'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'46'), N'en', N'Environment'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'47'), N'en', N'Market Demand'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'48'), N'en', N'Room Condition'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'49'), N'en', N'Location - view'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'50'), N'en', N'Unit Type Detail'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'51'), N'en', N'Road passing in front of the land'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'52'), N'en', N'Liquidity'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'53'), N'en', N'Local Development / Development in the Area'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'54'), N'en', N'Location'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'55'), N'en', N'Series'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'56'), N'en', N'Brand'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'57'), N'en', N'Engine Number'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'58'), N'en', N'Machine Capacity'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'59'), N'en', N'Machine name and details'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'60'), N'en', N'Machine Width'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'61'), N'en', N'Machine Length'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'62'), N'en', N'Machine Height'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'63'), N'en', N'Manufacturer'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'64'), N'en', N'Machine Model'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'65'), N'en', N'Energy used'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'66'), N'en', N'Usage Condition'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'67'), N'en', N'Utilization/ Usage Purpose'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'68'), N'en', N'Year of use'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'69'), N'en', N'Machinery Efficiency'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'70'), N'en', N'Machinery Technology'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'71'), N'en', N'Machinery Material/Assembly'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'72'), N'en', N'Machinery Condition'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'73'), N'en', N'Machinery Age (n)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'74'), N'en', N'Rules/Laws');
GO

-- Thai translations
INSERT INTO appraisal.MarketComparableFactorTranslations
    (MarketComparableFactorId, Language, FactorName)
VALUES
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'01'), N'th', N'เนื้อที่ดิน (ไร่)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'02'), N'th', N'สภาพที่ดิน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'03'), N'th', N'ระดับที่ดิน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'04'), N'th', N'หน้ากว้างที่ดินติดถนน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'05'), N'th', N'จํานวนด้านที่ติดถนน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'06'), N'th', N'รูปแปลงที่ดิน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'07'), N'th', N'ชื่ออาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'08'), N'th', N'ชื่อแบบอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'09'), N'th', N'รายละเอียดอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'10'), N'th', N'สภาพอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'11'), N'th', N'หน้ากว้างอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'12'), N'th', N'อายุอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'13'), N'th', N'ขนาดอาคาร/พื้นที่ใช้สอย'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'14'), N'th', N'การแบ่งพื้นที่ภายใน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'15'), N'th', N'ส่วนต่อเติมอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'16'), N'th', N'เลขที่/ที่ตั้ง/ทำเล'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'17'), N'th', N'ชื่อโครงการ/หมู่บ้าน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'18'), N'th', N'รูปแบบห้องพัก'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'19'), N'th', N'จํานวนห้องพักทั้งหมด'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'20'), N'th', N'อัตราเข้าพัก'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'21'), N'th', N'อัตราค่าเช่า'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'22'), N'th', N'อัตราค่าน้ำค่าไฟ(บาท/เดือน)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'23'), N'th', N'ขนาดห้อง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'24'), N'th', N'ผิวจราจร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'25'), N'th', N'สิทธิการเข้าออก'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'26'), N'th', N'การใช้ประโยชน์สูงสุด'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'27'), N'th', N'การใช้ประโยชน์ปัจจุบัน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'28'), N'th', N'ประเภทผังเมือง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'29'), N'th', N'ระบบสาธารณูปโภค'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'30'), N'th', N'สิ่งอำนวยความสะดวก'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'31'), N'th', N'ราคาเสนอขาย เงื่อนไข ธนาคารเคยรับราคา'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'32'), N'th', N'รายละเอียดอื่นๆ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'33'), N'th', N'ละติจูด (Latitude)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'34'), N'th', N'ลองติจูด (Longitude)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'35'), N'th', N'จํานวนชั้น'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'36'), N'th', N'จํานวนอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'37'), N'th', N'จํานวนห้อง(ยูนิต)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'38'), N'th', N'เนื้อที่ดิน (ไร่)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'39'), N'th', N'เนื้อที่ดิน (งาน)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'40'), N'th', N'เนื้อที่ดิน (วา)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'41'), N'th', N'แบบห้อง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'42'), N'th', N'ชั้นที่ตั้งห้องชุด'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'43'), N'th', N'ทำเลแปลง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'44'), N'th', N'ชื่อเสียงผู้ประกอบการ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'45'), N'th', N'รูปแบบอาคาร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'46'), N'th', N'สภาพแวดล้อม'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'47'), N'th', N'ความต้องการตลาด'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'48'), N'th', N'สภาพห้องชุด'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'49'), N'th', N'ทำเลวิว'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'50'), N'th', N'รายละเอียดรูปแบบ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'51'), N'th', N'ถนนผ่านหน้าที่ดิน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'52'), N'th', N'สภาพคล่อง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'53'), N'th', N'การพัฒนาในพื้นที่'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'54'), N'th', N'ตำแหน่งที่ตั้ง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'55'), N'th', N'รุ่น'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'56'), N'th', N'ยี่ห้อ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'57'), N'th', N'หมายเลขเครื่อง'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'58'), N'th', N'ขนาดความสามารถ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'59'), N'th', N'ชื่อและรายละเอียดเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'60'), N'th', N'ขนาดเครื่อง (กว้าง)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'61'), N'th', N'ขนาดเครื่อง (ยาว)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'62'), N'th', N'ขนาดเครื่อง (สูง)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'63'), N'th', N'ผู้ผลิต'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'64'), N'th', N'แบบเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'65'), N'th', N'พลังงานที่ใช้'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'66'), N'th', N'สภาพการใช้งาน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'67'), N'th', N'ใช้ในการ'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'68'), N'th', N'ปีที่ใช้งาน'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'69'), N'th', N'ประสิทธิภาพเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'70'), N'th', N'เทคโนโลยีเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'71'), N'th', N'วัสดุ/การประกอบเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'72'), N'th', N'สภาพเครื่องจักร'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'73'), N'th', N'อายุเครื่องจักร (n)'),
    ((SELECT TOP 1 Id FROM appraisal.MarketComparableFactors WHERE FactorCode = N'74'), N'th', N'ข้อกำหนดกฎหมาย');
GO
