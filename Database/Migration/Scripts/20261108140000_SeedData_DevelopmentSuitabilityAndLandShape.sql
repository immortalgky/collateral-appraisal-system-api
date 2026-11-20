-- ============================================================
-- CA: split the 'LandShape' parameter group.
--
-- LandShape mixed two topics: codes 01-03 are suitability for development, 04-08/99 are the geometric
-- shape of the plot. The suitability options move to a new group 'DevelopmentSuitability'
-- (appraisal.LandAppraisalDetails.DevelopmentSuitabilityType, ProjectLands.DevelopmentSuitabilityType)
-- and LandShape keeps only the shapes, now with Thai descriptions.
--
--   DevelopmentSuitability  new group, 01-03, TH + EN
--   LandShape 01-03         IsActive = 0 (kept so old references still resolve in the admin screen)
--   LandShape 04-08, 99     TH description translated; EN 05 spelling fixed ("Retangle" -> "Rectangle")
--
-- DevelopmentSuitability is a new group, so DELETE-then-INSERT keeps the script safe to replay. The LandShape
-- UPDATEs are keyed by group/language/code. The data move (LandShapeType -> DevelopmentSuitabilityType) is a
-- separate script: 20261108140100_DataFix_LandShapeToDevelopmentSuitability.sql.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write; restart the API after
-- running this or the dropdowns keep serving the old lists.
-- ============================================================
SET NOCOUNT ON;

DELETE FROM parameter.Parameters WHERE [group] = N'DevelopmentSuitability';

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DevelopmentSuitability', N'TH', N'EN', N'01', N'Shape and size suitable for development, highly beneficial', 1, 1),
    (N'DevelopmentSuitability', N'TH', N'TH', N'01', N'รูปแปลงและขนาดเหมาะสมต่อการพัฒนา ให้ประโยชน์ได้มาก', 1, 1),
    (N'DevelopmentSuitability', N'TH', N'EN', N'02', N'Shape and size suitable for development, moderately beneficial', 1, 2),
    (N'DevelopmentSuitability', N'TH', N'TH', N'02', N'รูปแปลงและขนาดเหมาะสมต่อการพัฒนา ให้ประโยชน์ปานกลาง', 1, 2),
    (N'DevelopmentSuitability', N'TH', N'EN', N'03', N'Shape and size not suitable for development', 1, 3),
    (N'DevelopmentSuitability', N'TH', N'TH', N'03', N'รูปแปลงและขนาดไม่เหมาะสมต่อการพัฒนา', 1, 3);

-- LandShape: suitability options leave the group
UPDATE parameter.Parameters SET [isactive] = 0
WHERE  [group] = N'LandShape' AND [code] IN (N'01', N'02', N'03');

-- LandShape: Thai descriptions for the shapes (TH rows only)
UPDATE p
SET    p.[description] = v.th
FROM   parameter.Parameters p
JOIN  (VALUES (N'04', N'สี่เหลี่ยมจัตุรัส'),
              (N'05', N'สี่เหลี่ยมผืนผ้า'),
              (N'06', N'สี่เหลี่ยมคางหมู'),
              (N'07', N'สามเหลี่ยมชายธง'),
              (N'08', N'หลายเหลี่ยม'),
              (N'99', N'อื่นๆ')) v(code, th) ON v.code = p.[code]
WHERE  p.[group] = N'LandShape' AND p.[language] = N'TH';

-- LandShape: EN spelling
UPDATE parameter.Parameters SET [description] = N'Rectangle'
WHERE  [group] = N'LandShape' AND [language] = N'EN' AND [code] = N'05';
GO
