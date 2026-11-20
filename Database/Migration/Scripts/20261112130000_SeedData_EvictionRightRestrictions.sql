-- ============================================================
-- Parameter group 'Eviction' (การรอนสิทธิ์ of a land): widen the seeded 3-code list (01, 02, 99) to the 13 kinds the
-- appraiser picks from, 99 อื่นๆ last.
--
--   01  TH "อยู่ในแนวสายไฟฟ้าแรงสูง" -> "แนวสายส่งไฟฟ้าแรงสูง" and EN "High-Voltage Power Line" -> "High Voltage Transmission Line",
--       each only where it still holds the seeded text (an admin's edit is never overwritten)
--   02  unchanged
--   03-13  new rows, TH + EN, seqno = the code number, inserted only where (group, country, language, code) is not there
--   99  seqno 3 -> 14, only where it still holds the seeded seqno and 03 has taken seqno 3 (so a hand-set order is kept)
--
-- Idempotent: every step matches only what the previous run did not already change.
--
-- NOTE: CachedParameterRepository is an in-memory cache with a 1 hour absolute expiry; only writes made through the
-- repository evict it, so restart the API after running this script or each instance serves the old list until it expires.
-- ============================================================
SET NOCOUNT ON;

UPDATE parameter.Parameters
SET    [description] = N'แนวสายส่งไฟฟ้าแรงสูง'
WHERE  [group] = N'Eviction' AND [language] = N'TH' AND [code] = N'01' AND [description] = N'อยู่ในแนวสายไฟฟ้าแรงสูง';

UPDATE parameter.Parameters
SET    [description] = N'High Voltage Transmission Line'
WHERE  [group] = N'Eviction' AND [language] = N'EN' AND [code] = N'01' AND [description] = N'High-Voltage Power Line';

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
SELECT N'Eviction', N'TH', l.lang, v.code, CASE l.lang WHEN N'TH' THEN v.th ELSE v.en END, 1, CAST(v.code AS int)
FROM  (VALUES
        (N'03', N'แนวรถไฟ / รถไฟฟ้าบนดิน',                           N'Railway / Elevated Rail Transit'),
        (N'04', N'แนวเขตทางหลวง / แนวขยายเขตทาง',                    N'Highway Right-of-Way / Road Widening Line'),
        (N'05', N'แนวท่อส่งก๊าซธรรมชาติ',                              N'Natural Gas Pipeline Corridor'),
        (N'06', N'แนวท่อส่งน้ำมัน',                                    N'Oil Pipeline Corridor'),
        (N'07', N'แนวท่อประปาหลัก / สาธารณูปโภคใต้ดิน',               N'Water Main / Underground Utilities'),
        (N'08', N'ตกเป็นทางภาระจำยอม (จดทะเบียน)',                    N'Registered Servitude Passage'),
        (N'09', N'เป็นทางเข้าออกให้แปลงอื่นตามสภาพ (ไม่จดทะเบียน)',   N'De Facto Passage for Other Plots (Unregistered)'),
        (N'10', N'ทางจำเป็น',                                          N'Statutory Right of Way'),
        (N'11', N'ถนน / สาธารณูปโภคในโครงการจัดสรร',                  N'Housing Estate Road / Utilities'),
        (N'12', N'เขตคลองชลประทาน',                                    N'Irrigation Canal Zone'),
        (N'13', N'แนวคันกั้นน้ำ / เขตป้องกันน้ำท่วม',                 N'Flood Embankment / Flood Protection Zone')
      ) v(code, th, en)
CROSS JOIN (VALUES (N'TH'), (N'EN')) l(lang)
WHERE NOT EXISTS (SELECT 1 FROM parameter.Parameters p
                  WHERE p.[group] = N'Eviction' AND p.[country] = N'TH' AND p.[language] = l.lang AND p.[code] = v.code);

SELECT @@ROWCOUNT AS EvictionRowsInserted;

UPDATE parameter.Parameters
SET    [seqno] = 14
WHERE  [group] = N'Eviction' AND [code] = N'99' AND [seqno] = 3
  AND  EXISTS (SELECT 1 FROM parameter.Parameters x WHERE x.[group] = N'Eviction' AND x.[code] = N'03' AND x.[seqno] = 3);

SELECT @@ROWCOUNT AS EvictionOtherMovedLast;
GO
