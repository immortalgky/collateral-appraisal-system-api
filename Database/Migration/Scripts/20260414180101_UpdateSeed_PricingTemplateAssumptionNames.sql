-- Backfill AssumptionName for rows seeded before the name mapping was established.
-- Idempotent: only updates rows where AssumptionName is empty, so it's safe to re-run.
-- M99 rows are skipped (template-specific custom names that must not be overwritten).
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'parameter') RETURN;

UPDATE parameter.PricingTemplateAssumptions
SET AssumptionName = CASE AssumptionType
    WHEN 'I00' THEN 'Room Income'
    WHEN 'I01' THEN 'Room Rental Income'
    WHEN 'I02' THEN 'Average Rental Rate'
    WHEN 'I03' THEN 'Energy Income'
    WHEN 'I04' THEN 'Utility Income'
    WHEN 'I05' THEN 'Food and Beverage Income'
    WHEN 'I06' THEN 'Other Income'
    WHEN 'E00' THEN 'Administration Fee'
    WHEN 'E01' THEN 'Advertising and Promotion Costs'
    WHEN 'E02' THEN 'Common Utility Fees'
    WHEN 'E03' THEN 'Contingency Expenses'
    WHEN 'E04' THEN 'Cost of Income from Utilities'
    WHEN 'E05' THEN 'Energy Cost'
    WHEN 'E06' THEN 'Fire Insurance Premium'
    WHEN 'E07' THEN 'Food and Beverage Expenses'
    WHEN 'E08' THEN 'Marketing and Promotion Costs'
    WHEN 'E09' THEN 'Operational and Administrative Expenses'
    WHEN 'E10' THEN 'Other Expenses'
    WHEN 'E11' THEN 'Project Management Compensation'
    WHEN 'E12' THEN 'Property Tax'
    WHEN 'E13' THEN 'Repair and Maintenance Costs'
    WHEN 'E14' THEN 'Reserve Funds for Building Improvements'
    WHEN 'E15' THEN 'Room Cost'
    WHEN 'E16' THEN 'Salary and Benefits'
    WHEN 'E17' THEN 'Sales and Marketing Expenses'
    WHEN 'E18' THEN 'Utility Expenses'
    WHEN 'E19' THEN 'Other Costs'
    WHEN 'E20' THEN 'Reserve for Asset Maintenance (FF&E / Cap Ex)'
    ELSE AssumptionName
END
WHERE AssumptionName = ''
  AND AssumptionType <> 'M99';
