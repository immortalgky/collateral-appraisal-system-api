-- ============================================================
-- Data fix: the 'CondoLocation' parameter group (ตำแหน่งห้องชุด: 1 = correct, 2 = incorrect) was seeded by
-- 20260317002600_SeedData_GeneralParameter.sql with the English text in its TH rows.
--
--   TH 1 : "Correct"   -> ถูกต้อง
--   TH 2 : "Incorrect" -> ไม่ถูกต้อง
--
-- Only where the TH description still equals the seeded English text, so a description an admin already edited is never
-- overwritten. Codes, seq numbers, IsActive and the EN rows are untouched. Replay-safe: a second run matches nothing.
--
-- NOTE: CachedParameterRepository is an in-memory cache with a 1 hour absolute expiry; only writes made through the
-- repository evict it, so restart the API after running this script or each instance serves the old text until it expires.
-- ============================================================
SET NOCOUNT ON;

UPDATE p
SET    p.[description] = v.th
FROM   parameter.Parameters p
JOIN  (VALUES (N'1', N'Correct',   N'ถูกต้อง'),
              (N'2', N'Incorrect', N'ไม่ถูกต้อง')) v(code, en, th)
       ON v.code = p.[code]
WHERE  p.[group] = N'CondoLocation'
  AND  p.[language] = N'TH'
  AND  p.[description] = v.en;

SELECT @@ROWCOUNT AS CondoLocationThaiRowsUpdated;
GO
