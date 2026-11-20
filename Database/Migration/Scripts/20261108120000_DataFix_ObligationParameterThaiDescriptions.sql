-- ============================================================
-- Data fix: the 'Obligation' parameter group was seeded (20260317002600_SeedData_GeneralParameter.sql)
-- with English text in its TH rows, so the Thai UI shows English chips.
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
JOIN  (VALUES (N'01', N'ไม่มีภาระผูกพัน'),
              (N'02', N'จำนองเป็นประกัน'),
              (N'03', N'ติดสัญญาเช่า'),
              (N'04', N'สิทธิเก็บกิน'),
              (N'05', N'สิทธิเหนือพื้นดิน'),
              (N'99', N'อื่นๆ')) v(code, th) ON v.code = p.[code]
WHERE  p.[group] = N'Obligation' AND p.[language] = N'TH';

SELECT @@ROWCOUNT AS ObligationThaiRowsUpdated;
GO
