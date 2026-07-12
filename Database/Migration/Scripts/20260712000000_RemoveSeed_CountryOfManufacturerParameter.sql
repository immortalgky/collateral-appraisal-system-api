-- ----------------------------------------
-- Remove orphaned CountryOfManufacturer parameter group.
-- The machinery "Country of Manufacture" dropdown sources options from the full ISO
-- `Country` group instead; these placeholder rows (codes 01-24) are no longer referenced
-- by any frontend/backend code, view, or stored procedure.
-- ----------------------------------------
DELETE FROM parameter.Parameters WHERE [group] = N'CountryOfManufacturer';
GO
