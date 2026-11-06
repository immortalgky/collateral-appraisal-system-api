-- ============================================================
-- CA: Rename parameter group 'Header' -> 'LawRegulationHeader' and re-label it (EN=6, TH=6)
--
-- Feeds the "หัวข้อ" (Header) dropdown on the Law & Regulation page
-- (appraisal.LawAndRegulations.HeaderCode). The original seed in
-- 20260317002600_SeedData_GeneralParameter.sql used the generic group name 'Header' with
-- placeholder labels ('01 - 01', '02 - 02', '03 - 03', '99 - 99'). This script:
--   1. Renames the group to 'LawRegulationHeader' (the old 'Header' rows are removed, not kept
--      alongside).
--   2. Replaces the placeholder labels with real ones and adds two headings
--      (04 Expropriation, 05 Forest / state reserved land).
--
-- DEPLOY TOGETHER WITH THE FRONTEND: the FE now reads group 'LawRegulationHeader'
-- (collateral-appraisal-system-app, branch fix/parameter/law-header-labels). Deploying only one
-- side leaves the dropdown empty.
--
-- Codes 01/02/03/99 are unchanged, so HeaderCode values already saved in LawAndRegulations
-- are untouched and keep a label; 99 stays "other". SeqNo is the display order, so 99 sits
-- third and 03/04/05 follow it.
--
-- DELETE-then-INSERT: nothing references parameter.Parameters by ParId (LawAndRegulations
-- stores only HeaderCode).
--
-- Re-runnable: the DELETE of both group names makes the INSERT idempotent if replayed.
--
-- NOTE: CachedParameterRepository caches with no TTL and no eviction on write — restart the API
-- after running this or the dropdown will keep serving the old rows.
-- ============================================================
SET NOCOUNT ON;

DELETE FROM parameter.Parameters WHERE [group] IN (N'Header', N'LawRegulationHeader');

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LawRegulationHeader', N'TH', N'EN', N'01', N'Town Planning Regulations', 1, 1),
    (N'LawRegulationHeader', N'TH', N'TH', N'01', N'ข้อกำหนดผังเมือง', 1, 1),
    (N'LawRegulationHeader', N'TH', N'EN', N'02', N'Building Control Law', 1, 2),
    (N'LawRegulationHeader', N'TH', N'TH', N'02', N'กฎหมายควบคุมอาคาร', 1, 2),
    (N'LawRegulationHeader', N'TH', N'EN', N'99', N'Other Laws', 1, 3),
    (N'LawRegulationHeader', N'TH', N'TH', N'99', N'กฎหมายอื่นๆ', 1, 3),
    (N'LawRegulationHeader', N'TH', N'EN', N'03', N'Government Development Projects', 1, 4),
    (N'LawRegulationHeader', N'TH', N'TH', N'03', N'โครงการพัฒนาของภาครัฐ', 1, 4),
    (N'LawRegulationHeader', N'TH', N'EN', N'04', N'Expropriation', 1, 5),
    (N'LawRegulationHeader', N'TH', N'TH', N'04', N'การเวนคืน', 1, 5),
    (N'LawRegulationHeader', N'TH', N'EN', N'05', N'Forest / State Reserved Land Check', 1, 6),
    (N'LawRegulationHeader', N'TH', N'TH', N'05', N'การตรวจสอบพื้นที่ป่าไม้ หรือที่ดินสงวนหวงห้ามของรัฐ', 1, 6);
GO
