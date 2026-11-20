-- ============================================================
-- Data fix: the 'Environment' parameter group (สภาพแวดล้อม — now on the land detail as well as the condo detail).
-- 20260317002600_SeedData_GeneralParameter.sql seeded its TH rows with the English text, so the Thai UI and the
-- appraisal book show English, and the EN row of code 07 reads "Industrial rea".
--
--   TH rows : the Thai description below, but ONLY where the TH description still equals the seeded English text
--             (code 07 also matches the "Industrial rea" typo), so a description an admin already edited is never overwritten.
--   EN 07   : "Industrial rea" -> "Industrial area", only where it is still that typo.
--
-- Codes, seq numbers and IsActive are untouched. Replay-safe: a second run finds nothing that still matches.
--
-- NOTE: CachedParameterRepository is an in-memory cache with a 1 hour absolute expiry; only writes made through the
-- repository evict it, so restart the API after running this script or each instance serves the old text until it expires.
-- ============================================================
SET NOCOUNT ON;

UPDATE p
SET    p.[description] = v.th
FROM   parameter.Parameters p
JOIN  (VALUES
        (N'01', N'Highly densely populated residential area',      N'Highly densely populated residential area',      N'ย่านที่อยู่อาศัยหนาแน่นมาก'),
        (N'02', N'Moderate densely populated residential area',    N'Moderate densely populated residential area',    N'ย่านที่อยู่อาศัยหนาแน่นปานกลาง'),
        (N'03', N'Low-Density residential area',                   N'Low-Density residential area',                   N'ย่านที่อยู่อาศัยหนาแน่นน้อย'),
        (N'04', N'Sparsely populated residential area, Rural',     N'Sparsely populated residential area, Rural',     N'ย่านที่อยู่อาศัยเบาบาง / ชนบท'),
        (N'05', N'Vacant land, far from community',                N'Vacant land, far from community',                N'ที่ดินว่างเปล่า ห่างไกลชุมชน'),
        (N'06', N'Commercial area',                                N'Commercial area',                                N'ย่านพาณิชยกรรม'),
        (N'07', N'Industrial area',                                N'Industrial rea',                                 N'ย่านอุตสาหกรรม'),
        (N'08', N'Agriculture area',                               N'Agriculture area',                               N'ย่านเกษตรกรรม'),
        (N'09', N'Central business district',                      N'Central business district',                      N'ย่านศูนย์กลางธุรกิจ'),
        (N'10', N'Mixed residential and commercial area',          N'Mixed residential and commercial area',          N'ย่านที่อยู่อาศัยผสมพาณิชยกรรม'),
        (N'99', N'Other',                                          N'Other',                                          N'อื่นๆ')
      ) v(code, en, alt, th)
       ON v.code = p.[code]
WHERE  p.[group] = N'Environment'
  AND  p.[language] = N'TH'
  AND  p.[description] IN (v.en, v.alt);

SELECT @@ROWCOUNT AS EnvironmentThaiRowsUpdated;

UPDATE parameter.Parameters
SET    [description] = N'Industrial area'
WHERE  [group] = N'Environment'
  AND  [language] = N'EN'
  AND  [code] = N'07'
  AND  [description] = N'Industrial rea';

SELECT @@ROWCOUNT AS EnvironmentEnglishTypoFixed;
GO
