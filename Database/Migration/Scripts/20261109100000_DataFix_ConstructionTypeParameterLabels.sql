-- ============================================================
-- Data fix: the 'ConstructionType' parameter group (BuildingAppraisalDetails.ConstructionType, the building's
-- type of construction) was seeded with English text in its TH rows (20260317002600_SeedData_GeneralParameter.sql),
-- so the Thai UI and the appraisal book show "House Estate" / "Build It Yourself".
--
--   01  TH บ้านในโครงการจัดสรร   EN Housing Estate   (was "House Estate")
--   02  TH ปลูกสร้างเอง          EN Self-Built       (was "Build It Yourself")
--   99  TH อื่นๆ                 EN Other            (EN unchanged)
--
-- Updates ONLY the Description. Codes, seq numbers and IsActive are untouched; values already stored on a
-- building (including the legacy 'HES' / 'Permanent') are not touched either. Replay-safe: each UPDATE is
-- keyed by group/language/code.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write; restart the API after
-- running this or the old English text keeps being served.
-- ============================================================
SET NOCOUNT ON;

UPDATE p
SET    p.[description] = v.[description]
FROM   parameter.Parameters p
JOIN  (VALUES (N'TH', N'01', N'บ้านในโครงการจัดสรร'),
              (N'TH', N'02', N'ปลูกสร้างเอง'),
              (N'TH', N'99', N'อื่นๆ'),
              (N'EN', N'01', N'Housing Estate'),
              (N'EN', N'02', N'Self-Built'),
              (N'EN', N'99', N'Other')) v([language], code, [description])
       ON v.[language] = p.[language] AND v.code = p.[code]
WHERE  p.[group] = N'ConstructionType';

SELECT @@ROWCOUNT AS ConstructionTypeRowsUpdated;
GO
