-- ============================================================
-- Data fix: the 'NoHouseNumber' parameter group (codes 01 = ยังไม่ติดเลขที่บ้าน, 02 = ยังไม่ขอเลขที่บ้าน) is
-- renamed to 'HouseNumberStatus', matching the BuildingAppraisalDetails column and API field of the same name
-- (EF migration RenameNoHouseNumberToHouseNumberStatus). The original seed,
-- 20260317002600_SeedData_GeneralParameter.sql, still inserts the old name on a fresh database; this script runs
-- after it and renames those rows. Codes, descriptions, seq and IsActive are untouched.
--
-- Replay-safe: a second run finds no 'NoHouseNumber' rows.
--
-- Rows whose (country, language, code) already exists under 'HouseNumberStatus' are skipped, so the unique index
-- UQ_Parameters_Group_Country_Language_Code cannot be violated.
--
-- NOTE: CachedParameterRepository is an in-memory cache with a 1 hour absolute expiry that is only evicted by
-- writes made through the repository, not by this script. Each API instance serves the old group name until
-- the entry expires (up to 1 hour) or the instance restarts.
-- ============================================================
SET NOCOUNT ON;

UPDATE o
SET    o.[group] = N'HouseNumberStatus'
FROM   parameter.Parameters o
WHERE  o.[group] = N'NoHouseNumber'
  AND  NOT EXISTS (SELECT 1 FROM parameter.Parameters n
                   WHERE n.[group] = N'HouseNumberStatus' AND n.Country = o.Country
                     AND n.[Language] = o.[Language] AND n.Code = o.Code);

SELECT @@ROWCOUNT AS HouseNumberStatusRowsRenamed;
GO
