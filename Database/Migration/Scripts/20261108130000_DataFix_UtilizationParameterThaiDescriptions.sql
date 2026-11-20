-- ============================================================
-- Data fix: the 'Utilization' parameter group (building's current use, BuildingAppraisalDetails.UtilizationType)
-- was seeded with English text in its TH rows, so the Thai UI and the appraisal book show English.
--
-- Updates ONLY the Description of the TH rows. The EN rows keep their English text, and codes,
-- seq numbers and IsActive are untouched. Replay-safe: each UPDATE is keyed by group/language/code.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write; restart the API after
-- running this or the old English text keeps being served.
-- ============================================================
SET NOCOUNT ON;

UPDATE p
SET    p.[description] = v.th
FROM   parameter.Parameters p
JOIN  (VALUES (N'01', N'ใช้อยู่อาศัย'),
              (N'02', N'อยู่อาศัยไม่น้อยกว่า 30%'),
              (N'03', N'อยู่อาศัยน้อยกว่า 30%'),
              (N'04', N'แบ่งให้เช่า'),
              (N'99', N'ใช้ประโยชน์อื่น')) v(code, th) ON v.code = p.[code]
WHERE  p.[group] = N'Utilization' AND p.[language] = N'TH';

SELECT @@ROWCOUNT AS UtilizationThaiRowsUpdated;
GO
