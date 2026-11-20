-- ============================================================
-- CA: seed parameter group 'UrbanPlanningColor' (สีผังเมืองตามแผนผังจริง).
--
-- Feeds appraisal.LandAppraisalDetails / CondoAppraisalDetails / ProjectLands .UrbanPlanningColor
-- (single code; 99 = other, the free text is in UrbanPlanningColorOther).
--
-- New group, so DELETE-then-INSERT keeps the script safe to replay.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write; restart the API after
-- running this or the dropdown keeps serving the old (empty) list.
-- ============================================================
SET NOCOUNT ON;

DELETE FROM parameter.Parameters WHERE [group] = N'UrbanPlanningColor';

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'UrbanPlanningColor', N'TH', N'EN', N'01', N'Red', 1, 1),
    (N'UrbanPlanningColor', N'TH', N'TH', N'01', N'สีแดง', 1, 1),
    (N'UrbanPlanningColor', N'TH', N'EN', N'02', N'Brown', 1, 2),
    (N'UrbanPlanningColor', N'TH', N'TH', N'02', N'สีน้ำตาล', 1, 2),
    (N'UrbanPlanningColor', N'TH', N'EN', N'03', N'Orange', 1, 3),
    (N'UrbanPlanningColor', N'TH', N'TH', N'03', N'สีส้ม', 1, 3),
    (N'UrbanPlanningColor', N'TH', N'EN', N'04', N'Yellow', 1, 4),
    (N'UrbanPlanningColor', N'TH', N'TH', N'04', N'สีเหลือง', 1, 4),
    (N'UrbanPlanningColor', N'TH', N'EN', N'05', N'Purple', 1, 5),
    (N'UrbanPlanningColor', N'TH', N'TH', N'05', N'สีม่วง', 1, 5),
    (N'UrbanPlanningColor', N'TH', N'EN', N'06', N'Green', 1, 6),
    (N'UrbanPlanningColor', N'TH', N'TH', N'06', N'สีเขียว', 1, 6),
    (N'UrbanPlanningColor', N'TH', N'EN', N'07', N'White with green diagonal hatching', 1, 7),
    (N'UrbanPlanningColor', N'TH', N'TH', N'07', N'สีขาวมีกรอบและเส้นทแยงสีเขียว', 1, 7),
    (N'UrbanPlanningColor', N'TH', N'EN', N'08', N'Blue', 1, 8),
    (N'UrbanPlanningColor', N'TH', N'TH', N'08', N'สีน้ำเงิน', 1, 8),
    (N'UrbanPlanningColor', N'TH', N'EN', N'09', N'Light brown', 1, 9),
    (N'UrbanPlanningColor', N'TH', N'TH', N'09', N'สีน้ำตาลอ่อน', 1, 9),
    (N'UrbanPlanningColor', N'TH', N'EN', N'99', N'Other', 1, 10),
    (N'UrbanPlanningColor', N'TH', N'TH', N'99', N'อื่นๆ', 1, 10);
GO
