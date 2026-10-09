-- ============================================================
-- Seed: reasons a slice of a land parcel's registered area is not appraisable.
-- Group: LandAreaDeductionReason
--
-- The bank already words the policy this way in the Limitation group, code 03:
--   "ถูกรุกล้ำ / ใช้เพื่อบุคคลอื่น / ตัดเนื้อที่ประเมินเนื่องจากสาเหตุอื่น"
-- Encroachment is one reason to cut appraised area, not the only one — hence a reason list
-- rather than an encroachment list.
--
-- Every reason here costs THIS parcel usable area, so every one of them is deducted. The row's
-- code and remark say which; there is no separate direction column.
--
-- Deliberately NOT on this list: "รุกล้ำที่ดินผู้อื่น" — our own structure standing on a neighbour's
-- land. That costs this parcel no area at all; the deed is untouched and fully usable. What is at
-- risk is the part of the BUILDING sitting on someone else's land, which the building form already
-- records as IsEncroachingOthers / EncroachingOthersArea. Adding it here would shrink a land area
-- that never shrank.
--
-- Idempotent: skips the block if code '01' of this group already exists.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM parameter.Parameters
               WHERE [Group] = N'LandAreaDeductionReason' AND [Code] = N'01')
BEGIN

    INSERT INTO parameter.Parameters ([Group], Country, [Language], [Code], [Description], IsActive, SeqNo) VALUES
    (N'LandAreaDeductionReason', N'TH', N'TH', N'01', N'ถูกรุกล้ำ – สิ่งปลูกสร้าง',            1,  1),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'01', N'Encroached - building',              1,  1),
    (N'LandAreaDeductionReason', N'TH', N'TH', N'02', N'ถูกรุกล้ำ – รั้ว/กำแพง',              1,  2),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'02', N'Encroached - fence/wall',            1,  2),
    (N'LandAreaDeductionReason', N'TH', N'TH', N'03', N'ถูกรุกล้ำ – ถนน/ทางเข้าออก',          1,  3),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'03', N'Encroached - road/access',           1,  3),
    (N'LandAreaDeductionReason', N'TH', N'TH', N'04', N'ใช้เพื่อบุคคลอื่น / ทางภาระจำยอม',     1,  4),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'04', N'Used by others / servitude',         1,  4),
    (N'LandAreaDeductionReason', N'TH', N'TH', N'05', N'แนวสายส่งไฟฟ้าแรงสูง',                1,  5),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'05', N'High-voltage line',                  1,  5),
    (N'LandAreaDeductionReason', N'TH', N'TH', N'06', N'ลำราง / คลอง / บ่อน้ำสาธารณะ',        1,  6),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'06', N'Public waterway',                    1,  6),
    -- 99 is this codebase's "other" convention (LandCheckMethodType, BuildingType and the rest
    -- all use it), so the free-text reason follows it rather than continuing the sequence.
    (N'LandAreaDeductionReason', N'TH', N'TH', N'99', N'สาเหตุอื่น',                          1, 99),
    (N'LandAreaDeductionReason', N'TH', N'EN', N'99', N'Other',                              1, 99);

END;
