-- ============================================================
-- CA: Seed parameter groups for the property-detail multi-select chips (appraisal book)
--
--   BuildingMaintenance  -> appraisal.BuildingAppraisalDetails.MaintenanceType   (single code)
--   Door                 -> appraisal.BuildingAppraisalDetails.DoorType          (JSON array of codes)
--   Window               -> appraisal.BuildingAppraisalDetails.WindowType        (JSON array of codes)
--   Sanitary             -> appraisal.BuildingAppraisalDetails.SanitaryType      (JSON array of codes)
--   BuildingSystem       -> appraisal.BuildingAppraisalDetails.BuildingSystemType (JSON array of codes)
--   Pollution            -> appraisal.LandAppraisalDetails.PollutionType and
--                           appraisal.CondoAppraisalDetails.PollutionType        (JSON array of codes)
-- Code 99 = "other"; the matching *Other column holds the free text.
--
-- All six groups are new, so DELETE-then-INSERT per group keeps the script safe to replay.
-- Nothing references parameter.Parameters by ParId (the entity columns store only the code).
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write; restart the API after
-- running this or the dropdowns keep serving the old (empty) lists.
-- ============================================================
SET NOCOUNT ON;

DELETE FROM parameter.Parameters
WHERE [group] IN (N'BuildingMaintenance', N'Door', N'Window', N'Sanitary', N'BuildingSystem', N'Pollution');

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingMaintenance', N'TH', N'EN', N'01', N'Good', 1, 1),
    (N'BuildingMaintenance', N'TH', N'TH', N'01', N'ดี', 1, 1),
    (N'BuildingMaintenance', N'TH', N'EN', N'02', N'Fair', 1, 2),
    (N'BuildingMaintenance', N'TH', N'TH', N'02', N'ปานกลาง', 1, 2),
    (N'BuildingMaintenance', N'TH', N'EN', N'03', N'Below standard', 1, 3),
    (N'BuildingMaintenance', N'TH', N'TH', N'03', N'ต่ำกว่ามาตรฐาน', 1, 3),
    (N'BuildingMaintenance', N'TH', N'EN', N'99', N'Other', 1, 4),
    (N'BuildingMaintenance', N'TH', N'TH', N'99', N'อื่นๆ', 1, 4),
    (N'Door', N'TH', N'EN', N'01', N'Hardwood swing door', 1, 1),
    (N'Door', N'TH', N'TH', N'01', N'บานเปิดไม้เนื้อแข็ง', 1, 1),
    (N'Door', N'TH', N'EN', N'02', N'Plywood-HDF swing door', 1, 2),
    (N'Door', N'TH', N'TH', N'02', N'บานเปิดไม้อัด/HDF', 1, 2),
    (N'Door', N'TH', N'EN', N'03', N'Aluminium-framed glass door', 1, 3),
    (N'Door', N'TH', N'TH', N'03', N'บานกระจกกรอบอลูมิเนียม', 1, 3),
    (N'Door', N'TH', N'EN', N'04', N'Steel door', 1, 4),
    (N'Door', N'TH', N'TH', N'04', N'บานเหล็ก', 1, 4),
    (N'Door', N'TH', N'EN', N'05', N'Roller shutter', 1, 5),
    (N'Door', N'TH', N'TH', N'05', N'บานเหล็กม้วน', 1, 5),
    (N'Door', N'TH', N'EN', N'06', N'PVC-UPVC door', 1, 6),
    (N'Door', N'TH', N'TH', N'06', N'บาน PVC/UPVC', 1, 6),
    (N'Door', N'TH', N'EN', N'99', N'Other', 1, 7),
    (N'Door', N'TH', N'TH', N'99', N'อื่นๆ', 1, 7),
    (N'Window', N'TH', N'EN', N'01', N'Aluminium sliding window', 1, 1),
    (N'Window', N'TH', N'TH', N'01', N'บานเลื่อนกระจกกรอบอลูมิเนียม', 1, 1),
    (N'Window', N'TH', N'EN', N'02', N'Aluminium casement window', 1, 2),
    (N'Window', N'TH', N'TH', N'02', N'บานเปิดกระจกกรอบอลูมิเนียม', 1, 2),
    (N'Window', N'TH', N'EN', N'03', N'Awning window', 1, 3),
    (N'Window', N'TH', N'TH', N'03', N'บานกระทุ้ง', 1, 3),
    (N'Window', N'TH', N'EN', N'04', N'Glass louvre window', 1, 4),
    (N'Window', N'TH', N'TH', N'04', N'บานเกล็ดกระจก', 1, 4),
    (N'Window', N'TH', N'EN', N'05', N'Wooden window', 1, 5),
    (N'Window', N'TH', N'TH', N'05', N'บานไม้', 1, 5),
    (N'Window', N'TH', N'EN', N'06', N'UPVC window', 1, 6),
    (N'Window', N'TH', N'TH', N'06', N'บาน UPVC', 1, 6),
    (N'Window', N'TH', N'EN', N'99', N'Other', 1, 7),
    (N'Window', N'TH', N'TH', N'99', N'อื่นๆ', 1, 7),
    (N'Sanitary', N'TH', N'EN', N'01', N'Sit-down toilet', 1, 1),
    (N'Sanitary', N'TH', N'TH', N'01', N'โถส้วมแบบนั่งราบ (ชักโครก)', 1, 1),
    (N'Sanitary', N'TH', N'EN', N'02', N'Squat toilet', 1, 2),
    (N'Sanitary', N'TH', N'TH', N'02', N'โถส้วมแบบนั่งยอง', 1, 2),
    (N'Sanitary', N'TH', N'EN', N'03', N'Urinal', 1, 3),
    (N'Sanitary', N'TH', N'TH', N'03', N'โถปัสสาวะ', 1, 3),
    (N'Sanitary', N'TH', N'EN', N'04', N'Washbasin', 1, 4),
    (N'Sanitary', N'TH', N'TH', N'04', N'อ่างล้างหน้า', 1, 4),
    (N'Sanitary', N'TH', N'EN', N'05', N'Shower', 1, 5),
    (N'Sanitary', N'TH', N'TH', N'05', N'ฝักบัว', 1, 5),
    (N'Sanitary', N'TH', N'EN', N'06', N'Bathtub', 1, 6),
    (N'Sanitary', N'TH', N'TH', N'06', N'อ่างอาบน้ำ', 1, 6),
    (N'Sanitary', N'TH', N'EN', N'07', N'Water heater', 1, 7),
    (N'Sanitary', N'TH', N'TH', N'07', N'เครื่องทำน้ำอุ่น', 1, 7),
    (N'Sanitary', N'TH', N'EN', N'08', N'Mirror and fittings', 1, 8),
    (N'Sanitary', N'TH', N'TH', N'08', N'กระจกเงาและอุปกรณ์ประกอบ', 1, 8),
    (N'Sanitary', N'TH', N'EN', N'99', N'Other', 1, 9),
    (N'Sanitary', N'TH', N'TH', N'99', N'อื่นๆ', 1, 9),
    (N'BuildingSystem', N'TH', N'EN', N'01', N'Electrical', 1, 1),
    (N'BuildingSystem', N'TH', N'TH', N'01', N'ระบบไฟฟ้า', 1, 1),
    (N'BuildingSystem', N'TH', N'EN', N'02', N'Water supply', 1, 2),
    (N'BuildingSystem', N'TH', N'TH', N'02', N'ระบบน้ำประปา', 1, 2),
    (N'BuildingSystem', N'TH', N'EN', N'03', N'Sanitation', 1, 3),
    (N'BuildingSystem', N'TH', N'TH', N'03', N'ระบบสุขาภิบาล', 1, 3),
    (N'BuildingSystem', N'TH', N'EN', N'04', N'Telephone and communications', 1, 4),
    (N'BuildingSystem', N'TH', N'TH', N'04', N'ระบบโทรศัพท์/สื่อสาร', 1, 4),
    (N'BuildingSystem', N'TH', N'EN', N'05', N'Air conditioning', 1, 5),
    (N'BuildingSystem', N'TH', N'TH', N'05', N'ระบบปรับอากาศ', 1, 5),
    (N'BuildingSystem', N'TH', N'EN', N'06', N'Elevator', 1, 6),
    (N'BuildingSystem', N'TH', N'TH', N'06', N'ระบบลิฟต์', 1, 6),
    (N'BuildingSystem', N'TH', N'EN', N'07', N'Fire protection', 1, 7),
    (N'BuildingSystem', N'TH', N'TH', N'07', N'ระบบป้องกันอัคคีภัย', 1, 7),
    (N'BuildingSystem', N'TH', N'EN', N'08', N'CCTV', 1, 8),
    (N'BuildingSystem', N'TH', N'TH', N'08', N'ระบบกล้องวงจรปิด', 1, 8),
    (N'BuildingSystem', N'TH', N'EN', N'09', N'Wastewater treatment', 1, 9),
    (N'BuildingSystem', N'TH', N'TH', N'09', N'ระบบบำบัดน้ำเสีย', 1, 9),
    (N'BuildingSystem', N'TH', N'EN', N'10', N'Backup power', 1, 10),
    (N'BuildingSystem', N'TH', N'TH', N'10', N'ระบบไฟฟ้าสำรอง', 1, 10),
    (N'BuildingSystem', N'TH', N'EN', N'11', N'Solar power', 1, 11),
    (N'BuildingSystem', N'TH', N'TH', N'11', N'ระบบโซลาร์เซลล์', 1, 11),
    (N'BuildingSystem', N'TH', N'EN', N'99', N'Other', 1, 12),
    (N'BuildingSystem', N'TH', N'TH', N'99', N'อื่นๆ', 1, 12),
    (N'Pollution', N'TH', N'EN', N'01', N'Odour', 1, 1),
    (N'Pollution', N'TH', N'TH', N'01', N'กลิ่น', 1, 1),
    (N'Pollution', N'TH', N'EN', N'02', N'Noise', 1, 2),
    (N'Pollution', N'TH', N'TH', N'02', N'เสียง', 1, 2),
    (N'Pollution', N'TH', N'EN', N'03', N'Dust and smoke', 1, 3),
    (N'Pollution', N'TH', N'TH', N'03', N'ฝุ่นละออง/ควัน', 1, 3),
    (N'Pollution', N'TH', N'EN', N'04', N'Wastewater', 1, 4),
    (N'Pollution', N'TH', N'TH', N'04', N'น้ำเสีย', 1, 4),
    (N'Pollution', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'Pollution', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO
