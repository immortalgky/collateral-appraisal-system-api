-- ----------------------------------------
-- Group: AdjustedPeriodPct (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES (N'AdjustedPeriodPct', N'TH', N'EN', N'01', N'Adjusted Period %', 1, 1),
       (N'AdjustedPeriodPct', N'TH', N'TH', N'01', N'Adjusted Period %', 1, 1);
GO

-- ----------------------------------------
-- Group: Allocation (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Allocation', N'TH', N'EN', N'01', N'Allocate New Projects', 1, 1),
    (N'Allocation', N'TH', N'TH', N'01', N'จัดสรรโครงการใหม่', 1, 1),
    (N'Allocation', N'TH', N'EN', N'02', N'Allocate Old Projects', 1, 2),
    (N'Allocation', N'TH', N'TH', N'02', N'จัดสรรโครงการเก่า', 1, 2),
    (N'Allocation', N'TH', N'EN', N'03', N'Not Allocate', 1, 3),
    (N'Allocation', N'TH', N'TH', N'03', N'ไม่จัดสรร', 1, 3);
GO

-- ----------------------------------------
-- Group: AnticipationOfProsperity (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AnticipationOfProsperity', N'TH', N'EN', N'01', N'Very Prosperous', 1, 1),
    (N'AnticipationOfProsperity', N'TH', N'TH', N'01', N'เจริญมาก', 1, 1),
    (N'AnticipationOfProsperity', N'TH', N'EN', N'02', N'Moderate', 1, 2),
    (N'AnticipationOfProsperity', N'TH', N'TH', N'02', N'ปานกลาง', 1, 2),
    (N'AnticipationOfProsperity', N'TH', N'EN', N'03', N'Likely to Prosper in the Future', 1, 3),
    (N'AnticipationOfProsperity', N'TH', N'TH', N'03', N'มีแนวโน้มเจริญในอนาคต', 1, 3),
    (N'AnticipationOfProsperity', N'TH', N'EN', N'04', N'Little Chance of Prosperity', 1, 4),
    (N'AnticipationOfProsperity', N'TH', N'TH', N'04', N'โอกาสเจริญน้อย', 1, 4),
    (N'AnticipationOfProsperity', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'AnticipationOfProsperity', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: AppendixImage (EN=14, TH=14)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppendixImage', N'TH', N'EN', N'01', N'Overview image', 1, 1),
    (N'AppendixImage', N'TH', N'TH', N'01', N'Overview image', 1, 1),
    (N'AppendixImage', N'TH', N'EN', N'02', N'Detailed close-up image', 1, 2),
    (N'AppendixImage', N'TH', N'TH', N'02', N'Detailed close-up image', 1, 2),
    (N'AppendixImage', N'TH', N'EN', N'03', N'Earth map', 1, 3),
    (N'AppendixImage', N'TH', N'TH', N'03', N'Earth map', 1, 3),
    (N'AppendixImage', N'TH', N'EN', N'04', N'Red zoning map (from the Department of Lands)', 1, 4),
    (N'AppendixImage', N'TH', N'TH', N'04', N'Red zoning map (from the Department of Lands)', 1, 4),
    (N'AppendixImage', N'TH', N'EN', N'05', N'City plan', 1, 5),
    (N'AppendixImage', N'TH', N'TH', N'05', N'City plan', 1, 5),
    (N'AppendixImage', N'TH', N'EN', N'06', N'Legal plan', 1, 6),
    (N'AppendixImage', N'TH', N'TH', N'06', N'Legal plan', 1, 6),
    (N'AppendixImage', N'TH', N'EN', N'07', N'Land layout', 1, 7),
    (N'AppendixImage', N'TH', N'TH', N'07', N'Land layout', 1, 7),
    (N'AppendixImage', N'TH', N'EN', N'08', N'Building layout', 1, 8),
    (N'AppendixImage', N'TH', N'TH', N'08', N'Building layout', 1, 8),
    (N'AppendixImage', N'TH', N'EN', N'09', N'Blueprint', 1, 9),
    (N'AppendixImage', N'TH', N'TH', N'09', N'Blueprint', 1, 9),
    (N'AppendixImage', N'TH', N'EN', N'10', N'Photographs with photo locations', 1, 10),
    (N'AppendixImage', N'TH', N'TH', N'10', N'Photographs with photo locations', 1, 10),
    (N'AppendixImage', N'TH', N'EN', N'11', N'Title deeds', 1, 11),
    (N'AppendixImage', N'TH', N'TH', N'11', N'Title deeds', 1, 11),
    (N'AppendixImage', N'TH', N'EN', N'12', N'Zoning information', 1, 12),
    (N'AppendixImage', N'TH', N'TH', N'12', N'Zoning information', 1, 12),
    (N'AppendixImage', N'TH', N'EN', N'13', N'Rawang', 1, 13),
    (N'AppendixImage', N'TH', N'TH', N'13', N'Rawang', 1, 13),
    (N'AppendixImage', N'TH', N'EN', N'14', N'Supporting documents', 1, 14),
    (N'AppendixImage', N'TH', N'TH', N'14', N'Supporting documents', 1, 14);
GO

-- ----------------------------------------
-- Group: AppointmentStatus (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppointmentStatus', N'TH', N'EN', N'P', N'Pending Approval', 1, 1),
    (N'AppointmentStatus', N'TH', N'TH', N'P', N'Pending Approval', 1, 1),
    (N'AppointmentStatus', N'TH', N'EN', N'A', N'Approved', 1, 2),
    (N'AppointmentStatus', N'TH', N'TH', N'A', N'Approved', 1, 2),
    (N'AppointmentStatus', N'TH', N'EN', N'R', N'Rejected', 1, 3),
    (N'AppointmentStatus', N'TH', N'TH', N'R', N'Rejected', 1, 3);
GO

-- ----------------------------------------
-- Group: AppraisalApplicationStatus (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'Pending', N'waiting assignment', 1, 1),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'Pending', N'รอการ assign', 1, 1),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'Assigned', N'assigned to appraiser or external admin', 1, 2),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'Assigned', N'assign ให้ผู้ประเมิน หรือ บริษัทประเมิน', 1, 2),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'InProgress', N'appraiser working on it', 1, 3),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'InProgress', N'ผุ้ประเมินทำการประเมิน', 1, 3),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'UnderReview', N'Checker/verification reviewing', 1, 4),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'UnderReview', N'Checker /verification ตรวจเล่ม', 1, 4),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'Completed', N'finalized and approved', 1, 5),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'Completed', N'อนุมัติเรียบร้อย', 1, 5),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'Cancelled', N'Appraisal cancelled', 1, 6),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'Cancelled', N'ยกเลิก', 1, 6),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'DocFollowup', N'pending document followup', 1, 7),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'DocFollowup', N'รอติดตามเอกสาร', 1, 7),
    (N'AppraisalApplicationStatus', N'TH', N'EN', N'Pending approval', N'Pending approval', 1, 8),
    (N'AppraisalApplicationStatus', N'TH', N'TH', N'Pending approval', N'รอการอนุมัติ', 1, 8);
GO

-- ----------------------------------------
-- Group: AppraisalFee (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalFee', N'TH', N'EN', N'1', N'External Appraisal fee', 1, 1),
    (N'AppraisalFee', N'TH', N'TH', N'1', N'External Appraisal fee', 1, 1);
GO

-- ----------------------------------------
-- Group: AppraisalPeriod (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalPeriod', N'TH', N'EN', N'01', N'Past 3 Years', 1, 1),
    (N'AppraisalPeriod', N'TH', N'TH', N'01', N'ผ่านมา 3 ปี', 1, 1),
    (N'AppraisalPeriod', N'TH', N'EN', N'02', N'Past 2 Years', 1, 2),
    (N'AppraisalPeriod', N'TH', N'TH', N'02', N'ผ่านมา 2 ปี', 1, 2),
    (N'AppraisalPeriod', N'TH', N'EN', N'03', N'Past 1 Year', 1, 3),
    (N'AppraisalPeriod', N'TH', N'TH', N'03', N'ผ่านมา 1 ปี', 1, 3),
    (N'AppraisalPeriod', N'TH', N'EN', N'04', N'Current', 1, 4),
    (N'AppraisalPeriod', N'TH', N'TH', N'04', N'ปีปัจจุบัน', 1, 4);
GO

-- ----------------------------------------
-- Group: AppraisalPurpose (EN=23, TH=23, Inactive=18)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalPurpose', N'TH', N'EN', N'01', N'Request for credit limit', 1, 1),
    (N'AppraisalPurpose', N'TH', N'TH', N'01', N'ขอวงเงินสินเชื่อ', 1, 1),
    (N'AppraisalPurpose', N'TH', N'EN', N'02', N'Request for credit limit increase', 1, 2),
    (N'AppraisalPurpose', N'TH', N'TH', N'02', N'ขอเพิ่มวงเงิน', 1, 2),
    (N'AppraisalPurpose', N'TH', N'EN', N'03', N'Review collateral value', 1, 3),
    (N'AppraisalPurpose', N'TH', N'TH', N'03', N'ทบทวนมูลค่าหลักประกัน', 1, 3),
    (N'AppraisalPurpose', N'TH', N'EN', N'04', N'Foreclose on debt', 1, 4),
    (N'AppraisalPurpose', N'TH', N'TH', N'04', N'ตีทรัพย์ชำระหนี้', 1, 4),
    (N'AppraisalPurpose', N'TH', N'EN', N'05', N'Property awaiting sale', 1, 5),
    (N'AppraisalPurpose', N'TH', N'TH', N'05', N'ทรัพย์สินรอการขาย', 1, 5),
    (N'AppraisalPurpose', N'TH', N'EN', N'M1', N'Add collateral data records to the LOS system', 0, 6),
    (N'AppraisalPurpose', N'TH', N'TH', N'M1', N'เพิ่มบันทึกข้อมูลหลักประกันเข้าระบบ LOS', 0, 6),
    (N'AppraisalPurpose', N'TH', N'EN', N'06', N'Inspect construction work', 1, 7),
    (N'AppraisalPurpose', N'TH', N'TH', N'06', N'ตรวจงวดงานก่อสร้าง', 1, 7),
    (N'AppraisalPurpose', N'TH', N'EN', N'M2', N'Request for credit limit (LOS system)', 0, 8),
    (N'AppraisalPurpose', N'TH', N'TH', N'M2', N'ขอวงเงินสินเชื่อ(ระบบ LOS)', 0, 8),
    (N'AppraisalPurpose', N'TH', N'EN', N'07', N'Evaluate prices to support small investors within the M/F project', 1, 9),
    (N'AppraisalPurpose', N'TH', N'TH', N'07', N'ประเมินราคาเพื่อสนับสนุนรายย่อยภายในโครงการ M/F', 1, 9),
    (N'AppraisalPurpose', N'TH', N'EN', N'M3', N'Review collateral value and request a credit limit increase', 0, 10),
    (N'AppraisalPurpose', N'TH', N'TH', N'M3', N'ทบทวนมูลค่าหลักประกันและขอเพิ่มวงเงิน', 0, 10),
    (N'AppraisalPurpose', N'TH', N'EN', N'M4', N'Review the collateral value according to the policy', 0, 11),
    (N'AppraisalPurpose', N'TH', N'TH', N'M4', N'ทบทวนมูลค่าหลักประกันตามนโยบาย', 0, 11),
    (N'AppraisalPurpose', N'TH', N'EN', N'M5', N'Change of appraisal certification', 0, 12),
    (N'AppraisalPurpose', N'TH', N'TH', N'M5', N'เปลี่ยนแปลงการรับรองราคาประเมิน', 0, 12),
    (N'AppraisalPurpose', N'TH', N'EN', N'M6', N'Check insurance claims', 0, 13),
    (N'AppraisalPurpose', N'TH', N'TH', N'M6', N'ตรวจสอบเคลมประกัน', 0, 13),
    (N'AppraisalPurpose', N'TH', N'EN', N'08', N'Check the machine installation', 1, 14),
    (N'AppraisalPurpose', N'TH', N'TH', N'08', N'ตรวจสอบการติดตั้งเครื่องจักร', 1, 14),
    (N'AppraisalPurpose', N'TH', N'EN', N'09', N'Review the value to support small investors within the M/F project', 1, 15),
    (N'AppraisalPurpose', N'TH', N'TH', N'09', N'ทบทวนมูลค่าเพื่อสนับสนุนรายย่อยภายในโครงการ M/F', 1, 15),
    (N'AppraisalPurpose', N'TH', N'EN', N'M7', N'Request for change of collateral', 0, 16),
    (N'AppraisalPurpose', N'TH', N'TH', N'M7', N'ขอเปลี่ยนแปลงหลักประกัน', 0, 16),
    (N'AppraisalPurpose', N'TH', N'EN', N'10', N'Check collateral damage', 1, 17),
    (N'AppraisalPurpose', N'TH', N'TH', N'10', N'ตรวจสอบความเสียหายของหลักประกัน', 1, 17),
    (N'AppraisalPurpose', N'TH', N'EN', N'11', N'100% construction inspection', 1, 18),
    (N'AppraisalPurpose', N'TH', N'TH', N'11', N'ตรวจสอบงานก่อสร้าง 100%', 1, 18),
    (N'AppraisalPurpose', N'TH', N'EN', N'M8', N'Request for credit limit increase (price assessment based on blueprint)', 0, 19),
    (N'AppraisalPurpose', N'TH', N'TH', N'M8', N'ขอเพิ่มวงเงิน (ประเมินราคาตามแบบแปลน)', 0, 19),
    (N'AppraisalPurpose', N'TH', N'EN', N'12', N'Request for credit limit (appeal, appraisal price)', 1, 20),
    (N'AppraisalPurpose', N'TH', N'TH', N'12', N'ขอวงเงินสินเชื่อ(อุทธรณ์ ราคาประเมิน)', 1, 20),
    (N'AppraisalPurpose', N'TH', N'EN', N'13', N'Review of collateral value (Asset warehousing)', 1, 21),
    (N'AppraisalPurpose', N'TH', N'TH', N'13', N'ทบทวนมูลค่าหลักประกัน (Asset warehousing)', 1, 21),
    (N'AppraisalPurpose', N'TH', N'EN', N'M9', N'Check assets', 0, 22),
    (N'AppraisalPurpose', N'TH', N'TH', N'M9', N'ตรวจสอบทรัพย์สิน', 0, 22),
    (N'AppraisalPurpose', N'TH', N'EN', N'14', N'Apply for Credit Limit (PMA)', 1, 23),
    (N'AppraisalPurpose', N'TH', N'TH', N'14', N'ขอวงเงินสินเชื่อ (PMA)', 1, 23);
GO

-- ----------------------------------------
-- Group: AppraisalRequestStatus (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalRequestStatus', N'TH', N'EN', N'01', N'Draft', 1, 1),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'01', N'Draft', 1, 1),
    (N'AppraisalRequestStatus', N'TH', N'EN', N'02', N'New', 1, 2),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'02', N'New', 1, 2),
    (N'AppraisalRequestStatus', N'TH', N'EN', N'03', N'UnderReview', 1, 3),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'03', N'UnderReview', 1, 3),
    (N'AppraisalRequestStatus', N'TH', N'EN', N'04', N'Submitted', 1, 4),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'04', N'Submitted', 1, 4),
    (N'AppraisalRequestStatus', N'TH', N'EN', N'05', N'Cancelled', 1, 5),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'05', N'Cancelled', 1, 5),
    (N'AppraisalRequestStatus', N'TH', N'EN', N'06', N'Completed', 1, 6),
    (N'AppraisalRequestStatus', N'TH', N'TH', N'06', N'Completed', 1, 6);
GO

-- ----------------------------------------
-- Group: AppraisalType (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AppraisalType', N'TH', N'EN', N'01', N'New Appraisal', 1, 1),
    (N'AppraisalType', N'TH', N'TH', N'01', N'ประเมินใหม่', 1, 1),
    (N'AppraisalType', N'TH', N'EN', N'02', N'Reappraisal', 1, 2),
    (N'AppraisalType', N'TH', N'TH', N'02', N'ทบทวน', 1, 2),
    (N'AppraisalType', N'TH', N'EN', N'03', N'Construction inspection/machine installation', 1, 3),
    (N'AppraisalType', N'TH', N'TH', N'03', N'ตรวจงวด/ติดตั้งเครื่องจักร', 1, 3),
    (N'AppraisalType', N'TH', N'EN', N'04', N'Block', 1, 4),
    (N'AppraisalType', N'TH', N'TH', N'04', N'ิBlock', 1, 4);
GO

-- ----------------------------------------
-- Group: Appraisal_Indicator (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Appraisal_Indicator', N'TH', N'EN', N'01', N'Appraise', 1, 1),
    (N'Appraisal_Indicator', N'TH', N'TH', N'01', N'ประเมิน', 1, 1),
    (N'Appraisal_Indicator', N'TH', N'EN', N'02', N'Not Appraised', 1, 2),
    (N'Appraisal_Indicator', N'TH', N'TH', N'02', N'ไม่ประเมิน', 1, 2);
GO

-- ----------------------------------------
-- Group: ApproachMethod (EN=10, TH=10)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ApproachMethod', N'TH', N'EN', N'WQS', N'Weighted Quality Scoring Method', 1, 1),
    (N'ApproachMethod', N'TH', N'TH', N'WQS', N'Weighted Quality Scoring Method', 1, 1),
    (N'ApproachMethod', N'TH', N'EN', N'SaleAjGrid', N'Sale Ajustment Grid Method', 1, 2),
    (N'ApproachMethod', N'TH', N'TH', N'SaleAjGrid', N'Sale Ajustment Grid Method', 1, 2),
    (N'ApproachMethod', N'TH', N'EN', N'DirectComp', N'Direct Comparison Method', 1, 3),
    (N'ApproachMethod', N'TH', N'TH', N'DirectComp', N'Direct Comparison Method', 1, 3),
    (N'ApproachMethod', N'TH', N'EN', N'BuildinCst', N'Buidling Cost', 1, 4),
    (N'ApproachMethod', N'TH', N'TH', N'BuildinCst', N'Buidling Cost', 1, 4),
    (N'ApproachMethod', N'TH', N'EN', N'ProfitRent', N'Profit Rent Analysis Method', 1, 8),
    (N'ApproachMethod', N'TH', N'TH', N'ProfitRent', N'Profit Rent Analysis Method', 1, 8),
    (N'ApproachMethod', N'TH', N'EN', N'Leasehold', N'DCF Leasehold', 1, 9),
    (N'ApproachMethod', N'TH', N'TH', N'Leasehold', N'DCF Leasehold', 1, 9),
    (N'ApproachMethod', N'TH', N'EN', N'MachineCst', N'Machinary Cost', 1, 10),
    (N'ApproachMethod', N'TH', N'TH', N'MachineCst', N'Machinary Cost', 1, 10),
    (N'ApproachMethod', N'TH', N'EN', N'DCS', N'Discounted Cashflow Analysis Method', 1, 11),
    (N'ApproachMethod', N'TH', N'TH', N'DCS', N'Discounted Cashflow Analysis Method', 1, 11),
    (N'ApproachMethod', N'TH', N'EN', N'DirectCapt', N'Direct Capitalization Analysis Method', 1, 12),
    (N'ApproachMethod', N'TH', N'TH', N'DirectCapt', N'Direct Capitalization Analysis Method', 1, 12),
    (N'ApproachMethod', N'TH', N'EN', N'Hypothesis', N'Hypothesis Method', 1, 13),
    (N'ApproachMethod', N'TH', N'TH', N'Hypothesis', N'Hypothesis Method', 1, 13);
GO

-- ----------------------------------------
-- Group: ApprovalDecisionGroup1 (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ApprovalDecisionGroup1', N'TH', N'EN', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup1', N'TH', N'TH', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup1', N'TH', N'EN', N'02', N'Disagree', 1, 2),
    (N'ApprovalDecisionGroup1', N'TH', N'TH', N'02', N'Disagree', 1, 2),
    (N'ApprovalDecisionGroup1', N'TH', N'EN', N'03', N'Route Back', 1, 3),
    (N'ApprovalDecisionGroup1', N'TH', N'TH', N'03', N'Route Back', 1, 3);
GO

-- ----------------------------------------
-- Group: ApprovalDecisionGroup2 (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ApprovalDecisionGroup2', N'TH', N'EN', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup2', N'TH', N'TH', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup2', N'TH', N'EN', N'02', N'Disagree', 1, 2),
    (N'ApprovalDecisionGroup2', N'TH', N'TH', N'02', N'Disagree', 1, 2),
    (N'ApprovalDecisionGroup2', N'TH', N'EN', N'03', N'Route Back', 1, 3),
    (N'ApprovalDecisionGroup2', N'TH', N'TH', N'03', N'Route Back', 1, 3);
GO

-- ----------------------------------------
-- Group: ApprovalDecisionGroup3 (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ApprovalDecisionGroup3', N'TH', N'EN', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup3', N'TH', N'TH', N'01', N'Agree', 1, 1),
    (N'ApprovalDecisionGroup3', N'TH', N'EN', N'02', N'Disagree', 1, 2),
    (N'ApprovalDecisionGroup3', N'TH', N'TH', N'02', N'Disagree', 1, 2);
GO

-- ----------------------------------------
-- Group: ApprovalGroup (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ApprovalGroup', N'TH', N'EN', N'01', N'1', 1, 1),
    (N'ApprovalGroup', N'TH', N'TH', N'01', N'1', 1, 1),
    (N'ApprovalGroup', N'TH', N'EN', N'02', N'2', 1, 2),
    (N'ApprovalGroup', N'TH', N'TH', N'02', N'2', 1, 2),
    (N'ApprovalGroup', N'TH', N'EN', N'03', N'3', 1, 3),
    (N'ApprovalGroup', N'TH', N'TH', N'03', N'3', 1, 3);
GO

-- ----------------------------------------
-- Group: Approve (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Approve', N'TH', N'EN', N'Completed', N'Completed', 1, 8),
    (N'Approve', N'TH', N'TH', N'Completed', N'Completed', 1, 8);
GO

-- ----------------------------------------
-- Group: Approved (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Approved', N'TH', N'EN', N'Closed', N'migration only completed', 1, 9),
    (N'Approved', N'TH', N'TH', N'Closed', N'migration only completed', 1, 9);
GO

-- ----------------------------------------
-- Group: Architecture (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Architecture', N'TH', N'EN', N'01', N'Floor Surface', 1, 1),
    (N'Architecture', N'TH', N'TH', N'01', N'งานผิวพื้น', 1, 1),
    (N'Architecture', N'TH', N'EN', N'02', N'Wall', 1, 2),
    (N'Architecture', N'TH', N'TH', N'02', N'งานผนัง วัสดุบุผนัง', 1, 2),
    (N'Architecture', N'TH', N'EN', N'03', N'Ceiling', 1, 3),
    (N'Architecture', N'TH', N'TH', N'03', N'ฝ้าเพดาน', 1, 3),
    (N'Architecture', N'TH', N'EN', N'04', N'Doors & Windows', 1, 4),
    (N'Architecture', N'TH', N'TH', N'04', N'ประตู & หน้าต่าง', 1, 4),
    (N'Architecture', N'TH', N'EN', N'05', N'Sanitary Ware', 1, 5),
    (N'Architecture', N'TH', N'TH', N'05', N'งานสุขภัณฑ์', 1, 5),
    (N'Architecture', N'TH', N'EN', N'06', N'Painting', 1, 6),
    (N'Architecture', N'TH', N'TH', N'06', N'งานทาสี', 1, 6),
    (N'Architecture', N'TH', N'EN', N'07', N'Stair', 1, 7),
    (N'Architecture', N'TH', N'TH', N'07', N'งานบันได', 1, 7),
    (N'Architecture', N'TH', N'EN', N'08', N'Miscellaneous', 1, 8),
    (N'Architecture', N'TH', N'TH', N'08', N'งานเบ็ดเตล็ด', 1, 8);
GO

-- ----------------------------------------
-- Group: AssignmentMethod (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AssignmentMethod', N'TH', N'EN', N'01', N'Assign to external company with quotation', 1, 1),
    (N'AssignmentMethod', N'TH', N'TH', N'01', N'assign บริษัทประเมิน พร้อม quotation', 1, 1),
    (N'AssignmentMethod', N'TH', N'EN', N'02', N'Assign to external company without quotation', 1, 2),
    (N'AssignmentMethod', N'TH', N'TH', N'02', N'assign บริษัทประเมิน ไม่ได้ quotation', 1, 2),
    (N'AssignmentMethod', N'TH', N'EN', N'03', N'Assign to internal', 1, 3),
    (N'AssignmentMethod', N'TH', N'TH', N'03', N'assign ประเมินใน', 1, 3);
GO

-- ----------------------------------------
-- Group: AssignmentType (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AssignmentType', N'TH', N'EN', N'1', N'Roundrobin', 1, 1),
    (N'AssignmentType', N'TH', N'TH', N'1', N'Roundrobin', 1, 1),
    (N'AssignmentType', N'TH', N'EN', N'2', N'Self-assign', 1, 2),
    (N'AssignmentType', N'TH', N'TH', N'2', N'Self-assign', 1, 2);
GO

-- ----------------------------------------
-- Group: AssumptionCategory (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'AssumptionCategory', N'TH', N'EN', N'01', N'Operating Income', 1, 1),
    (N'AssumptionCategory', N'TH', N'TH', N'01', N'Operating Income', 1, 1),
    (N'AssumptionCategory', N'TH', N'EN', N'02', N'Gross Income', 1, 2),
    (N'AssumptionCategory', N'TH', N'TH', N'02', N'Gross Income', 1, 2),
    (N'AssumptionCategory', N'TH', N'EN', N'03', N'Direct Operating Expenses', 1, 3),
    (N'AssumptionCategory', N'TH', N'TH', N'03', N'Direct Operating Expenses', 1, 3),
    (N'AssumptionCategory', N'TH', N'EN', N'04', N'Administrative and Management Expenses', 1, 4),
    (N'AssumptionCategory', N'TH', N'TH', N'04', N'Administrative and Management Expenses', 1, 4),
    (N'AssumptionCategory', N'TH', N'EN', N'05', N'Fixed Charges', 1, 5),
    (N'AssumptionCategory', N'TH', N'TH', N'05', N'Fixed Charges', 1, 5);
GO

-- ----------------------------------------
-- Group: BathroomFlooringMaterials (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BathroomFlooringMaterials', N'TH', N'EN', N'01', N'Polished concrete', 1, 1),
    (N'BathroomFlooringMaterials', N'TH', N'TH', N'01', N'Polished concrete', 1, 1),
    (N'BathroomFlooringMaterials', N'TH', N'EN', N'02', N'Glazed tiles', 1, 2),
    (N'BathroomFlooringMaterials', N'TH', N'TH', N'02', N'Glazed tiles', 1, 2),
    (N'BathroomFlooringMaterials', N'TH', N'EN', N'03', N'Marble', 1, 3),
    (N'BathroomFlooringMaterials', N'TH', N'TH', N'03', N'Marble', 1, 3),
    (N'BathroomFlooringMaterials', N'TH', N'EN', N'99', N'Other', 1, 4),
    (N'BathroomFlooringMaterials', N'TH', N'TH', N'99', N'Other', 1, 4);
GO

-- ----------------------------------------
-- Group: BlockStatus (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BlockStatus', N'TH', N'EN', N'01', N'Available', 1, 1),
    (N'BlockStatus', N'TH', N'TH', N'01', N'Available', 1, 1),
    (N'BlockStatus', N'TH', N'EN', N'1', N'Available', 1, 1),
    (N'BlockStatus', N'TH', N'TH', N'1', N'Available', 1, 1),
    (N'BlockStatus', N'TH', N'EN', N'02', N'Sold', 1, 2),
    (N'BlockStatus', N'TH', N'TH', N'02', N'Sold', 1, 2),
    (N'BlockStatus', N'TH', N'EN', N'2', N'Sold', 1, 2),
    (N'BlockStatus', N'TH', N'TH', N'2', N'Sold', 1, 2),
    (N'BlockStatus', N'TH', N'EN', N'03', N'Newly Sold', 1, 3),
    (N'BlockStatus', N'TH', N'TH', N'03', N'Newly Sold', 1, 3),
    (N'BlockStatus', N'TH', N'EN', N'04', N'Match Difference', 1, 4),
    (N'BlockStatus', N'TH', N'TH', N'04', N'Match Difference', 1, 4);
GO

-- ----------------------------------------
-- Group: Block_Facilities (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Block_Facilities', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'Block_Facilities', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'Block_Facilities', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'Block_Facilities', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'Block_Facilities', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'Block_Facilities', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'Block_Facilities', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'Block_Facilities', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'Block_Facilities', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'Block_Facilities', N'TH', N'TH', N'05', N'05', 1, 5);
GO

-- ----------------------------------------
-- Group: Block_Utilities (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Block_Utilities', N'TH', N'EN', N'01', N'Permanent Electricity', 1, 1),
    (N'Block_Utilities', N'TH', N'TH', N'01', N'ไฟฟ้าถาวร', 1, 1),
    (N'Block_Utilities', N'TH', N'EN', N'02', N'Tap Water/Groundwater', 1, 2),
    (N'Block_Utilities', N'TH', N'TH', N'02', N'น้ำประปา/น้ำบาดาล', 1, 2),
    (N'Block_Utilities', N'TH', N'EN', N'03', N'Street Electricity', 1, 3),
    (N'Block_Utilities', N'TH', N'TH', N'03', N'ท่อระบายน้ำ/บ่อพัก', 1, 3),
    (N'Block_Utilities', N'TH', N'EN', N'04', N'Drainage Pipe/Manhole', 1, 4),
    (N'Block_Utilities', N'TH', N'TH', N'04', N'ไฟฟ้าถนน', 1, 4),
    (N'Block_Utilities', N'TH', N'EN', N'05', N'Other', 1, 5),
    (N'Block_Utilities', N'TH', N'TH', N'05', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: BoundaryMarker (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BoundaryMarker', N'TH', N'EN', N'01', N'Found boundary marker', 1, 1),
    (N'BoundaryMarker', N'TH', N'TH', N'01', N'Found boundary marker', 1, 1),
    (N'BoundaryMarker', N'TH', N'EN', N'02', N'The boundary marker is unclear.', 1, 2),
    (N'BoundaryMarker', N'TH', N'TH', N'02', N'The boundary marker is unclear.', 1, 2),
    (N'BoundaryMarker', N'TH', N'EN', N'03', N'No boundary marker found', 1, 3),
    (N'BoundaryMarker', N'TH', N'TH', N'03', N'No boundary marker found', 1, 3),
    (N'BoundaryMarker', N'TH', N'EN', N'99', N'other', 1, 4),
    (N'BoundaryMarker', N'TH', N'TH', N'99', N'other', 1, 4),
    (N'BoundaryMarker', N'TH', N'EN', N'04', N'Damaged', 1, 5),
    (N'BoundaryMarker', N'TH', N'TH', N'04', N'Damaged', 1, 5);
GO

-- ----------------------------------------
-- Group: BuildingCondition (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingCondition', N'TH', N'EN', N'01', N'New', 1, 1),
    (N'BuildingCondition', N'TH', N'TH', N'01', N'ใหม่', 1, 1),
    (N'BuildingCondition', N'TH', N'EN', N'02', N'Moderate', 1, 2),
    (N'BuildingCondition', N'TH', N'TH', N'02', N'ปานกลาง', 1, 2),
    (N'BuildingCondition', N'TH', N'EN', N'03', N'Old', 1, 3),
    (N'BuildingCondition', N'TH', N'TH', N'03', N'เก่า', 1, 3),
    (N'BuildingCondition', N'TH', N'EN', N'04', N'Construction', 1, 4),
    (N'BuildingCondition', N'TH', N'TH', N'04', N'อยู่ระหว่างก่อสร้าง', 1, 4),
    (N'BuildingCondition', N'TH', N'EN', N'05', N'Dilapidated', 1, 5),
    (N'BuildingCondition', N'TH', N'TH', N'05', N'ทรุดโทรมขาดการดูแลรักษา', 1, 5),
    (N'BuildingCondition', N'TH', N'EN', N'99', N'Other', 1, 6),
    (N'BuildingCondition', N'TH', N'TH', N'99', N'อื่นๆ', 1, 6);
GO

-- ----------------------------------------
-- Group: BuildingForm (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingForm', N'TH', N'EN', N'01', N'Normal', 1, 1),
    (N'BuildingForm', N'TH', N'TH', N'01', N'Normal', 1, 1),
    (N'BuildingForm', N'TH', N'EN', N'02', N'Good', 1, 2),
    (N'BuildingForm', N'TH', N'TH', N'02', N'Good', 1, 2),
    (N'BuildingForm', N'TH', N'EN', N'03', N'Very Good', 1, 3),
    (N'BuildingForm', N'TH', N'TH', N'03', N'Very Good', 1, 3);
GO

-- ----------------------------------------
-- Group: BuildingManagementSystem (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingManagementSystem', N'TH', N'EN', N'01', N'Electrical System', 1, 1),
    (N'BuildingManagementSystem', N'TH', N'TH', N'01', N'Electrical System', 1, 1),
    (N'BuildingManagementSystem', N'TH', N'EN', N'02', N'Sanitary System', 1, 2),
    (N'BuildingManagementSystem', N'TH', N'TH', N'02', N'Sanitary System', 1, 2),
    (N'BuildingManagementSystem', N'TH', N'EN', N'03', N'Fire Protection System', 1, 3),
    (N'BuildingManagementSystem', N'TH', N'TH', N'03', N'Fire Protection System', 1, 3);
GO

-- ----------------------------------------
-- Group: BuildingMaterial (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingMaterial', N'TH', N'EN', N'01', N'Very Good', 1, 1),
    (N'BuildingMaterial', N'TH', N'TH', N'01', N'ดีมาก', 1, 1),
    (N'BuildingMaterial', N'TH', N'EN', N'02', N'Good', 1, 2),
    (N'BuildingMaterial', N'TH', N'TH', N'02', N'ดี', 1, 2),
    (N'BuildingMaterial', N'TH', N'EN', N'03', N'Moderate', 1, 3),
    (N'BuildingMaterial', N'TH', N'TH', N'03', N'ปานกลาง', 1, 3),
    (N'BuildingMaterial', N'TH', N'EN', N'04', N'Fair Enough', 1, 4),
    (N'BuildingMaterial', N'TH', N'TH', N'04', N'พอใช้', 1, 4),
    (N'BuildingMaterial', N'TH', N'EN', N'05', N'Low', 1, 5),
    (N'BuildingMaterial', N'TH', N'TH', N'05', N'ต่ำ', 1, 5);
GO

-- ----------------------------------------
-- Group: BuildingStatus (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingStatus', N'TH', N'EN', N'01', N'Original building', 1, 1),
    (N'BuildingStatus', N'TH', N'TH', N'01', N'Original building', 1, 1),
    (N'BuildingStatus', N'TH', N'EN', N'02', N'Extension', 1, 2),
    (N'BuildingStatus', N'TH', N'TH', N'02', N'Extension', 1, 2),
    (N'BuildingStatus', N'TH', N'EN', N'03', N'Construction', 1, 3),
    (N'BuildingStatus', N'TH', N'TH', N'03', N'Construction', 1, 3);
GO

-- ----------------------------------------
-- Group: BuildingStructure (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingStructure', N'TH', N'EN', N'01', N'Groundwork', 1, 1),
    (N'BuildingStructure', N'TH', N'TH', N'01', N'Groundwork', 1, 1),
    (N'BuildingStructure', N'TH', N'EN', N'02', N'Pillar', 1, 2),
    (N'BuildingStructure', N'TH', N'TH', N'02', N'Pillar', 1, 2),
    (N'BuildingStructure', N'TH', N'EN', N'03', N'Floor', 1, 3),
    (N'BuildingStructure', N'TH', N'TH', N'03', N'Floor', 1, 3),
    (N'BuildingStructure', N'TH', N'EN', N'04', N'Stair', 1, 4),
    (N'BuildingStructure', N'TH', N'TH', N'04', N'Stair', 1, 4),
    (N'BuildingStructure', N'TH', N'EN', N'05', N'Rooftop Floor', 1, 5),
    (N'BuildingStructure', N'TH', N'TH', N'05', N'Rooftop Floor', 1, 5);
GO

-- ----------------------------------------
-- Group: BuildingStyle (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingStyle', N'TH', N'EN', N'01', N'Very Good', 1, 1),
    (N'BuildingStyle', N'TH', N'TH', N'01', N'ดีมาก', 1, 1),
    (N'BuildingStyle', N'TH', N'EN', N'02', N'Good', 1, 2),
    (N'BuildingStyle', N'TH', N'TH', N'02', N'ดี', 1, 2),
    (N'BuildingStyle', N'TH', N'EN', N'03', N'Moderate', 1, 3),
    (N'BuildingStyle', N'TH', N'TH', N'03', N'ปานกลาง', 1, 3),
    (N'BuildingStyle', N'TH', N'EN', N'04', N'Fair Enough', 1, 4),
    (N'BuildingStyle', N'TH', N'TH', N'04', N'พอใช้', 1, 4);
GO

-- ----------------------------------------
-- Group: BuildingType (EN=14, TH=14)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BuildingType', N'TH', N'EN', N'01', N'Single house', 1, 1),
    (N'BuildingType', N'TH', N'TH', N'01', N'บ้านเดี่ยว', 1, 1),
    (N'BuildingType', N'TH', N'EN', N'02', N'commercial building', 1, 2),
    (N'BuildingType', N'TH', N'TH', N'02', N'อาคารพาณิชย์', 1, 2),
    (N'BuildingType', N'TH', N'EN', N'03', N'semi-detached house', 1, 3),
    (N'BuildingType', N'TH', N'TH', N'03', N'บ้านแฝด', 1, 3),
    (N'BuildingType', N'TH', N'EN', N'04', N'Townhouse', 1, 4),
    (N'BuildingType', N'TH', N'TH', N'04', N'ทาวน์เฮ้าส์', 1, 4),
    (N'BuildingType', N'TH', N'EN', N'05', N'apartment', 1, 5),
    (N'BuildingType', N'TH', N'TH', N'05', N'ห้องชุด', 1, 5),
    (N'BuildingType', N'TH', N'EN', N'06', N'project', 1, 6),
    (N'BuildingType', N'TH', N'TH', N'06', N'โครงการ', 1, 6),
    (N'BuildingType', N'TH', N'EN', N'07', N'office building', 1, 7),
    (N'BuildingType', N'TH', N'TH', N'07', N'อาคารสำนักงาน', 1, 7),
    (N'BuildingType', N'TH', N'EN', N'08', N'hotel', 1, 8),
    (N'BuildingType', N'TH', N'TH', N'08', N'โรงแรม', 1, 8),
    (N'BuildingType', N'TH', N'EN', N'09', N'shopping center', 1, 9),
    (N'BuildingType', N'TH', N'TH', N'09', N'ศูนย์การค้า', 1, 9),
    (N'BuildingType', N'TH', N'EN', N'10', N'factory', 1, 10),
    (N'BuildingType', N'TH', N'TH', N'10', N'โรงงาน', 1, 10),
    (N'BuildingType', N'TH', N'EN', N'11', N'warehouse', 1, 11),
    (N'BuildingType', N'TH', N'TH', N'11', N'โกดัง', 1, 11),
    (N'BuildingType', N'TH', N'EN', N'12', N'residential building', 1, 12),
    (N'BuildingType', N'TH', N'TH', N'12', N'อาคารพักอาศัย', 1, 12),
    (N'BuildingType', N'TH', N'EN', N'13', N'Apartment', 1, 13),
    (N'BuildingType', N'TH', N'TH', N'13', N'อพาร์ทเม้นท์', 1, 13),
    (N'BuildingType', N'TH', N'EN', N'99', N'other', 1, 14),
    (N'BuildingType', N'TH', N'TH', N'99', N'อื่นๆ', 1, 14);
GO

-- ----------------------------------------
-- Group: BusinessTaxExpenses (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BusinessTaxExpenses', N'TH', N'EN', N'01', N'Specific Business Tax Expenses', 1, 1),
    (N'BusinessTaxExpenses', N'TH', N'TH', N'01', N'Specific Business Tax Expenses', 1, 1);
GO

-- ----------------------------------------
-- Group: CancelReason (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CancelReason', N'TH', N'EN', N'01', N'LOS/CLS Cancel', 1, 1),
    (N'CancelReason', N'TH', N'TH', N'01', N'สินเชื่อแจ้งยกเลิก', 1, 1),
    (N'CancelReason', N'TH', N'EN', N'02', N'Customer Cancel', 1, 2),
    (N'CancelReason', N'TH', N'TH', N'02', N'ลูกค้าแจ้งยกเลิก', 1, 2),
    (N'CancelReason', N'TH', N'EN', N'03', N'Previouse Appraisal date less than 6 months', 1, 3),
    (N'CancelReason', N'TH', N'TH', N'03', N'เคยแล้วประเมินแล้วไม่เกิน 6 เดือน', 1, 3),
    (N'CancelReason', N'TH', N'EN', N'04', N'Exeed time to select Appraisal Company', 1, 4),
    (N'CancelReason', N'TH', N'TH', N'04', N'เกินกำหนดการเลือกบริษัทประเมิน', 1, 4),
    (N'CancelReason', N'TH', N'EN', N'05', N'Plan of customer incompleted', 1, 5),
    (N'CancelReason', N'TH', N'TH', N'05', N'แบบแปลนลูกค้ายังไม่เสร็จ', 1, 5),
    (N'CancelReason', N'TH', N'EN', N'06', N'Customer not agree to pay appraisal fee.', 1, 6),
    (N'CancelReason', N'TH', N'TH', N'06', N'ลูกค้าไม่ยินยอมชำระค่าธรรมเนียมประเมิน', 1, 6),
    (N'CancelReason', N'TH', N'EN', N'99', N'Other', 1, 7),
    (N'CancelReason', N'TH', N'TH', N'99', N'อื่นๆ', 1, 7);
GO

-- ----------------------------------------
-- Group: CapitalRatePct (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CapitalRatePct', N'TH', N'EN', N'01', N'Capitalization Rate (%)', 1, 1),
    (N'CapitalRatePct', N'TH', N'TH', N'01', N'Capitalization Rate (%)', 1, 1);
GO

-- ----------------------------------------
-- Group: Ceiling (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Ceiling', N'TH', N'EN', N'01', N'Smooth Gypsum', 1, 1),
    (N'Ceiling', N'TH', N'TH', N'01', N'Smooth Gypsum', 1, 1),
    (N'Ceiling', N'TH', N'EN', N'02', N'T-Bar', 1, 2),
    (N'Ceiling', N'TH', N'TH', N'02', N'T-Bar', 1, 2),
    (N'Ceiling', N'TH', N'EN', N'03', N'Wood', 1, 3),
    (N'Ceiling', N'TH', N'TH', N'03', N'Wood', 1, 3),
    (N'Ceiling', N'TH', N'EN', N'04', N'ฉาบปูนเรียบ', 1, 4),
    (N'Ceiling', N'TH', N'TH', N'04', N'ฉาบปูนเรียบ', 1, 4),
    (N'Ceiling', N'TH', N'EN', N'05', N'Smartboard', 1, 5),
    (N'Ceiling', N'TH', N'TH', N'05', N'Smartboard', 1, 5),
    (N'Ceiling', N'TH', N'EN', N'99', N'Other', 1, 6),
    (N'Ceiling', N'TH', N'TH', N'99', N'Other', 1, 6);
GO

-- ----------------------------------------
-- Group: Channel (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Channel', N'TH', N'EN', N'LOS', N'LOS', 1, 1),
    (N'Channel', N'TH', N'TH', N'LOS', N'LOS', 1, 1),
    (N'Channel', N'TH', N'EN', N'CLS', N'CLS', 1, 2),
    (N'Channel', N'TH', N'TH', N'CLS', N'CLS', 1, 2),
    (N'Channel', N'TH', N'EN', N'SIBS', N'SIBS', 1, 3),
    (N'Channel', N'TH', N'TH', N'SIBS', N'AS400', 1, 3),
    (N'Channel', N'TH', N'EN', N'MANUAL', N'Manual', 1, 4),
    (N'Channel', N'TH', N'TH', N'MANUAL', N'Manual', 1, 4);
GO

-- ----------------------------------------
-- Group: CheckBy (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CheckBy', N'TH', N'EN', N'01', N'Plot', 1, 1),
    (N'CheckBy', N'TH', N'TH', N'01', N'แปลงคง', 1, 1),
    (N'CheckBy', N'TH', N'EN', N'02', N'Cadastral', 1, 2),
    (N'CheckBy', N'TH', N'TH', N'02', N'ระวาง', 1, 2),
    (N'CheckBy', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'CheckBy', N'TH', N'TH', N'99', N'อื่นๆ', 1, 3),
    (N'CheckBy', N'TH', N'EN', N'04', N'Shape, size, and surroundings match the title deed.', 1, 4),
    (N'CheckBy', N'TH', N'TH', N'04', N'รูปร่าง ขนาด ทิศทาง และสภาพแวดล้อมตรงตามโฉนด', 1, 4);
GO

-- ----------------------------------------
-- Group: CheckOwner (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CheckOwner', N'TH', N'EN', N'01', N'Can', 1, 1),
    (N'CheckOwner', N'TH', N'TH', N'01', N'สามารถตรวจสอบกรรมสิทธิ์หลักประกันได้', 1, 1),
    (N'CheckOwner', N'TH', N'EN', N'02', N'Can Not', 1, 2),
    (N'CheckOwner', N'TH', N'TH', N'02', N'ไม่สามารถตรวจสอบกรรมสิทธิ์หลักประกันได้', 1, 2);
GO

-- ----------------------------------------
-- Group: CoefficientOfDecision (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CoefficientOfDecision', N'TH', N'EN', N'01', N'Coefficient Of Decision', 1, 1),
    (N'CoefficientOfDecision', N'TH', N'TH', N'01', N'Coefficient Of Decision', 1, 1);
GO

-- ----------------------------------------
-- Group: CollateralStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CollateralStatus', N'TH', N'EN', N'1', N'New Collateral', 1, 1),
    (N'CollateralStatus', N'TH', N'TH', N'1', N'หลักประกันใหม่', 1, 1),
    (N'CollateralStatus', N'TH', N'EN', N'2', N'Old Collateral', 1, 2),
    (N'CollateralStatus', N'TH', N'TH', N'2', N'หลักประกันเก่า', 1, 2);
GO

-- ----------------------------------------
-- Group: CollateralType (EN=33, TH=33)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CollateralType', N'TH', N'EN', N'01', N'Land', 1, 1),
    (N'CollateralType', N'TH', N'TH', N'01', N'ที่ดิน', 1, 1),
    (N'CollateralType', N'TH', N'EN', N'02', N'Land with buildings', 1, 2),
    (N'CollateralType', N'TH', N'TH', N'02', N'ที่ดินพร้อมสิ่งปลูกสร้าง', 1, 2),
    (N'CollateralType', N'TH', N'EN', N'03', N'Land with buildings (blueprint)', 1, 3),
    (N'CollateralType', N'TH', N'TH', N'03', N'ที่ดินพร้อมสิ่งปลูกสร้าง (แบบแปลน)', 1, 3),
    (N'CollateralType', N'TH', N'EN', N'04', N'Land allocation (whole project)', 1, 4),
    (N'CollateralType', N'TH', N'TH', N'04', N'ที่ดินจัดสรร (ทั้งโครงการ)', 1, 4),
    (N'CollateralType', N'TH', N'EN', N'05', N'Buildings', 1, 5),
    (N'CollateralType', N'TH', N'TH', N'05', N'สิ่งปลูกสร้าง', 1, 5),
    (N'CollateralType', N'TH', N'EN', N'06', N'Building (blueprint)', 1, 6),
    (N'CollateralType', N'TH', N'TH', N'06', N'สิ่งปลูกสร้าง (แบบแปลน)', 1, 6),
    (N'CollateralType', N'TH', N'EN', N'07', N'Building (whole project)', 1, 7),
    (N'CollateralType', N'TH', N'TH', N'07', N'สิ่งปลูกสร้าง (ทั้งโครงการ)', 1, 7),
    (N'CollateralType', N'TH', N'EN', N'08', N'Condominium', 1, 8),
    (N'CollateralType', N'TH', N'TH', N'08', N'อาคารชุด (ห้องชุด)', 1, 8),
    (N'CollateralType', N'TH', N'EN', N'09', N'Leasehold rights, real estate', 1, 9),
    (N'CollateralType', N'TH', N'TH', N'09', N'สิทธิการเช่าอสังหาริมทรัพย์', 1, 9),
    (N'CollateralType', N'TH', N'EN', N'10', N'Car', 1, 10),
    (N'CollateralType', N'TH', N'TH', N'10', N'รถยนต์', 1, 10),
    (N'CollateralType', N'TH', N'EN', N'11', N'Machinery', 1, 11),
    (N'CollateralType', N'TH', N'TH', N'11', N'เครื่องจักร', 1, 11),
    (N'CollateralType', N'TH', N'EN', N'12', N'Ship', 1, 12),
    (N'CollateralType', N'TH', N'TH', N'12', N'เรือ', 1, 12),
    (N'CollateralType', N'TH', N'EN', N'13', N'Land (Part 1)', 1, 13),
    (N'CollateralType', N'TH', N'TH', N'13', N'ที่ดิน (ส่วนที่ 1)', 1, 13),
    (N'CollateralType', N'TH', N'EN', N'14', N'Land (Part 2)', 1, 14),
    (N'CollateralType', N'TH', N'TH', N'14', N'ที่ดิน (ส่วนที่ 2)', 1, 14),
    (N'CollateralType', N'TH', N'EN', N'15', N'Building (Part 1)', 1, 15),
    (N'CollateralType', N'TH', N'TH', N'15', N'สิ่งปลูกสร้าง (ส่วนที่ 1)', 1, 15),
    (N'CollateralType', N'TH', N'EN', N'16', N'Building (Part 2)', 1, 16),
    (N'CollateralType', N'TH', N'TH', N'16', N'สิ่งปลูกสร้าง (ส่วนที่ 2)', 1, 16),
    (N'CollateralType', N'TH', N'EN', N'17', N'Land (Part 2)', 1, 17),
    (N'CollateralType', N'TH', N'TH', N'17', N'ที่ดิน (ส่วนที่ 2)', 1, 17),
    (N'CollateralType', N'TH', N'EN', N'18', N'Building (Part 2)', 1, 18),
    (N'CollateralType', N'TH', N'TH', N'18', N'สิ่งปลูกสร้าง (ส่วนที่ 2)', 1, 18),
    (N'CollateralType', N'TH', N'EN', N'19', N'Land (Group 1)', 1, 19),
    (N'CollateralType', N'TH', N'TH', N'19', N'ที่ดิน (กลุ่มที่ 1)', 1, 19),
    (N'CollateralType', N'TH', N'EN', N'20', N'Building (Group 1)', 1, 20),
    (N'CollateralType', N'TH', N'TH', N'20', N'สิ่งปลูกสร้าง (กลุ่มที่ 1)', 1, 20),
    (N'CollateralType', N'TH', N'EN', N'21', N'Land (Group 2)', 1, 21),
    (N'CollateralType', N'TH', N'TH', N'21', N'ที่ดิน (กลุ่มที่ 2)', 1, 21),
    (N'CollateralType', N'TH', N'EN', N'22', N'Building (Group 2)', 1, 22),
    (N'CollateralType', N'TH', N'TH', N'22', N'สิ่งปลูกสร้าง (กลุ่มที่ 2)', 1, 22),
    (N'CollateralType', N'TH', N'EN', N'23', N'Land with buildings (Group 1)', 1, 23),
    (N'CollateralType', N'TH', N'TH', N'23', N'ที่ดินพร้อมสิ่งปลูกสร้าง (กลุ่มที่ 1)', 1, 23),
    (N'CollateralType', N'TH', N'EN', N'24', N'Land with buildings (Group 2)', 1, 24),
    (N'CollateralType', N'TH', N'TH', N'24', N'ที่ดินพร้อมสิ่งปลูกสร้าง (กลุ่มที่ 2)', 1, 24),
    (N'CollateralType', N'TH', N'EN', N'25', N'Leasehold rights (land with buildings)', 1, 25),
    (N'CollateralType', N'TH', N'TH', N'25', N'สิทธิการเช่า (ที่ดินพร้อมสิ่งปลูกสร้าง)', 1, 25),
    (N'CollateralType', N'TH', N'EN', N'26', N'Land (Group 3)', 1, 26),
    (N'CollateralType', N'TH', N'TH', N'26', N'ที่ดิน (กลุ่มที่ 3)', 1, 26),
    (N'CollateralType', N'TH', N'EN', N'27', N'Land (Group 4)', 1, 27),
    (N'CollateralType', N'TH', N'TH', N'27', N'ที่ดิน (กลุ่มที่ 4)', 1, 27),
    (N'CollateralType', N'TH', N'EN', N'28', N'Leasehold rights (condominium)', 1, 28),
    (N'CollateralType', N'TH', N'TH', N'28', N'สิทธิการเช่า (อาคารชุด)', 1, 28),
    (N'CollateralType', N'TH', N'EN', N'29', N'Land lease rights', 1, 29),
    (N'CollateralType', N'TH', N'TH', N'29', N'สิทธิการเช่าที่ดิน', 1, 29),
    (N'CollateralType', N'TH', N'EN', N'30', N'Leasehold rights', 1, 30),
    (N'CollateralType', N'TH', N'TH', N'30', N'สิทธิการเช่า', 1, 30),
    (N'CollateralType', N'TH', N'EN', N'31', N'Lease rights for space within shopping center', 1, 31),
    (N'CollateralType', N'TH', N'TH', N'31', N'สิทธิการเช่าพื้นที่ในศูนย์การค้า', 1, 31),
    (N'CollateralType', N'TH', N'EN', N'32', N'Land with buildings (BlockLand)', 1, 32),
    (N'CollateralType', N'TH', N'TH', N'32', N'ที่ดินพร้อมสิ่งปลูกสร้าง (โครงการบ้านจัดสรร)', 1, 32),
    (N'CollateralType', N'TH', N'EN', N'33', N'Condominium (BlockCondo)', 1, 33),
    (N'CollateralType', N'TH', N'TH', N'33', N'อาคารชุด (โครงการอาคารชุด)', 1, 33);
GO

-- ----------------------------------------
-- Group: Condition (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Condition', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'Condition', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'Condition', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'Condition', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'Condition', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'Condition', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'Condition', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'Condition', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'Condition', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'Condition', N'TH', N'TH', N'05', N'05', 1, 5);
GO

-- ----------------------------------------
-- Group: ConditionUse (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ConditionUse', N'TH', N'EN', N'01', N'In Used', 1, 1),
    (N'ConditionUse', N'TH', N'TH', N'01', N'In Used', 1, 1),
    (N'ConditionUse', N'TH', N'EN', N'02', N'Not In Used', 1, 2),
    (N'ConditionUse', N'TH', N'TH', N'02', N'Not In Used', 1, 2),
    (N'ConditionUse', N'TH', N'EN', N'03', N'Not Found', 1, 3),
    (N'ConditionUse', N'TH', N'TH', N'03', N'Not Found', 1, 3);
GO

-- ----------------------------------------
-- Group: CondoCondition (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CondoCondition', N'TH', N'EN', N'01', N'New', 1, 1),
    (N'CondoCondition', N'TH', N'TH', N'01', N'New', 1, 1),
    (N'CondoCondition', N'TH', N'EN', N'02', N'Moderate', 1, 2),
    (N'CondoCondition', N'TH', N'TH', N'02', N'Moderate', 1, 2),
    (N'CondoCondition', N'TH', N'EN', N'03', N'Old', 1, 3),
    (N'CondoCondition', N'TH', N'TH', N'03', N'Old', 1, 3),
    (N'CondoCondition', N'TH', N'EN', N'04', N'Construction', 1, 4),
    (N'CondoCondition', N'TH', N'TH', N'04', N'Construction', 1, 4),
    (N'CondoCondition', N'TH', N'EN', N'05', N'Dilapidated', 1, 5),
    (N'CondoCondition', N'TH', N'TH', N'05', N'Dilapidated', 1, 5);
GO

-- ----------------------------------------
-- Group: CondoLocation (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CondoLocation', N'TH', N'EN', N'1', N'Correct', 1, 1),
    (N'CondoLocation', N'TH', N'TH', N'1', N'Correct', 1, 1),
    (N'CondoLocation', N'TH', N'EN', N'2', N'Incorrect', 1, 2),
    (N'CondoLocation', N'TH', N'TH', N'2', N'Incorrect', 1, 2);
GO

-- ----------------------------------------
-- Group: Condo_PublicUtility (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Condo_PublicUtility', N'TH', N'EN', N'01', N'Permanent Electricity', 1, 1),
    (N'Condo_PublicUtility', N'TH', N'TH', N'01', N'Permanent Electricity', 1, 1),
    (N'Condo_PublicUtility', N'TH', N'EN', N'02', N'Tap Water/Ground Water', 1, 2),
    (N'Condo_PublicUtility', N'TH', N'TH', N'02', N'Tap Water/Ground Water', 1, 2),
    (N'Condo_PublicUtility', N'TH', N'EN', N'03', N'Street Electricity', 1, 3),
    (N'Condo_PublicUtility', N'TH', N'TH', N'03', N'Street Electricity', 1, 3),
    (N'Condo_PublicUtility', N'TH', N'EN', N'04', N'Manhole/Drainage Pipe', 1, 4),
    (N'Condo_PublicUtility', N'TH', N'TH', N'04', N'Manhole/Drainage Pipe', 1, 4),
    (N'Condo_PublicUtility', N'TH', N'EN', N'05', N'Others', 1, 5),
    (N'Condo_PublicUtility', N'TH', N'TH', N'05', N'Others', 1, 5);
GO

-- ----------------------------------------
-- Group: Condo_RoadSurface (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Condo_RoadSurface', N'TH', N'EN', N'01', N'Concrete', 1, 1),
    (N'Condo_RoadSurface', N'TH', N'TH', N'01', N'Concrete', 1, 1),
    (N'Condo_RoadSurface', N'TH', N'EN', N'02', N'Asphalt', 1, 2),
    (N'Condo_RoadSurface', N'TH', N'TH', N'02', N'Asphalt', 1, 2),
    (N'Condo_RoadSurface', N'TH', N'EN', N'03', N'Gravel/Crushed Stone', 1, 3),
    (N'Condo_RoadSurface', N'TH', N'TH', N'03', N'Gravel/Crushed Stone', 1, 3),
    (N'Condo_RoadSurface', N'TH', N'EN', N'04', N'Soil', 1, 4),
    (N'Condo_RoadSurface', N'TH', N'TH', N'04', N'Soil', 1, 4);
GO

-- ----------------------------------------
-- Group: Condo_Roof (EN=10, TH=10)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Condo_Roof', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'Condo_Roof', N'TH', N'TH', N'01', N'Reinforced Concrete', 1, 1),
    (N'Condo_Roof', N'TH', N'EN', N'02', N'Tiles', 1, 2),
    (N'Condo_Roof', N'TH', N'TH', N'02', N'Tiles', 1, 2),
    (N'Condo_Roof', N'TH', N'EN', N'03', N'Corrugated Tiles', 1, 3),
    (N'Condo_Roof', N'TH', N'TH', N'03', N'Corrugated Tiles', 1, 3),
    (N'Condo_Roof', N'TH', N'EN', N'04', N'Duplex', 1, 4),
    (N'Condo_Roof', N'TH', N'TH', N'04', N'Duplex', 1, 4),
    (N'Condo_Roof', N'TH', N'EN', N'05', N'Metal Sheet', 1, 5),
    (N'Condo_Roof', N'TH', N'TH', N'05', N'Metal Sheet', 1, 5),
    (N'Condo_Roof', N'TH', N'EN', N'06', N'Vinyl', 1, 6),
    (N'Condo_Roof', N'TH', N'TH', N'06', N'Vinyl', 1, 6),
    (N'Condo_Roof', N'TH', N'EN', N'07', N'Terracotta Tiles', 1, 7),
    (N'Condo_Roof', N'TH', N'TH', N'07', N'Terracotta Tiles', 1, 7),
    (N'Condo_Roof', N'TH', N'EN', N'08', N'Zinc', 1, 8),
    (N'Condo_Roof', N'TH', N'TH', N'08', N'Zinc', 1, 8),
    (N'Condo_Roof', N'TH', N'EN', N'09', N'Unable to Verify', 1, 9),
    (N'Condo_Roof', N'TH', N'TH', N'09', N'Unable to Verify', 1, 9),
    (N'Condo_Roof', N'TH', N'EN', N'10', N'Others', 1, 10),
    (N'Condo_Roof', N'TH', N'TH', N'10', N'Others', 1, 10);
GO

-- ----------------------------------------
-- Group: ConstructionMaterials (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ConstructionMaterials', N'TH', N'EN', N'01', N'Normal', 1, 1),
    (N'ConstructionMaterials', N'TH', N'TH', N'01', N'Normal', 1, 1),
    (N'ConstructionMaterials', N'TH', N'EN', N'02', N'Good', 1, 2),
    (N'ConstructionMaterials', N'TH', N'TH', N'02', N'Good', 1, 2),
    (N'ConstructionMaterials', N'TH', N'EN', N'03', N'Very Good', 1, 3),
    (N'ConstructionMaterials', N'TH', N'TH', N'03', N'Very Good', 1, 3);
GO

-- ----------------------------------------
-- Group: ConstructionStyle (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ConstructionStyle', N'TH', N'EN', N'01', N'Building', 1, 1),
    (N'ConstructionStyle', N'TH', N'TH', N'01', N'ตึก', 1, 1),
    (N'ConstructionStyle', N'TH', N'EN', N'02', N'Half-Timbered Building', 1, 2),
    (N'ConstructionStyle', N'TH', N'TH', N'02', N'ตึกครึ่งไม้', 1, 2),
    (N'ConstructionStyle', N'TH', N'EN', N'03', N'Wood', 1, 3),
    (N'ConstructionStyle', N'TH', N'TH', N'03', N'ไม้', 1, 3);
GO

-- ----------------------------------------
-- Group: ConstructionType (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ConstructionType', N'TH', N'EN', N'01', N'House Estate', 1, 1),
    (N'ConstructionType', N'TH', N'TH', N'01', N'House Estate', 1, 1),
    (N'ConstructionType', N'TH', N'EN', N'02', N'Build It Yourself', 1, 2),
    (N'ConstructionType', N'TH', N'TH', N'02', N'Build It Yourself', 1, 2),
    (N'ConstructionType', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'ConstructionType', N'TH', N'TH', N'99', N'Other', 1, 3);
GO

-- ----------------------------------------
-- Group: ConstructionWork (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ConstructionWork', N'TH', N'EN', N'01', N'Building Stucture', 1, 1),
    (N'ConstructionWork', N'TH', N'TH', N'01', N'Building Stucture', 1, 1),
    (N'ConstructionWork', N'TH', N'EN', N'02', N'Architecture', 1, 2),
    (N'ConstructionWork', N'TH', N'TH', N'02', N'Architecture', 1, 2),
    (N'ConstructionWork', N'TH', N'EN', N'03', N'Building Management System', 1, 3),
    (N'ConstructionWork', N'TH', N'TH', N'03', N'Building Management System', 1, 3);
GO

-- ----------------------------------------
-- Group: ContingencyAllowance (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ContingencyAllowance', N'TH', N'EN', N'01', N'Contingency Allowance', 1, 1),
    (N'ContingencyAllowance', N'TH', N'TH', N'01', N'Contingency Allowance', 1, 1);
GO

-- ----------------------------------------
-- Group: ContractRentalFeeGrowthRate (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ContractRentalFeeGrowthRate', N'TH', N'EN', N'01', N'Frequency', 1, 1),
    (N'ContractRentalFeeGrowthRate', N'TH', N'TH', N'01', N'Frequency', 1, 1),
    (N'ContractRentalFeeGrowthRate', N'TH', N'EN', N'02', N'Period', 1, 2),
    (N'ContractRentalFeeGrowthRate', N'TH', N'TH', N'02', N'Period', 1, 2);
GO

-- ----------------------------------------
-- Group: CountryOfManufacturer (EN=24, TH=24)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'CountryOfManufacturer', N'TH', N'EN', N'01', N'China', 1, 1),
    (N'CountryOfManufacturer', N'TH', N'TH', N'01', N'China', 1, 1),
    (N'CountryOfManufacturer', N'TH', N'EN', N'02', N'Thai', 1, 2),
    (N'CountryOfManufacturer', N'TH', N'TH', N'02', N'Thai', 1, 2),
    (N'CountryOfManufacturer', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'CountryOfManufacturer', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'CountryOfManufacturer', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'CountryOfManufacturer', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'CountryOfManufacturer', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'CountryOfManufacturer', N'TH', N'TH', N'05', N'05', 1, 5),
    (N'CountryOfManufacturer', N'TH', N'EN', N'06', N'06', 1, 6),
    (N'CountryOfManufacturer', N'TH', N'TH', N'06', N'06', 1, 6),
    (N'CountryOfManufacturer', N'TH', N'EN', N'07', N'07', 1, 7),
    (N'CountryOfManufacturer', N'TH', N'TH', N'07', N'07', 1, 7),
    (N'CountryOfManufacturer', N'TH', N'EN', N'08', N'08', 1, 8),
    (N'CountryOfManufacturer', N'TH', N'TH', N'08', N'08', 1, 8),
    (N'CountryOfManufacturer', N'TH', N'EN', N'09', N'09', 1, 9),
    (N'CountryOfManufacturer', N'TH', N'TH', N'09', N'09', 1, 9),
    (N'CountryOfManufacturer', N'TH', N'EN', N'10', N'10', 1, 10),
    (N'CountryOfManufacturer', N'TH', N'TH', N'10', N'10', 1, 10),
    (N'CountryOfManufacturer', N'TH', N'EN', N'11', N'11', 1, 11),
    (N'CountryOfManufacturer', N'TH', N'TH', N'11', N'11', 1, 11),
    (N'CountryOfManufacturer', N'TH', N'EN', N'12', N'12', 1, 12),
    (N'CountryOfManufacturer', N'TH', N'TH', N'12', N'12', 1, 12),
    (N'CountryOfManufacturer', N'TH', N'EN', N'13', N'13', 1, 13),
    (N'CountryOfManufacturer', N'TH', N'TH', N'13', N'13', 1, 13),
    (N'CountryOfManufacturer', N'TH', N'EN', N'14', N'14', 1, 14),
    (N'CountryOfManufacturer', N'TH', N'TH', N'14', N'14', 1, 14),
    (N'CountryOfManufacturer', N'TH', N'EN', N'15', N'15', 1, 15),
    (N'CountryOfManufacturer', N'TH', N'TH', N'15', N'15', 1, 15),
    (N'CountryOfManufacturer', N'TH', N'EN', N'16', N'16', 1, 16),
    (N'CountryOfManufacturer', N'TH', N'TH', N'16', N'16', 1, 16),
    (N'CountryOfManufacturer', N'TH', N'EN', N'17', N'17', 1, 17),
    (N'CountryOfManufacturer', N'TH', N'TH', N'17', N'17', 1, 17),
    (N'CountryOfManufacturer', N'TH', N'EN', N'18', N'18', 1, 18),
    (N'CountryOfManufacturer', N'TH', N'TH', N'18', N'18', 1, 18),
    (N'CountryOfManufacturer', N'TH', N'EN', N'19', N'19', 1, 19),
    (N'CountryOfManufacturer', N'TH', N'TH', N'19', N'19', 1, 19),
    (N'CountryOfManufacturer', N'TH', N'EN', N'20', N'20', 1, 20),
    (N'CountryOfManufacturer', N'TH', N'TH', N'20', N'20', 1, 20),
    (N'CountryOfManufacturer', N'TH', N'EN', N'21', N'21', 1, 21),
    (N'CountryOfManufacturer', N'TH', N'TH', N'21', N'21', 1, 21),
    (N'CountryOfManufacturer', N'TH', N'EN', N'22', N'22', 1, 22),
    (N'CountryOfManufacturer', N'TH', N'TH', N'22', N'22', 1, 22),
    (N'CountryOfManufacturer', N'TH', N'EN', N'23', N'23', 1, 23),
    (N'CountryOfManufacturer', N'TH', N'TH', N'23', N'23', 1, 23),
    (N'CountryOfManufacturer', N'TH', N'EN', N'24', N'24', 1, 24),
    (N'CountryOfManufacturer', N'TH', N'TH', N'24', N'24', 1, 24);
GO

-- ----------------------------------------
-- Group: DCFAssumptionType (EN=29, TH=29)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DCFAssumptionType', N'TH', N'EN', N'01', N'Administration Fee', 1, 1),
    (N'DCFAssumptionType', N'TH', N'TH', N'01', N'Administration Fee', 1, 1),
    (N'DCFAssumptionType', N'TH', N'EN', N'02', N'Advertising and Promotion Costs', 1, 2),
    (N'DCFAssumptionType', N'TH', N'TH', N'02', N'Advertising and Promotion Costs', 1, 2),
    (N'DCFAssumptionType', N'TH', N'EN', N'03', N'Average Rental Rate', 1, 3),
    (N'DCFAssumptionType', N'TH', N'TH', N'03', N'Average Rental Rate', 1, 3),
    (N'DCFAssumptionType', N'TH', N'EN', N'04', N'Common Utility Fees', 1, 4),
    (N'DCFAssumptionType', N'TH', N'TH', N'04', N'Common Utility Fees', 1, 4),
    (N'DCFAssumptionType', N'TH', N'EN', N'05', N'Contingency Expenses', 1, 5),
    (N'DCFAssumptionType', N'TH', N'TH', N'05', N'Contingency Expenses', 1, 5),
    (N'DCFAssumptionType', N'TH', N'EN', N'06', N'Cost of Income from Utilities', 1, 6),
    (N'DCFAssumptionType', N'TH', N'TH', N'06', N'Cost of Income from Utilities', 1, 6),
    (N'DCFAssumptionType', N'TH', N'EN', N'07', N'Energy Cost', 1, 7),
    (N'DCFAssumptionType', N'TH', N'TH', N'07', N'Energy Cost', 1, 7),
    (N'DCFAssumptionType', N'TH', N'EN', N'08', N'Energy Income', 1, 8),
    (N'DCFAssumptionType', N'TH', N'TH', N'08', N'Energy Income', 1, 8),
    (N'DCFAssumptionType', N'TH', N'EN', N'09', N'Fire Insurance Premium', 1, 9),
    (N'DCFAssumptionType', N'TH', N'TH', N'09', N'Fire Insurance Premium', 1, 9),
    (N'DCFAssumptionType', N'TH', N'EN', N'10', N'Food and beverage expenses', 1, 10),
    (N'DCFAssumptionType', N'TH', N'TH', N'10', N'Food and beverage expenses', 1, 10),
    (N'DCFAssumptionType', N'TH', N'EN', N'11', N'Food and Beverage Income', 1, 11),
    (N'DCFAssumptionType', N'TH', N'TH', N'11', N'Food and Beverage Income', 1, 11),
    (N'DCFAssumptionType', N'TH', N'EN', N'12', N'Marketing and Promotion Costs', 1, 12),
    (N'DCFAssumptionType', N'TH', N'TH', N'12', N'Marketing and Promotion Costs', 1, 12),
    (N'DCFAssumptionType', N'TH', N'EN', N'13', N'Miscellaneous', 1, 13),
    (N'DCFAssumptionType', N'TH', N'TH', N'13', N'Miscellaneous', 1, 13),
    (N'DCFAssumptionType', N'TH', N'EN', N'14', N'Operational and Administrative expenses', 1, 14),
    (N'DCFAssumptionType', N'TH', N'TH', N'14', N'Operational and Administrative expenses', 1, 14),
    (N'DCFAssumptionType', N'TH', N'EN', N'15', N'Other Costs', 1, 15),
    (N'DCFAssumptionType', N'TH', N'TH', N'15', N'Other Costs', 1, 15),
    (N'DCFAssumptionType', N'TH', N'EN', N'16', N'Other Expenses', 1, 16),
    (N'DCFAssumptionType', N'TH', N'TH', N'16', N'Other Expenses', 1, 16),
    (N'DCFAssumptionType', N'TH', N'EN', N'17', N'Other Income', 1, 17),
    (N'DCFAssumptionType', N'TH', N'TH', N'17', N'Other Income', 1, 17),
    (N'DCFAssumptionType', N'TH', N'EN', N'18', N'Project Management Compensation', 1, 18),
    (N'DCFAssumptionType', N'TH', N'TH', N'18', N'Project Management Compensation', 1, 18),
    (N'DCFAssumptionType', N'TH', N'EN', N'19', N'Property Tax', 1, 19),
    (N'DCFAssumptionType', N'TH', N'TH', N'19', N'Property Tax', 1, 19),
    (N'DCFAssumptionType', N'TH', N'EN', N'20', N'Repair and Maintenance Costs', 1, 20),
    (N'DCFAssumptionType', N'TH', N'TH', N'20', N'Repair and Maintenance Costs', 1, 20),
    (N'DCFAssumptionType', N'TH', N'EN', N'21', N'Reserve Funds for Building Improvements', 1, 21),
    (N'DCFAssumptionType', N'TH', N'TH', N'21', N'Reserve Funds for Building Improvements', 1, 21),
    (N'DCFAssumptionType', N'TH', N'EN', N'22', N'Room Cost', 1, 22),
    (N'DCFAssumptionType', N'TH', N'TH', N'22', N'Room Cost', 1, 22),
    (N'DCFAssumptionType', N'TH', N'EN', N'23', N'Room Income', 1, 23),
    (N'DCFAssumptionType', N'TH', N'TH', N'23', N'Room Income', 1, 23),
    (N'DCFAssumptionType', N'TH', N'EN', N'24', N'Room Rental Income', 1, 24),
    (N'DCFAssumptionType', N'TH', N'TH', N'24', N'Room Rental Income', 1, 24),
    (N'DCFAssumptionType', N'TH', N'EN', N'25', N'Salary and Benefits', 1, 25),
    (N'DCFAssumptionType', N'TH', N'TH', N'25', N'Salary and Benefits', 1, 25),
    (N'DCFAssumptionType', N'TH', N'EN', N'26', N'Sales and Marketing Expenses', 1, 26),
    (N'DCFAssumptionType', N'TH', N'TH', N'26', N'Sales and Marketing Expenses', 1, 26),
    (N'DCFAssumptionType', N'TH', N'EN', N'27', N'Utility Expenses', 1, 27),
    (N'DCFAssumptionType', N'TH', N'TH', N'27', N'Utility Expenses', 1, 27),
    (N'DCFAssumptionType', N'TH', N'EN', N'28', N'Utility Income', 1, 28),
    (N'DCFAssumptionType', N'TH', N'TH', N'28', N'Utility Income', 1, 28),
    (N'DCFAssumptionType', N'TH', N'EN', N'29', N'29', 1, 29),
    (N'DCFAssumptionType', N'TH', N'TH', N'29', N'29', 1, 29);
GO

-- ----------------------------------------
-- Group: DCFCategory (EN=14, TH=14)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DCFCategory', N'TH', N'EN', N'01', N'Parameter based on tier of property value', 1, 1),
    (N'DCFCategory', N'TH', N'TH', N'01', N'Parameter based on tier of property value', 1, 1),
    (N'DCFCategory', N'TH', N'EN', N'02', N'Position-Based Salary Calculation', 1, 2),
    (N'DCFCategory', N'TH', N'TH', N'02', N'Position-Based Salary Calculation', 1, 2),
    (N'DCFCategory', N'TH', N'EN', N'03', N'Proportion of the new replacement cost', 1, 3),
    (N'DCFCategory', N'TH', N'TH', N'03', N'Proportion of the new replacement cost', 1, 3),
    (N'DCFCategory', N'TH', N'EN', N'04', N'Proportional', 1, 4),
    (N'DCFCategory', N'TH', N'TH', N'04', N'Proportional', 1, 4),
    (N'DCFCategory', N'TH', N'EN', N'05', N'Rental Income per Square Meter', 1, 5),
    (N'DCFCategory', N'TH', N'TH', N'05', N'Rental Income per Square Meter', 1, 5),
    (N'DCFCategory', N'TH', N'EN', N'06', N'Room Costs based on expenses per room per day', 1, 6),
    (N'DCFCategory', N'TH', N'TH', N'06', N'Room Costs based on expenses per room per day', 1, 6),
    (N'DCFCategory', N'TH', N'EN', N'07', N'Seasonal Rates', 1, 7),
    (N'DCFCategory', N'TH', N'TH', N'07', N'Seasonal Rates', 1, 7),
    (N'DCFCategory', N'TH', N'EN', N'08', N'Specified Energy Cost Index', 1, 8),
    (N'DCFCategory', N'TH', N'TH', N'08', N'Specified Energy Cost Index', 1, 8),
    (N'DCFCategory', N'TH', N'EN', N'09', N'Specified food and beverage expenses per room per day', 1, 9),
    (N'DCFCategory', N'TH', N'TH', N'09', N'Specified food and beverage expenses per room per day', 1, 9),
    (N'DCFCategory', N'TH', N'EN', N'10', N'Specified Monthly Rental Income', 1, 10),
    (N'DCFCategory', N'TH', N'TH', N'10', N'Specified Monthly Rental Income', 1, 10),
    (N'DCFCategory', N'TH', N'EN', N'11', N'Specified Room Income with Growth', 1, 11),
    (N'DCFCategory', N'TH', N'TH', N'11', N'Specified Room Income with Growth', 1, 11),
    (N'DCFCategory', N'TH', N'EN', N'12', N'Specified Room Income with Growth by Occupancy Rate', 1, 12),
    (N'DCFCategory', N'TH', N'TH', N'12', N'Specified Room Income with Growth by Occupancy Rate', 1, 12),
    (N'DCFCategory', N'TH', N'EN', N'13', N'Specified Value With Growth', 1, 13),
    (N'DCFCategory', N'TH', N'TH', N'13', N'Specified Value With Growth', 1, 13),
    (N'DCFCategory', N'TH', N'EN', N'14', N'Specify Room Income Per Pay', 1, 14),
    (N'DCFCategory', N'TH', N'TH', N'14', N'Specify Room Income Per Pay', 1, 14);
GO

-- ----------------------------------------
-- Group: DCFDisRate (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DCFDisRate', N'TH', N'EN', N'01', N'Discounted Rate (%)', 1, 1),
    (N'DCFDisRate', N'TH', N'TH', N'01', N'Discounted Rate (%)', 1, 1);
GO

-- ----------------------------------------
-- Group: Decoration (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Decoration', N'TH', N'EN', N'01', N'Ready to move in', 1, 1),
    (N'Decoration', N'TH', N'TH', N'01', N'ที่อยู่อาศัยตกแต่งพร้อมอยู่', 1, 1),
    (N'Decoration', N'TH', N'EN', N'02', N'Partially', 1, 2),
    (N'Decoration', N'TH', N'TH', N'02', N'ที่อยู่อาศัยตกแต่งบางส่วน', 1, 2),
    (N'Decoration', N'TH', N'EN', N'03', N'None', 1, 3),
    (N'Decoration', N'TH', N'TH', N'03', N'ที่อยู่อาศัยไม่ตกแต่ง', 1, 3),
    (N'Decoration', N'TH', N'EN', N'99', N'Other', 1, 4),
    (N'Decoration', N'TH', N'TH', N'99', N'อื่นๆ', 1, 4);
GO

-- ----------------------------------------
-- Group: DecorationDesc (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DecorationDesc', N'TH', N'EN', N'New Description', N'New Thai Description', 1, 5),
    (N'DecorationDesc', N'TH', N'TH', N'New Description', N'New Thai Description', 1, 5);
GO

-- ----------------------------------------
-- Group: DeedType (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DeedType', N'TH', N'EN', N'DEED', N'Title deed', 1, 1),
    (N'DeedType', N'TH', N'TH', N'DEED', N'Title deed', 1, 1),
    (N'DeedType', N'TH', N'EN', N'NS3', N'Nor Sor 3', 1, 2),
    (N'DeedType', N'TH', N'TH', N'NS3', N'นส 3', 1, 2),
    (N'DeedType', N'TH', N'EN', N'NS3K', N'Nor Sor 3 K', 1, 3),
    (N'DeedType', N'TH', N'TH', N'NS3K', N'นส 3 ก', 1, 3),
    (N'DeedType', N'TH', N'EN', N'NS3KO', N'Nor Sor 3 Ko', 1, 4),
    (N'DeedType', N'TH', N'TH', N'NS3KO', N'นส 3 ข', 1, 4),
    (N'DeedType', N'TH', N'EN', N'POSR', N'Document of possessory rights to land', 1, 5),
    (N'DeedType', N'TH', N'TH', N'POSR', N'ตราจอง', 1, 5),
    (N'DeedType', N'TH', N'EN', N'OTHER', N'Other', 1, 6),
    (N'DeedType', N'TH', N'TH', N'OTHER', N'อื่นๆ', 1, 6);
GO

-- ----------------------------------------
-- Group: Depreciation (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Depreciation', N'TH', N'EN', N'01', N'Period', 1, 1),
    (N'Depreciation', N'TH', N'TH', N'01', N'Period', 1, 1),
    (N'Depreciation', N'TH', N'EN', N'02', N'Gross', 1, 2),
    (N'Depreciation', N'TH', N'TH', N'02', N'Gross', 1, 2);
GO

-- ----------------------------------------
-- Group: DocumentType (EN=43, TH=43)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DocumentType', N'TH', N'EN', N'D001', N'Complete Valuation Report', 1, 1),
    (N'DocumentType', N'TH', N'TH', N'D001', N'เล่มประเมินสมบูรณ์', 1, 1),
    (N'DocumentType', N'TH', N'EN', N'D002', N'Property Valuation Document', 1, 2),
    (N'DocumentType', N'TH', N'TH', N'D002', N'เอกสารประเมินค่าทรัพย์สิน', 1, 2),
    (N'DocumentType', N'TH', N'EN', N'D003', N'Property Value Analysis Table', 1, 3),
    (N'DocumentType', N'TH', N'TH', N'D003', N'เอกสารตารางวิเคราะห์มูลค่าทรัพย์สิน', 1, 3),
    (N'DocumentType', N'TH', N'EN', N'D004', N'General Location Map', 1, 4),
    (N'DocumentType', N'TH', N'TH', N'D004', N'เอกสารแผนที่โดยสังเขป', 1, 4),
    (N'DocumentType', N'TH', N'EN', N'D005', N'Aerial Photograph Map', 1, 5),
    (N'DocumentType', N'TH', N'TH', N'D005', N'เอกสารแผนที่ภาพถ่ายทางอากาศ', 1, 5),
    (N'DocumentType', N'TH', N'EN', N'D006', N'Property Location Map', 1, 6),
    (N'DocumentType', N'TH', N'TH', N'D006', N'เอกสารแผนที่ที่ตั้งทรัพย์สิน', 1, 6),
    (N'DocumentType', N'TH', N'EN', N'D007', N'Land Layout Plan', 1, 7),
    (N'DocumentType', N'TH', N'TH', N'D007', N'เอกสารผังที่ดิน', 1, 7),
    (N'DocumentType', N'TH', N'EN', N'D008', N'Project Layout Plan', 1, 8),
    (N'DocumentType', N'TH', N'TH', N'D008', N'เอกสารผังโครงการ', 1, 8),
    (N'DocumentType', N'TH', N'EN', N'D009', N'Architectural Plans', 1, 9),
    (N'DocumentType', N'TH', N'TH', N'D009', N'เอกสารแบบแปลน', 1, 9),
    (N'DocumentType', N'TH', N'EN', N'D010', N'Property Valuation Photographs', 1, 10),
    (N'DocumentType', N'TH', N'TH', N'D010', N'เอกสารภาพถ่ายแสดงทรัพย์สินที่ประเมินค่า', 1, 10),
    (N'DocumentType', N'TH', N'EN', N'D011', N'Construction Photographs', 1, 11),
    (N'DocumentType', N'TH', N'TH', N'D011', N'เอกสารภาพถ่ายการก่อสร้าง', 1, 11),
    (N'DocumentType', N'TH', N'EN', N'D012', N'Construction Progress Table', 1, 12),
    (N'DocumentType', N'TH', N'TH', N'D012', N'เอกสารตารางแสดงผลงานการก่อสร้าง', 1, 12),
    (N'DocumentType', N'TH', N'EN', N'D013', N'Original Size Ownership Document', 1, 13),
    (N'DocumentType', N'TH', N'TH', N'D013', N'เอกสารสิทธิ์ขนาดเท่าตัวจริง', 1, 13),
    (N'DocumentType', N'TH', N'EN', N'D014', N'Sale and Purchase Agreement', 1, 14),
    (N'DocumentType', N'TH', N'TH', N'D014', N'สัญญาซื้อขาย', 1, 14),
    (N'DocumentType', N'TH', N'EN', N'D015', N'Identification Card', 1, 15),
    (N'DocumentType', N'TH', N'TH', N'D015', N'บัตรประชาชน', 1, 15),
    (N'DocumentType', N'TH', N'EN', N'D016', N'Borrower''s Certification Letter', 1, 16),
    (N'DocumentType', N'TH', N'TH', N'D016', N'หนังสือรับรองของผู้กู้', 1, 16),
    (N'DocumentType', N'TH', N'EN', N'D017', N'Building Permit', 1, 17),
    (N'DocumentType', N'TH', N'TH', N'D017', N'ใบขออนุญาตสิ่งปลูกสร้าง', 1, 17),
    (N'DocumentType', N'TH', N'EN', N'D018', N'House Number Application', 1, 18),
    (N'DocumentType', N'TH', N'TH', N'D018', N'คำขอเลขที่บ้าน', 1, 18),
    (N'DocumentType', N'TH', N'EN', N'D019', N'House Registration (Homeowner)', 1, 19),
    (N'DocumentType', N'TH', N'TH', N'D019', N'ทะเบียนบ้าน (เจ้าบ้าน)', 1, 19),
    (N'DocumentType', N'TH', N'EN', N'D020', N'Building Plan', 1, 20),
    (N'DocumentType', N'TH', N'TH', N'D020', N'แบบแปลนอาคาร', 1, 20),
    (N'DocumentType', N'TH', N'EN', N'D021', N'Land Allocation Permit', 1, 21),
    (N'DocumentType', N'TH', N'TH', N'D021', N'ใบขออนุญาตจัดสรรที่ดิน', 1, 21),
    (N'DocumentType', N'TH', N'EN', N'D022', N'Project Plan', 1, 22),
    (N'DocumentType', N'TH', N'TH', N'D022', N'ผังโครงการ', 1, 22),
    (N'DocumentType', N'TH', N'EN', N'D023', N'Project Building Permit', 1, 23),
    (N'DocumentType', N'TH', N'TH', N'D023', N'ใบขออนุญาตสิ่งปลูกสร้างของโครงการ', 1, 23),
    (N'DocumentType', N'TH', N'EN', N'D024', N'Certification Letter (Project Owner)', 1, 24),
    (N'DocumentType', N'TH', N'TH', N'D024', N'หนังสือรับรอง (เจ้าของโครงการ)', 1, 24),
    (N'DocumentType', N'TH', N'EN', N'D025', N'Construction Contract', 1, 25),
    (N'DocumentType', N'TH', N'TH', N'D025', N'หนังสือสัญญาจ้างก่อสร้าง', 1, 25),
    (N'DocumentType', N'TH', N'EN', N'D026', N'Building Permit License', 1, 26),
    (N'DocumentType', N'TH', N'TH', N'D026', N'ใบอนุญาตปลูกสร้าง', 1, 26),
    (N'DocumentType', N'TH', N'EN', N'D027', N'Simplified Map', 1, 27),
    (N'DocumentType', N'TH', N'TH', N'D027', N'แผนที่โดยสังเขป', 1, 27),
    (N'DocumentType', N'TH', N'EN', N'D028', N'Vehicle Registration Manual', 1, 28),
    (N'DocumentType', N'TH', N'TH', N'D028', N'คู่มือทะเบียนรถยนต์', 1, 28),
    (N'DocumentType', N'TH', N'EN', N'D029', N'Machinery Registration and Location Diagram', 1, 29),
    (N'DocumentType', N'TH', N'TH', N'D029', N'ทะเบียนเครื่องจักรและแผนผังที่ตั้ง', 1, 29),
    (N'DocumentType', N'TH', N'EN', N'D030', N'Purchase Order (Invoice)', 1, 30),
    (N'DocumentType', N'TH', N'TH', N'D030', N'ใบสั่งซื้อ(Invoice)', 1, 30),
    (N'DocumentType', N'TH', N'EN', N'D031', N'Machinery Operation Manual', 1, 31),
    (N'DocumentType', N'TH', N'TH', N'D031', N'คู่มือการใช้งานเครื่องจักร', 1, 31),
    (N'DocumentType', N'TH', N'EN', N'D032', N'Boat Registration', 1, 32),
    (N'DocumentType', N'TH', N'TH', N'D032', N'ทะเบียนเรือ', 1, 32),
    (N'DocumentType', N'TH', N'EN', N'D033', N'Boat Registration Certificate', 1, 33),
    (N'DocumentType', N'TH', N'TH', N'D033', N'ใบทะเบียนประจำเรือ', 1, 33),
    (N'DocumentType', N'TH', N'EN', N'D034', N'Boat Construction Contract', 1, 34),
    (N'DocumentType', N'TH', N'TH', N'D034', N'สัญญาต่อเรือ', 1, 34),
    (N'DocumentType', N'TH', N'EN', N'D035', N'Property Survey and Valuation Appointment Request', 1, 35),
    (N'DocumentType', N'TH', N'TH', N'D035', N'ใบขอนัดสำรวจและประเมินราคา', 1, 35),
    (N'DocumentType', N'TH', N'EN', N'D036', N'Previous Property Valuation Report Summary', 1, 36),
    (N'DocumentType', N'TH', N'TH', N'D036', N'ใบสรุปรายงานการประเมินราคาทรัพย์สิน (เดิม)', 1, 36),
    (N'DocumentType', N'TH', N'EN', N'D037', N'Mortgage Agreement', 1, 37),
    (N'DocumentType', N'TH', N'TH', N'D037', N'หนังสือสัญญาจำนองเป็นหลักประกัน', 1, 37),
    (N'DocumentType', N'TH', N'EN', N'D038', N'Loan Agreement', 1, 38),
    (N'DocumentType', N'TH', N'TH', N'D038', N'หนังสือสัญญากู้เงิน', 1, 38),
    (N'DocumentType', N'TH', N'EN', N'D039', N'Map', 1, 39),
    (N'DocumentType', N'TH', N'TH', N'D039', N'แผนที่', 1, 39),
    (N'DocumentType', N'TH', N'EN', N'D040', N'Project Plan', 1, 40),
    (N'DocumentType', N'TH', N'TH', N'D040', N'ผังโครงการ', 1, 40),
    (N'DocumentType', N'TH', N'EN', N'D041', N'Others', 1, 41),
    (N'DocumentType', N'TH', N'TH', N'D041', N'อื่นๆ', 1, 41),
    (N'DocumentType', N'TH', N'EN', N'D042', N'Construction Progress Inspection Summary Report', 1, 42),
    (N'DocumentType', N'TH', N'TH', N'D042', N'รายงานสรุปผลการตรวจสอบงานก่อสร้าง', 1, 42),
    (N'DocumentType', N'TH', N'EN', N'D043', N'Property Valuation Summary Report', 1, 43),
    (N'DocumentType', N'TH', N'TH', N'D043', N'รายงานสรุปผลการประเมินราคาทรัพย์สิน', 1, 43);
GO

-- ----------------------------------------
-- Group: DocumentValidation (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'DocumentValidation', N'TH', N'EN', N'01', N'Correctly Matched', 1, 1),
    (N'DocumentValidation', N'TH', N'TH', N'01', N'Correctly Matched', 1, 1),
    (N'DocumentValidation', N'TH', N'EN', N'02', N'Not Consistent', 1, 2),
    (N'DocumentValidation', N'TH', N'TH', N'02', N'Not Consistent', 1, 2);
GO

-- ----------------------------------------
-- Group: Encroachment (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Encroachment', N'TH', N'EN', N'01', N'Is Encroached', 1, 1),
    (N'Encroachment', N'TH', N'TH', N'01', N'รุกล้ำ', 1, 1),
    (N'Encroachment', N'TH', N'EN', N'02', N'Is Not Encroached', 1, 2),
    (N'Encroachment', N'TH', N'TH', N'02', N'ไม่รุกล้ำ', 1, 2);
GO

-- ----------------------------------------
-- Group: Environment (EN=10, TH=10)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Environment', N'TH', N'EN', N'01', N'Highly densely populated residential area', 1, 1),
    (N'Environment', N'TH', N'TH', N'01', N'Highly densely populated residential area', 1, 1),
    (N'Environment', N'TH', N'EN', N'02', N'Moderate densely populated residential area', 1, 2),
    (N'Environment', N'TH', N'TH', N'02', N'Moderate densely populated residential area', 1, 2),
    (N'Environment', N'TH', N'EN', N'03', N'Low-Density residential area', 1, 3),
    (N'Environment', N'TH', N'TH', N'03', N'Low-Density residential area', 1, 3),
    (N'Environment', N'TH', N'EN', N'04', N'Sparsely populated residential area, Rural', 1, 4),
    (N'Environment', N'TH', N'TH', N'04', N'Sparsely populated residential area, Rural', 1, 4),
    (N'Environment', N'TH', N'EN', N'05', N'Vacant land, far from community', 1, 5),
    (N'Environment', N'TH', N'TH', N'05', N'Vacant land, far from community', 1, 5),
    (N'Environment', N'TH', N'EN', N'06', N'Commercial area', 1, 6),
    (N'Environment', N'TH', N'TH', N'06', N'Commercial area', 1, 6),
    (N'Environment', N'TH', N'EN', N'07', N'Industrial rea', 1, 7),
    (N'Environment', N'TH', N'TH', N'07', N'Industrial rea', 1, 7),
    (N'Environment', N'TH', N'EN', N'08', N'Agriculture area', 1, 8),
    (N'Environment', N'TH', N'TH', N'08', N'Agriculture area', 1, 8),
    (N'Environment', N'TH', N'EN', N'09', N'Central business district', 1, 9),
    (N'Environment', N'TH', N'TH', N'09', N'Central business district', 1, 9),
    (N'Environment', N'TH', N'EN', N'10', N'Mixed residential and commercial area', 1, 10),
    (N'Environment', N'TH', N'TH', N'10', N'Mixed residential and commercial area', 1, 10);
GO

-- ----------------------------------------
-- Group: EstRiskAndExpectedProfit (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'EstRiskAndExpectedProfit', N'TH', N'EN', N'01', N'Estimate Risk and Expected Profit', 1, 1),
    (N'EstRiskAndExpectedProfit', N'TH', N'TH', N'01', N'Estimate Risk and Expected Profit', 1, 1);
GO

-- ----------------------------------------
-- Group: EvaluationStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'EvaluationStatus', N'TH', N'EN', N'01', N'In progress', 1, 1),
    (N'EvaluationStatus', N'TH', N'TH', N'01', N'In progress', 1, 1),
    (N'EvaluationStatus', N'TH', N'EN', N'02', N'completed', 1, 2),
    (N'EvaluationStatus', N'TH', N'TH', N'02', N'completed', 1, 2);
GO

-- ----------------------------------------
-- Group: Eviction (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Eviction', N'TH', N'EN', N'01', N'Permanent Electricity', 1, 1),
    (N'Eviction', N'TH', N'TH', N'01', N'อยู่ในแนวสายไฟฟ้าแรงสูง', 1, 1),
    (N'Eviction', N'TH', N'EN', N'02', N'Tap Water/Ground Water', 1, 2),
    (N'Eviction', N'TH', N'TH', N'02', N'แนวรถไฟฟ้าใต้ดิน', 1, 2),
    (N'Eviction', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'Eviction', N'TH', N'TH', N'99', N'.อื่นๆ', 1, 3);
GO

-- ----------------------------------------
-- Group: ExAC_Decision (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ExAC_Decision', N'TH', N'EN', N'01', N'Proceed', 1, 1),
    (N'ExAC_Decision', N'TH', N'TH', N'01', N'Proceed', 1, 1),
    (N'ExAC_Decision', N'TH', N'EN', N'02', N'Route Back to External Appraisal Execution', 1, 2),
    (N'ExAC_Decision', N'TH', N'TH', N'02', N'Route Back to External Appraisal Execution', 1, 2),
    (N'ExAC_Decision', N'TH', N'EN', N'03', N'Route Back to External Appraisal Assignment', 1, 3),
    (N'ExAC_Decision', N'TH', N'TH', N'03', N'Route Back to External Appraisal Assignment', 1, 3);
GO

-- ----------------------------------------
-- Group: ExAD_Decision (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ExAD_Decision', N'TH', N'EN', N'01', N'Proceed', 1, 1),
    (N'ExAD_Decision', N'TH', N'TH', N'01', N'Proceed', 1, 1),
    (N'ExAD_Decision', N'TH', N'EN', N'02', N'Route Back to Appraisal Assignment', 1, 2),
    (N'ExAD_Decision', N'TH', N'TH', N'02', N'Route Back to Appraisal Assignment', 1, 2);
GO

-- ----------------------------------------
-- Group: ExAE_Decision (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ExAE_Decision', N'TH', N'EN', N'01', N'Proceed', 1, 1),
    (N'ExAE_Decision', N'TH', N'TH', N'01', N'Proceed', 1, 1),
    (N'ExAE_Decision', N'TH', N'EN', N'02', N'Route Back to External Appraisal Assignment', 1, 2),
    (N'ExAE_Decision', N'TH', N'TH', N'02', N'Route Back to External Appraisal Assignment', 1, 2);
GO

-- ----------------------------------------
-- Group: ExAS_Decision (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ExAS_Decision', N'TH', N'EN', N'01', N'Route Back to External Appraisal Assignment', 1, 1),
    (N'ExAS_Decision', N'TH', N'TH', N'01', N'Route Back to External Appraisal Assignment', 1, 1);
GO

-- ----------------------------------------
-- Group: ExAV_Decision (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ExAV_Decision', N'TH', N'EN', N'01', N'Proceed', 1, 1),
    (N'ExAV_Decision', N'TH', N'TH', N'01', N'Proceed', 1, 1),
    (N'ExAV_Decision', N'TH', N'EN', N'02', N'Route Back to External Appraisal Check', 1, 2),
    (N'ExAV_Decision', N'TH', N'TH', N'02', N'Route Back to External Appraisal Check', 1, 2),
    (N'ExAV_Decision', N'TH', N'EN', N'03', N'Route Back to External Appraisal Execution', 1, 3),
    (N'ExAV_Decision', N'TH', N'TH', N'03', N'Route Back to External Appraisal Execution', 1, 3),
    (N'ExAV_Decision', N'TH', N'EN', N'04', N'Route Back to External Appraisal Assignment', 1, 4),
    (N'ExAV_Decision', N'TH', N'TH', N'04', N'Route Back to External Appraisal Assignment', 1, 4);
GO

-- ----------------------------------------
-- Group: Expropriation (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Expropriation', N'TH', N'EN', N'01', N'Is Expropriated', 1, 1),
    (N'Expropriation', N'TH', N'TH', N'01', N'Is Expropriated', 1, 1),
    (N'Expropriation', N'TH', N'EN', N'02', N'In Line Expropriated', 1, 2),
    (N'Expropriation', N'TH', N'TH', N'02', N'In Line Expropriated', 1, 2);
GO

-- ----------------------------------------
-- Group: Exterior (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Exterior', N'TH', N'EN', N'01', N'Wood', 1, 1),
    (N'Exterior', N'TH', N'TH', N'01', N'Wood', 1, 1),
    (N'Exterior', N'TH', N'EN', N'02', N'Smooth plastered brickwork and painted', 1, 2),
    (N'Exterior', N'TH', N'TH', N'02', N'Smooth plastered brickwork and painted', 1, 2),
    (N'Exterior', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'Exterior', N'TH', N'TH', N'99', N'Other', 1, 3),
    (N'Exterior', N'TH', N'EN', N'03', N'Shera', 1, 4),
    (N'Exterior', N'TH', N'TH', N'03', N'Shera', 1, 4);
GO

-- ----------------------------------------
-- Group: External data (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'External data', N'TH', N'EN', N'0', N'1', 1, 4),
    (N'External data', N'TH', N'TH', N'0', N'01', 1, 4);
GO

-- ----------------------------------------
-- Group: Facilities (EN=18, TH=18)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Facilities', N'TH', N'EN', N'01', N'Passenger Elevator', 1, 1),
    (N'Facilities', N'TH', N'TH', N'01', N'Passenger Elevator', 1, 1),
    (N'Facilities', N'TH', N'EN', N'02', N'Hallway', 1, 2),
    (N'Facilities', N'TH', N'TH', N'02', N'Hallway', 1, 2),
    (N'Facilities', N'TH', N'EN', N'03', N'Parking', 1, 3),
    (N'Facilities', N'TH', N'TH', N'03', N'Parking', 1, 3),
    (N'Facilities', N'TH', N'EN', N'04', N'Fire Escape Stairs', 1, 4),
    (N'Facilities', N'TH', N'TH', N'04', N'Fire Escape Stairs', 1, 4),
    (N'Facilities', N'TH', N'EN', N'05', N'Fire Extinguishing System', 1, 5),
    (N'Facilities', N'TH', N'TH', N'05', N'Fire Extinguishing System', 1, 5),
    (N'Facilities', N'TH', N'EN', N'06', N'Swimming Pool', 1, 6),
    (N'Facilities', N'TH', N'TH', N'06', N'Swimming Pool', 1, 6),
    (N'Facilities', N'TH', N'EN', N'07', N'Fitness Room', 1, 7),
    (N'Facilities', N'TH', N'TH', N'07', N'Fitness Room', 1, 7),
    (N'Facilities', N'TH', N'EN', N'08', N'Garden', 1, 8),
    (N'Facilities', N'TH', N'TH', N'08', N'Garden', 1, 8),
    (N'Facilities', N'TH', N'EN', N'09', N'Outdoor Stadium', 1, 9),
    (N'Facilities', N'TH', N'TH', N'09', N'Outdoor Stadium', 1, 9),
    (N'Facilities', N'TH', N'EN', N'10', N'Club', 1, 10),
    (N'Facilities', N'TH', N'TH', N'10', N'Club', 1, 10),
    (N'Facilities', N'TH', N'EN', N'11', N'Steam Room', 1, 11),
    (N'Facilities', N'TH', N'TH', N'11', N'Steam Room', 1, 11),
    (N'Facilities', N'TH', N'EN', N'12', N'Security System', 1, 12),
    (N'Facilities', N'TH', N'TH', N'12', N'Security System', 1, 12),
    (N'Facilities', N'TH', N'EN', N'13', N'Key Card System', 1, 13),
    (N'Facilities', N'TH', N'TH', N'13', N'Key Card System', 1, 13),
    (N'Facilities', N'TH', N'EN', N'14', N'Legal Entity', 1, 14),
    (N'Facilities', N'TH', N'TH', N'14', N'Legal Entity', 1, 14),
    (N'Facilities', N'TH', N'EN', N'15', N'Garbage Disposal Point', 1, 15),
    (N'Facilities', N'TH', N'TH', N'15', N'Garbage Disposal Point', 1, 15),
    (N'Facilities', N'TH', N'EN', N'16', N'Waste Disposal and System', 1, 16),
    (N'Facilities', N'TH', N'TH', N'16', N'Waste Disposal and System', 1, 16),
    (N'Facilities', N'TH', N'EN', N'17', N'Kindergarten', 1, 17),
    (N'Facilities', N'TH', N'TH', N'17', N'Kindergarten', 1, 17),
    (N'Facilities', N'TH', N'EN', N'99', N'Others', 1, 18),
    (N'Facilities', N'TH', N'TH', N'99', N'Others', 1, 18);
GO

-- ----------------------------------------
-- Group: FeePaymentMethod (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FeePaymentMethod', N'TH', N'EN', N'01', N'Paid at the bank (before the appraisal date)', 1, 1),
    (N'FeePaymentMethod', N'TH', N'TH', N'01', N'ชำระที่ธนาคารฯ (ก่อนวันประเมิน)', 1, 1),
    (N'FeePaymentMethod', N'TH', N'EN', N'02', N'Paid on the appraisal date', 1, 2),
    (N'FeePaymentMethod', N'TH', N'TH', N'02', N'ชำระวันประเมิน', 1, 2),
    (N'FeePaymentMethod', N'TH', N'EN', N'03', N'Customer partially paid; remaining paid on the appraisal date', 1, 3),
    (N'FeePaymentMethod', N'TH', N'TH', N'03', N'ลูกค้าชำระบางส่วนที่เหลือชำระวันประเมิน', 1, 3),
    (N'FeePaymentMethod', N'TH', N'EN', N'04', N'Customer partially paid / bank absorbed part of the fee', 1, 4),
    (N'FeePaymentMethod', N'TH', N'TH', N'04', N'ลูกค้าชำระบางส่วน / bank absorb บางส่วน', 1, 4),
    (N'FeePaymentMethod', N'TH', N'EN', N'05', N'Exempted due to M/F', 1, 5),
    (N'FeePaymentMethod', N'TH', N'TH', N'05', N'ยกเว้นเนื่องจาก M/F', 1, 5),
    (N'FeePaymentMethod', N'TH', N'EN', N'06', N'Exempted due to retail customer under M/F', 1, 6),
    (N'FeePaymentMethod', N'TH', N'TH', N'06', N'ยกเว้นเนื่องจาก รายย่อยใน M/F', 1, 6),
    (N'FeePaymentMethod', N'TH', N'EN', N'07', N'Exempted due to other reasons', 1, 7),
    (N'FeePaymentMethod', N'TH', N'TH', N'07', N'ยกเว้นด้วยสาเหตุ อื่นๆ', 1, 7),
    (N'FeePaymentMethod', N'TH', N'EN', N'99', N'Others', 1, 8),
    (N'FeePaymentMethod', N'TH', N'TH', N'99', N'อื่นๆ', 1, 8);
GO

-- ----------------------------------------
-- Group: FeePaymentStatus (EN=9, TH=9)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FeePaymentStatus', N'TH', N'EN', N'01', N'FeePaymentStatus', 1, 1),
    (N'FeePaymentStatus', N'TH', N'TH', N'01', N'ยังไม่ได้ชำระ', 1, 1),
    (N'FeePaymentStatus', N'TH', N'EN', N'02', N'FeePaymentStatus', 1, 2),
    (N'FeePaymentStatus', N'TH', N'TH', N'02', N'ชำระบางส่วน', 1, 2),
    (N'FeePaymentStatus', N'TH', N'EN', N'03', N'FeePaymentStatus', 1, 3),
    (N'FeePaymentStatus', N'TH', N'TH', N'03', N'PAID (เก็บทางลูกค้า)', 1, 3),
    (N'FeePaymentStatus', N'TH', N'EN', N'04', N'FeePaymentStatus', 1, 4),
    (N'FeePaymentStatus', N'TH', N'TH', N'04', N'ชำระเต็มจำนวน(เก็บทางลูกค้าบางส่วน)', 1, 4),
    (N'FeePaymentStatus', N'TH', N'EN', N'05', N'FeePaymentStatus', 1, 5),
    (N'FeePaymentStatus', N'TH', N'TH', N'05', N'ได้รับการยกเว้น(วางบิลธนาคาร)', 1, 5),
    (N'FeePaymentStatus', N'TH', N'EN', N'06', N'FeePaymentStatus', 1, 6),
    (N'FeePaymentStatus', N'TH', N'TH', N'06', N'อยู่ระหว่างการทำเล่มประเมิน', 1, 6),
    (N'FeePaymentStatus', N'TH', N'EN', N'07', N'FeePaymentStatus', 1, 7),
    (N'FeePaymentStatus', N'TH', N'TH', N'07', N'Pending Invoice', 1, 7),
    (N'FeePaymentStatus', N'TH', N'EN', N'08', N'FeePaymentStatus', 1, 8),
    (N'FeePaymentStatus', N'TH', N'TH', N'08', N'Pending payment request', 1, 8),
    (N'FeePaymentStatus', N'TH', N'EN', N'09', N'FeePaymentStatus', 1, 9),
    (N'FeePaymentStatus', N'TH', N'TH', N'09', N'PAID', 1, 9);
GO

-- ----------------------------------------
-- Group: Fence (EN=9, TH=9)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Fence', N'TH', N'EN', N'01', N'Cement Block', 1, 1),
    (N'Fence', N'TH', N'TH', N'01', N'Cement Block', 1, 1),
    (N'Fence', N'TH', N'EN', N'02', N'Wood', 1, 2),
    (N'Fence', N'TH', N'TH', N'02', N'Wood', 1, 2),
    (N'Fence', N'TH', N'EN', N'03', N'Iron', 1, 3),
    (N'Fence', N'TH', N'TH', N'03', N'Iron', 1, 3),
    (N'Fence', N'TH', N'EN', N'04', N'Brick', 1, 4),
    (N'Fence', N'TH', N'TH', N'04', N'Brick', 1, 4),
    (N'Fence', N'TH', N'EN', N'05', N'Stainless Steel', 1, 5),
    (N'Fence', N'TH', N'TH', N'05', N'Stainless Steel', 1, 5),
    (N'Fence', N'TH', N'EN', N'06', N'No Fence', 1, 6),
    (N'Fence', N'TH', N'TH', N'06', N'No Fence', 1, 6),
    (N'Fence', N'TH', N'EN', N'07', N'Wire Mesh', 1, 7),
    (N'Fence', N'TH', N'TH', N'07', N'Wire Mesh', 1, 7),
    (N'Fence', N'TH', N'EN', N'08', N'Barbed Wire', 1, 8),
    (N'Fence', N'TH', N'TH', N'08', N'Barbed Wire', 1, 8),
    (N'Fence', N'TH', N'EN', N'99', N'Other', 1, 9),
    (N'Fence', N'TH', N'TH', N'99', N'Other', 1, 9);
GO

-- ----------------------------------------
-- Group: FireInsuranceCondition (EN=12, TH=12)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FireInsuranceCondition', N'TH', N'EN', N'01', N'Condo height < 8 floors', 1, 1),
    (N'FireInsuranceCondition', N'TH', N'TH', N'01', N'คอนโด สูงไม่เกิน 8 ชั้น', 1, 1),
    (N'FireInsuranceCondition', N'TH', N'EN', N'02', N'Condo height > 8 floors', 1, 2),
    (N'FireInsuranceCondition', N'TH', N'TH', N'02', N'คอนโด สูง > 8 ชั้น', 1, 2),
    (N'FireInsuranceCondition', N'TH', N'EN', N'03', N'Condo height < 8 floors and with mezzanine floor', 1, 3),
    (N'FireInsuranceCondition', N'TH', N'TH', N'03', N'คอนโด สูงไม่เกิน 8 ชั้น มีชั้นลอย', 1, 3),
    (N'FireInsuranceCondition', N'TH', N'EN', N'04', N'Condo height > 8 floors and with mezzanine floor', 1, 4),
    (N'FireInsuranceCondition', N'TH', N'TH', N'04', N'คอนโด สูง > 8 ชั้น มีชั้นลอย', 1, 4),
    (N'FireInsuranceCondition', N'TH', N'EN', N'05', N'towhouse 1-2 floors', 1, 5),
    (N'FireInsuranceCondition', N'TH', N'TH', N'05', N'ทาวน์เฮ้าส์ 1 - 2 ชั้น', 1, 5),
    (N'FireInsuranceCondition', N'TH', N'EN', N'06', N'towhouse 3 floors', 1, 6),
    (N'FireInsuranceCondition', N'TH', N'TH', N'06', N'ทาวน์เฮ้าส์ 3 ชั้น', 1, 6),
    (N'FireInsuranceCondition', N'TH', N'EN', N'07', N'twinhouse', 1, 7),
    (N'FireInsuranceCondition', N'TH', N'TH', N'07', N'บ้านแฝด', 1, 7),
    (N'FireInsuranceCondition', N'TH', N'EN', N'08', N'singlehouse area < = 150 sqm.', 1, 8),
    (N'FireInsuranceCondition', N'TH', N'TH', N'08', N'บ้านเดียว พื้นที่ ไม่เกิน 150 ตร.ม.', 1, 8),
    (N'FireInsuranceCondition', N'TH', N'EN', N'09', N'singlehouse area > 150 sqm. and < = 200 sqm', 1, 9),
    (N'FireInsuranceCondition', N'TH', N'TH', N'09', N'บ้านเดียว พื้นที่ มากกว่า 150 แต่ไม่เกิน 200 ตร.ม.', 1, 9),
    (N'FireInsuranceCondition', N'TH', N'EN', N'10', N'singlehouse area > 200 sqm. and < = 400 sqm', 1, 10),
    (N'FireInsuranceCondition', N'TH', N'TH', N'10', N'บ้านเดียว พื้นที่ มากกว่า 200 แต่ไม่เกิน 400 ตร.ม.', 1, 10),
    (N'FireInsuranceCondition', N'TH', N'EN', N'11', N'singlehouse area > 400 sqm. and < = 500 sqm', 1, 11),
    (N'FireInsuranceCondition', N'TH', N'TH', N'11', N'บ้านเดียว พื้นที่ มากกว่า 400 แต่ไม่เกิน 500 ตร.ม.', 1, 11),
    (N'FireInsuranceCondition', N'TH', N'EN', N'12', N'singlehouse area > 500 sqm.', 1, 12),
    (N'FireInsuranceCondition', N'TH', N'TH', N'12', N'บ้านเดียว พื้นที่ มากกว่า 500 ตร.ม.', 1, 12);
GO

-- ----------------------------------------
-- Group: FloorStructure (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FloorStructure', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'FloorStructure', N'TH', N'TH', N'01', N'Reinforced Concrete', 1, 1),
    (N'FloorStructure', N'TH', N'EN', N'02', N'Wood', 1, 2),
    (N'FloorStructure', N'TH', N'TH', N'02', N'Wood', 1, 2),
    (N'FloorStructure', N'TH', N'EN', N'03', N'Precast Concrete Slab', 1, 3),
    (N'FloorStructure', N'TH', N'TH', N'03', N'Precast Concrete Slab', 1, 3),
    (N'FloorStructure', N'TH', N'EN', N'99', N'Others', 1, 4),
    (N'FloorStructure', N'TH', N'TH', N'99', N'Others', 1, 4);
GO

-- ----------------------------------------
-- Group: FloorSurface (EN=11, TH=11)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FloorSurface', N'TH', N'EN', N'01', N'Granite', 1, 1),
    (N'FloorSurface', N'TH', N'TH', N'01', N'Granite', 1, 1),
    (N'FloorSurface', N'TH', N'EN', N'02', N'Wood/Parquet', 1, 2),
    (N'FloorSurface', N'TH', N'TH', N'02', N'Wood/Parquet', 1, 2),
    (N'FloorSurface', N'TH', N'EN', N'03', N'Tiles', 1, 3),
    (N'FloorSurface', N'TH', N'TH', N'03', N'Tiles', 1, 3),
    (N'FloorSurface', N'TH', N'EN', N'04', N'Ceramic Tiles (Glazed)', 1, 4),
    (N'FloorSurface', N'TH', N'TH', N'04', N'Ceramic Tiles (Glazed)', 1, 4),
    (N'FloorSurface', N'TH', N'EN', N'05', N'Terracotta Tiles', 1, 5),
    (N'FloorSurface', N'TH', N'TH', N'05', N'Terracotta Tiles', 1, 5),
    (N'FloorSurface', N'TH', N'EN', N'06', N'rubber tiles', 1, 6),
    (N'FloorSurface', N'TH', N'TH', N'06', N'rubber tiles', 1, 6),
    (N'FloorSurface', N'TH', N'EN', N'07', N'Polished Floors', 1, 7),
    (N'FloorSurface', N'TH', N'TH', N'07', N'Polished Floors', 1, 7),
    (N'FloorSurface', N'TH', N'EN', N'08', N'Laminate', 1, 8),
    (N'FloorSurface', N'TH', N'TH', N'08', N'Laminate', 1, 8),
    (N'FloorSurface', N'TH', N'EN', N'09', N'Grinding Stone', 1, 9),
    (N'FloorSurface', N'TH', N'TH', N'09', N'Grinding Stone', 1, 9),
    (N'FloorSurface', N'TH', N'EN', N'10', N'Marble', 1, 10),
    (N'FloorSurface', N'TH', N'TH', N'10', N'Marble', 1, 10),
    (N'FloorSurface', N'TH', N'EN', N'99', N'Others', 1, 11),
    (N'FloorSurface', N'TH', N'TH', N'99', N'Others', 1, 11);
GO

-- ----------------------------------------
-- Group: FloorType (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FloorType', N'TH', N'EN', N'01', N'Normal', 1, 1),
    (N'FloorType', N'TH', N'TH', N'01', N'Normal', 1, 1),
    (N'FloorType', N'TH', N'EN', N'02', N'Mezzanine', 1, 2),
    (N'FloorType', N'TH', N'TH', N'02', N'Mezzanine', 1, 2),
    (N'FloorType', N'TH', N'EN', N'03', N'High foundation', 1, 3),
    (N'FloorType', N'TH', N'TH', N'03', N'High foundation', 1, 3),
    (N'FloorType', N'TH', N'EN', N'04', N'Rooftop', 1, 4),
    (N'FloorType', N'TH', N'TH', N'04', N'Rooftop', 1, 4);
GO

-- ----------------------------------------
-- Group: FontColor (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FontColor', N'TH', N'EN', N'01', N'Red', 1, 1),
    (N'FontColor', N'TH', N'TH', N'01', N'Red', 1, 1),
    (N'FontColor', N'TH', N'EN', N'02', N'Orange', 1, 2),
    (N'FontColor', N'TH', N'TH', N'02', N'Orange', 1, 2),
    (N'FontColor', N'TH', N'EN', N'03', N'Yellow', 1, 3),
    (N'FontColor', N'TH', N'TH', N'03', N'Yellow', 1, 3),
    (N'FontColor', N'TH', N'EN', N'04', N'Green', 1, 4),
    (N'FontColor', N'TH', N'TH', N'04', N'Green', 1, 4),
    (N'FontColor', N'TH', N'EN', N'05', N'Blue', 1, 5),
    (N'FontColor', N'TH', N'TH', N'05', N'Blue', 1, 5),
    (N'FontColor', N'TH', N'EN', N'06', N'Purple', 1, 6),
    (N'FontColor', N'TH', N'TH', N'06', N'Purple', 1, 6),
    (N'FontColor', N'TH', N'EN', N'07', N'Black', 1, 7),
    (N'FontColor', N'TH', N'TH', N'07', N'Black', 1, 7),
    (N'FontColor', N'TH', N'EN', N'08', N'White', 1, 8),
    (N'FontColor', N'TH', N'TH', N'08', N'White', 1, 8);
GO

-- ----------------------------------------
-- Group: FontStyle (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FontStyle', N'TH', N'EN', N'01', N'Bold', 1, 1),
    (N'FontStyle', N'TH', N'TH', N'01', N'Bold', 1, 1),
    (N'FontStyle', N'TH', N'EN', N'02', N'Italic', 1, 2),
    (N'FontStyle', N'TH', N'TH', N'02', N'Italic', 1, 2),
    (N'FontStyle', N'TH', N'EN', N'03', N'Underline', 1, 3),
    (N'FontStyle', N'TH', N'TH', N'03', N'Underline', 1, 3);
GO

-- ----------------------------------------
-- Group: ForceSalesValuePCT (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ForceSalesValuePCT', N'TH', N'EN', N'01', N'Force Sale Value', 1, 1),
    (N'ForceSalesValuePCT', N'TH', N'TH', N'01', N'Force Sale Value', 1, 1);
GO

-- ----------------------------------------
-- Group: FunctionDate (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'FunctionDate', N'TH', N'EN', N'01', N'Request Date', 1, 1),
    (N'FunctionDate', N'TH', N'TH', N'01', N'Request Date', 1, 1),
    (N'FunctionDate', N'TH', N'EN', N'02', N'Assigned Date', 1, 2),
    (N'FunctionDate', N'TH', N'TH', N'02', N'Assigned Date', 1, 2),
    (N'FunctionDate', N'TH', N'EN', N'03', N'Approved Date', 1, 3),
    (N'FunctionDate', N'TH', N'TH', N'03', N'Approved Date', 1, 3);
GO

-- ----------------------------------------
-- Group: GeneralStructure (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'GeneralStructure', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'GeneralStructure', N'TH', N'TH', N'01', N'คอนกรีตเสริมเหล็ก', 1, 1),
    (N'GeneralStructure', N'TH', N'EN', N'02', N'Steel', 1, 2),
    (N'GeneralStructure', N'TH', N'TH', N'02', N'เหล็ก', 1, 2),
    (N'GeneralStructure', N'TH', N'EN', N'03', N'Wood', 1, 3),
    (N'GeneralStructure', N'TH', N'TH', N'03', N'ไม้', 1, 3),
    (N'GeneralStructure', N'TH', N'EN', N'04', N'Can''t Check', 1, 4),
    (N'GeneralStructure', N'TH', N'TH', N'04', N'ไม่สามารถตรวจได้', 1, 4),
    (N'GeneralStructure', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'GeneralStructure', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: GroundFlooringMaterials (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'GroundFlooringMaterials', N'TH', N'EN', N'01', N'Polished concrete', 1, 1),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'01', N'Polished concrete', 1, 1),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'02', N'Glazed tiles', 1, 2),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'02', N'Glazed tiles', 1, 2),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'03', N'Marble', 1, 3),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'03', N'Marble', 1, 3),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'04', N'Granite', 1, 4),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'04', N'Granite', 1, 4),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'05', N'Laminate', 1, 5),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'05', N'Laminate', 1, 5),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'06', N'Parquet', 1, 6),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'06', N'Parquet', 1, 6),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'07', N'Rubber Tiles', 1, 7),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'07', N'Rubber Tiles', 1, 7),
    (N'GroundFlooringMaterials', N'TH', N'EN', N'99', N'Other', 1, 8),
    (N'GroundFlooringMaterials', N'TH', N'TH', N'99', N'Other', 1, 8);
GO

-- ----------------------------------------
-- Group: HasBuilding (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'HasBuilding', N'TH', N'EN', N'01', N'Yes', 1, 1),
    (N'HasBuilding', N'TH', N'TH', N'01', N'มี', 1, 1),
    (N'HasBuilding', N'TH', N'EN', N'02', N'No', 1, 2),
    (N'HasBuilding', N'TH', N'TH', N'02', N'ไม่มี', 1, 2),
    (N'HasBuilding', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'HasBuilding', N'TH', N'TH', N'99', N'อื่นๆ', 1, 3);
GO

-- ----------------------------------------
-- Group: Header (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Header', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'Header', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'Header', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'Header', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'Header', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'Header', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'Header', N'TH', N'EN', N'99', N'99', 1, 4),
    (N'Header', N'TH', N'TH', N'99', N'99', 1, 4);
GO

-- ----------------------------------------
-- Group: ImportChannel (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ImportChannel', N'TH', N'EN', N'01', N'External Data', 1, 1),
    (N'ImportChannel', N'TH', N'TH', N'01', N'ข้อมูลภายนอก', 1, 1),
    (N'ImportChannel', N'TH', N'EN', N'02', N'Survey Data', 1, 2),
    (N'ImportChannel', N'TH', N'TH', N'02', N'ข้อมููลสำรวจ', 1, 2);
GO

-- ----------------------------------------
-- Group: ImportTypeDesc (English) (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ImportTypeDesc (English)', N'TH', N'EN', N'IsDeleted', N'UpdatedBy', 1, 3),
    (N'ImportTypeDesc (English)', N'TH', N'TH', N'IsDeleted', N'New Code', 1, 3);
GO

-- ----------------------------------------
-- Group: InForestBoundary (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'InForestBoundary', N'TH', N'EN', N'01', N'Not in Forest Boundary', 1, 1),
    (N'InForestBoundary', N'TH', N'TH', N'01', N'Not in Forest Boundary', 1, 1),
    (N'InForestBoundary', N'TH', N'EN', N'02', N'In Forest Boundary', 1, 2),
    (N'InForestBoundary', N'TH', N'TH', N'02', N'In Forest Boundary', 1, 2);
GO

-- ----------------------------------------
-- Group: IndicatorYesNo (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'IndicatorYesNo', N'TH', N'EN', N'Y', N'Yes', 1, 1),
    (N'IndicatorYesNo', N'TH', N'TH', N'Y', N'Yes', 1, 1),
    (N'IndicatorYesNo', N'TH', N'EN', N'N', N'No', 1, 2),
    (N'IndicatorYesNo', N'TH', N'TH', N'N', N'No', 1, 2);
GO

-- ----------------------------------------
-- Group: Interior (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Interior', N'TH', N'EN', N'01', N'Wood', 1, 1),
    (N'Interior', N'TH', N'TH', N'01', N'Wood', 1, 1),
    (N'Interior', N'TH', N'EN', N'02', N'Smooth plastered brickwork and painted', 1, 2),
    (N'Interior', N'TH', N'TH', N'02', N'Smooth plastered brickwork and painted', 1, 2),
    (N'Interior', N'TH', N'EN', N'03', N'Wallpaper', 1, 3),
    (N'Interior', N'TH', N'TH', N'03', N'Wallpaper', 1, 3),
    (N'Interior', N'TH', N'EN', N'99', N'Other', 1, 4),
    (N'Interior', N'TH', N'TH', N'99', N'Other', 1, 4);
GO

-- ----------------------------------------
-- Group: InvoiceStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'InvoiceStatus', N'TH', N'EN', N'01', N'Pending', 1, 1),
    (N'InvoiceStatus', N'TH', N'TH', N'01', N'Pending', 1, 1),
    (N'InvoiceStatus', N'TH', N'EN', N'02', N'Pending Paid', 1, 2),
    (N'InvoiceStatus', N'TH', N'TH', N'02', N'Pending Paid', 1, 2);
GO

-- ----------------------------------------
-- Group: LandAccessibility (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandAccessibility', N'TH', N'EN', N'01', N'Able', 1, 1),
    (N'LandAccessibility', N'TH', N'TH', N'01', N'ได้', 1, 1),
    (N'LandAccessibility', N'TH', N'EN', N'02', N'Unable', 1, 2),
    (N'LandAccessibility', N'TH', N'TH', N'02', N'ไม่ได้', 1, 2),
    (N'LandAccessibility', N'TH', N'EN', N'03', N'Is Alteration', 1, 3),
    (N'LandAccessibility', N'TH', N'TH', N'03', N'ต้องมีการปรับปรุงสภาพทางเข้า-ออก', 1, 3),
    (N'LandAccessibility', N'TH', N'EN', N'04', N'Access is seasonal', 1, 4),
    (N'LandAccessibility', N'TH', N'TH', N'04', N'เข้า-ออกได้ตามฤดูกาล', 1, 4);
GO

-- ----------------------------------------
-- Group: LandEntranceExit (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandEntranceExit', N'TH', N'EN', N'01', N'Public Interest', 1, 1),
    (N'LandEntranceExit', N'TH', N'TH', N'01', N'ทางสาธารณประโยชน์', 1, 1),
    (N'LandEntranceExit', N'TH', N'EN', N'02', N'Inside the Allocation Project', 1, 2),
    (N'LandEntranceExit', N'TH', N'TH', N'02', N'ทางภายในโครงการจัดสรรที่ได้รับอนุญาตจัดสรร', 1, 2),
    (N'LandEntranceExit', N'TH', N'EN', N'03', N'Personal', 1, 3),
    (N'LandEntranceExit', N'TH', N'TH', N'03', N'ทางส่วนบุคคล(ต้องนำมาจดภาระจำยอม)', 1, 3),
    (N'LandEntranceExit', N'TH', N'EN', N'04', N'Servitude', 1, 4),
    (N'LandEntranceExit', N'TH', N'TH', N'04', N'ทางภาระจำยอม', 1, 4),
    (N'LandEntranceExit', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'LandEntranceExit', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5),
    (N'LandEntranceExit', N'TH', N'EN', N'05', N'Near BTS/MRT', 1, 6),
    (N'LandEntranceExit', N'TH', N'TH', N'05', N'ใกล้รถไฟฟ้า BTS/MRT', 1, 6);
GO

-- ----------------------------------------
-- Group: LandLocation (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandLocation', N'TH', N'EN', N'01', N'Correct', 1, 1),
    (N'LandLocation', N'TH', N'TH', N'01', N'Correct', 1, 1),
    (N'LandLocation', N'TH', N'EN', N'02', N'Incorrect', 1, 2),
    (N'LandLocation', N'TH', N'TH', N'02', N'Incorrect', 1, 2);
GO

-- ----------------------------------------
-- Group: LandOffice (EN=747, TH=747)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandOffice', N'TH', N'EN', N'001', N'กรุงเทพมหานคร สาขาบางเขน', 1, 1),
    (N'LandOffice', N'TH', N'TH', N'001', N'กรุงเทพมหานคร สาขาบางเขน', 1, 1),
    (N'LandOffice', N'TH', N'EN', N'002', N'กรุงเทพมหานคร สาขาพระโขนง', 1, 2),
    (N'LandOffice', N'TH', N'TH', N'002', N'กรุงเทพมหานคร สาขาพระโขนง', 1, 2),
    (N'LandOffice', N'TH', N'EN', N'003', N'กรุงเทพมหานคร สาขาบางขุนเทียน', 1, 3),
    (N'LandOffice', N'TH', N'TH', N'003', N'กรุงเทพมหานคร สาขาบางขุนเทียน', 1, 3),
    (N'LandOffice', N'TH', N'EN', N'004', N'กรุงเทพมหานคร สาขาบางกอกน้อย', 1, 4),
    (N'LandOffice', N'TH', N'TH', N'004', N'กรุงเทพมหานคร สาขาบางกอกน้อย', 1, 4),
    (N'LandOffice', N'TH', N'EN', N'005', N'กรุงเทพมหานคร สาขาบางกะปิ', 1, 5),
    (N'LandOffice', N'TH', N'TH', N'005', N'กรุงเทพมหานคร สาขาบางกะปิ', 1, 5),
    (N'LandOffice', N'TH', N'EN', N'006', N'กรุงเทพมหานคร สาขาหนองแขม', 1, 6),
    (N'LandOffice', N'TH', N'TH', N'006', N'กรุงเทพมหานคร สาขาหนองแขม', 1, 6),
    (N'LandOffice', N'TH', N'EN', N'007', N'กรุงเทพมหานคร สาขามีนบุรี', 1, 7),
    (N'LandOffice', N'TH', N'TH', N'007', N'กรุงเทพมหานคร สาขามีนบุรี', 1, 7),
    (N'LandOffice', N'TH', N'EN', N'008', N'กรุงเทพมหานคร สาขาห้วยขวาง', 1, 8),
    (N'LandOffice', N'TH', N'TH', N'008', N'กรุงเทพมหานคร สาขาห้วยขวาง', 1, 8),
    (N'LandOffice', N'TH', N'EN', N'009', N'กรุงเทพมหานคร สาขาจตุจักร', 1, 9),
    (N'LandOffice', N'TH', N'TH', N'009', N'กรุงเทพมหานคร สาขาจตุจักร', 1, 9),
    (N'LandOffice', N'TH', N'EN', N'010', N'กรุงเทพมหานคร สาขาธนบุรี', 1, 10),
    (N'LandOffice', N'TH', N'TH', N'010', N'กรุงเทพมหานคร สาขาธนบุรี', 1, 10),
    (N'LandOffice', N'TH', N'EN', N'011', N'กรุงเทพมหานคร สาขาลาดพร้าว', 1, 11),
    (N'LandOffice', N'TH', N'TH', N'011', N'กรุงเทพมหานคร สาขาลาดพร้าว', 1, 11),
    (N'LandOffice', N'TH', N'EN', N'012', N'กรุงเทพมหานคร สาขาดอนเมือง', 1, 12),
    (N'LandOffice', N'TH', N'TH', N'012', N'กรุงเทพมหานคร สาขาดอนเมือง', 1, 12),
    (N'LandOffice', N'TH', N'EN', N'013', N'กรุงเทพมหานคร สาขาบึงกุ่ม', 1, 13),
    (N'LandOffice', N'TH', N'TH', N'013', N'กรุงเทพมหานคร สาขาบึงกุ่ม', 1, 13),
    (N'LandOffice', N'TH', N'EN', N'014', N'กรุงเทพมหานคร สาขาประเวศ', 1, 14),
    (N'LandOffice', N'TH', N'TH', N'014', N'กรุงเทพมหานคร สาขาประเวศ', 1, 14),
    (N'LandOffice', N'TH', N'EN', N'015', N'กรุงเทพมหานคร สาขาหนองจอก', 1, 15),
    (N'LandOffice', N'TH', N'TH', N'015', N'กรุงเทพมหานคร สาขาหนองจอก', 1, 15),
    (N'LandOffice', N'TH', N'EN', N'016', N'กรุงเทพมหานคร สาขาลาดกระบัง', 1, 16),
    (N'LandOffice', N'TH', N'TH', N'016', N'กรุงเทพมหานคร สาขาลาดกระบัง', 1, 16),
    (N'LandOffice', N'TH', N'EN', N'017', N'กระบี่', 1, 17),
    (N'LandOffice', N'TH', N'TH', N'017', N'กระบี่', 1, 17),
    (N'LandOffice', N'TH', N'EN', N'018', N'กระบี่ สาขาอ่าวลึก', 1, 18),
    (N'LandOffice', N'TH', N'TH', N'018', N'กระบี่ สาขาอ่าวลึก', 1, 18),
    (N'LandOffice', N'TH', N'EN', N'019', N'กระบี่ สาขาคลองท่อม', 1, 19),
    (N'LandOffice', N'TH', N'TH', N'019', N'กระบี่ สาขาคลองท่อม', 1, 19),
    (N'LandOffice', N'TH', N'EN', N'020', N'กาญจนบุรี', 1, 20),
    (N'LandOffice', N'TH', N'TH', N'020', N'กาญจนบุรี', 1, 20),
    (N'LandOffice', N'TH', N'EN', N'021', N'กาญจนบุรี สาขาท่ามะกา', 1, 21),
    (N'LandOffice', N'TH', N'TH', N'021', N'กาญจนบุรี สาขาท่ามะกา', 1, 21),
    (N'LandOffice', N'TH', N'EN', N'022', N'กาญจนบุรี สาขาบ่อพลอย', 1, 22),
    (N'LandOffice', N'TH', N'TH', N'022', N'กาญจนบุรี สาขาบ่อพลอย', 1, 22),
    (N'LandOffice', N'TH', N'EN', N'023', N'กาญจนบุรี ส่วนแยกเลาขวัญ', 1, 23),
    (N'LandOffice', N'TH', N'TH', N'023', N'กาญจนบุรี ส่วนแยกเลาขวัญ', 1, 23),
    (N'LandOffice', N'TH', N'EN', N'024', N'กาญจนบุรี ส่วนแยกพนมทวน', 1, 24),
    (N'LandOffice', N'TH', N'TH', N'024', N'กาญจนบุรี ส่วนแยกพนมทวน', 1, 24),
    (N'LandOffice', N'TH', N'EN', N'025', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', 1, 25),
    (N'LandOffice', N'TH', N'TH', N'025', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', 1, 25),
    (N'LandOffice', N'TH', N'EN', N'026', N'กาญจนบุรี สาขาท่าม่วง', 1, 26),
    (N'LandOffice', N'TH', N'TH', N'026', N'กาญจนบุรี สาขาท่าม่วง', 1, 26),
    (N'LandOffice', N'TH', N'EN', N'027', N'จังหวัดกาฬสินธุ์', 1, 27),
    (N'LandOffice', N'TH', N'TH', N'027', N'จังหวัดกาฬสินธุ์', 1, 27),
    (N'LandOffice', N'TH', N'EN', N'028', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', 1, 28),
    (N'LandOffice', N'TH', N'TH', N'028', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', 1, 28),
    (N'LandOffice', N'TH', N'EN', N'029', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', 1, 29),
    (N'LandOffice', N'TH', N'TH', N'029', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', 1, 29),
    (N'LandOffice', N'TH', N'EN', N'030', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 30),
    (N'LandOffice', N'TH', N'TH', N'030', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 30),
    (N'LandOffice', N'TH', N'EN', N'031', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 31),
    (N'LandOffice', N'TH', N'TH', N'031', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 31),
    (N'LandOffice', N'TH', N'EN', N'032', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', 1, 32),
    (N'LandOffice', N'TH', N'TH', N'032', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', 1, 32),
    (N'LandOffice', N'TH', N'EN', N'033', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', 1, 33),
    (N'LandOffice', N'TH', N'TH', N'033', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', 1, 33),
    (N'LandOffice', N'TH', N'EN', N'034', N'จังหวัดกำแพงเพชร', 1, 34),
    (N'LandOffice', N'TH', N'TH', N'034', N'จังหวัดกำแพงเพชร', 1, 34),
    (N'LandOffice', N'TH', N'EN', N'035', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', 1, 35),
    (N'LandOffice', N'TH', N'TH', N'035', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', 1, 35),
    (N'LandOffice', N'TH', N'EN', N'036', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', 1, 36),
    (N'LandOffice', N'TH', N'TH', N'036', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', 1, 36),
    (N'LandOffice', N'TH', N'EN', N'037', N'จังหวัดขอนแก่น', 1, 37),
    (N'LandOffice', N'TH', N'TH', N'037', N'จังหวัดขอนแก่น', 1, 37),
    (N'LandOffice', N'TH', N'EN', N'038', N'จังหวัดขอนแก่น สาขาพล', 1, 38),
    (N'LandOffice', N'TH', N'TH', N'038', N'จังหวัดขอนแก่น สาขาพล', 1, 38),
    (N'LandOffice', N'TH', N'EN', N'039', N'จังหวัดขอนแก่น สาขาชุมแพ', 1, 39),
    (N'LandOffice', N'TH', N'TH', N'039', N'จังหวัดขอนแก่น สาขาชุมแพ', 1, 39),
    (N'LandOffice', N'TH', N'EN', N'040', N'จังหวัดขอนแก่น สาขาบ้านไผ่', 1, 40),
    (N'LandOffice', N'TH', N'TH', N'040', N'จังหวัดขอนแก่น สาขาบ้านไผ่', 1, 40),
    (N'LandOffice', N'TH', N'EN', N'041', N'จังหวัดขอนแก่น สาขากระนวน', 1, 41),
    (N'LandOffice', N'TH', N'TH', N'041', N'จังหวัดขอนแก่น สาขากระนวน', 1, 41),
    (N'LandOffice', N'TH', N'EN', N'042', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', 1, 42),
    (N'LandOffice', N'TH', N'TH', N'042', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', 1, 42),
    (N'LandOffice', N'TH', N'EN', N'043', N'จังหวัดขอนแก่น สาขาหนองเรือ', 1, 43),
    (N'LandOffice', N'TH', N'TH', N'043', N'จังหวัดขอนแก่น สาขาหนองเรือ', 1, 43),
    (N'LandOffice', N'TH', N'EN', N'044', N'จังหวัดขอนแก่น สาขามัญจาคีรี', 1, 44),
    (N'LandOffice', N'TH', N'TH', N'044', N'จังหวัดขอนแก่น สาขามัญจาคีรี', 1, 44),
    (N'LandOffice', N'TH', N'EN', N'045', N'จังหวัดขอนแก่น สาขาภูเวียง', 1, 45),
    (N'LandOffice', N'TH', N'TH', N'045', N'จังหวัดขอนแก่น สาขาภูเวียง', 1, 45),
    (N'LandOffice', N'TH', N'EN', N'046', N'จังหวัดขอนแก่น สาขาน้ำพอง', 1, 46),
    (N'LandOffice', N'TH', N'TH', N'046', N'จังหวัดขอนแก่น สาขาน้ำพอง', 1, 46),
    (N'LandOffice', N'TH', N'EN', N'047', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', 1, 47),
    (N'LandOffice', N'TH', N'TH', N'047', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', 1, 47),
    (N'LandOffice', N'TH', N'EN', N'048', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', 1, 48),
    (N'LandOffice', N'TH', N'TH', N'048', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', 1, 48),
    (N'LandOffice', N'TH', N'EN', N'049', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', 1, 49),
    (N'LandOffice', N'TH', N'TH', N'049', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', 1, 49),
    (N'LandOffice', N'TH', N'EN', N'050', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', 1, 50),
    (N'LandOffice', N'TH', N'TH', N'050', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', 1, 50),
    (N'LandOffice', N'TH', N'EN', N'051', N'จังหวัดจันทบุรี', 1, 51),
    (N'LandOffice', N'TH', N'TH', N'051', N'จังหวัดจันทบุรี', 1, 51),
    (N'LandOffice', N'TH', N'EN', N'052', N'จังหวัดจันทบุรี สาขาท่าใหม่', 1, 52),
    (N'LandOffice', N'TH', N'TH', N'052', N'จังหวัดจันทบุรี สาขาท่าใหม่', 1, 52),
    (N'LandOffice', N'TH', N'EN', N'053', N'จังหวัดจันทบุรี ส่วนแยกขลุง', 1, 53),
    (N'LandOffice', N'TH', N'TH', N'053', N'จังหวัดจันทบุรี ส่วนแยกขลุง', 1, 53),
    (N'LandOffice', N'TH', N'EN', N'054', N'จังหวัดจันทบุรี สาขามะขาม', 1, 54),
    (N'LandOffice', N'TH', N'TH', N'054', N'จังหวัดจันทบุรี สาขามะขาม', 1, 54),
    (N'LandOffice', N'TH', N'EN', N'055', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', 1, 55),
    (N'LandOffice', N'TH', N'TH', N'055', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', 1, 55),
    (N'LandOffice', N'TH', N'EN', N'056', N'จังหวัดฉะเชิงเทรา', 1, 56),
    (N'LandOffice', N'TH', N'TH', N'056', N'จังหวัดฉะเชิงเทรา', 1, 56),
    (N'LandOffice', N'TH', N'EN', N'057', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', 1, 57),
    (N'LandOffice', N'TH', N'TH', N'057', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', 1, 57),
    (N'LandOffice', N'TH', N'EN', N'058', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', 1, 58),
    (N'LandOffice', N'TH', N'TH', N'058', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', 1, 58),
    (N'LandOffice', N'TH', N'EN', N'059', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', 1, 59),
    (N'LandOffice', N'TH', N'TH', N'059', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', 1, 59),
    (N'LandOffice', N'TH', N'EN', N'060', N'จังหวัดชลบุรี', 1, 60),
    (N'LandOffice', N'TH', N'TH', N'060', N'จังหวัดชลบุรี', 1, 60),
    (N'LandOffice', N'TH', N'EN', N'061', N'จังหวัดชลบุรี สาขาบางละมุง', 1, 61),
    (N'LandOffice', N'TH', N'TH', N'061', N'จังหวัดชลบุรี สาขาบางละมุง', 1, 61),
    (N'LandOffice', N'TH', N'EN', N'062', N'จังหวัดชลบุรี สาขาพนัสนิคม', 1, 62),
    (N'LandOffice', N'TH', N'TH', N'062', N'จังหวัดชลบุรี สาขาพนัสนิคม', 1, 62),
    (N'LandOffice', N'TH', N'EN', N'063', N'จังหวัดชลบุรี สาขาศรีราชา', 1, 63),
    (N'LandOffice', N'TH', N'TH', N'063', N'จังหวัดชลบุรี สาขาศรีราชา', 1, 63),
    (N'LandOffice', N'TH', N'EN', N'064', N'จังหวัดชลบุรี สาขาสัตหีบ', 1, 64),
    (N'LandOffice', N'TH', N'TH', N'064', N'จังหวัดชลบุรี สาขาสัตหีบ', 1, 64),
    (N'LandOffice', N'TH', N'EN', N'065', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', 1, 65),
    (N'LandOffice', N'TH', N'TH', N'065', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', 1, 65),
    (N'LandOffice', N'TH', N'EN', N'066', N'จังหวัดชัยภูมิ', 1, 66),
    (N'LandOffice', N'TH', N'TH', N'066', N'จังหวัดชัยภูมิ', 1, 66),
    (N'LandOffice', N'TH', N'EN', N'067', N'จังหวัดชัยภูมิ สาขาภูเขียว', 1, 67),
    (N'LandOffice', N'TH', N'TH', N'067', N'จังหวัดชัยภูมิ สาขาภูเขียว', 1, 67),
    (N'LandOffice', N'TH', N'EN', N'068', N'จังหวัดชัยภูมิ สาขาจัตุรัส', 1, 68),
    (N'LandOffice', N'TH', N'TH', N'068', N'จังหวัดชัยภูมิ สาขาจัตุรัส', 1, 68),
    (N'LandOffice', N'TH', N'EN', N'069', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', 1, 69),
    (N'LandOffice', N'TH', N'TH', N'069', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', 1, 69),
    (N'LandOffice', N'TH', N'EN', N'070', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', 1, 70),
    (N'LandOffice', N'TH', N'TH', N'070', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', 1, 70),
    (N'LandOffice', N'TH', N'EN', N'071', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', 1, 71),
    (N'LandOffice', N'TH', N'TH', N'071', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', 1, 71),
    (N'LandOffice', N'TH', N'EN', N'072', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', 1, 72),
    (N'LandOffice', N'TH', N'TH', N'072', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', 1, 72),
    (N'LandOffice', N'TH', N'EN', N'073', N'จังหวัดชัยนาท', 1, 73),
    (N'LandOffice', N'TH', N'TH', N'073', N'จังหวัดชัยนาท', 1, 73),
    (N'LandOffice', N'TH', N'EN', N'074', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', 1, 74),
    (N'LandOffice', N'TH', N'TH', N'074', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', 1, 74),
    (N'LandOffice', N'TH', N'EN', N'075', N'จังหวัดชัยนาท สาขาสรรคบุรี', 1, 75),
    (N'LandOffice', N'TH', N'TH', N'075', N'จังหวัดชัยนาท สาขาสรรคบุรี', 1, 75),
    (N'LandOffice', N'TH', N'EN', N'076', N'จังหวัดชัยนาท สาขาหันคา', 1, 76),
    (N'LandOffice', N'TH', N'TH', N'076', N'จังหวัดชัยนาท สาขาหันคา', 1, 76),
    (N'LandOffice', N'TH', N'EN', N'077', N'จังหวัดชุมพร', 1, 77),
    (N'LandOffice', N'TH', N'TH', N'077', N'จังหวัดชุมพร', 1, 77),
    (N'LandOffice', N'TH', N'EN', N'078', N'จังหวัดชุมพร สาขาหลังสวน', 1, 78),
    (N'LandOffice', N'TH', N'TH', N'078', N'จังหวัดชุมพร สาขาหลังสวน', 1, 78),
    (N'LandOffice', N'TH', N'EN', N'079', N'จังหวัดชุมพร สาขาปะทิว', 1, 79),
    (N'LandOffice', N'TH', N'TH', N'079', N'จังหวัดชุมพร สาขาปะทิว', 1, 79),
    (N'LandOffice', N'TH', N'EN', N'080', N'จังหวัดชุมพร สาขาสวี', 1, 80),
    (N'LandOffice', N'TH', N'TH', N'080', N'จังหวัดชุมพร สาขาสวี', 1, 80),
    (N'LandOffice', N'TH', N'EN', N'081', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', 1, 81),
    (N'LandOffice', N'TH', N'TH', N'081', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', 1, 81),
    (N'LandOffice', N'TH', N'EN', N'082', N'จังหวัดเชียงใหม่', 1, 82),
    (N'LandOffice', N'TH', N'TH', N'082', N'จังหวัดเชียงใหม่', 1, 82),
    (N'LandOffice', N'TH', N'EN', N'083', N'จังหวัดเชียงใหม่ สาขาจอมทอง', 1, 83),
    (N'LandOffice', N'TH', N'TH', N'083', N'จังหวัดเชียงใหม่ สาขาจอมทอง', 1, 83),
    (N'LandOffice', N'TH', N'EN', N'084', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', 1, 84),
    (N'LandOffice', N'TH', N'TH', N'084', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', 1, 84),
    (N'LandOffice', N'TH', N'EN', N'085', N'จังหวัดเชียงใหม่ สาขาฝาง', 1, 85),
    (N'LandOffice', N'TH', N'TH', N'085', N'จังหวัดเชียงใหม่ สาขาฝาง', 1, 85),
    (N'LandOffice', N'TH', N'EN', N'086', N'จังหวัดเชียงใหม่ สาขาพร้าว', 1, 86),
    (N'LandOffice', N'TH', N'TH', N'086', N'จังหวัดเชียงใหม่ สาขาพร้าว', 1, 86),
    (N'LandOffice', N'TH', N'EN', N'087', N'จังหวัดเชียงใหม่ สาขาแม่แตง', 1, 87),
    (N'LandOffice', N'TH', N'TH', N'087', N'จังหวัดเชียงใหม่ สาขาแม่แตง', 1, 87),
    (N'LandOffice', N'TH', N'EN', N'088', N'จังหวัดเชียงใหม่ สาขาแม่ริม', 1, 88),
    (N'LandOffice', N'TH', N'TH', N'088', N'จังหวัดเชียงใหม่ สาขาแม่ริม', 1, 88),
    (N'LandOffice', N'TH', N'EN', N'089', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', 1, 89),
    (N'LandOffice', N'TH', N'TH', N'089', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', 1, 89),
    (N'LandOffice', N'TH', N'EN', N'090', N'จังหวัดเชียงใหม่ สาขาสันทราย', 1, 90),
    (N'LandOffice', N'TH', N'TH', N'090', N'จังหวัดเชียงใหม่ สาขาสันทราย', 1, 90),
    (N'LandOffice', N'TH', N'EN', N'091', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', 1, 91),
    (N'LandOffice', N'TH', N'TH', N'091', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', 1, 91),
    (N'LandOffice', N'TH', N'EN', N'092', N'จังหวัดเชียงใหม่ สาขาหางดง', 1, 92),
    (N'LandOffice', N'TH', N'TH', N'092', N'จังหวัดเชียงใหม่ สาขาหางดง', 1, 92),
    (N'LandOffice', N'TH', N'EN', N'093', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', 1, 93),
    (N'LandOffice', N'TH', N'TH', N'093', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', 1, 93),
    (N'LandOffice', N'TH', N'EN', N'094', N'จังหวัดเชียงใหม่ สาขาสารภี', 1, 94),
    (N'LandOffice', N'TH', N'TH', N'094', N'จังหวัดเชียงใหม่ สาขาสารภี', 1, 94),
    (N'LandOffice', N'TH', N'EN', N'095', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', 1, 95),
    (N'LandOffice', N'TH', N'TH', N'095', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', 1, 95),
    (N'LandOffice', N'TH', N'EN', N'096', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', 1, 96),
    (N'LandOffice', N'TH', N'TH', N'096', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', 1, 96),
    (N'LandOffice', N'TH', N'EN', N'097', N'จังหวัดเชียงราย', 1, 97),
    (N'LandOffice', N'TH', N'TH', N'097', N'จังหวัดเชียงราย', 1, 97),
    (N'LandOffice', N'TH', N'EN', N'098', N'จังหวัดเชียงราย สาขาเชียงของ', 1, 98),
    (N'LandOffice', N'TH', N'TH', N'098', N'จังหวัดเชียงราย สาขาเชียงของ', 1, 98),
    (N'LandOffice', N'TH', N'EN', N'099', N'จังหวัดเชียงราย สาขาพาน', 1, 99),
    (N'LandOffice', N'TH', N'TH', N'099', N'จังหวัดเชียงราย สาขาพาน', 1, 99),
    (N'LandOffice', N'TH', N'EN', N'100', N'จังหวัดเชียงราย สาขาเทิง', 1, 100),
    (N'LandOffice', N'TH', N'TH', N'100', N'จังหวัดเชียงราย สาขาเทิง', 1, 100),
    (N'LandOffice', N'TH', N'EN', N'101', N'จังหวัดเชียงราย สาขาแม่จัน', 1, 101),
    (N'LandOffice', N'TH', N'TH', N'101', N'จังหวัดเชียงราย สาขาแม่จัน', 1, 101),
    (N'LandOffice', N'TH', N'EN', N'102', N'จังหวัดเชียงราย สาขาแม่สาย', 1, 102),
    (N'LandOffice', N'TH', N'TH', N'102', N'จังหวัดเชียงราย สาขาแม่สาย', 1, 102),
    (N'LandOffice', N'TH', N'EN', N'103', N'จังหวัดเชียงราย สาขาเวียงชัย', 1, 103),
    (N'LandOffice', N'TH', N'TH', N'103', N'จังหวัดเชียงราย สาขาเวียงชัย', 1, 103),
    (N'LandOffice', N'TH', N'EN', N'104', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', 1, 104),
    (N'LandOffice', N'TH', N'TH', N'104', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', 1, 104),
    (N'LandOffice', N'TH', N'EN', N'105', N'จังหวัดเชียงราย สาขาเชียงแสน', 1, 105),
    (N'LandOffice', N'TH', N'TH', N'105', N'จังหวัดเชียงราย สาขาเชียงแสน', 1, 105),
    (N'LandOffice', N'TH', N'EN', N'106', N'จังหวัดตรัง', 1, 106),
    (N'LandOffice', N'TH', N'TH', N'106', N'จังหวัดตรัง', 1, 106),
    (N'LandOffice', N'TH', N'EN', N'107', N'จังหวัดตรัง สาขาห้วยยอด', 1, 107),
    (N'LandOffice', N'TH', N'TH', N'107', N'จังหวัดตรัง สาขาห้วยยอด', 1, 107),
    (N'LandOffice', N'TH', N'EN', N'108', N'จังหวัดตรัง สาขาย่านตาขาว', 1, 108),
    (N'LandOffice', N'TH', N'TH', N'108', N'จังหวัดตรัง สาขาย่านตาขาว', 1, 108),
    (N'LandOffice', N'TH', N'EN', N'109', N'จังหวัดตรัง สาขากันตรัง', 1, 109),
    (N'LandOffice', N'TH', N'TH', N'109', N'จังหวัดตรัง สาขากันตรัง', 1, 109),
    (N'LandOffice', N'TH', N'EN', N'110', N'จังหวัดตาก', 1, 110),
    (N'LandOffice', N'TH', N'TH', N'110', N'จังหวัดตาก', 1, 110),
    (N'LandOffice', N'TH', N'EN', N'111', N'จังหวัดตาก สาขาแม่สอด', 1, 111),
    (N'LandOffice', N'TH', N'TH', N'111', N'จังหวัดตาก สาขาแม่สอด', 1, 111),
    (N'LandOffice', N'TH', N'EN', N'112', N'จังหวัดตาก สาขาสามเงา', 1, 112),
    (N'LandOffice', N'TH', N'TH', N'112', N'จังหวัดตาก สาขาสามเงา', 1, 112),
    (N'LandOffice', N'TH', N'EN', N'113', N'จังหวัดตราด', 1, 113),
    (N'LandOffice', N'TH', N'TH', N'113', N'จังหวัดตราด', 1, 113),
    (N'LandOffice', N'TH', N'EN', N'114', N'จังหวัดตราด ส่วนแยกเขาสมิง', 1, 114),
    (N'LandOffice', N'TH', N'TH', N'114', N'จังหวัดตราด ส่วนแยกเขาสมิง', 1, 114),
    (N'LandOffice', N'TH', N'EN', N'115', N'จังหวัดตราด ส่วนแยกแหลมงอบ', 1, 115),
    (N'LandOffice', N'TH', N'TH', N'115', N'จังหวัดตราด ส่วนแยกแหลมงอบ', 1, 115),
    (N'LandOffice', N'TH', N'EN', N'116', N'จังหวัดนครนายก', 1, 116),
    (N'LandOffice', N'TH', N'TH', N'116', N'จังหวัดนครนายก', 1, 116),
    (N'LandOffice', N'TH', N'EN', N'117', N'จังหวัดนครนายก สาขาองครักษ์', 1, 117),
    (N'LandOffice', N'TH', N'TH', N'117', N'จังหวัดนครนายก สาขาองครักษ์', 1, 117),
    (N'LandOffice', N'TH', N'EN', N'118', N'จังหวัดนครปฐม', 1, 118),
    (N'LandOffice', N'TH', N'TH', N'118', N'จังหวัดนครปฐม', 1, 118),
    (N'LandOffice', N'TH', N'EN', N'119', N'จังหวัดนครปฐม สาขานครชัยศรี', 1, 119),
    (N'LandOffice', N'TH', N'TH', N'119', N'จังหวัดนครปฐม สาขานครชัยศรี', 1, 119),
    (N'LandOffice', N'TH', N'EN', N'120', N'จังหวัดนครปฐม สาขาบางเลน', 1, 120),
    (N'LandOffice', N'TH', N'TH', N'120', N'จังหวัดนครปฐม สาขาบางเลน', 1, 120),
    (N'LandOffice', N'TH', N'EN', N'121', N'จังหวัดนครปฐม สาขาสามพราน', 1, 121),
    (N'LandOffice', N'TH', N'TH', N'121', N'จังหวัดนครปฐม สาขาสามพราน', 1, 121),
    (N'LandOffice', N'TH', N'EN', N'122', N'จังหวัดนครปฐม สาขากำแพงแสน', 1, 122),
    (N'LandOffice', N'TH', N'TH', N'122', N'จังหวัดนครปฐม สาขากำแพงแสน', 1, 122),
    (N'LandOffice', N'TH', N'EN', N'123', N'จังหวัดนครปฐม สาขานครพนม', 1, 123),
    (N'LandOffice', N'TH', N'TH', N'123', N'จังหวัดนครปฐม สาขานครพนม', 1, 123),
    (N'LandOffice', N'TH', N'EN', N'124', N'จังหวัดนครพนม สาขาธาตุพนม', 1, 124),
    (N'LandOffice', N'TH', N'TH', N'124', N'จังหวัดนครพนม สาขาธาตุพนม', 1, 124),
    (N'LandOffice', N'TH', N'EN', N'125', N'จังหวัดนครพนม สาขาศรีสงคราม', 1, 125),
    (N'LandOffice', N'TH', N'TH', N'125', N'จังหวัดนครพนม สาขาศรีสงคราม', 1, 125),
    (N'LandOffice', N'TH', N'EN', N'126', N'จังหวัดนครพนม สาขาท่าอุเทน', 1, 126),
    (N'LandOffice', N'TH', N'TH', N'126', N'จังหวัดนครพนม สาขาท่าอุเทน', 1, 126),
    (N'LandOffice', N'TH', N'EN', N'127', N'จังหวัดนครพนม สาขานาแก', 1, 127),
    (N'LandOffice', N'TH', N'TH', N'127', N'จังหวัดนครพนม สาขานาแก', 1, 127),
    (N'LandOffice', N'TH', N'EN', N'128', N'จังหวัดนครพนม สาขาเรณูนคร', 1, 128),
    (N'LandOffice', N'TH', N'TH', N'128', N'จังหวัดนครพนม สาขาเรณูนคร', 1, 128),
    (N'LandOffice', N'TH', N'EN', N'129', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', 1, 129),
    (N'LandOffice', N'TH', N'TH', N'129', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', 1, 129),
    (N'LandOffice', N'TH', N'EN', N'130', N'จังหวัดนครราชสีมา', 1, 130),
    (N'LandOffice', N'TH', N'TH', N'130', N'จังหวัดนครราชสีมา', 1, 130),
    (N'LandOffice', N'TH', N'EN', N'131', N'จังหวัดนครราชสีมา สาขาคง', 1, 131),
    (N'LandOffice', N'TH', N'TH', N'131', N'จังหวัดนครราชสีมา สาขาคง', 1, 131),
    (N'LandOffice', N'TH', N'EN', N'132', N'จังหวัดนครราชสีมา สาขาครบุรี', 1, 132),
    (N'LandOffice', N'TH', N'TH', N'132', N'จังหวัดนครราชสีมา สาขาครบุรี', 1, 132),
    (N'LandOffice', N'TH', N'EN', N'133', N'จังหวัดนครราชสีมา สาขาจักราช', 1, 133),
    (N'LandOffice', N'TH', N'TH', N'133', N'จังหวัดนครราชสีมา สาขาจักราช', 1, 133),
    (N'LandOffice', N'TH', N'EN', N'134', N'จังหวัดนครราชสีมา สาขาชุมพวง', 1, 134),
    (N'LandOffice', N'TH', N'TH', N'134', N'จังหวัดนครราชสีมา สาขาชุมพวง', 1, 134),
    (N'LandOffice', N'TH', N'EN', N'135', N'จังหวัดนครราชสีมา สาขาโชคชัย', 1, 135),
    (N'LandOffice', N'TH', N'TH', N'135', N'จังหวัดนครราชสีมา สาขาโชคชัย', 1, 135),
    (N'LandOffice', N'TH', N'EN', N'136', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', 1, 136),
    (N'LandOffice', N'TH', N'TH', N'136', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', 1, 136),
    (N'LandOffice', N'TH', N'EN', N'137', N'จังหวัดนครราชสีมา สาขาโนนไทย', 1, 137),
    (N'LandOffice', N'TH', N'TH', N'137', N'จังหวัดนครราชสีมา สาขาโนนไทย', 1, 137),
    (N'LandOffice', N'TH', N'EN', N'138', N'จังหวัดนครรราชสีมา สาขาโนนสูง', 1, 138),
    (N'LandOffice', N'TH', N'TH', N'138', N'จังหวัดนครรราชสีมา สาขาโนนสูง', 1, 138),
    (N'LandOffice', N'TH', N'EN', N'139', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', 1, 139),
    (N'LandOffice', N'TH', N'TH', N'139', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', 1, 139),
    (N'LandOffice', N'TH', N'EN', N'140', N'จังหวัดนครราชสีมา สาขาประทาย', 1, 140),
    (N'LandOffice', N'TH', N'TH', N'140', N'จังหวัดนครราชสีมา สาขาประทาย', 1, 140),
    (N'LandOffice', N'TH', N'EN', N'141', N'จังหวัดนครราชสีมา สาขาปักธงชัย', 1, 141),
    (N'LandOffice', N'TH', N'TH', N'141', N'จังหวัดนครราชสีมา สาขาปักธงชัย', 1, 141),
    (N'LandOffice', N'TH', N'EN', N'142', N'จังหวัดนครราชสีมา สาขาปากช่อง', 1, 142),
    (N'LandOffice', N'TH', N'TH', N'142', N'จังหวัดนครราชสีมา สาขาปากช่อง', 1, 142),
    (N'LandOffice', N'TH', N'EN', N'143', N'จังหวัดนครราชสีมา สาขาพิมาย', 1, 143),
    (N'LandOffice', N'TH', N'TH', N'143', N'จังหวัดนครราชสีมา สาขาพิมาย', 1, 143),
    (N'LandOffice', N'TH', N'EN', N'144', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', 1, 144),
    (N'LandOffice', N'TH', N'TH', N'144', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', 1, 144),
    (N'LandOffice', N'TH', N'EN', N'145', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', 1, 145),
    (N'LandOffice', N'TH', N'TH', N'145', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', 1, 145),
    (N'LandOffice', N'TH', N'EN', N'146', N'จังหวัดนครราชสีมา สาขาสูงเนิน', 1, 146),
    (N'LandOffice', N'TH', N'TH', N'146', N'จังหวัดนครราชสีมา สาขาสูงเนิน', 1, 146),
    (N'LandOffice', N'TH', N'EN', N'147', N'จังหวัดนครศรีธรรมราช', 1, 147),
    (N'LandOffice', N'TH', N'TH', N'147', N'จังหวัดนครศรีธรรมราช', 1, 147),
    (N'LandOffice', N'TH', N'EN', N'148', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', 1, 148),
    (N'LandOffice', N'TH', N'TH', N'148', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', 1, 148),
    (N'LandOffice', N'TH', N'EN', N'149', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', 1, 149),
    (N'LandOffice', N'TH', N'TH', N'149', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', 1, 149),
    (N'LandOffice', N'TH', N'EN', N'150', N'จังหวัดนครศรีธรรมราช สาขาสิชล', 1, 150),
    (N'LandOffice', N'TH', N'TH', N'150', N'จังหวัดนครศรีธรรมราช สาขาสิชล', 1, 150),
    (N'LandOffice', N'TH', N'EN', N'151', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', 1, 151),
    (N'LandOffice', N'TH', N'TH', N'151', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', 1, 151),
    (N'LandOffice', N'TH', N'EN', N'152', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', 1, 152),
    (N'LandOffice', N'TH', N'TH', N'152', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', 1, 152),
    (N'LandOffice', N'TH', N'EN', N'153', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', 1, 153),
    (N'LandOffice', N'TH', N'TH', N'153', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', 1, 153),
    (N'LandOffice', N'TH', N'EN', N'154', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', 1, 154),
    (N'LandOffice', N'TH', N'TH', N'154', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', 1, 154),
    (N'LandOffice', N'TH', N'EN', N'155', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', 1, 155),
    (N'LandOffice', N'TH', N'TH', N'155', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', 1, 155),
    (N'LandOffice', N'TH', N'EN', N'156', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', 1, 156),
    (N'LandOffice', N'TH', N'TH', N'156', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', 1, 156),
    (N'LandOffice', N'TH', N'EN', N'157', N'จังหวัดนครสวรรค์', 1, 157),
    (N'LandOffice', N'TH', N'TH', N'157', N'จังหวัดนครสวรรค์', 1, 157),
    (N'LandOffice', N'TH', N'EN', N'158', N'จังหวัดนครสวรรค์ สาขาชุมแสง', 1, 158),
    (N'LandOffice', N'TH', N'TH', N'158', N'จังหวัดนครสวรรค์ สาขาชุมแสง', 1, 158),
    (N'LandOffice', N'TH', N'EN', N'159', N'จังหวัดนครสวรรค์ สาขาตาคลี', 1, 159),
    (N'LandOffice', N'TH', N'TH', N'159', N'จังหวัดนครสวรรค์ สาขาตาคลี', 1, 159),
    (N'LandOffice', N'TH', N'EN', N'160', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', 1, 160),
    (N'LandOffice', N'TH', N'TH', N'160', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', 1, 160),
    (N'LandOffice', N'TH', N'EN', N'161', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', 1, 161),
    (N'LandOffice', N'TH', N'TH', N'161', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', 1, 161),
    (N'LandOffice', N'TH', N'EN', N'162', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', 1, 162),
    (N'LandOffice', N'TH', N'TH', N'162', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', 1, 162),
    (N'LandOffice', N'TH', N'EN', N'163', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', 1, 163),
    (N'LandOffice', N'TH', N'TH', N'163', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', 1, 163),
    (N'LandOffice', N'TH', N'EN', N'164', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', 1, 164),
    (N'LandOffice', N'TH', N'TH', N'164', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', 1, 164),
    (N'LandOffice', N'TH', N'EN', N'165', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', 1, 165),
    (N'LandOffice', N'TH', N'TH', N'165', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', 1, 165),
    (N'LandOffice', N'TH', N'EN', N'166', N'จังหวัดนครสวรรค์ สาขาลาดยาว', 1, 166),
    (N'LandOffice', N'TH', N'TH', N'166', N'จังหวัดนครสวรรค์ สาขาลาดยาว', 1, 166),
    (N'LandOffice', N'TH', N'EN', N'167', N'จังหวัดนนทบุรี', 1, 167),
    (N'LandOffice', N'TH', N'TH', N'167', N'จังหวัดนนทบุรี', 1, 167),
    (N'LandOffice', N'TH', N'EN', N'168', N'จังหวัดนนทบุรี สาขาบางบัวทอง', 1, 168),
    (N'LandOffice', N'TH', N'TH', N'168', N'จังหวัดนนทบุรี สาขาบางบัวทอง', 1, 168),
    (N'LandOffice', N'TH', N'EN', N'169', N'จังหวัดนนทบุรี สาขาบางใหญ่', 1, 169),
    (N'LandOffice', N'TH', N'TH', N'169', N'จังหวัดนนทบุรี สาขาบางใหญ่', 1, 169),
    (N'LandOffice', N'TH', N'EN', N'170', N'จังหวัดนนทบุรี สาขาปากเกร็ด', 1, 170),
    (N'LandOffice', N'TH', N'TH', N'170', N'จังหวัดนนทบุรี สาขาปากเกร็ด', 1, 170),
    (N'LandOffice', N'TH', N'EN', N'171', N'จังหวัดนราธิวาส', 1, 171),
    (N'LandOffice', N'TH', N'TH', N'171', N'จังหวัดนราธิวาส', 1, 171),
    (N'LandOffice', N'TH', N'EN', N'172', N'จังหวัดประจวบคีรีขันธ์', 1, 172),
    (N'LandOffice', N'TH', N'TH', N'172', N'จังหวัดประจวบคีรีขันธ์', 1, 172),
    (N'LandOffice', N'TH', N'EN', N'173', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', 1, 173),
    (N'LandOffice', N'TH', N'TH', N'173', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', 1, 173),
    (N'LandOffice', N'TH', N'EN', N'174', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', 1, 174),
    (N'LandOffice', N'TH', N'TH', N'174', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', 1, 174),
    (N'LandOffice', N'TH', N'EN', N'175', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', 1, 175),
    (N'LandOffice', N'TH', N'TH', N'175', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', 1, 175),
    (N'LandOffice', N'TH', N'EN', N'176', N'จังหวัดปทุมธานี', 1, 176),
    (N'LandOffice', N'TH', N'TH', N'176', N'จังหวัดปทุมธานี', 1, 176),
    (N'LandOffice', N'TH', N'EN', N'177', N'จังหวัดปทุมธานี สาขาธัญบุรี', 1, 177),
    (N'LandOffice', N'TH', N'TH', N'177', N'จังหวัดปทุมธานี สาขาธัญบุรี', 1, 177),
    (N'LandOffice', N'TH', N'EN', N'178', N'จังหวัดปทุมธานี สาขาคลองหลวง', 1, 178),
    (N'LandOffice', N'TH', N'TH', N'178', N'จังหวัดปทุมธานี สาขาคลองหลวง', 1, 178),
    (N'LandOffice', N'TH', N'EN', N'179', N'จังหวัดปทุมธานี สาขาลำลูกกา', 1, 179),
    (N'LandOffice', N'TH', N'TH', N'179', N'จังหวัดปทุมธานี สาขาลำลูกกา', 1, 179),
    (N'LandOffice', N'TH', N'EN', N'180', N'จังหวัดเพชรบุรี', 1, 180),
    (N'LandOffice', N'TH', N'TH', N'180', N'จังหวัดเพชรบุรี', 1, 180),
    (N'LandOffice', N'TH', N'EN', N'181', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', 1, 181),
    (N'LandOffice', N'TH', N'TH', N'181', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', 1, 181),
    (N'LandOffice', N'TH', N'EN', N'182', N'จังหวัดเพชรบุรี สาขาท่ายาง', 1, 182),
    (N'LandOffice', N'TH', N'TH', N'182', N'จังหวัดเพชรบุรี สาขาท่ายาง', 1, 182),
    (N'LandOffice', N'TH', N'EN', N'183', N'จังหวัดเพชรบุรี สาขาชะอำ', 1, 183),
    (N'LandOffice', N'TH', N'TH', N'183', N'จังหวัดเพชรบุรี สาขาชะอำ', 1, 183),
    (N'LandOffice', N'TH', N'EN', N'184', N'จังหวัดภูเก็ต', 1, 184),
    (N'LandOffice', N'TH', N'TH', N'184', N'จังหวัดภูเก็ต', 1, 184),
    (N'LandOffice', N'TH', N'EN', N'185', N'จังหวัดภูเก็ต ส่วนแยกถลาง', 1, 185),
    (N'LandOffice', N'TH', N'TH', N'185', N'จังหวัดภูเก็ต ส่วนแยกถลาง', 1, 185),
    (N'LandOffice', N'TH', N'EN', N'186', N'จังหวัดยโสธร', 1, 186),
    (N'LandOffice', N'TH', N'TH', N'186', N'จังหวัดยโสธร', 1, 186),
    (N'LandOffice', N'TH', N'EN', N'187', N'จังหวัดยโสธร สาขามหาชนะชัย', 1, 187),
    (N'LandOffice', N'TH', N'TH', N'187', N'จังหวัดยโสธร สาขามหาชนะชัย', 1, 187),
    (N'LandOffice', N'TH', N'EN', N'188', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', 1, 188),
    (N'LandOffice', N'TH', N'TH', N'188', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', 1, 188),
    (N'LandOffice', N'TH', N'EN', N'189', N'จังหวัดยโสธร สาขาเลิงนกทา', 1, 189),
    (N'LandOffice', N'TH', N'TH', N'189', N'จังหวัดยโสธร สาขาเลิงนกทา', 1, 189),
    (N'LandOffice', N'TH', N'EN', N'190', N'จังหวัดระยอง', 1, 190),
    (N'LandOffice', N'TH', N'TH', N'190', N'จังหวัดระยอง', 1, 190),
    (N'LandOffice', N'TH', N'EN', N'191', N'จังหวัดระยอง สาขาแกลง', 1, 191),
    (N'LandOffice', N'TH', N'TH', N'191', N'จังหวัดระยอง สาขาแกลง', 1, 191),
    (N'LandOffice', N'TH', N'EN', N'192', N'จังหวัดระยอง สาขาบ้านค่าย', 1, 192),
    (N'LandOffice', N'TH', N'TH', N'192', N'จังหวัดระยอง สาขาบ้านค่าย', 1, 192),
    (N'LandOffice', N'TH', N'EN', N'193', N'จังหวัดระยอง สาขาบ้านฉาง', 1, 193),
    (N'LandOffice', N'TH', N'TH', N'193', N'จังหวัดระยอง สาขาบ้านฉาง', 1, 193),
    (N'LandOffice', N'TH', N'EN', N'194', N'จังหวัดระยอง สาขาปลวกแดง', 1, 194),
    (N'LandOffice', N'TH', N'TH', N'194', N'จังหวัดระยอง สาขาปลวกแดง', 1, 194),
    (N'LandOffice', N'TH', N'EN', N'195', N'จังหวัดสมุทรปราการ', 1, 195),
    (N'LandOffice', N'TH', N'TH', N'195', N'จังหวัดสมุทรปราการ', 1, 195),
    (N'LandOffice', N'TH', N'EN', N'196', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 196),
    (N'LandOffice', N'TH', N'TH', N'196', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 196),
    (N'LandOffice', N'TH', N'EN', N'197', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 197),
    (N'LandOffice', N'TH', N'TH', N'197', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 197),
    (N'LandOffice', N'TH', N'EN', N'198', N'จังหวัดสมุทรสาคร', 1, 198),
    (N'LandOffice', N'TH', N'TH', N'198', N'จังหวัดสมุทรสาคร', 1, 198),
    (N'LandOffice', N'TH', N'EN', N'199', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', 1, 199),
    (N'LandOffice', N'TH', N'TH', N'199', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', 1, 199),
    (N'LandOffice', N'TH', N'EN', N'200', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', 1, 200),
    (N'LandOffice', N'TH', N'TH', N'200', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', 1, 200),
    (N'LandOffice', N'TH', N'EN', N'201', N'จังหวัดสมุทรสงคราม', 1, 201),
    (N'LandOffice', N'TH', N'TH', N'201', N'จังหวัดสมุทรสงคราม', 1, 201),
    (N'LandOffice', N'TH', N'EN', N'202', N'สระบุรี', 1, 202),
    (N'LandOffice', N'TH', N'TH', N'202', N'สระบุรี', 1, 202),
    (N'LandOffice', N'TH', N'EN', N'203', N'จังหวัดสระบุรี สาขาแก่งคอย', 1, 203),
    (N'LandOffice', N'TH', N'TH', N'203', N'จังหวัดสระบุรี สาขาแก่งคอย', 1, 203),
    (N'LandOffice', N'TH', N'EN', N'204', N'สระบุรี สาขาหนองแค', 1, 204),
    (N'LandOffice', N'TH', N'TH', N'204', N'สระบุรี สาขาหนองแค', 1, 204),
    (N'LandOffice', N'TH', N'EN', N'205', N'สระบุรี สาขาพระพุทธบาท', 1, 205),
    (N'LandOffice', N'TH', N'TH', N'205', N'สระบุรี สาขาพระพุทธบาท', 1, 205),
    (N'LandOffice', N'TH', N'EN', N'206', N'กรุงเทพมหานคร สำนักงานใหญ่', 1, 206),
    (N'LandOffice', N'TH', N'TH', N'206', N'กรุงเทพมหานคร สำนักงานใหญ่', 1, 206),
    (N'LandOffice', N'TH', N'EN', N'207', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', 1, 207),
    (N'LandOffice', N'TH', N'TH', N'207', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', 1, 207),
    (N'LandOffice', N'TH', N'EN', N'208', N'นนทบุรี  สาขาปากเกร็ด', 1, 208),
    (N'LandOffice', N'TH', N'TH', N'208', N'นนทบุรี  สาขาปากเกร็ด', 1, 208),
    (N'LandOffice', N'TH', N'EN', N'209', N'จังหวัดพระนครศรีอยุธยา', 1, 209),
    (N'LandOffice', N'TH', N'TH', N'209', N'จังหวัดพระนครศรีอยุธยา', 1, 209),
    (N'LandOffice', N'TH', N'EN', N'210', N'พระนครศรีอยุธยา สาขาเสนา', 1, 210),
    (N'LandOffice', N'TH', N'TH', N'210', N'พระนครศรีอยุธยา สาขาเสนา', 1, 210),
    (N'LandOffice', N'TH', N'EN', N'211', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', 1, 211),
    (N'LandOffice', N'TH', N'TH', N'211', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', 1, 211),
    (N'LandOffice', N'TH', N'EN', N'212', N'พระนครศรีอยุธยา สาขาท่าเรือ', 1, 212),
    (N'LandOffice', N'TH', N'TH', N'212', N'พระนครศรีอยุธยา สาขาท่าเรือ', 1, 212),
    (N'LandOffice', N'TH', N'EN', N'213', N'ลำพูน สาขาป่าซาง', 1, 213),
    (N'LandOffice', N'TH', N'TH', N'213', N'ลำพูน สาขาป่าซาง', 1, 213),
    (N'LandOffice', N'TH', N'EN', N'214', N'ปราจีนบุรี', 1, 214),
    (N'LandOffice', N'TH', N'TH', N'214', N'ปราจีนบุรี', 1, 214),
    (N'LandOffice', N'TH', N'EN', N'215', N'ลำปาง', 1, 215),
    (N'LandOffice', N'TH', N'TH', N'215', N'ลำปาง', 1, 215),
    (N'LandOffice', N'TH', N'EN', N'216', N'จังหวัดภูเก็ต', 1, 216),
    (N'LandOffice', N'TH', N'TH', N'216', N'จังหวัดภูเก็ต', 1, 216),
    (N'LandOffice', N'TH', N'EN', N'217', N'พิษณุโลก', 1, 217),
    (N'LandOffice', N'TH', N'TH', N'217', N'พิษณุโลก', 1, 217),
    (N'LandOffice', N'TH', N'EN', N'218', N'อุดรธานี', 1, 218),
    (N'LandOffice', N'TH', N'TH', N'218', N'อุดรธานี', 1, 218),
    (N'LandOffice', N'TH', N'EN', N'219', N'สงขลา สาขาหาดใหญ่', 1, 219),
    (N'LandOffice', N'TH', N'TH', N'219', N'สงขลา สาขาหาดใหญ่', 1, 219),
    (N'LandOffice', N'TH', N'EN', N'220', N'สุพรรณบุรี', 1, 220),
    (N'LandOffice', N'TH', N'TH', N'220', N'สุพรรณบุรี', 1, 220),
    (N'LandOffice', N'TH', N'EN', N'221', N'สุราษฎร์ธานี', 1, 221),
    (N'LandOffice', N'TH', N'TH', N'221', N'สุราษฎร์ธานี', 1, 221),
    (N'LandOffice', N'TH', N'EN', N'222', N'อุบลราชธานี', 1, 222),
    (N'LandOffice', N'TH', N'TH', N'222', N'อุบลราชธานี', 1, 222),
    (N'LandOffice', N'TH', N'EN', N'223', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', 1, 223),
    (N'LandOffice', N'TH', N'TH', N'223', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', 1, 223),
    (N'LandOffice', N'TH', N'EN', N'224', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', 1, 224),
    (N'LandOffice', N'TH', N'TH', N'224', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', 1, 224),
    (N'LandOffice', N'TH', N'EN', N'225', N'อุดรธานี', 1, 225),
    (N'LandOffice', N'TH', N'TH', N'225', N'อุดรธานี', 1, 225),
    (N'LandOffice', N'TH', N'EN', N'226', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', 1, 226),
    (N'LandOffice', N'TH', N'TH', N'226', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', 1, 226),
    (N'LandOffice', N'TH', N'EN', N'227', N'จังหวัดราชบุรี', 1, 227),
    (N'LandOffice', N'TH', N'TH', N'227', N'จังหวัดราชบุรี', 1, 227),
    (N'LandOffice', N'TH', N'EN', N'228', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', 1, 228),
    (N'LandOffice', N'TH', N'TH', N'228', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', 1, 228),
    (N'LandOffice', N'TH', N'EN', N'229', N'กรุงเทพมหานคร', 1, 229),
    (N'LandOffice', N'TH', N'TH', N'229', N'กรุงเทพมหานคร', 1, 229),
    (N'LandOffice', N'TH', N'EN', N'230', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 230),
    (N'LandOffice', N'TH', N'TH', N'230', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 230),
    (N'LandOffice', N'TH', N'EN', N'231', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', 1, 231),
    (N'LandOffice', N'TH', N'TH', N'231', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', 1, 231),
    (N'LandOffice', N'TH', N'EN', N'232', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 232),
    (N'LandOffice', N'TH', N'TH', N'232', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 232),
    (N'LandOffice', N'TH', N'EN', N'233', N'พังงา สาขาตะกั่วทุ่ง', 1, 233),
    (N'LandOffice', N'TH', N'TH', N'233', N'พังงา สาขาตะกั่วทุ่ง', 1, 233),
    (N'LandOffice', N'TH', N'EN', N'234', N'จังหวัดสมุทรปราการ', 1, 234),
    (N'LandOffice', N'TH', N'TH', N'234', N'จังหวัดสมุทรปราการ', 1, 234),
    (N'LandOffice', N'TH', N'EN', N'235', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', 1, 235),
    (N'LandOffice', N'TH', N'TH', N'235', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', 1, 235),
    (N'LandOffice', N'TH', N'EN', N'236', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', 1, 236),
    (N'LandOffice', N'TH', N'TH', N'236', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', 1, 236),
    (N'LandOffice', N'TH', N'EN', N'237', N'จังหวัดมหาสารคาม', 1, 237),
    (N'LandOffice', N'TH', N'TH', N'237', N'จังหวัดมหาสารคาม', 1, 237),
    (N'LandOffice', N'TH', N'EN', N'238', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', 1, 238),
    (N'LandOffice', N'TH', N'TH', N'238', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', 1, 238),
    (N'LandOffice', N'TH', N'EN', N'239', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 239),
    (N'LandOffice', N'TH', N'TH', N'239', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 239),
    (N'LandOffice', N'TH', N'EN', N'240', N'จังหวัดร้อยเอ็ด', 1, 240),
    (N'LandOffice', N'TH', N'TH', N'240', N'จังหวัดร้อยเอ็ด', 1, 240),
    (N'LandOffice', N'TH', N'EN', N'241', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', 1, 241),
    (N'LandOffice', N'TH', N'TH', N'241', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', 1, 241),
    (N'LandOffice', N'TH', N'EN', N'242', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', 1, 242),
    (N'LandOffice', N'TH', N'TH', N'242', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', 1, 242),
    (N'LandOffice', N'TH', N'EN', N'243', N'จังหวัดลำพูน', 1, 243),
    (N'LandOffice', N'TH', N'TH', N'243', N'จังหวัดลำพูน', 1, 243),
    (N'LandOffice', N'TH', N'EN', N'244', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', 1, 244),
    (N'LandOffice', N'TH', N'TH', N'244', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', 1, 244),
    (N'LandOffice', N'TH', N'EN', N'245', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', 1, 245),
    (N'LandOffice', N'TH', N'TH', N'245', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', 1, 245),
    (N'LandOffice', N'TH', N'EN', N'246', N'จังหวัดพัทลุง', 1, 246),
    (N'LandOffice', N'TH', N'TH', N'246', N'จังหวัดพัทลุง', 1, 246),
    (N'LandOffice', N'TH', N'EN', N'247', N'จังหวัดลำปาง สาขาเกาะคา', 1, 247),
    (N'LandOffice', N'TH', N'TH', N'247', N'จังหวัดลำปาง สาขาเกาะคา', 1, 247),
    (N'LandOffice', N'TH', N'EN', N'248', N'จังหวัดพังงา', 1, 248),
    (N'LandOffice', N'TH', N'TH', N'248', N'จังหวัดพังงา', 1, 248),
    (N'LandOffice', N'TH', N'EN', N'249', N'จังหวัดปทุมธานี สาขาสามโคก', 1, 249),
    (N'LandOffice', N'TH', N'TH', N'249', N'จังหวัดปทุมธานี สาขาสามโคก', 1, 249),
    (N'LandOffice', N'TH', N'EN', N'250', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', 1, 250),
    (N'LandOffice', N'TH', N'TH', N'250', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', 1, 250),
    (N'LandOffice', N'TH', N'EN', N'251', N'อุดรธานี สาขาหนองวัวซอ', 1, 251),
    (N'LandOffice', N'TH', N'TH', N'251', N'อุดรธานี สาขาหนองวัวซอ', 1, 251),
    (N'LandOffice', N'TH', N'EN', N'252', N'จังหวัดเพชรบูรณ์', 1, 252),
    (N'LandOffice', N'TH', N'TH', N'252', N'จังหวัดเพชรบูรณ์', 1, 252),
    (N'LandOffice', N'TH', N'EN', N'253', N'จังหวัดสงขลา สาขาหาดใหญ่', 1, 253),
    (N'LandOffice', N'TH', N'TH', N'253', N'จังหวัดสงขลา สาขาหาดใหญ่', 1, 253),
    (N'LandOffice', N'TH', N'EN', N'254', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', 1, 254),
    (N'LandOffice', N'TH', N'TH', N'254', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', 1, 254),
    (N'LandOffice', N'TH', N'EN', N'255', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', 1, 255),
    (N'LandOffice', N'TH', N'TH', N'255', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', 1, 255),
    (N'LandOffice', N'TH', N'EN', N'256', N'ปราจีนบุรี สาขากบินทร์บุรี', 1, 256),
    (N'LandOffice', N'TH', N'TH', N'256', N'ปราจีนบุรี สาขากบินทร์บุรี', 1, 256),
    (N'LandOffice', N'TH', N'EN', N'257', N'จังหวัดสกลนคร สาขาบ้านม่วง', 1, 257),
    (N'LandOffice', N'TH', N'TH', N'257', N'จังหวัดสกลนคร สาขาบ้านม่วง', 1, 257),
    (N'LandOffice', N'TH', N'EN', N'258', N'จังหวัดสุรินทร์', 1, 258),
    (N'LandOffice', N'TH', N'TH', N'258', N'จังหวัดสุรินทร์', 1, 258),
    (N'LandOffice', N'TH', N'EN', N'259', N'พระนครศรีอยุธยา สาขาวังน้อย', 1, 259),
    (N'LandOffice', N'TH', N'TH', N'259', N'พระนครศรีอยุธยา สาขาวังน้อย', 1, 259),
    (N'LandOffice', N'TH', N'EN', N'260', N'จังหวัดหนองคาย สาขาโพนพิสัย', 1, 260),
    (N'LandOffice', N'TH', N'TH', N'260', N'จังหวัดหนองคาย สาขาโพนพิสัย', 1, 260),
    (N'LandOffice', N'TH', N'EN', N'261', N'สาขาโพนพิสัย', 1, 261),
    (N'LandOffice', N'TH', N'TH', N'261', N'สาขาโพนพิสัย', 1, 261),
    (N'LandOffice', N'TH', N'EN', N'262', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', 1, 262),
    (N'LandOffice', N'TH', N'TH', N'262', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', 1, 262),
    (N'LandOffice', N'TH', N'EN', N'263', N'จังหวัดบึงกาฬ', 1, 263),
    (N'LandOffice', N'TH', N'TH', N'263', N'จังหวัดบึงกาฬ', 1, 263),
    (N'LandOffice', N'TH', N'EN', N'264', N'จังหวัดน่าน', 1, 264),
    (N'LandOffice', N'TH', N'TH', N'264', N'จังหวัดน่าน', 1, 264),
    (N'LandOffice', N'TH', N'EN', N'265', N'จังหวักอุดรธานี', 1, 265),
    (N'LandOffice', N'TH', N'TH', N'265', N'จังหวักอุดรธานี', 1, 265),
    (N'LandOffice', N'TH', N'EN', N'266', N'จังหวัดอุดรธานี', 1, 266),
    (N'LandOffice', N'TH', N'TH', N'266', N'จังหวัดอุดรธานี', 1, 266),
    (N'LandOffice', N'TH', N'EN', N'267', N'จังหวัดชลบุรี สาขาบ้านบึง', 1, 267),
    (N'LandOffice', N'TH', N'TH', N'267', N'จังหวัดชลบุรี สาขาบ้านบึง', 1, 267),
    (N'LandOffice', N'TH', N'EN', N'268', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', 1, 268),
    (N'LandOffice', N'TH', N'TH', N'268', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', 1, 268),
    (N'LandOffice', N'TH', N'EN', N'269', N'จังหวัดพะเยา สาขาปง', 1, 269),
    (N'LandOffice', N'TH', N'TH', N'269', N'จังหวัดพะเยา สาขาปง', 1, 269),
    (N'LandOffice', N'TH', N'EN', N'270', N'จังหวัดน่าน สาขาปัว', 1, 270),
    (N'LandOffice', N'TH', N'TH', N'270', N'จังหวัดน่าน สาขาปัว', 1, 270),
    (N'LandOffice', N'TH', N'EN', N'271', N'จังหวัดพะเยา สาขาจุน', 1, 271),
    (N'LandOffice', N'TH', N'TH', N'271', N'จังหวัดพะเยา สาขาจุน', 1, 271),
    (N'LandOffice', N'TH', N'EN', N'272', N'จังหวัดสงขลา สาขาสะเดา', 1, 272),
    (N'LandOffice', N'TH', N'TH', N'272', N'จังหวัดสงขลา สาขาสะเดา', 1, 272),
    (N'LandOffice', N'TH', N'EN', N'273', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', 1, 273),
    (N'LandOffice', N'TH', N'TH', N'273', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', 1, 273),
    (N'LandOffice', N'TH', N'EN', N'274', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', 1, 274),
    (N'LandOffice', N'TH', N'TH', N'274', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', 1, 274),
    (N'LandOffice', N'TH', N'EN', N'275', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', 1, 275),
    (N'LandOffice', N'TH', N'TH', N'275', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', 1, 275),
    (N'LandOffice', N'TH', N'EN', N'276', N'ตราด สาขาแหลมงอบ', 1, 276),
    (N'LandOffice', N'TH', N'TH', N'276', N'ตราด สาขาแหลมงอบ', 1, 276),
    (N'LandOffice', N'TH', N'EN', N'277', N'จังหวัดอุดรธานี สาขาหนองหาน', 1, 277),
    (N'LandOffice', N'TH', N'TH', N'277', N'จังหวัดอุดรธานี สาขาหนองหาน', 1, 277),
    (N'LandOffice', N'TH', N'EN', N'278', N'จังหวัดอุดรธานี สาขากุมภวาปี', 1, 278),
    (N'LandOffice', N'TH', N'TH', N'278', N'จังหวัดอุดรธานี สาขากุมภวาปี', 1, 278),
    (N'LandOffice', N'TH', N'EN', N'279', N'จังหวัดอ่างทอง', 1, 279),
    (N'LandOffice', N'TH', N'TH', N'279', N'จังหวัดอ่างทอง', 1, 279),
    (N'LandOffice', N'TH', N'EN', N'280', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', 1, 280),
    (N'LandOffice', N'TH', N'TH', N'280', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', 1, 280),
    (N'LandOffice', N'TH', N'EN', N'281', N'จังหวัดเลย สาขาด่านซ้าย', 1, 281),
    (N'LandOffice', N'TH', N'TH', N'281', N'จังหวัดเลย สาขาด่านซ้าย', 1, 281),
    (N'LandOffice', N'TH', N'EN', N'282', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 282),
    (N'LandOffice', N'TH', N'TH', N'282', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 282),
    (N'LandOffice', N'TH', N'EN', N'283', N'จังหวัดบึงกาฬ สาขาเซกา', 1, 283),
    (N'LandOffice', N'TH', N'TH', N'283', N'จังหวัดบึงกาฬ สาขาเซกา', 1, 283),
    (N'LandOffice', N'TH', N'EN', N'284', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', 1, 284),
    (N'LandOffice', N'TH', N'TH', N'284', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', 1, 284),
    (N'LandOffice', N'TH', N'EN', N'285', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', 1, 285),
    (N'LandOffice', N'TH', N'TH', N'285', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', 1, 285),
    (N'LandOffice', N'TH', N'EN', N'286', N'จังหวักพิษณุโลก', 1, 286),
    (N'LandOffice', N'TH', N'TH', N'286', N'จังหวักพิษณุโลก', 1, 286),
    (N'LandOffice', N'TH', N'EN', N'287', N'จังหวัดพิษณุโลก', 1, 287),
    (N'LandOffice', N'TH', N'TH', N'287', N'จังหวัดพิษณุโลก', 1, 287),
    (N'LandOffice', N'TH', N'EN', N'288', N'จังหวัดภูเก็ต สาขาถลาง', 1, 288),
    (N'LandOffice', N'TH', N'TH', N'288', N'จังหวัดภูเก็ต สาขาถลาง', 1, 288),
    (N'LandOffice', N'TH', N'EN', N'289', N'จังหวัดพังงา ส่วนแยกโคกกลอย', 1, 289),
    (N'LandOffice', N'TH', N'TH', N'289', N'จังหวัดพังงา ส่วนแยกโคกกลอย', 1, 289),
    (N'LandOffice', N'TH', N'EN', N'290', N'จังหวัดหนองบัวลำภู', 1, 290),
    (N'LandOffice', N'TH', N'TH', N'290', N'จังหวัดหนองบัวลำภู', 1, 290),
    (N'LandOffice', N'TH', N'EN', N'291', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 291),
    (N'LandOffice', N'TH', N'TH', N'291', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 291),
    (N'LandOffice', N'TH', N'EN', N'292', N'จังหวัดบุรีรัมย์', 1, 292),
    (N'LandOffice', N'TH', N'TH', N'292', N'จังหวัดบุรีรัมย์', 1, 292),
    (N'LandOffice', N'TH', N'EN', N'293', N'จังหวัดอุดรธานี สาขาบ้านดุง', 1, 293),
    (N'LandOffice', N'TH', N'TH', N'293', N'จังหวัดอุดรธานี สาขาบ้านดุง', 1, 293),
    (N'LandOffice', N'TH', N'EN', N'294', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', 1, 294),
    (N'LandOffice', N'TH', N'TH', N'294', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', 1, 294),
    (N'LandOffice', N'TH', N'EN', N'295', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', 1, 295),
    (N'LandOffice', N'TH', N'TH', N'295', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', 1, 295),
    (N'LandOffice', N'TH', N'EN', N'296', N'จังหวัดสุรินทร์ สาขาจอมพระ', 1, 296),
    (N'LandOffice', N'TH', N'TH', N'296', N'จังหวัดสุรินทร์ สาขาจอมพระ', 1, 296),
    (N'LandOffice', N'TH', N'EN', N'297', N'จังหวัดนครพนม', 1, 297),
    (N'LandOffice', N'TH', N'TH', N'297', N'จังหวัดนครพนม', 1, 297),
    (N'LandOffice', N'TH', N'EN', N'298', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', 1, 298),
    (N'LandOffice', N'TH', N'TH', N'298', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', 1, 298),
    (N'LandOffice', N'TH', N'EN', N'299', N'สาขาหล่มสัก', 1, 299),
    (N'LandOffice', N'TH', N'TH', N'299', N'สาขาหล่มสัก', 1, 299),
    (N'LandOffice', N'TH', N'EN', N'300', N'จังหวัดพิจิตร', 1, 300),
    (N'LandOffice', N'TH', N'TH', N'300', N'จังหวัดพิจิตร', 1, 300),
    (N'LandOffice', N'TH', N'EN', N'301', N'อุดรธานี สาขาเพ็ญ', 1, 301),
    (N'LandOffice', N'TH', N'TH', N'301', N'อุดรธานี สาขาเพ็ญ', 1, 301),
    (N'LandOffice', N'TH', N'EN', N'302', N'จังหวัดอุดรธานี สาขาบ้านผือ', 1, 302),
    (N'LandOffice', N'TH', N'TH', N'302', N'จังหวัดอุดรธานี สาขาบ้านผือ', 1, 302),
    (N'LandOffice', N'TH', N'EN', N'303', N'สาขาบ้านผือ', 1, 303),
    (N'LandOffice', N'TH', N'TH', N'303', N'สาขาบ้านผือ', 1, 303),
    (N'LandOffice', N'TH', N'EN', N'304', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 304),
    (N'LandOffice', N'TH', N'TH', N'304', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 304),
    (N'LandOffice', N'TH', N'EN', N'305', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 305),
    (N'LandOffice', N'TH', N'TH', N'305', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 305),
    (N'LandOffice', N'TH', N'EN', N'306', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', 1, 306),
    (N'LandOffice', N'TH', N'TH', N'306', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', 1, 306),
    (N'LandOffice', N'TH', N'EN', N'307', N'ที่ว่าการอำเภอกะทู้', 1, 307),
    (N'LandOffice', N'TH', N'TH', N'307', N'ที่ว่าการอำเภอกะทู้', 1, 307),
    (N'LandOffice', N'TH', N'EN', N'308', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', 1, 308),
    (N'LandOffice', N'TH', N'TH', N'308', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', 1, 308),
    (N'LandOffice', N'TH', N'EN', N'309', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', 1, 309),
    (N'LandOffice', N'TH', N'TH', N'309', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', 1, 309),
    (N'LandOffice', N'TH', N'EN', N'310', N'จังหวัดแพร่ สาขาสูงเม่น', 1, 310),
    (N'LandOffice', N'TH', N'TH', N'310', N'จังหวัดแพร่ สาขาสูงเม่น', 1, 310),
    (N'LandOffice', N'TH', N'EN', N'311', N'จังหวัดสระบุรี สาขา หนองแค', 1, 311),
    (N'LandOffice', N'TH', N'TH', N'311', N'จังหวัดสระบุรี สาขา หนองแค', 1, 311),
    (N'LandOffice', N'TH', N'EN', N'312', N'จังหวัดบุรีรัมย์ สาขานางรอง', 1, 312),
    (N'LandOffice', N'TH', N'TH', N'312', N'จังหวัดบุรีรัมย์ สาขานางรอง', 1, 312),
    (N'LandOffice', N'TH', N'EN', N'313', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', 1, 313),
    (N'LandOffice', N'TH', N'TH', N'313', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', 1, 313),
    (N'LandOffice', N'TH', N'EN', N'314', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', 1, 314),
    (N'LandOffice', N'TH', N'TH', N'314', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', 1, 314),
    (N'LandOffice', N'TH', N'EN', N'315', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', 1, 315),
    (N'LandOffice', N'TH', N'TH', N'315', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', 1, 315),
    (N'LandOffice', N'TH', N'EN', N'316', N'จังหวัดมุกดาหาร', 1, 316),
    (N'LandOffice', N'TH', N'TH', N'316', N'จังหวัดมุกดาหาร', 1, 316),
    (N'LandOffice', N'TH', N'EN', N'317', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', 1, 317),
    (N'LandOffice', N'TH', N'TH', N'317', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', 1, 317),
    (N'LandOffice', N'TH', N'EN', N'318', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', 1, 318),
    (N'LandOffice', N'TH', N'TH', N'318', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', 1, 318),
    (N'LandOffice', N'TH', N'EN', N'319', N'จังหวัดมหาสารคาม สาขาบรบือ', 1, 319),
    (N'LandOffice', N'TH', N'TH', N'319', N'จังหวัดมหาสารคาม สาขาบรบือ', 1, 319),
    (N'LandOffice', N'TH', N'EN', N'320', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', 1, 320),
    (N'LandOffice', N'TH', N'TH', N'320', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', 1, 320),
    (N'LandOffice', N'TH', N'EN', N'321', N'จังหวัดสกลนคร', 1, 321),
    (N'LandOffice', N'TH', N'TH', N'321', N'จังหวัดสกลนคร', 1, 321),
    (N'LandOffice', N'TH', N'EN', N'322', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', 1, 322),
    (N'LandOffice', N'TH', N'TH', N'322', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', 1, 322),
    (N'LandOffice', N'TH', N'EN', N'323', N'จังหวัดศรีสะเกษ', 1, 323),
    (N'LandOffice', N'TH', N'TH', N'323', N'จังหวัดศรีสะเกษ', 1, 323),
    (N'LandOffice', N'TH', N'EN', N'324', N'จังหวัดศรีสะเกษ', 1, 324),
    (N'LandOffice', N'TH', N'TH', N'324', N'จังหวัดศรีสะเกษ', 1, 324),
    (N'LandOffice', N'TH', N'EN', N'325', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', 1, 325),
    (N'LandOffice', N'TH', N'TH', N'325', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', 1, 325),
    (N'LandOffice', N'TH', N'EN', N'326', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', 1, 326),
    (N'LandOffice', N'TH', N'TH', N'326', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', 1, 326),
    (N'LandOffice', N'TH', N'EN', N'327', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', 1, 327),
    (N'LandOffice', N'TH', N'TH', N'327', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', 1, 327),
    (N'LandOffice', N'TH', N'EN', N'328', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', 1, 328),
    (N'LandOffice', N'TH', N'TH', N'328', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', 1, 328),
    (N'LandOffice', N'TH', N'EN', N'329', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', 1, 329),
    (N'LandOffice', N'TH', N'TH', N'329', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', 1, 329),
    (N'LandOffice', N'TH', N'EN', N'330', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', 1, 330),
    (N'LandOffice', N'TH', N'TH', N'330', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', 1, 330),
    (N'LandOffice', N'TH', N'EN', N'331', N'จังหวัดปราจีนบุรี', 1, 331),
    (N'LandOffice', N'TH', N'TH', N'331', N'จังหวัดปราจีนบุรี', 1, 331),
    (N'LandOffice', N'TH', N'EN', N'332', N'จังหวัดเลย', 1, 332),
    (N'LandOffice', N'TH', N'TH', N'332', N'จังหวัดเลย', 1, 332),
    (N'LandOffice', N'TH', N'EN', N'333', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 333),
    (N'LandOffice', N'TH', N'TH', N'333', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 333),
    (N'LandOffice', N'TH', N'EN', N'334', N'จังหวัดเพชรบุรี สาขาเขาย้อย', 1, 334),
    (N'LandOffice', N'TH', N'TH', N'334', N'จังหวัดเพชรบุรี สาขาเขาย้อย', 1, 334),
    (N'LandOffice', N'TH', N'EN', N'335', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 335),
    (N'LandOffice', N'TH', N'TH', N'335', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 335),
    (N'LandOffice', N'TH', N'EN', N'336', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 336),
    (N'LandOffice', N'TH', N'TH', N'336', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 336),
    (N'LandOffice', N'TH', N'EN', N'337', N'จังหวัดอุบลราชธานี', 1, 337),
    (N'LandOffice', N'TH', N'TH', N'337', N'จังหวัดอุบลราชธานี', 1, 337),
    (N'LandOffice', N'TH', N'EN', N'338', N'จังหวัดเลย สาขาวังสะพุง', 1, 338),
    (N'LandOffice', N'TH', N'TH', N'338', N'จังหวัดเลย สาขาวังสะพุง', 1, 338),
    (N'LandOffice', N'TH', N'EN', N'339', N'จังหวัดสิงห์บุรี', 1, 339),
    (N'LandOffice', N'TH', N'TH', N'339', N'จังหวัดสิงห์บุรี', 1, 339),
    (N'LandOffice', N'TH', N'EN', N'340', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', 1, 340),
    (N'LandOffice', N'TH', N'TH', N'340', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', 1, 340),
    (N'LandOffice', N'TH', N'EN', N'341', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', 1, 341),
    (N'LandOffice', N'TH', N'TH', N'341', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', 1, 341),
    (N'LandOffice', N'TH', N'EN', N'342', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 342),
    (N'LandOffice', N'TH', N'TH', N'342', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 342),
    (N'LandOffice', N'TH', N'EN', N'343', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 343),
    (N'LandOffice', N'TH', N'TH', N'343', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 343),
    (N'LandOffice', N'TH', N'EN', N'344', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 344),
    (N'LandOffice', N'TH', N'TH', N'344', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 344),
    (N'LandOffice', N'TH', N'EN', N'345', N'จังหวัดพะเยา', 1, 345),
    (N'LandOffice', N'TH', N'TH', N'345', N'จังหวัดพะเยา', 1, 345),
    (N'LandOffice', N'TH', N'EN', N'346', N'จังหวัดน่าน สาขาท่าวังผา', 1, 346),
    (N'LandOffice', N'TH', N'TH', N'346', N'จังหวัดน่าน สาขาท่าวังผา', 1, 346),
    (N'LandOffice', N'TH', N'EN', N'347', N'จังหวัดปัตตานี สาขาสายบุรี', 1, 347),
    (N'LandOffice', N'TH', N'TH', N'347', N'จังหวัดปัตตานี สาขาสายบุรี', 1, 347),
    (N'LandOffice', N'TH', N'EN', N'348', N'จังหวัดพิจิตร สาขาโพทะเล', 1, 348),
    (N'LandOffice', N'TH', N'TH', N'348', N'จังหวัดพิจิตร สาขาโพทะเล', 1, 348),
    (N'LandOffice', N'TH', N'EN', N'349', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', 1, 349),
    (N'LandOffice', N'TH', N'TH', N'349', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', 1, 349),
    (N'LandOffice', N'TH', N'EN', N'350', N'จังหวัดอำนาจเจริญ', 1, 350),
    (N'LandOffice', N'TH', N'TH', N'350', N'จังหวัดอำนาจเจริญ', 1, 350),
    (N'LandOffice', N'TH', N'EN', N'351', N'จังหวัดขอนแก่น สาขาบ้านฝาง', 1, 351),
    (N'LandOffice', N'TH', N'TH', N'351', N'จังหวัดขอนแก่น สาขาบ้านฝาง', 1, 351),
    (N'LandOffice', N'TH', N'EN', N'352', N'จังหวัดพิษณุโลก สาขาบางระกำ', 1, 352),
    (N'LandOffice', N'TH', N'TH', N'352', N'จังหวัดพิษณุโลก สาขาบางระกำ', 1, 352),
    (N'LandOffice', N'TH', N'EN', N'353', N'จังหวัดอุตรดิตถ์', 1, 353),
    (N'LandOffice', N'TH', N'TH', N'353', N'จังหวัดอุตรดิตถ์', 1, 353),
    (N'LandOffice', N'TH', N'EN', N'354', N'จังหวัดหนองคาย', 1, 354),
    (N'LandOffice', N'TH', N'TH', N'354', N'จังหวัดหนองคาย', 1, 354),
    (N'LandOffice', N'TH', N'EN', N'355', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', 1, 355),
    (N'LandOffice', N'TH', N'TH', N'355', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', 1, 355),
    (N'LandOffice', N'TH', N'EN', N'356', N'จังหวัดสงขลา สาขาบางกล่ำ', 1, 356),
    (N'LandOffice', N'TH', N'TH', N'356', N'จังหวัดสงขลา สาขาบางกล่ำ', 1, 356),
    (N'LandOffice', N'TH', N'EN', N'357', N'สาขาแก่งคอย', 1, 357),
    (N'LandOffice', N'TH', N'TH', N'357', N'สาขาแก่งคอย', 1, 357),
    (N'LandOffice', N'TH', N'EN', N'358', N'จังหวัดสระบุรี', 1, 358),
    (N'LandOffice', N'TH', N'TH', N'358', N'จังหวัดสระบุรี', 1, 358),
    (N'LandOffice', N'TH', N'EN', N'359', N'จังหวัดพังงา สาขาท้ายเหมือง', 1, 359),
    (N'LandOffice', N'TH', N'TH', N'359', N'จังหวัดพังงา สาขาท้ายเหมือง', 1, 359),
    (N'LandOffice', N'TH', N'EN', N'360', N'สุราษฎร์ธานี สาขาพุนพิน', 1, 360),
    (N'LandOffice', N'TH', N'TH', N'360', N'สุราษฎร์ธานี สาขาพุนพิน', 1, 360),
    (N'LandOffice', N'TH', N'EN', N'361', N'จังหวัดอุดรธานี สาขาศรีธาตุ', 1, 361),
    (N'LandOffice', N'TH', N'TH', N'361', N'จังหวัดอุดรธานี สาขาศรีธาตุ', 1, 361),
    (N'LandOffice', N'TH', N'EN', N'362', N'จังหวัดสุราษฎร์ธานี', 1, 362),
    (N'LandOffice', N'TH', N'TH', N'362', N'จังหวัดสุราษฎร์ธานี', 1, 362),
    (N'LandOffice', N'TH', N'EN', N'363', N'สาขาพรรณานิคม', 1, 363),
    (N'LandOffice', N'TH', N'TH', N'363', N'สาขาพรรณานิคม', 1, 363),
    (N'LandOffice', N'TH', N'EN', N'364', N'จังหวัดสกลนคร สาขาพรรณานิคม', 1, 364),
    (N'LandOffice', N'TH', N'TH', N'364', N'จังหวัดสกลนคร สาขาพรรณานิคม', 1, 364),
    (N'LandOffice', N'TH', N'EN', N'365', N'จังหวัดสุรินทร์ สาขาปราสาท', 1, 365),
    (N'LandOffice', N'TH', N'TH', N'365', N'จังหวัดสุรินทร์ สาขาปราสาท', 1, 365),
    (N'LandOffice', N'TH', N'EN', N'366', N'จังหวัดสุรินทร์ สาขาสังขะ', 1, 366),
    (N'LandOffice', N'TH', N'TH', N'366', N'จังหวัดสุรินทร์ สาขาสังขะ', 1, 366),
    (N'LandOffice', N'TH', N'EN', N'367', N'ศรีสะเกษ สาขาขุนหาญ', 1, 367),
    (N'LandOffice', N'TH', N'TH', N'367', N'ศรีสะเกษ สาขาขุนหาญ', 1, 367),
    (N'LandOffice', N'TH', N'EN', N'368', N'ศรีสะเกษ สาขาขุนหาญ', 1, 368),
    (N'LandOffice', N'TH', N'TH', N'368', N'ศรีสะเกษ สาขาขุนหาญ', 1, 368),
    (N'LandOffice', N'TH', N'EN', N'369', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', 1, 369),
    (N'LandOffice', N'TH', N'TH', N'369', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', 1, 369),
    (N'LandOffice', N'TH', N'EN', N'370', N'จังหวัดสกลนคร สาขาวานรนิวาส', 1, 370),
    (N'LandOffice', N'TH', N'TH', N'370', N'จังหวัดสกลนคร สาขาวานรนิวาส', 1, 370),
    (N'LandOffice', N'TH', N'EN', N'371', N'จังหวัดราชบุรี สาขาบ้านโป่ง', 1, 371),
    (N'LandOffice', N'TH', N'TH', N'371', N'จังหวัดราชบุรี สาขาบ้านโป่ง', 1, 371),
    (N'LandOffice', N'TH', N'EN', N'372', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', 1, 372),
    (N'LandOffice', N'TH', N'TH', N'372', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', 1, 372),
    (N'LandOffice', N'TH', N'EN', N'373', N'นครปฐม (นครชัยศรี)', 1, 373),
    (N'LandOffice', N'TH', N'TH', N'373', N'นครปฐม (นครชัยศรี)', 1, 373),
    (N'LandOffice', N'TH', N'EN', N'374', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', 1, 374),
    (N'LandOffice', N'TH', N'TH', N'374', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', 1, 374),
    (N'LandOffice', N'TH', N'EN', N'375', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', 1, 375),
    (N'LandOffice', N'TH', N'TH', N'375', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', 1, 375),
    (N'LandOffice', N'TH', N'EN', N'376', N'จังหวัดสกลนคร สาขาอากาศอำนวย', 1, 376),
    (N'LandOffice', N'TH', N'TH', N'376', N'จังหวัดสกลนคร สาขาอากาศอำนวย', 1, 376),
    (N'LandOffice', N'TH', N'EN', N'377', N'จังหวัดลำปาง', 1, 377),
    (N'LandOffice', N'TH', N'TH', N'377', N'จังหวัดลำปาง', 1, 377),
    (N'LandOffice', N'TH', N'EN', N'378', N'จังหวัดกระบี่ สาขาคลองท่อม', 1, 378),
    (N'LandOffice', N'TH', N'TH', N'378', N'จังหวัดกระบี่ สาขาคลองท่อม', 1, 378),
    (N'LandOffice', N'TH', N'EN', N'379', N'จังหวัดเชียงราย สาขาแม่สรวย', 1, 379),
    (N'LandOffice', N'TH', N'TH', N'379', N'จังหวัดเชียงราย สาขาแม่สรวย', 1, 379),
    (N'LandOffice', N'TH', N'EN', N'380', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', 1, 380),
    (N'LandOffice', N'TH', N'TH', N'380', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', 1, 380),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาบางเขน', N'กรุงเทพมหานคร สาขาบางเขน', 1, 382),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาบางเขน', N'กรุงเทพมหานคร สาขาบางเขน', 1, 382),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาพระโขนง', N'กรุงเทพมหานคร สาขาพระโขนง', 1, 383),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาพระโขนง', N'กรุงเทพมหานคร สาขาพระโขนง', 1, 383),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาบางขุนเทียน', N'กรุงเทพมหานคร สาขาบางขุนเทียน', 1, 384),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาบางขุนเทียน', N'กรุงเทพมหานคร สาขาบางขุนเทียน', 1, 384),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาบางกอกน้อย', N'กรุงเทพมหานคร สาขาบางกอกน้อย', 1, 385),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาบางกอกน้อย', N'กรุงเทพมหานคร สาขาบางกอกน้อย', 1, 385),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาบางกะปิ', N'กรุงเทพมหานคร สาขาบางกะปิ', 1, 386),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาบางกะปิ', N'กรุงเทพมหานคร สาขาบางกะปิ', 1, 386),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาหนองแขม', N'กรุงเทพมหานคร สาขาหนองแขม', 1, 387),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาหนองแขม', N'กรุงเทพมหานคร สาขาหนองแขม', 1, 387),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขามีนบุรี', N'กรุงเทพมหานคร สาขามีนบุรี', 1, 388),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขามีนบุรี', N'กรุงเทพมหานคร สาขามีนบุรี', 1, 388),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาห้วยขวาง', N'กรุงเทพมหานคร สาขาห้วยขวาง', 1, 389),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาห้วยขวาง', N'กรุงเทพมหานคร สาขาห้วยขวาง', 1, 389),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาจตุจักร', N'กรุงเทพมหานคร สาขาจตุจักร', 1, 390),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาจตุจักร', N'กรุงเทพมหานคร สาขาจตุจักร', 1, 390),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาธนบุรี', N'กรุงเทพมหานคร สาขาธนบุรี', 1, 391),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาธนบุรี', N'กรุงเทพมหานคร สาขาธนบุรี', 1, 391),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาลาดพร้าว', N'กรุงเทพมหานคร สาขาลาดพร้าว', 1, 392),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาลาดพร้าว', N'กรุงเทพมหานคร สาขาลาดพร้าว', 1, 392),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาดอนเมือง', N'กรุงเทพมหานคร สาขาดอนเมือง', 1, 393),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาดอนเมือง', N'กรุงเทพมหานคร สาขาดอนเมือง', 1, 393),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาบึงกุ่ม', N'กรุงเทพมหานคร สาขาบึงกุ่ม', 1, 394),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาบึงกุ่ม', N'กรุงเทพมหานคร สาขาบึงกุ่ม', 1, 394),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาประเวศ', N'กรุงเทพมหานคร สาขาประเวศ', 1, 395),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาประเวศ', N'กรุงเทพมหานคร สาขาประเวศ', 1, 395),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาหนองจอก', N'กรุงเทพมหานคร สาขาหนองจอก', 1, 396),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาหนองจอก', N'กรุงเทพมหานคร สาขาหนองจอก', 1, 396),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สาขาลาดกระบัง', N'กรุงเทพมหานคร สาขาลาดกระบัง', 1, 397),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สาขาลาดกระบัง', N'กรุงเทพมหานคร สาขาลาดกระบัง', 1, 397),
    (N'LandOffice', N'TH', N'EN', N'กระบี่', N'กระบี่', 1, 398),
    (N'LandOffice', N'TH', N'TH', N'กระบี่', N'กระบี่', 1, 398),
    (N'LandOffice', N'TH', N'EN', N'กระบี่ สาขาอ่าวลึก', N'กระบี่ สาขาอ่าวลึก', 1, 399),
    (N'LandOffice', N'TH', N'TH', N'กระบี่ สาขาอ่าวลึก', N'กระบี่ สาขาอ่าวลึก', 1, 399),
    (N'LandOffice', N'TH', N'EN', N'กระบี่ สาขาคลองท่อม', N'กระบี่ สาขาคลองท่อม', 1, 400),
    (N'LandOffice', N'TH', N'TH', N'กระบี่ สาขาคลองท่อม', N'กระบี่ สาขาคลองท่อม', 1, 400),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี', N'กาญจนบุรี', 1, 401),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี', N'กาญจนบุรี', 1, 401),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี สาขาท่ามะกา', N'กาญจนบุรี สาขาท่ามะกา', 1, 402),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี สาขาท่ามะกา', N'กาญจนบุรี สาขาท่ามะกา', 1, 402),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี สาขาบ่อพลอย', N'กาญจนบุรี สาขาบ่อพลอย', 1, 403),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี สาขาบ่อพลอย', N'กาญจนบุรี สาขาบ่อพลอย', 1, 403),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี ส่วนแยกเลาขวัญ', N'กาญจนบุรี ส่วนแยกเลาขวัญ', 1, 404),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี ส่วนแยกเลาขวัญ', N'กาญจนบุรี ส่วนแยกเลาขวัญ', 1, 404),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี ส่วนแยกพนมทวน', N'กาญจนบุรี ส่วนแยกพนมทวน', 1, 405),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี ส่วนแยกพนมทวน', N'กาญจนบุรี ส่วนแยกพนมทวน', 1, 405),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', 1, 406),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', N'กาญจนบุรี ส่วนแยกทองผาภูมิ', 1, 406),
    (N'LandOffice', N'TH', N'EN', N'กาญจนบุรี สาขาท่าม่วง', N'กาญจนบุรี สาขาท่าม่วง', 1, 407),
    (N'LandOffice', N'TH', N'TH', N'กาญจนบุรี สาขาท่าม่วง', N'กาญจนบุรี สาขาท่าม่วง', 1, 407),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์', N'จังหวัดกาฬสินธุ์', 1, 408),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์', N'จังหวัดกาฬสินธุ์', 1, 408),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', 1, 409),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', N'จังหวัดกาฬสินธุ์ สาขากุฉินารายณ์', 1, 409),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', 1, 410),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', N'จังหวัดกาฬสินธุ์ สาขากมลาไสย', 1, 410),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 411),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', N'จังหวัดกาฬสินธุ์ สาขายางตลาด', 1, 411),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', 1, 413),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', N'จังหวัดกาฬสินธุ์ สาขาสมเด็จ', 1, 413),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', 1, 414),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', N'จังหวัดกาฬสินธุ์ สาขาหนองกุงศรี', 1, 414),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกำแพงเพชร', N'จังหวัดกำแพงเพชร', 1, 415),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกำแพงเพชร', N'จังหวัดกำแพงเพชร', 1, 415),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', 1, 416),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', N'จังหวัดกำแพงเพชร สาขาคลองขลุง', 1, 416),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', 1, 417),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', N'จังหวัดกำแพงเพชร สาขาขาณุวรลักษบุรี', 1, 417),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น', N'จังหวัดขอนแก่น', 1, 418),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น', N'จังหวัดขอนแก่น', 1, 418),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาพล', N'จังหวัดขอนแก่น สาขาพล', 1, 419),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาพล', N'จังหวัดขอนแก่น สาขาพล', 1, 419),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาชุมแพ', N'จังหวัดขอนแก่น สาขาชุมแพ', 1, 420),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาชุมแพ', N'จังหวัดขอนแก่น สาขาชุมแพ', 1, 420),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาบ้านไผ่', N'จังหวัดขอนแก่น สาขาบ้านไผ่', 1, 421),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาบ้านไผ่', N'จังหวัดขอนแก่น สาขาบ้านไผ่', 1, 421),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขากระนวน', N'จังหวัดขอนแก่น สาขากระนวน', 1, 422),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขากระนวน', N'จังหวัดขอนแก่น สาขากระนวน', 1, 422),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', 1, 423),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', N'จังหวัดขอนแก่น สาขาหนองสองห้อง', 1, 423),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาหนองเรือ', N'จังหวัดขอนแก่น สาขาหนองเรือ', 1, 424),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาหนองเรือ', N'จังหวัดขอนแก่น สาขาหนองเรือ', 1, 424),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขามัญจาคีรี', N'จังหวัดขอนแก่น สาขามัญจาคีรี', 1, 425),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขามัญจาคีรี', N'จังหวัดขอนแก่น สาขามัญจาคีรี', 1, 425),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาภูเวียง', N'จังหวัดขอนแก่น สาขาภูเวียง', 1, 426),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาภูเวียง', N'จังหวัดขอนแก่น สาขาภูเวียง', 1, 426),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาน้ำพอง', N'จังหวัดขอนแก่น สาขาน้ำพอง', 1, 427),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาน้ำพอง', N'จังหวัดขอนแก่น สาขาน้ำพอง', 1, 427),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', 1, 428),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', N'จังหวัดขอนแก่น ส่วนแยกบ้านฝาง', 1, 428),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', 1, 429),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', N'จังหวัดขอนแก่น ส่วนแยกพระยืน', 1, 429),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', 1, 430),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', N'จังหวัดขอนแก่น ส่วนแยกแวงน้อย', 1, 430),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', 1, 431),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', N'จังหวัดขอนแก่น ส่วนแยกสีชมพู', 1, 431),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี', N'จังหวัดจันทบุรี', 1, 432),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี', N'จังหวัดจันทบุรี', 1, 432),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี สาขาท่าใหม่', N'จังหวัดจันทบุรี สาขาท่าใหม่', 1, 433),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี สาขาท่าใหม่', N'จังหวัดจันทบุรี สาขาท่าใหม่', 1, 433),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี ส่วนแยกขลุง', N'จังหวัดจันทบุรี ส่วนแยกขลุง', 1, 434),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี ส่วนแยกขลุง', N'จังหวัดจันทบุรี ส่วนแยกขลุง', 1, 434),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี สาขามะขาม', N'จังหวัดจันทบุรี สาขามะขาม', 1, 435),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี สาขามะขาม', N'จังหวัดจันทบุรี สาขามะขาม', 1, 435),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', 1, 436),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', N'จังหวัดจันทบุรี ส่วนแยกแหลมสิงห์', 1, 436),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดฉะเชิงเทรา', N'จังหวัดฉะเชิงเทรา', 1, 437),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดฉะเชิงเทรา', N'จังหวัดฉะเชิงเทรา', 1, 437),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', 1, 438),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', N'จังหวัดฉะเชิงเทรา สาขาพนมสารคาม', 1, 438),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', 1, 439),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', N'จังหวัดฉะเชิงเทรา สาขาบางคล้า', 1, 439),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', 1, 440),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', N'จังหวัดฉะเชิงเทรา สาขาบางปะกง', 1, 440),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี', N'จังหวัดชลบุรี', 1, 441),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี', N'จังหวัดชลบุรี', 1, 441),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี สาขาบางละมุง', N'จังหวัดชลบุรี สาขาบางละมุง', 1, 442),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี สาขาบางละมุง', N'จังหวัดชลบุรี สาขาบางละมุง', 1, 442),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี สาขาพนัสนิคม', N'จังหวัดชลบุรี สาขาพนัสนิคม', 1, 443),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี สาขาพนัสนิคม', N'จังหวัดชลบุรี สาขาพนัสนิคม', 1, 443),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี สาขาศรีราชา', N'จังหวัดชลบุรี สาขาศรีราชา', 1, 444),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี สาขาศรีราชา', N'จังหวัดชลบุรี สาขาศรีราชา', 1, 444),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี สาขาสัตหีบ', N'จังหวัดชลบุรี สาขาสัตหีบ', 1, 445),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี สาขาสัตหีบ', N'จังหวัดชลบุรี สาขาสัตหีบ', 1, 445),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', 1, 446),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', N'จังหวัดชลบุรี ส่วนแยกบ้านบึง', 1, 446),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ', N'จังหวัดชัยภูมิ', 1, 447),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ', N'จังหวัดชัยภูมิ', 1, 447),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาภูเขียว', N'จังหวัดชัยภูมิ สาขาภูเขียว', 1, 448),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาภูเขียว', N'จังหวัดชัยภูมิ สาขาภูเขียว', 1, 448),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาจัตุรัส', N'จังหวัดชัยภูมิ สาขาจัตุรัส', 1, 449),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาจัตุรัส', N'จังหวัดชัยภูมิ สาขาจัตุรัส', 1, 449),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', 1, 450),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', N'จังหวัดชัยภูมิ สาขาแก้งคร้อ', 1, 450),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', 1, 451),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', N'จังหวัดชัยภูมิ สาขาคอนสวรรค์', 1, 451),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', 1, 452),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', N'จังหวัดชัยภูมิ สาขาเกษตรสมบูรณ์', 1, 452),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', 1, 453),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', N'จังหวัดชัยภูมิ ส่วนแยกบ้านเขว้า', 1, 453),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยนาท', N'จังหวัดชัยนาท', 1, 454),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยนาท', N'จังหวัดชัยนาท', 1, 454),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', 1, 455),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', N'จังหวัดชัยนาท ส่วนแยกวัดสิงห์', 1, 455),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยนาท สาขาสรรคบุรี', N'จังหวัดชัยนาท สาขาสรรคบุรี', 1, 456),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยนาท สาขาสรรคบุรี', N'จังหวัดชัยนาท สาขาสรรคบุรี', 1, 456),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยนาท สาขาหันคา', N'จังหวัดชัยนาท สาขาหันคา', 1, 457),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยนาท สาขาหันคา', N'จังหวัดชัยนาท สาขาหันคา', 1, 457),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชุมพร', N'จังหวัดชุมพร', 1, 458),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชุมพร', N'จังหวัดชุมพร', 1, 458),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชุมพร สาขาหลังสวน', N'จังหวัดชุมพร สาขาหลังสวน', 1, 459),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชุมพร สาขาหลังสวน', N'จังหวัดชุมพร สาขาหลังสวน', 1, 459),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชุมพร สาขาปะทิว', N'จังหวัดชุมพร สาขาปะทิว', 1, 460),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชุมพร สาขาปะทิว', N'จังหวัดชุมพร สาขาปะทิว', 1, 460),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชุมพร สาขาสวี', N'จังหวัดชุมพร สาขาสวี', 1, 461),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชุมพร สาขาสวี', N'จังหวัดชุมพร สาขาสวี', 1, 461),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', 1, 462),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', N'จังหวัดชุมพร ส่วนแยกท่าแซะ', 1, 462),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่', N'จังหวัดเชียงใหม่', 1, 463),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่', N'จังหวัดเชียงใหม่', 1, 463),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาจอมทอง', N'จังหวัดเชียงใหม่ สาขาจอมทอง', 1, 464),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาจอมทอง', N'จังหวัดเชียงใหม่ สาขาจอมทอง', 1, 464),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', 1, 465),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', N'จังหวัดเชียงใหม่ สาขาดอยสะเก็ด', 1, 465),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาฝาง', N'จังหวัดเชียงใหม่ สาขาฝาง', 1, 466),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาฝาง', N'จังหวัดเชียงใหม่ สาขาฝาง', 1, 466),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาพร้าว', N'จังหวัดเชียงใหม่ สาขาพร้าว', 1, 467),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาพร้าว', N'จังหวัดเชียงใหม่ สาขาพร้าว', 1, 467),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาแม่แตง', N'จังหวัดเชียงใหม่ สาขาแม่แตง', 1, 468),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาแม่แตง', N'จังหวัดเชียงใหม่ สาขาแม่แตง', 1, 468),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาแม่ริม', N'จังหวัดเชียงใหม่ สาขาแม่ริม', 1, 469),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาแม่ริม', N'จังหวัดเชียงใหม่ สาขาแม่ริม', 1, 469),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', 1, 470),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', N'จังหวัดเชียงใหม่ สาขาสันกำแพง', 1, 470),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาสันทราย', N'จังหวัดเชียงใหม่ สาขาสันทราย', 1, 471),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาสันทราย', N'จังหวัดเชียงใหม่ สาขาสันทราย', 1, 471),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', 1, 472),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', N'จังหวัดเชียงใหม่ สาขาสันป่าตอง', 1, 472),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาหางดง', N'จังหวัดเชียงใหม่ สาขาหางดง', 1, 473),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาหางดง', N'จังหวัดเชียงใหม่ สาขาหางดง', 1, 473),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', 1, 474),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', N'จังหวัดเชียงใหม่ สาขาเชียงดาว', 1, 474),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ สาขาสารภี', N'จังหวัดเชียงใหม่ สาขาสารภี', 1, 475),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ สาขาสารภี', N'จังหวัดเชียงใหม่ สาขาสารภี', 1, 475),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', 1, 476),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', N'จังหวัดเชียงใหม่ ส่วนแยกแม่แจ่ม', 1, 476),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', 1, 477),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', N'จังหวัดเชียงใหม่ ส่วนแยกสะเมิง', 1, 477),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย', N'จังหวัดเชียงราย', 1, 478),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย', N'จังหวัดเชียงราย', 1, 478),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาเชียงของ', N'จังหวัดเชียงราย สาขาเชียงของ', 1, 479),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาเชียงของ', N'จังหวัดเชียงราย สาขาเชียงของ', 1, 479),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาพาน', N'จังหวัดเชียงราย สาขาพาน', 1, 480),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาพาน', N'จังหวัดเชียงราย สาขาพาน', 1, 480),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาเทิง', N'จังหวัดเชียงราย สาขาเทิง', 1, 481),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาเทิง', N'จังหวัดเชียงราย สาขาเทิง', 1, 481),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาแม่จัน', N'จังหวัดเชียงราย สาขาแม่จัน', 1, 482),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาแม่จัน', N'จังหวัดเชียงราย สาขาแม่จัน', 1, 482),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาแม่สาย', N'จังหวัดเชียงราย สาขาแม่สาย', 1, 483),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาแม่สาย', N'จังหวัดเชียงราย สาขาแม่สาย', 1, 483),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาเวียงชัย', N'จังหวัดเชียงราย สาขาเวียงชัย', 1, 484),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาเวียงชัย', N'จังหวัดเชียงราย สาขาเวียงชัย', 1, 484),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', 1, 485),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', N'จังหวัดเชียงราย สาขาเวียงป่าเป้า', 1, 485),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาเชียงแสน', N'จังหวัดเชียงราย สาขาเชียงแสน', 1, 486),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาเชียงแสน', N'จังหวัดเชียงราย สาขาเชียงแสน', 1, 486),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตรัง', N'จังหวัดตรัง', 1, 487),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตรัง', N'จังหวัดตรัง', 1, 487),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตรัง สาขาห้วยยอด', N'จังหวัดตรัง สาขาห้วยยอด', 1, 488),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตรัง สาขาห้วยยอด', N'จังหวัดตรัง สาขาห้วยยอด', 1, 488),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตรัง สาขาย่านตาขาว', N'จังหวัดตรัง สาขาย่านตาขาว', 1, 489),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตรัง สาขาย่านตาขาว', N'จังหวัดตรัง สาขาย่านตาขาว', 1, 489),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตรัง สาขากันตรัง', N'จังหวัดตรัง สาขากันตรัง', 1, 490),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตรัง สาขากันตรัง', N'จังหวัดตรัง สาขากันตรัง', 1, 490),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตาก', N'จังหวัดตาก', 1, 491),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตาก', N'จังหวัดตาก', 1, 491),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตาก สาขาแม่สอด', N'จังหวัดตาก สาขาแม่สอด', 1, 492),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตาก สาขาแม่สอด', N'จังหวัดตาก สาขาแม่สอด', 1, 492),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตาก สาขาสามเงา', N'จังหวัดตาก สาขาสามเงา', 1, 493),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตาก สาขาสามเงา', N'จังหวัดตาก สาขาสามเงา', 1, 493),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตราด', N'จังหวัดตราด', 1, 494),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตราด', N'จังหวัดตราด', 1, 494),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตราด ส่วนแยกเขาสมิง', N'จังหวัดตราด ส่วนแยกเขาสมิง', 1, 495),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตราด ส่วนแยกเขาสมิง', N'จังหวัดตราด ส่วนแยกเขาสมิง', 1, 495),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตราด ส่วนแยกแหลมงอบ', N'จังหวัดตราด ส่วนแยกแหลมงอบ', 1, 496),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตราด ส่วนแยกแหลมงอบ', N'จังหวัดตราด ส่วนแยกแหลมงอบ', 1, 496),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครนายก', N'จังหวัดนครนายก', 1, 497),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครนายก', N'จังหวัดนครนายก', 1, 497),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครนายก สาขาองครักษ์', N'จังหวัดนครนายก สาขาองครักษ์', 1, 498),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครนายก สาขาองครักษ์', N'จังหวัดนครนายก สาขาองครักษ์', 1, 498),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม', N'จังหวัดนครปฐม', 1, 499),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม', N'จังหวัดนครปฐม', 1, 499),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม สาขานครชัยศรี', N'จังหวัดนครปฐม สาขานครชัยศรี', 1, 500),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม สาขานครชัยศรี', N'จังหวัดนครปฐม สาขานครชัยศรี', 1, 500),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม สาขาบางเลน', N'จังหวัดนครปฐม สาขาบางเลน', 1, 501),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม สาขาบางเลน', N'จังหวัดนครปฐม สาขาบางเลน', 1, 501),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม สาขาสามพราน', N'จังหวัดนครปฐม สาขาสามพราน', 1, 502),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม สาขาสามพราน', N'จังหวัดนครปฐม สาขาสามพราน', 1, 502);
GO

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม สาขากำแพงแสน', N'จังหวัดนครปฐม สาขากำแพงแสน', 1, 503),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม สาขากำแพงแสน', N'จังหวัดนครปฐม สาขากำแพงแสน', 1, 503),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครปฐม สาขานครพนม', N'จังหวัดนครปฐม สาขานครพนม', 1, 504),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครปฐม สาขานครพนม', N'จังหวัดนครปฐม สาขานครพนม', 1, 504),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขาธาตุพนม', N'จังหวัดนครพนม สาขาธาตุพนม', 1, 505),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขาธาตุพนม', N'จังหวัดนครพนม สาขาธาตุพนม', 1, 505),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขาศรีสงคราม', N'จังหวัดนครพนม สาขาศรีสงคราม', 1, 506),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขาศรีสงคราม', N'จังหวัดนครพนม สาขาศรีสงคราม', 1, 506),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขาท่าอุเทน', N'จังหวัดนครพนม สาขาท่าอุเทน', 1, 507),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขาท่าอุเทน', N'จังหวัดนครพนม สาขาท่าอุเทน', 1, 507),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขานาแก', N'จังหวัดนครพนม สาขานาแก', 1, 508),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขานาแก', N'จังหวัดนครพนม สาขานาแก', 1, 508),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขาเรณูนคร', N'จังหวัดนครพนม สาขาเรณูนคร', 1, 509),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขาเรณูนคร', N'จังหวัดนครพนม สาขาเรณูนคร', 1, 509),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', 1, 510),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', N'จังหวัดนครพนม สาขาท่าอุเทน ส่วนแยกบ้านแพง', 1, 510),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา', N'จังหวัดนครราชสีมา', 1, 511),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา', N'จังหวัดนครราชสีมา', 1, 511),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาคง', N'จังหวัดนครราชสีมา สาขาคง', 1, 512),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาคง', N'จังหวัดนครราชสีมา สาขาคง', 1, 512),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาครบุรี', N'จังหวัดนครราชสีมา สาขาครบุรี', 1, 513),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาครบุรี', N'จังหวัดนครราชสีมา สาขาครบุรี', 1, 513),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาจักราช', N'จังหวัดนครราชสีมา สาขาจักราช', 1, 514),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาจักราช', N'จังหวัดนครราชสีมา สาขาจักราช', 1, 514),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาชุมพวง', N'จังหวัดนครราชสีมา สาขาชุมพวง', 1, 515),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาชุมพวง', N'จังหวัดนครราชสีมา สาขาชุมพวง', 1, 515),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาโชคชัย', N'จังหวัดนครราชสีมา สาขาโชคชัย', 1, 516),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาโชคชัย', N'จังหวัดนครราชสีมา สาขาโชคชัย', 1, 516),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', 1, 517),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', N'จังหวัดนครราชสีมา สาขาด่านขุนทด', 1, 517),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาโนนไทย', N'จังหวัดนครราชสีมา สาขาโนนไทย', 1, 518),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาโนนไทย', N'จังหวัดนครราชสีมา สาขาโนนไทย', 1, 518),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครรราชสีมา สาขาโนนสูง', N'จังหวัดนครรราชสีมา สาขาโนนสูง', 1, 519),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครรราชสีมา สาขาโนนสูง', N'จังหวัดนครรราชสีมา สาขาโนนสูง', 1, 519),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', 1, 520),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', N'จังหวัดนครราชสีมา สาขาบัวใหญ่', 1, 520),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาประทาย', N'จังหวัดนครราชสีมา สาขาประทาย', 1, 521),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาประทาย', N'จังหวัดนครราชสีมา สาขาประทาย', 1, 521),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาปักธงชัย', N'จังหวัดนครราชสีมา สาขาปักธงชัย', 1, 522),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาปักธงชัย', N'จังหวัดนครราชสีมา สาขาปักธงชัย', 1, 522),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาปากช่อง', N'จังหวัดนครราชสีมา สาขาปากช่อง', 1, 523),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาปากช่อง', N'จังหวัดนครราชสีมา สาขาปากช่อง', 1, 523),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาพิมาย', N'จังหวัดนครราชสีมา สาขาพิมาย', 1, 524),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาพิมาย', N'จังหวัดนครราชสีมา สาขาพิมาย', 1, 524),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', 1, 525),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', N'จังหวัดนครราชสีมา สาขาสีคิ้ว', 1, 525),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', 1, 526),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', N'จังหวัดนครราชสีมา สาขาขามสะแกแสง', 1, 526),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครราชสีมา สาขาสูงเนิน', N'จังหวัดนครราชสีมา สาขาสูงเนิน', 1, 527),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครราชสีมา สาขาสูงเนิน', N'จังหวัดนครราชสีมา สาขาสูงเนิน', 1, 527),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช', N'จังหวัดนครศรีธรรมราช', 1, 528),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช', N'จังหวัดนครศรีธรรมราช', 1, 528),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', 1, 529),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', N'จังหวัดนครศรีธรรมราช สาขาลานสกา', 1, 529),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', 1, 530),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', N'จังหวัดนครศรีธรรมราช สาขาทุ่งสง', 1, 530),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาสิชล', N'จังหวัดนครศรีธรรมราช สาขาสิชล', 1, 531),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาสิชล', N'จังหวัดนครศรีธรรมราช สาขาสิชล', 1, 531),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', 1, 532),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', N'จังหวัดนครศรีธรรมราช สาขาปากพนัง', 1, 532),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', 1, 533),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', N'จังหวัดนครศรีธรรมราช สาขาหัวไทร', 1, 533),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', 1, 534),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', N'จังหวัดนครศรีธรรมราช สาขาท่าศาลา', 1, 534),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', 1, 535),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', N'จังหวัดนครศรีธรรมราช สาขาฉวาง', 1, 535),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', 1, 536),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', N'จังหวัดนครศรีธรรมราช สาขาชะอวด', 1, 536),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', 1, 537),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', N'จังหวัดนครศรีธรรมราช สาขาเชียรใหญ่', 1, 537),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์', N'จังหวัดนครสวรรค์', 1, 538),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์', N'จังหวัดนครสวรรค์', 1, 538),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาชุมแสง', N'จังหวัดนครสวรรค์ สาขาชุมแสง', 1, 539),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาชุมแสง', N'จังหวัดนครสวรรค์ สาขาชุมแสง', 1, 539),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาตาคลี', N'จังหวัดนครสวรรค์ สาขาตาคลี', 1, 540),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาตาคลี', N'จังหวัดนครสวรรค์ สาขาตาคลี', 1, 540),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', 1, 541),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', N'จังหวัดนครสวรรค์ สาขาตาคลี ส่วนแยกตากฟ้า', 1, 541),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', 1, 542),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', N'จังหวัดนครสวรรค์ สาขาท่าตะโก', 1, 542),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', 1, 543),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', N'จังหวัดนครสวรรค์ สาขาชุมแสง ส่วนแยกหนองบัว', 1, 543),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', 1, 544),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', N'จังหวัดนครสวรรค์ สาขาท่าตะโก ส่วนแยกไพศาลี', 1, 544),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', 1, 545),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', N'จังหวัดนครสวรรค์ สาขาบรรพตพิสัย', 1, 545),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', 1, 546),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', N'จังหวัดนครสวรรค์ สาขาพยุหะคีรี', 1, 546),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครสวรรค์ สาขาลาดยาว', N'จังหวัดนครสวรรค์ สาขาลาดยาว', 1, 547),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครสวรรค์ สาขาลาดยาว', N'จังหวัดนครสวรรค์ สาขาลาดยาว', 1, 547),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนนทบุรี', N'จังหวัดนนทบุรี', 1, 548),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนนทบุรี', N'จังหวัดนนทบุรี', 1, 548),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนนทบุรี สาขาบางบัวทอง', N'จังหวัดนนทบุรี สาขาบางบัวทอง', 1, 549),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนนทบุรี สาขาบางบัวทอง', N'จังหวัดนนทบุรี สาขาบางบัวทอง', 1, 549),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนนทบุรี สาขาบางใหญ่', N'จังหวัดนนทบุรี สาขาบางใหญ่', 1, 550),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนนทบุรี สาขาบางใหญ่', N'จังหวัดนนทบุรี สาขาบางใหญ่', 1, 550),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนนทบุรี สาขาปากเกร็ด', N'จังหวัดนนทบุรี สาขาปากเกร็ด', 1, 551),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนนทบุรี สาขาปากเกร็ด', N'จังหวัดนนทบุรี สาขาปากเกร็ด', 1, 551),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนราธิวาส', N'จังหวัดนราธิวาส', 1, 552),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนราธิวาส', N'จังหวัดนราธิวาส', 1, 552),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดประจวบคีรีขันธ์', N'จังหวัดประจวบคีรีขันธ์', 1, 553),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดประจวบคีรีขันธ์', N'จังหวัดประจวบคีรีขันธ์', 1, 553),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', 1, 554),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', N'จังหวัดประจวบคีรีขันธ์ สาขาหัวหิน', 1, 554),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', 1, 555),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', N'จังหวัดประจวบคีรีขันธ์ สาขาบางสะพาน', 1, 555),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', 1, 556),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', N'จังหวัดประจวบคีรีขันธ์ สาขหัวหิน ส่วนแยกปราณบุรี', 1, 556),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปทุมธานี', N'จังหวัดปทุมธานี', 1, 557),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปทุมธานี', N'จังหวัดปทุมธานี', 1, 557),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปทุมธานี สาขาธัญบุรี', N'จังหวัดปทุมธานี สาขาธัญบุรี', 1, 558),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปทุมธานี สาขาธัญบุรี', N'จังหวัดปทุมธานี สาขาธัญบุรี', 1, 558),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปทุมธานี สาขาคลองหลวง', N'จังหวัดปทุมธานี สาขาคลองหลวง', 1, 559),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปทุมธานี สาขาคลองหลวง', N'จังหวัดปทุมธานี สาขาคลองหลวง', 1, 559),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปทุมธานี สาขาลำลูกกา', N'จังหวัดปทุมธานี สาขาลำลูกกา', 1, 560),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปทุมธานี สาขาลำลูกกา', N'จังหวัดปทุมธานี สาขาลำลูกกา', 1, 560),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบุรี', N'จังหวัดเพชรบุรี', 1, 561),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบุรี', N'จังหวัดเพชรบุรี', 1, 561),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', 1, 562),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', N'จังหวัดเพชรบุรี ส่วนแยกเขาย้อย', 1, 562),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบุรี สาขาท่ายาง', N'จังหวัดเพชรบุรี สาขาท่ายาง', 1, 563),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบุรี สาขาท่ายาง', N'จังหวัดเพชรบุรี สาขาท่ายาง', 1, 563),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบุรี สาขาชะอำ', N'จังหวัดเพชรบุรี สาขาชะอำ', 1, 564),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบุรี สาขาชะอำ', N'จังหวัดเพชรบุรี สาขาชะอำ', 1, 564),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดภูเก็ต', N'จังหวัดภูเก็ต', 1, 565),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดภูเก็ต', N'จังหวัดภูเก็ต', 1, 565),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดภูเก็ต ส่วนแยกถลาง', N'จังหวัดภูเก็ต ส่วนแยกถลาง', 1, 566),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดภูเก็ต ส่วนแยกถลาง', N'จังหวัดภูเก็ต ส่วนแยกถลาง', 1, 566),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดยโสธร', N'จังหวัดยโสธร', 1, 567),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดยโสธร', N'จังหวัดยโสธร', 1, 567),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดยโสธร สาขามหาชนะชัย', N'จังหวัดยโสธร สาขามหาชนะชัย', 1, 568),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดยโสธร สาขามหาชนะชัย', N'จังหวัดยโสธร สาขามหาชนะชัย', 1, 568),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', 1, 569),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', N'จังหวัดยโสธร สาขาคำเขื่อนแก้ว', 1, 569),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดยโสธร สาขาเลิงนกทา', N'จังหวัดยโสธร สาขาเลิงนกทา', 1, 570),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดยโสธร สาขาเลิงนกทา', N'จังหวัดยโสธร สาขาเลิงนกทา', 1, 570),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดระยอง', N'จังหวัดระยอง', 1, 571),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดระยอง', N'จังหวัดระยอง', 1, 571),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดระยอง สาขาแกลง', N'จังหวัดระยอง สาขาแกลง', 1, 572),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดระยอง สาขาแกลง', N'จังหวัดระยอง สาขาแกลง', 1, 572),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดระยอง สาขาบ้านค่าย', N'จังหวัดระยอง สาขาบ้านค่าย', 1, 573),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดระยอง สาขาบ้านค่าย', N'จังหวัดระยอง สาขาบ้านค่าย', 1, 573),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดระยอง สาขาบ้านฉาง', N'จังหวัดระยอง สาขาบ้านฉาง', 1, 574),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดระยอง สาขาบ้านฉาง', N'จังหวัดระยอง สาขาบ้านฉาง', 1, 574),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดระยอง สาขาปลวกแดง', N'จังหวัดระยอง สาขาปลวกแดง', 1, 575),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดระยอง สาขาปลวกแดง', N'จังหวัดระยอง สาขาปลวกแดง', 1, 575),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ', N'จังหวัดสมุทรปราการ', 1, 576),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ', N'จังหวัดสมุทรปราการ', 1, 576),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ สาขาบางพลี', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 577),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ สาขาบางพลี', N'จังหวัดสมุทรปราการ สาขาบางพลี', 1, 577),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ สาขาพระประแดง', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 578),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ สาขาพระประแดง', N'จังหวัดสมุทรปราการ สาขาพระประแดง', 1, 578),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรสาคร', N'จังหวัดสมุทรสาคร', 1, 579),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรสาคร', N'จังหวัดสมุทรสาคร', 1, 579),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', 1, 580),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', N'จังหวัดสมุทรสาคร สาขากระทุ่มแบน', 1, 580),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', 1, 581),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', N'จังหวัดสมุทรสาคร สาขาบ้านแพ้ว', 1, 581),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรสงคราม', N'จังหวัดสมุทรสงคราม', 1, 582),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรสงคราม', N'จังหวัดสมุทรสงคราม', 1, 582),
    (N'LandOffice', N'TH', N'EN', N'สระบุรี', N'สระบุรี', 1, 583),
    (N'LandOffice', N'TH', N'TH', N'สระบุรี', N'สระบุรี', 1, 583),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสระบุรี สาขาแก่งคอย', N'จังหวัดสระบุรี สาขาแก่งคอย', 1, 584),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสระบุรี สาขาแก่งคอย', N'จังหวัดสระบุรี สาขาแก่งคอย', 1, 584),
    (N'LandOffice', N'TH', N'EN', N'สระบุรี สาขาหนองแค', N'สระบุรี สาขาหนองแค', 1, 585),
    (N'LandOffice', N'TH', N'TH', N'สระบุรี สาขาหนองแค', N'สระบุรี สาขาหนองแค', 1, 585),
    (N'LandOffice', N'TH', N'EN', N'สระบุรี สาขาพระพุทธบาท', N'สระบุรี สาขาพระพุทธบาท', 1, 586),
    (N'LandOffice', N'TH', N'TH', N'สระบุรี สาขาพระพุทธบาท', N'สระบุรี สาขาพระพุทธบาท', 1, 586),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร สำนักงานใหญ่', N'กรุงเทพมหานคร สำนักงานใหญ่', 1, 587),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร สำนักงานใหญ่', N'กรุงเทพมหานคร สำนักงานใหญ่', 1, 587),
    (N'LandOffice', N'TH', N'EN', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', 1, 588),
    (N'LandOffice', N'TH', N'TH', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', N'ระยอง สาขาบ้านค่าย ส่วนแยกปลวกแดง', 1, 588),
    (N'LandOffice', N'TH', N'EN', N'นนทบุรี  สาขาปากเกร็ด', N'นนทบุรี  สาขาปากเกร็ด', 1, 589),
    (N'LandOffice', N'TH', N'TH', N'นนทบุรี  สาขาปากเกร็ด', N'นนทบุรี  สาขาปากเกร็ด', 1, 589),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพระนครศรีอยุธยา', N'จังหวัดพระนครศรีอยุธยา', 1, 590),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพระนครศรีอยุธยา', N'จังหวัดพระนครศรีอยุธยา', 1, 590),
    (N'LandOffice', N'TH', N'EN', N'พระนครศรีอยุธยา สาขาเสนา', N'พระนครศรีอยุธยา สาขาเสนา', 1, 591),
    (N'LandOffice', N'TH', N'TH', N'พระนครศรีอยุธยา สาขาเสนา', N'พระนครศรีอยุธยา สาขาเสนา', 1, 591),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', 1, 592),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', N'จังหวัดพระนครศรีอยุธยา สาขาวังน้อย', 1, 592),
    (N'LandOffice', N'TH', N'EN', N'พระนครศรีอยุธยา สาขาท่าเรือ', N'พระนครศรีอยุธยา สาขาท่าเรือ', 1, 593),
    (N'LandOffice', N'TH', N'TH', N'พระนครศรีอยุธยา สาขาท่าเรือ', N'พระนครศรีอยุธยา สาขาท่าเรือ', 1, 593),
    (N'LandOffice', N'TH', N'EN', N'ลำพูน สาขาป่าซาง', N'ลำพูน สาขาป่าซาง', 1, 594),
    (N'LandOffice', N'TH', N'TH', N'ลำพูน สาขาป่าซาง', N'ลำพูน สาขาป่าซาง', 1, 594),
    (N'LandOffice', N'TH', N'EN', N'ปราจีนบุรี', N'ปราจีนบุรี', 1, 595),
    (N'LandOffice', N'TH', N'TH', N'ปราจีนบุรี', N'ปราจีนบุรี', 1, 595),
    (N'LandOffice', N'TH', N'EN', N'ลำปาง', N'ลำปาง', 1, 596),
    (N'LandOffice', N'TH', N'TH', N'ลำปาง', N'ลำปาง', 1, 596),
    (N'LandOffice', N'TH', N'EN', N'พิษณุโลก', N'พิษณุโลก', 1, 598),
    (N'LandOffice', N'TH', N'TH', N'พิษณุโลก', N'พิษณุโลก', 1, 598),
    (N'LandOffice', N'TH', N'EN', N'อุดรธานี', N'อุดรธานี', 1, 599),
    (N'LandOffice', N'TH', N'TH', N'อุดรธานี', N'อุดรธานี', 1, 599),
    (N'LandOffice', N'TH', N'EN', N'สงขลา สาขาหาดใหญ่', N'สงขลา สาขาหาดใหญ่', 1, 600),
    (N'LandOffice', N'TH', N'TH', N'สงขลา สาขาหาดใหญ่', N'สงขลา สาขาหาดใหญ่', 1, 600),
    (N'LandOffice', N'TH', N'EN', N'สุพรรณบุรี', N'สุพรรณบุรี', 1, 601),
    (N'LandOffice', N'TH', N'TH', N'สุพรรณบุรี', N'สุพรรณบุรี', 1, 601),
    (N'LandOffice', N'TH', N'EN', N'สุราษฎร์ธานี', N'สุราษฎร์ธานี', 1, 602),
    (N'LandOffice', N'TH', N'TH', N'สุราษฎร์ธานี', N'สุราษฎร์ธานี', 1, 602),
    (N'LandOffice', N'TH', N'EN', N'อุบลราชธานี', N'อุบลราชธานี', 1, 603),
    (N'LandOffice', N'TH', N'TH', N'อุบลราชธานี', N'อุบลราชธานี', 1, 603),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', 1, 604),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', N'จังหวัดนครศรีธรรมราช สาขาร่อนพิบูลย์', 1, 604),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', 1, 605),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', N'จังหวัดอ่างทอง สาขาวิเศษชัยชาญ', 1, 605),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', 1, 607),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', N'จังหวัดสมุทรปราการ  สาขาพระสมุทรเจดีย์', 1, 607),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดราชบุรี', N'จังหวัดราชบุรี', 1, 608),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดราชบุรี', N'จังหวัดราชบุรี', 1, 608),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', 1, 609),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', N'จังหวัดสมุทรปราการ สาขาพระสมุทรเจดีย์', 1, 609),
    (N'LandOffice', N'TH', N'EN', N'กรุงเทพมหานคร', N'กรุงเทพมหานคร', 1, 610),
    (N'LandOffice', N'TH', N'TH', N'กรุงเทพมหานคร', N'กรุงเทพมหานคร', 1, 610),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 611),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', N'จังหวัดอุบลราชธานี สาขาวารินชำราบ', 1, 611),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', 1, 612),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย', 1, 612),
    (N'LandOffice', N'TH', N'EN', N'พังงา สาขาตะกั่วทุ่ง', N'พังงา สาขาตะกั่วทุ่ง', 1, 614),
    (N'LandOffice', N'TH', N'TH', N'พังงา สาขาตะกั่วทุ่ง', N'พังงา สาขาตะกั่วทุ่ง', 1, 614),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', 1, 616),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', N'จังหวัดสมุทรปราการ สาขาบางบ่อ', 1, 616),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', 1, 617),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', N'จังหวัดจันทบุรี สาขาโป่งน้ำร้อน', 1, 617),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม', N'จังหวัดมหาสารคาม', 1, 618),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม', N'จังหวัดมหาสารคาม', 1, 618),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', 1, 619),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', N'จังหวัดบุรีรัมย์ สาขาคูเมือง', 1, 619),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด', N'จังหวัดร้อยเอ็ด', 1, 621),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด', N'จังหวัดร้อยเอ็ด', 1, 621),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', 1, 622),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', N'จังหวัดร้อยเอ็ด สาขาเสลภูมิ', 1, 622),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', 1, 623),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', N'จังหวัดร้อยเอ็ด สาขาธวัชบุรี', 1, 623),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดลำพูน', N'จังหวัดลำพูน', 1, 624),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดลำพูน', N'จังหวัดลำพูน', 1, 624),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', 1, 625),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', N'จังหวัดร้อยเอ็ด สาขาโพนทอง', 1, 625),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', 1, 626),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', N'จังหวัดบุรีรัมย์ สาขาประโคนชัย', 1, 626),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพัทลุง', N'จังหวัดพัทลุง', 1, 627),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพัทลุง', N'จังหวัดพัทลุง', 1, 627),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดลำปาง สาขาเกาะคา', N'จังหวัดลำปาง สาขาเกาะคา', 1, 628),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดลำปาง สาขาเกาะคา', N'จังหวัดลำปาง สาขาเกาะคา', 1, 628),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพังงา', N'จังหวัดพังงา', 1, 629),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพังงา', N'จังหวัดพังงา', 1, 629),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปทุมธานี สาขาสามโคก', N'จังหวัดปทุมธานี สาขาสามโคก', 1, 630),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปทุมธานี สาขาสามโคก', N'จังหวัดปทุมธานี สาขาสามโคก', 1, 630),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', 1, 631),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', N'จังหวัดอุดรธานี สาขาหนองวัวซอ', 1, 631),
    (N'LandOffice', N'TH', N'EN', N'อุดรธานี สาขาหนองวัวซอ', N'อุดรธานี สาขาหนองวัวซอ', 1, 632),
    (N'LandOffice', N'TH', N'TH', N'อุดรธานี สาขาหนองวัวซอ', N'อุดรธานี สาขาหนองวัวซอ', 1, 632),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบูรณ์', N'จังหวัดเพชรบูรณ์', 1, 633),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบูรณ์', N'จังหวัดเพชรบูรณ์', 1, 633),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสงขลา สาขาหาดใหญ่', N'จังหวัดสงขลา สาขาหาดใหญ่', 1, 634),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสงขลา สาขาหาดใหญ่', N'จังหวัดสงขลา สาขาหาดใหญ่', 1, 634),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', 1, 635),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', N'จังหวัดสะแก้ว สาขาอรัญประเทศ', 1, 635),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', 1, 636),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', N'จังหวัดสรแก้ว สาขาอรัญประเทศ', 1, 636),
    (N'LandOffice', N'TH', N'EN', N'ปราจีนบุรี สาขากบินทร์บุรี', N'ปราจีนบุรี สาขากบินทร์บุรี', 1, 637),
    (N'LandOffice', N'TH', N'TH', N'ปราจีนบุรี สาขากบินทร์บุรี', N'ปราจีนบุรี สาขากบินทร์บุรี', 1, 637),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร สาขาบ้านม่วง', N'จังหวัดสกลนคร สาขาบ้านม่วง', 1, 638),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร สาขาบ้านม่วง', N'จังหวัดสกลนคร สาขาบ้านม่วง', 1, 638),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุรินทร์', N'จังหวัดสุรินทร์', 1, 639),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุรินทร์', N'จังหวัดสุรินทร์', 1, 639),
    (N'LandOffice', N'TH', N'EN', N'พระนครศรีอยุธยา สาขาวังน้อย', N'พระนครศรีอยุธยา สาขาวังน้อย', 1, 640),
    (N'LandOffice', N'TH', N'TH', N'พระนครศรีอยุธยา สาขาวังน้อย', N'พระนครศรีอยุธยา สาขาวังน้อย', 1, 640),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดหนองคาย สาขาโพนพิสัย', N'จังหวัดหนองคาย สาขาโพนพิสัย', 1, 641),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดหนองคาย สาขาโพนพิสัย', N'จังหวัดหนองคาย สาขาโพนพิสัย', 1, 641),
    (N'LandOffice', N'TH', N'EN', N'สาขาโพนพิสัย', N'สาขาโพนพิสัย', 1, 642),
    (N'LandOffice', N'TH', N'TH', N'สาขาโพนพิสัย', N'สาขาโพนพิสัย', 1, 642),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', 1, 643),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', N'จังหวัดสระแก้ว สาขาอรัญประเทศ', 1, 643),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบึงกาฬ', N'จังหวัดบึงกาฬ', 1, 644),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบึงกาฬ', N'จังหวัดบึงกาฬ', 1, 644),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดน่าน', N'จังหวัดน่าน', 1, 645),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดน่าน', N'จังหวัดน่าน', 1, 645),
    (N'LandOffice', N'TH', N'EN', N'จังหวักอุดรธานี', N'จังหวักอุดรธานี', 1, 646),
    (N'LandOffice', N'TH', N'TH', N'จังหวักอุดรธานี', N'จังหวักอุดรธานี', 1, 646),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี', N'จังหวัดอุดรธานี', 1, 647),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี', N'จังหวัดอุดรธานี', 1, 647),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชลบุรี สาขาบ้านบึง', N'จังหวัดชลบุรี สาขาบ้านบึง', 1, 648),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชลบุรี สาขาบ้านบึง', N'จังหวัดชลบุรี สาขาบ้านบึง', 1, 648),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', 1, 649),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', N'จังหวัดอุบลราชธานี สาขาเดชอุดม', 1, 649),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพะเยา สาขาปง', N'จังหวัดพะเยา สาขาปง', 1, 650),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพะเยา สาขาปง', N'จังหวัดพะเยา สาขาปง', 1, 650),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดน่าน สาขาปัว', N'จังหวัดน่าน สาขาปัว', 1, 651),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดน่าน สาขาปัว', N'จังหวัดน่าน สาขาปัว', 1, 651),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพะเยา สาขาจุน', N'จังหวัดพะเยา สาขาจุน', 1, 652),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพะเยา สาขาจุน', N'จังหวัดพะเยา สาขาจุน', 1, 652),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสงขลา สาขาสะเดา', N'จังหวัดสงขลา สาขาสะเดา', 1, 653),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสงขลา สาขาสะเดา', N'จังหวัดสงขลา สาขาสะเดา', 1, 653),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', 1, 654),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', N'จังหวัดมหาสารคาม สาขากันทรวิชัย', 1, 654),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', 1, 655),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', N'จังหวัดศรีสะเกษ สาขากันทรลักษ์', 1, 655),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', 1, 656),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', N'จังหวัดมหาสารคาม สาขาพยัคฆภูมิพิสัย', 1, 656),
    (N'LandOffice', N'TH', N'EN', N'ตราด สาขาแหลมงอบ', N'ตราด สาขาแหลมงอบ', 1, 657),
    (N'LandOffice', N'TH', N'TH', N'ตราด สาขาแหลมงอบ', N'ตราด สาขาแหลมงอบ', 1, 657),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขาหนองหาน', N'จังหวัดอุดรธานี สาขาหนองหาน', 1, 658),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขาหนองหาน', N'จังหวัดอุดรธานี สาขาหนองหาน', 1, 658),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขากุมภวาปี', N'จังหวัดอุดรธานี สาขากุมภวาปี', 1, 659),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขากุมภวาปี', N'จังหวัดอุดรธานี สาขากุมภวาปี', 1, 659),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอ่างทอง', N'จังหวัดอ่างทอง', 1, 660),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอ่างทอง', N'จังหวัดอ่างทอง', 1, 660),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', 1, 661),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', N'จังหวัดร้อยเอ็ด สาขาสุวรรณภูมิ', 1, 661),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเลย สาขาด่านซ้าย', N'จังหวัดเลย สาขาด่านซ้าย', 1, 662),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเลย สาขาด่านซ้าย', N'จังหวัดเลย สาขาด่านซ้าย', 1, 662),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม สาขาเชียงยืน', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 663),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม สาขาเชียงยืน', N'จังหวัดมหาสารคาม สาขาเชียงยืน', 1, 663),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบึงกาฬ สาขาเซกา', N'จังหวัดบึงกาฬ สาขาเซกา', 1, 664),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบึงกาฬ สาขาเซกา', N'จังหวัดบึงกาฬ สาขาเซกา', 1, 664),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', 1, 665),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', N'จังหวัดขอนแก่น สาขาเขาสวนกวาง', 1, 665),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', 1, 666),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', N'จังหวัดนครศรีธรรมราช สาขาทุ่งใหญ่', 1, 666),
    (N'LandOffice', N'TH', N'EN', N'จังหวักพิษณุโลก', N'จังหวักพิษณุโลก', 1, 667),
    (N'LandOffice', N'TH', N'TH', N'จังหวักพิษณุโลก', N'จังหวักพิษณุโลก', 1, 667),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพิษณุโลก', N'จังหวัดพิษณุโลก', 1, 668),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพิษณุโลก', N'จังหวัดพิษณุโลก', 1, 668),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดภูเก็ต สาขาถลาง', N'จังหวัดภูเก็ต สาขาถลาง', 1, 669),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดภูเก็ต สาขาถลาง', N'จังหวัดภูเก็ต สาขาถลาง', 1, 669),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพังงา ส่วนแยกโคกกลอย', N'จังหวัดพังงา ส่วนแยกโคกกลอย', 1, 670),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพังงา ส่วนแยกโคกกลอย', N'จังหวัดพังงา ส่วนแยกโคกกลอย', 1, 670),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดหนองบัวลำภู', N'จังหวัดหนองบัวลำภู', 1, 671),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดหนองบัวลำภู', N'จังหวัดหนองบัวลำภู', 1, 671),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบุรีรัมย์', N'จังหวัดบุรีรัมย์', 1, 673),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบุรีรัมย์', N'จังหวัดบุรีรัมย์', 1, 673),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขาบ้านดุง', N'จังหวัดอุดรธานี สาขาบ้านดุง', 1, 674),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขาบ้านดุง', N'จังหวัดอุดรธานี สาขาบ้านดุง', 1, 674),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', 1, 675),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', N'จังหวัดลำพูน สาขาบ้านโฮ่ง', 1, 675),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', 1, 676),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', N'จังหวัดมหาสารคาม สาขาวาปีปทุม', 1, 676),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุรินทร์ สาขาจอมพระ', N'จังหวัดสุรินทร์ สาขาจอมพระ', 1, 677),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุรินทร์ สาขาจอมพระ', N'จังหวัดสุรินทร์ สาขาจอมพระ', 1, 677),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดนครพนม', N'จังหวัดนครพนม', 1, 678),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดนครพนม', N'จังหวัดนครพนม', 1, 678),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', 1, 679),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก', 1, 679),
    (N'LandOffice', N'TH', N'EN', N'สาขาหล่มสัก', N'สาขาหล่มสัก', 1, 680),
    (N'LandOffice', N'TH', N'TH', N'สาขาหล่มสัก', N'สาขาหล่มสัก', 1, 680),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพิจิตร', N'จังหวัดพิจิตร', 1, 681),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพิจิตร', N'จังหวัดพิจิตร', 1, 681),
    (N'LandOffice', N'TH', N'EN', N'อุดรธานี สาขาเพ็ญ', N'อุดรธานี สาขาเพ็ญ', 1, 682),
    (N'LandOffice', N'TH', N'TH', N'อุดรธานี สาขาเพ็ญ', N'อุดรธานี สาขาเพ็ญ', 1, 682),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขาบ้านผือ', N'จังหวัดอุดรธานี สาขาบ้านผือ', 1, 683),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขาบ้านผือ', N'จังหวัดอุดรธานี สาขาบ้านผือ', 1, 683),
    (N'LandOffice', N'TH', N'EN', N'สาขาบ้านผือ', N'สาขาบ้านผือ', 1, 684),
    (N'LandOffice', N'TH', N'TH', N'สาขาบ้านผือ', N'สาขาบ้านผือ', 1, 684),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 685),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', N'จังหวัดเพชรบูรณ์ สาขาหล่มสัก ส่วนแยกเขาค้อ', 1, 685),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', 1, 687),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', N'จังหวัดพิษณุโลก สาขาพรหมพิราม', 1, 687),
    (N'LandOffice', N'TH', N'EN', N'ที่ว่าการอำเภอกะทู้', N'ที่ว่าการอำเภอกะทู้', 1, 688),
    (N'LandOffice', N'TH', N'TH', N'ที่ว่าการอำเภอกะทู้', N'ที่ว่าการอำเภอกะทู้', 1, 688),
    (N'LandOffice', N'TH', N'EN', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', 1, 689),
    (N'LandOffice', N'TH', N'TH', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', N'ที่ว่าการอำเภอกะทู้ จังหวัดภูเก็ต', 1, 689),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', 1, 690),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', N'จังหวัดสุราษฎร์ธานี สาขาเกาะสมุย ส่วนแยกเกาะพงัน', 1, 690),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดแพร่ สาขาสูงเม่น', N'จังหวัดแพร่ สาขาสูงเม่น', 1, 691),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดแพร่ สาขาสูงเม่น', N'จังหวัดแพร่ สาขาสูงเม่น', 1, 691),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสระบุรี สาขา หนองแค', N'จังหวัดสระบุรี สาขา หนองแค', 1, 692),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสระบุรี สาขา หนองแค', N'จังหวัดสระบุรี สาขา หนองแค', 1, 692),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดบุรีรัมย์ สาขานางรอง', N'จังหวัดบุรีรัมย์ สาขานางรอง', 1, 693),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดบุรีรัมย์ สาขานางรอง', N'จังหวัดบุรีรัมย์ สาขานางรอง', 1, 693),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', 1, 694),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', N'จังหวัดพระนครศรีอยุธยา สาขาเสนา', 1, 694),
    (N'LandOffice', N'TH', N'EN', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', 1, 695),
    (N'LandOffice', N'TH', N'TH', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', N'สำนักงานที่ดินจังหวัดน่าน สาขาเวียงสา', 1, 695),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', 1, 696),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', N'จังหวัดกาญจนบุรี สาขาทองผาภูมิ', 1, 696),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมุกดาหาร', N'จังหวัดมุกดาหาร', 1, 697),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมุกดาหาร', N'จังหวัดมุกดาหาร', 1, 697),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', 1, 698),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', N'จังหวัดศรีสะเกษ สาขาอุทุมพรพิสัย', 1, 698),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', 1, 699),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', N'จังหวัดชัยภูมิ สาขาบำเหน็จรงค์', 1, 699),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดมหาสารคาม สาขาบรบือ', N'จังหวัดมหาสารคาม สาขาบรบือ', 1, 700),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดมหาสารคาม สาขาบรบือ', N'จังหวัดมหาสารคาม สาขาบรบือ', 1, 700),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', 1, 701),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', N'จังหวัดอุบลราชธานี สาขาพิบูลมังสาหาร', 1, 701),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร', N'จังหวัดสกลนคร', 1, 702),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร', N'จังหวัดสกลนคร', 1, 702),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', 1, 703),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', N'จังหวัดเพชรบูรณ์ สาขาวิเชียรบุรี', 1, 703),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดศรีสะเกษ', N'จังหวัดศรีสะเกษ', 1, 704),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดศรีสะเกษ', N'จังหวัดศรีสะเกษ', 1, 704),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', 1, 706),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', N'จังหวัดร้อยเอ็ด สาขาอาจสามารถ', 1, 706),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', 1, 707),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', N'จังหวัดร้อยเอ็ด สาขาศรีสมเด็จ', 1, 707),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', 1, 708),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', N'จังหวัดร้อยเอ็ด สาขาจตุรพักตรพิมาน', 1, 708),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', 1, 709),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', N'จังหวัดประจวบคีรีขันธ์ สาขาปราณบุรี', 1, 709),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', 1, 710),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', N'จังหวัดสกลนคร สาขาสว่างแดนดิน', 1, 710),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', 1, 711),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', N'จังหวัดตาก สาขาแม่สอด ส่วนแยกแม่ระมาด', 1, 711),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปราจีนบุรี', N'จังหวัดปราจีนบุรี', 1, 712),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปราจีนบุรี', N'จังหวัดปราจีนบุรี', 1, 712),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเลย', N'จังหวัดเลย', 1, 713),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเลย', N'จังหวัดเลย', 1, 713),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเพชรบุรี สาขาเขาย้อย', N'จังหวัดเพชรบุรี สาขาเขาย้อย', 1, 715),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเพชรบุรี สาขาเขาย้อย', N'จังหวัดเพชรบุรี สาขาเขาย้อย', 1, 715),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 716),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', N'จังหวัดปราจีนบุรี สาขากบินทร์บุรี', 1, 716),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุบลราชธานี', N'จังหวัดอุบลราชธานี', 1, 718),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุบลราชธานี', N'จังหวัดอุบลราชธานี', 1, 718),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเลย สาขาวังสะพุง', N'จังหวัดเลย สาขาวังสะพุง', 1, 719),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเลย สาขาวังสะพุง', N'จังหวัดเลย สาขาวังสะพุง', 1, 719),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสิงห์บุรี', N'จังหวัดสิงห์บุรี', 1, 720),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสิงห์บุรี', N'จังหวัดสิงห์บุรี', 1, 720),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', 1, 721),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', N'จังหวัดอำนาจเจริญ สาขาหัวตะพาน', 1, 721),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', 1, 722),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', N'จังหวัดสุราษฎร์ธานี สาขาไชยา', 1, 722),
    (N'LandOffice', N'TH', N'EN', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 723),
    (N'LandOffice', N'TH', N'TH', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', N'สุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 723),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 724),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์ ส่วนแยกดอนสัก', 1, 724),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพะเยา', N'จังหวัดพะเยา', 1, 726),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพะเยา', N'จังหวัดพะเยา', 1, 726),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดน่าน สาขาท่าวังผา', N'จังหวัดน่าน สาขาท่าวังผา', 1, 727),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดน่าน สาขาท่าวังผา', N'จังหวัดน่าน สาขาท่าวังผา', 1, 727),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดปัตตานี สาขาสายบุรี', N'จังหวัดปัตตานี สาขาสายบุรี', 1, 728),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดปัตตานี สาขาสายบุรี', N'จังหวัดปัตตานี สาขาสายบุรี', 1, 728),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพิจิตร สาขาโพทะเล', N'จังหวัดพิจิตร สาขาโพทะเล', 1, 729),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพิจิตร สาขาโพทะเล', N'จังหวัดพิจิตร สาขาโพทะเล', 1, 729),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', 1, 730),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', N'จังหวัดสุพรรณบุรี สาขาเดิมบางนางบวช', 1, 730),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอำนาจเจริญ', N'จังหวัดอำนาจเจริญ', 1, 731),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอำนาจเจริญ', N'จังหวัดอำนาจเจริญ', 1, 731),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดขอนแก่น สาขาบ้านฝาง', N'จังหวัดขอนแก่น สาขาบ้านฝาง', 1, 732),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดขอนแก่น สาขาบ้านฝาง', N'จังหวัดขอนแก่น สาขาบ้านฝาง', 1, 732),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพิษณุโลก สาขาบางระกำ', N'จังหวัดพิษณุโลก สาขาบางระกำ', 1, 733),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพิษณุโลก สาขาบางระกำ', N'จังหวัดพิษณุโลก สาขาบางระกำ', 1, 733),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุตรดิตถ์', N'จังหวัดอุตรดิตถ์', 1, 734),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุตรดิตถ์', N'จังหวัดอุตรดิตถ์', 1, 734),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดหนองคาย', N'จังหวัดหนองคาย', 1, 735),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดหนองคาย', N'จังหวัดหนองคาย', 1, 735),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', 1, 736),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', N'จังหวัดสุราษฎร์ธานี สาขากาญจนดิษฐ์', 1, 736),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสงขลา สาขาบางกล่ำ', N'จังหวัดสงขลา สาขาบางกล่ำ', 1, 737),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสงขลา สาขาบางกล่ำ', N'จังหวัดสงขลา สาขาบางกล่ำ', 1, 737),
    (N'LandOffice', N'TH', N'EN', N'สาขาแก่งคอย', N'สาขาแก่งคอย', 1, 738),
    (N'LandOffice', N'TH', N'TH', N'สาขาแก่งคอย', N'สาขาแก่งคอย', 1, 738),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสระบุรี', N'จังหวัดสระบุรี', 1, 739),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสระบุรี', N'จังหวัดสระบุรี', 1, 739),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดพังงา สาขาท้ายเหมือง', N'จังหวัดพังงา สาขาท้ายเหมือง', 1, 740),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดพังงา สาขาท้ายเหมือง', N'จังหวัดพังงา สาขาท้ายเหมือง', 1, 740),
    (N'LandOffice', N'TH', N'EN', N'สุราษฎร์ธานี สาขาพุนพิน', N'สุราษฎร์ธานี สาขาพุนพิน', 1, 741),
    (N'LandOffice', N'TH', N'TH', N'สุราษฎร์ธานี สาขาพุนพิน', N'สุราษฎร์ธานี สาขาพุนพิน', 1, 741),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดอุดรธานี สาขาศรีธาตุ', N'จังหวัดอุดรธานี สาขาศรีธาตุ', 1, 742),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดอุดรธานี สาขาศรีธาตุ', N'จังหวัดอุดรธานี สาขาศรีธาตุ', 1, 742),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี', N'จังหวัดสุราษฎร์ธานี', 1, 743),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี', N'จังหวัดสุราษฎร์ธานี', 1, 743),
    (N'LandOffice', N'TH', N'EN', N'สาขาพรรณานิคม', N'สาขาพรรณานิคม', 1, 744),
    (N'LandOffice', N'TH', N'TH', N'สาขาพรรณานิคม', N'สาขาพรรณานิคม', 1, 744),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร สาขาพรรณานิคม', N'จังหวัดสกลนคร สาขาพรรณานิคม', 1, 745),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร สาขาพรรณานิคม', N'จังหวัดสกลนคร สาขาพรรณานิคม', 1, 745),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุรินทร์ สาขาปราสาท', N'จังหวัดสุรินทร์ สาขาปราสาท', 1, 746),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุรินทร์ สาขาปราสาท', N'จังหวัดสุรินทร์ สาขาปราสาท', 1, 746),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุรินทร์ สาขาสังขะ', N'จังหวัดสุรินทร์ สาขาสังขะ', 1, 747),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุรินทร์ สาขาสังขะ', N'จังหวัดสุรินทร์ สาขาสังขะ', 1, 747),
    (N'LandOffice', N'TH', N'EN', N'ศรีสะเกษ สาขาขุนหาญ', N'ศรีสะเกษ สาขาขุนหาญ', 1, 748),
    (N'LandOffice', N'TH', N'TH', N'ศรีสะเกษ สาขาขุนหาญ', N'ศรีสะเกษ สาขาขุนหาญ', 1, 748),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', 1, 750),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', N'จังหวัดศรีสะเกษ สาขาขุนหาญ', 1, 750),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร สาขาวานรนิวาส', N'จังหวัดสกลนคร สาขาวานรนิวาส', 1, 751),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร สาขาวานรนิวาส', N'จังหวัดสกลนคร สาขาวานรนิวาส', 1, 751),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดราชบุรี สาขาบ้านโป่ง', N'จังหวัดราชบุรี สาขาบ้านโป่ง', 1, 752),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดราชบุรี สาขาบ้านโป่ง', N'จังหวัดราชบุรี สาขาบ้านโป่ง', 1, 752),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', 1, 753),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', N'จังหวัดร้อยเอ็ด สาขาพนมไพร', 1, 753),
    (N'LandOffice', N'TH', N'EN', N'นครปฐม (นครชัยศรี)', N'นครปฐม (นครชัยศรี)', 1, 754),
    (N'LandOffice', N'TH', N'TH', N'นครปฐม (นครชัยศรี)', N'นครปฐม (นครชัยศรี)', 1, 754),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', 1, 755),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุบ', 1, 755),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', 1, 756),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', N'จังหวัดสุราษฎร์ธานี สาขาบ้านตาขุน', 1, 756),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดสกลนคร สาขาอากาศอำนวย', N'จังหวัดสกลนคร สาขาอากาศอำนวย', 1, 757),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดสกลนคร สาขาอากาศอำนวย', N'จังหวัดสกลนคร สาขาอากาศอำนวย', 1, 757),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดลำปาง', N'จังหวัดลำปาง', 1, 758),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดลำปาง', N'จังหวัดลำปาง', 1, 758),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดกระบี่ สาขาคลองท่อม', N'จังหวัดกระบี่ สาขาคลองท่อม', 1, 759),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดกระบี่ สาขาคลองท่อม', N'จังหวัดกระบี่ สาขาคลองท่อม', 1, 759),
    (N'LandOffice', N'TH', N'EN', N'จังหวัดเชียงราย สาขาแม่สรวย', N'จังหวัดเชียงราย สาขาแม่สรวย', 1, 760),
    (N'LandOffice', N'TH', N'TH', N'จังหวัดเชียงราย สาขาแม่สรวย', N'จังหวัดเชียงราย สาขาแม่สรวย', 1, 760),
    (N'LandOffice', N'TH', N'EN', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', 1, 761),
    (N'LandOffice', N'TH', N'TH', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', N'สำนักงานที่ดินจังหวัดสงขลา สาขาเทพา', 1, 761);
GO

-- ----------------------------------------
-- Group: LandOfficeDesc (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandOfficeDesc', N'TH', N'EN', N'Description', N'Description', 1, 381),
    (N'LandOfficeDesc', N'TH', N'TH', N'Description', N'Description', 1, 381);
GO

-- ----------------------------------------
-- Group: LandShape (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandShape', N'TH', N'EN', N'01', N'A shape with soil, space is appropriate for development made a very beneficial.', 1, 1),
    (N'LandShape', N'TH', N'TH', N'01', N'A shape with soil, space is appropriate for development made a very beneficial.', 1, 1),
    (N'LandShape', N'TH', N'EN', N'02', N'A shape with soil, space is appropriate for development benefit and medium.', 1, 2),
    (N'LandShape', N'TH', N'TH', N'02', N'A shape with soil, space is appropriate for development benefit and medium.', 1, 2),
    (N'LandShape', N'TH', N'EN', N'03', N'A shape with soil, space, there are no appropriate development benefits.', 1, 3),
    (N'LandShape', N'TH', N'TH', N'03', N'A shape with soil, space, there are no appropriate development benefits.', 1, 3),
    (N'LandShape', N'TH', N'EN', N'04', N'Square', 1, 4),
    (N'LandShape', N'TH', N'TH', N'04', N'Square', 1, 4),
    (N'LandShape', N'TH', N'EN', N'05', N'Retangle', 1, 5),
    (N'LandShape', N'TH', N'TH', N'05', N'Retangle', 1, 5),
    (N'LandShape', N'TH', N'EN', N'06', N'Trapezium', 1, 6),
    (N'LandShape', N'TH', N'TH', N'06', N'Trapezium', 1, 6),
    (N'LandShape', N'TH', N'EN', N'07', N'Pennant triangle', 1, 7),
    (N'LandShape', N'TH', N'TH', N'07', N'Pennant triangle', 1, 7),
    (N'LandShape', N'TH', N'EN', N'08', N'Polygon', 1, 8),
    (N'LandShape', N'TH', N'TH', N'08', N'Polygon', 1, 8);
GO

-- ----------------------------------------
-- Group: LandUse (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandUse', N'TH', N'EN', N'01', N'Residence', 1, 1),
    (N'LandUse', N'TH', N'TH', N'01', N'ที่อยู่อาศัย', 1, 1),
    (N'LandUse', N'TH', N'EN', N'02', N'Agriculture', 1, 2),
    (N'LandUse', N'TH', N'TH', N'02', N'เกษตรกรรม', 1, 2),
    (N'LandUse', N'TH', N'EN', N'03', N'Commerce', 1, 3),
    (N'LandUse', N'TH', N'TH', N'03', N'พาณิชยกรรม', 1, 3),
    (N'LandUse', N'TH', N'EN', N'04', N'Industry', 1, 4),
    (N'LandUse', N'TH', N'TH', N'04', N'อุตสาหกรรม', 1, 4),
    (N'LandUse', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'LandUse', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: LandValueGrowthRate (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LandValueGrowthRate', N'TH', N'EN', N'01', N'Frequency', 1, 1),
    (N'LandValueGrowthRate', N'TH', N'TH', N'01', N'Frequency', 1, 1),
    (N'LandValueGrowthRate', N'TH', N'EN', N'02', N'Period', 1, 2),
    (N'LandValueGrowthRate', N'TH', N'TH', N'02', N'Period', 1, 2);
GO

-- ----------------------------------------
-- Group: Landfill (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Landfill', N'TH', N'EN', N'01', N'Empty Land', 1, 1),
    (N'Landfill', N'TH', N'TH', N'01', N'ที่ดินว่างเปล่า', 1, 1),
    (N'Landfill', N'TH', N'EN', N'02', N'Filled', 1, 2),
    (N'Landfill', N'TH', N'TH', N'02', N'ถมแล้ว', 1, 2),
    (N'Landfill', N'TH', N'EN', N'03', N'Not Fill Yet', 1, 3),
    (N'Landfill', N'TH', N'TH', N'03', N'ยังไม่ถม', 1, 3),
    (N'Landfill', N'TH', N'EN', N'04', N'Partially Filled', 1, 4),
    (N'Landfill', N'TH', N'TH', N'04', N'ถมบางส่วน', 1, 4),
    (N'Landfill', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'Landfill', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: Layout (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Layout', N'TH', N'EN', N'01', N'1', 1, 1),
    (N'Layout', N'TH', N'TH', N'01', N'1', 1, 1),
    (N'Layout', N'TH', N'EN', N'02', N'2', 1, 2),
    (N'Layout', N'TH', N'TH', N'02', N'2', 1, 2),
    (N'Layout', N'TH', N'EN', N'03', N'3', 1, 3),
    (N'Layout', N'TH', N'TH', N'03', N'3', 1, 3),
    (N'Layout', N'TH', N'EN', N'04', N'4', 1, 4),
    (N'Layout', N'TH', N'TH', N'04', N'4', 1, 4);
GO

-- ----------------------------------------
-- Group: Limitation (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Limitation', N'TH', N'EN', N'01', N'Is Expropriate', 1, 1),
    (N'Limitation', N'TH', N'TH', N'01', N'อยู่ในระวางเวนคืน ตาม พรฎ/พรบ : ระบุ', 1, 1),
    (N'Limitation', N'TH', N'EN', N'02', N'In Line Expropriate', 1, 2),
    (N'Limitation', N'TH', N'TH', N'02', N'อยู่ในแนวเวนคืน ตาม พรฎ/พรบ : ระบุ', 1, 2),
    (N'Limitation', N'TH', N'EN', N'03', N'Is Encroached', 1, 3),
    (N'Limitation', N'TH', N'TH', N'03', N'ถูกรุกล้ำ / ใช้เพื่อบุคคลอื่น/ตัดเนื้อที่ประเมินเนื่องจากสาเหตุอื่น', 1, 3),
    (N'Limitation', N'TH', N'EN', N'04', N'Electricity Over 100M', 1, 4),
    (N'Limitation', N'TH', N'TH', N'04', N'จุดสิ้นสุดสาธารณูปโภคไฟฟ้าถาวรอยู่ห่างหลักประกันไปตามแนวถนน/ซอย', 1, 4),
    (N'Limitation', N'TH', N'EN', N'05', N'Is Landlocked', 1, 5),
    (N'Limitation', N'TH', N'TH', N'05', N'รูปร่างลักษณะที่ดินหลักประกันไม่สามารถนำรถยนต์เข้าไปในที่ดินได้', 1, 5),
    (N'Limitation', N'TH', N'EN', N'06', N'In Forest Boundary', 1, 6),
    (N'Limitation', N'TH', N'TH', N'06', N'อยู่ในเขตป่าฯ / อยู่ในเขต สปก. / อุทยานฯ', 1, 6);
GO

-- ----------------------------------------
-- Group: Liquidity (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Liquidity', N'TH', N'EN', N'01', N'High Volume of Transactions', 1, 1),
    (N'Liquidity', N'TH', N'TH', N'01', N'High Volume of Transactions', 1, 1),
    (N'Liquidity', N'TH', N'EN', N'02', N'Moderate Volume of Transactions', 1, 2),
    (N'Liquidity', N'TH', N'TH', N'02', N'Moderate Volume of Transactions', 1, 2),
    (N'Liquidity', N'TH', N'EN', N'03', N'Low Volume of Transactions', 1, 3),
    (N'Liquidity', N'TH', N'TH', N'03', N'Low Volume of Transactions', 1, 3),
    (N'Liquidity', N'TH', N'EN', N'04', N'No Transactions', 1, 4),
    (N'Liquidity', N'TH', N'TH', N'04', N'No Transactions', 1, 4);
GO

-- ----------------------------------------
-- Group: LocalDev (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LocalDev', N'TH', N'EN', N'01', N'Primarily Commercial Development', 1, 1),
    (N'LocalDev', N'TH', N'TH', N'01', N'Primarily Commercial Development', 1, 1),
    (N'LocalDev', N'TH', N'EN', N'02', N'Mixed Commercial and Residential or Industrial Development', 1, 2),
    (N'LocalDev', N'TH', N'TH', N'02', N'Mixed Commercial and Residential or Industrial Development', 1, 2),
    (N'LocalDev', N'TH', N'EN', N'03', N'Primarily Residential Development', 1, 3),
    (N'LocalDev', N'TH', N'TH', N'03', N'Primarily Residential Development', 1, 3),
    (N'LocalDev', N'TH', N'EN', N'04', N'Primarily Agricultural Development', 1, 4),
    (N'LocalDev', N'TH', N'TH', N'04', N'Primarily Agricultural Development', 1, 4),
    (N'LocalDev', N'TH', N'EN', N'05', N'No Development Yet, Mostly Vacant Land', 1, 5),
    (N'LocalDev', N'TH', N'TH', N'05', N'No Development Yet, Mostly Vacant Land', 1, 5);
GO

-- ----------------------------------------
-- Group: Location (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Location', N'TH', N'EN', N'01', N'Sanitary Zone', 1, 1),
    (N'Location', N'TH', N'TH', N'01', N'ในเขตสุขาภิบาล', 1, 1),
    (N'Location', N'TH', N'EN', N'02', N'Municipality', 1, 2),
    (N'Location', N'TH', N'TH', N'02', N'ในเขตเทศบาล', 1, 2),
    (N'Location', N'TH', N'EN', N'03', N'Subdistrict Administrative Organization Area', 1, 3),
    (N'Location', N'TH', N'TH', N'03', N'เขต อบต.', 1, 3),
    (N'Location', N'TH', N'EN', N'04', N'Bangkok Metropolitan Area', 1, 4),
    (N'Location', N'TH', N'TH', N'04', N'เขต กทม.', 1, 4),
    (N'Location', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'Location', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: LocationAssumptionMethod (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LocationAssumptionMethod', N'TH', N'EN', N'01', N'Method 1: Adjust Price per Sq.M.', 1, 1),
    (N'LocationAssumptionMethod', N'TH', N'TH', N'01', N'Method 1: Adjust Price per Sq.M.', 1, 1),
    (N'LocationAssumptionMethod', N'TH', N'EN', N'02', N'Method 2: Adjust Price by a Percentage of the Standard Room Price (retrieve from Price Analysis)', 1, 2),
    (N'LocationAssumptionMethod', N'TH', N'TH', N'02', N'Method 2: Adjust Price by a Percentage of the Standard Room Price (retrieve from Price Analysis)', 1, 2),
    (N'LocationAssumptionMethod', N'TH', N'EN', N'03', N'Method 3: Adjust Price by Lump Sum Amount', 1, 3),
    (N'LocationAssumptionMethod', N'TH', N'TH', N'03', N'Method 3: Adjust Price by Lump Sum Amount', 1, 3);
GO

-- ----------------------------------------
-- Group: LocationView (EN=11, TH=11)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'LocationView', N'TH', N'EN', N'01', N'Pool view', 1, 1),
    (N'LocationView', N'TH', N'TH', N'01', N'Pool view', 1, 1),
    (N'LocationView', N'TH', N'EN', N'02', N'River view', 1, 2),
    (N'LocationView', N'TH', N'TH', N'02', N'River view', 1, 2),
    (N'LocationView', N'TH', N'EN', N'03', N'Clubhouse view', 1, 3),
    (N'LocationView', N'TH', N'TH', N'03', N'Clubhouse view', 1, 3),
    (N'LocationView', N'TH', N'EN', N'04', N'Near/adjacent to elevator', 1, 4),
    (N'LocationView', N'TH', N'TH', N'04', N'Near/adjacent to elevator', 1, 4),
    (N'LocationView', N'TH', N'EN', N'05', N'Near/adjacent to trach room', 1, 5),
    (N'LocationView', N'TH', N'TH', N'05', N'Near/adjacent to trach room', 1, 5),
    (N'LocationView', N'TH', N'EN', N'06', N'Corner room', 1, 6),
    (N'LocationView', N'TH', N'TH', N'06', N'Corner room', 1, 6),
    (N'LocationView', N'TH', N'EN', N'07', N'Garden view', 1, 7),
    (N'LocationView', N'TH', N'TH', N'07', N'Garden view', 1, 7),
    (N'LocationView', N'TH', N'EN', N'08', N'City view', 1, 8),
    (N'LocationView', N'TH', N'TH', N'08', N'City view', 1, 8),
    (N'LocationView', N'TH', N'EN', N'09', N'Sea view', 1, 9),
    (N'LocationView', N'TH', N'TH', N'09', N'Sea view', 1, 9),
    (N'LocationView', N'TH', N'EN', N'10', N'Mountain view', 1, 10),
    (N'LocationView', N'TH', N'TH', N'10', N'Mountain view', 1, 10),
    (N'LocationView', N'TH', N'EN', N'11', N'Central floor (or central area)', 1, 11),
    (N'LocationView', N'TH', N'TH', N'11', N'Central floor (or central area)', 1, 11);
GO

-- ----------------------------------------
-- Group: MachineStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MachineStatus', N'TH', N'EN', N'1', N'Installed', 1, 1),
    (N'MachineStatus', N'TH', N'TH', N'1', N'ติดตั้ง', 1, 1),
    (N'MachineStatus', N'TH', N'EN', N'2', N'Under Procurement', 1, 2),
    (N'MachineStatus', N'TH', N'TH', N'2', N'อยู่ระหว่างการจัดซื้อ', 1, 2);
GO

-- ----------------------------------------
-- Group: MachineType (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MachineType', N'TH', N'EN', N'1', N'Construction & Heavy Machinery', 1, 1),
    (N'MachineType', N'TH', N'TH', N'1', N'ก่อสร้างและเครื่องจักรกลหนัก', 1, 1),
    (N'MachineType', N'TH', N'EN', N'2', N'Industrial / Manufacturing Machinery', 1, 2),
    (N'MachineType', N'TH', N'TH', N'2', N'อุตสหกรรม / เครื่องจักรการผลิด', 1, 2),
    (N'MachineType', N'TH', N'EN', N'3', N'Energy / Utilities Machinery', 1, 3),
    (N'MachineType', N'TH', N'TH', N'3', N'พลังงาน/ เครื่องจักรสาธารณูปโภค', 1, 3);
GO

-- ----------------------------------------
-- Group: MeasurementUnits (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MeasurementUnits', N'TH', N'EN', N'01', N'Baht/Sq.Wa', 1, 1),
    (N'MeasurementUnits', N'TH', N'TH', N'01', N'Baht/Sq.Wa', 1, 1),
    (N'MeasurementUnits', N'TH', N'EN', N'02', N'Baht/Sq. Meter', 1, 2),
    (N'MeasurementUnits', N'TH', N'TH', N'02', N'Baht/Sq. Meter', 1, 2),
    (N'MeasurementUnits', N'TH', N'EN', N'03', N'Baht/Unit', 1, 3),
    (N'MeasurementUnits', N'TH', N'TH', N'03', N'Baht/Unit', 1, 3),
    (N'MeasurementUnits', N'TH', N'EN', N'04', N'Baht/Day', 1, 4),
    (N'MeasurementUnits', N'TH', N'TH', N'04', N'Baht/Day', 1, 4),
    (N'MeasurementUnits', N'TH', N'EN', N'05', N'Baht/Month', 1, 5),
    (N'MeasurementUnits', N'TH', N'TH', N'05', N'Baht/Month', 1, 5),
    (N'MeasurementUnits', N'TH', N'EN', N'06', N'Baht/Year', 1, 6),
    (N'MeasurementUnits', N'TH', N'TH', N'06', N'Baht/Year', 1, 6);
GO

-- ----------------------------------------
-- Group: MeetingPosition (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MeetingPosition', N'TH', N'EN', N'01', N'Chairman', 1, 1),
    (N'MeetingPosition', N'TH', N'TH', N'01', N'Chairman', 1, 1),
    (N'MeetingPosition', N'TH', N'EN', N'02', N'Director', 1, 2),
    (N'MeetingPosition', N'TH', N'TH', N'02', N'Director', 1, 2),
    (N'MeetingPosition', N'TH', N'EN', N'03', N'Secretary', 1, 3),
    (N'MeetingPosition', N'TH', N'TH', N'03', N'Secretary', 1, 3);
GO

-- ----------------------------------------
-- Group: MeetingStatus (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MeetingStatus', N'TH', N'EN', N'01', N'New', 1, 1),
    (N'MeetingStatus', N'TH', N'TH', N'01', N'New', 1, 1),
    (N'MeetingStatus', N'TH', N'EN', N'02', N'Invitation Sent', 1, 2),
    (N'MeetingStatus', N'TH', N'TH', N'02', N'Invitation Sent', 1, 2),
    (N'MeetingStatus', N'TH', N'EN', N'03', N'Editing', 1, 3),
    (N'MeetingStatus', N'TH', N'TH', N'03', N'Editing', 1, 3),
    (N'MeetingStatus', N'TH', N'EN', N'04', N'In Progress', 1, 4),
    (N'MeetingStatus', N'TH', N'TH', N'04', N'In Progress', 1, 4),
    (N'MeetingStatus', N'TH', N'EN', N'05', N'Rollback Follow Up', 1, 5),
    (N'MeetingStatus', N'TH', N'TH', N'05', N'Rollback Follow Up', 1, 5),
    (N'MeetingStatus', N'TH', N'EN', N'06', N'Closed', 1, 6),
    (N'MeetingStatus', N'TH', N'TH', N'06', N'Closed', 1, 6),
    (N'MeetingStatus', N'TH', N'EN', N'07', N'Cancelled', 1, 7),
    (N'MeetingStatus', N'TH', N'TH', N'07', N'Cancelled', 1, 7);
GO

-- ----------------------------------------
-- Group: Meeting_MoreOptions (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Meeting_MoreOptions', N'TH', N'EN', N'01', N'Edit', 1, 1),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'01', N'Edit', 1, 1),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'02', N'Cut-Off Time', 1, 2),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'02', N'Cut-Off Time', 1, 2),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'03', N'Send Email', 1, 3),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'03', N'Send Email', 1, 3),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'04', N'Cancel Meeting', 1, 4),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'04', N'Cancel Meeting', 1, 4),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'05', N'Report', 1, 5),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'05', N'Report', 1, 5),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'06', N'View', 1, 6),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'06', N'View', 1, 6),
    (N'Meeting_MoreOptions', N'TH', N'EN', N'07', N'Release / Route Back', 1, 7),
    (N'Meeting_MoreOptions', N'TH', N'TH', N'07', N'Release / Route Back', 1, 7);
GO

-- ----------------------------------------
-- Group: MissedOutOnTheSurvey (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'MissedOutOnTheSurvey', N'TH', N'EN', N'01', N'Yes', 1, 1),
    (N'MissedOutOnTheSurvey', N'TH', N'TH', N'01', N'Yes', 1, 1),
    (N'MissedOutOnTheSurvey', N'TH', N'EN', N'02', N'No', 1, 2),
    (N'MissedOutOnTheSurvey', N'TH', N'TH', N'02', N'No', 1, 2);
GO

-- ----------------------------------------
-- Group: Movement (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Movement', N'TH', N'EN', N'F', N'Forward', 1, 1),
    (N'Movement', N'TH', N'TH', N'F', N'Forward', 1, 1),
    (N'Movement', N'TH', N'EN', N'B', N'Backward', 1, 2),
    (N'Movement', N'TH', N'TH', N'B', N'Backward', 1, 2);
GO

-- ----------------------------------------
-- Group: NR_Decision (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'NR_Decision', N'TH', N'EN', N'01', N'Proceed', 1, 1),
    (N'NR_Decision', N'TH', N'TH', N'01', N'Proceed', 1, 1);
GO

-- ----------------------------------------
-- Group: NoHouseNumber (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'NoHouseNumber', N'TH', N'EN', N'01', N'Not Installed', 1, 1),
    (N'NoHouseNumber', N'TH', N'TH', N'01', N'ยังไม่ติดเลขที่บ้าน', 1, 1),
    (N'NoHouseNumber', N'TH', N'EN', N'02', N'Not Request', 1, 2),
    (N'NoHouseNumber', N'TH', N'TH', N'02', N'ยังไม่ขอเลขที่บ้าน', 1, 2);
GO

-- ----------------------------------------
-- Group: Obligation (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Obligation', N'TH', N'EN', N'01', N'No Obligations', 1, 1),
    (N'Obligation', N'TH', N'TH', N'01', N'No Obligations', 1, 1),
    (N'Obligation', N'TH', N'EN', N'02', N'Mortgage as Security', 1, 2),
    (N'Obligation', N'TH', N'TH', N'02', N'Mortgage as Security', 1, 2),
    (N'Obligation', N'TH', N'EN', N'03', N'Attached to lease contract', 1, 3),
    (N'Obligation', N'TH', N'TH', N'03', N'Attached to lease contract', 1, 3),
    (N'Obligation', N'TH', N'EN', N'04', N'Usufruct', 1, 4),
    (N'Obligation', N'TH', N'TH', N'04', N'Usufruct', 1, 4),
    (N'Obligation', N'TH', N'EN', N'05', N'Superficies', 1, 5),
    (N'Obligation', N'TH', N'TH', N'05', N'Superficies', 1, 5),
    (N'Obligation', N'TH', N'EN', N'99', N'Other', 1, 6),
    (N'Obligation', N'TH', N'TH', N'99', N'Other', 1, 6);
GO

-- ----------------------------------------
-- Group: Old Description (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Old Description', N'TH', N'EN', N'New Description', N'Migration only 2,6,8', 1, 7),
    (N'Old Description', N'TH', N'TH', N'New Description', N'Migration only 2,6,8', 1, 7);
GO

-- ----------------------------------------
-- Group: OtherExpensesinProject (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'OtherExpensesinProject', N'TH', N'EN', N'01', N'Other Expenses in the Project Expenses Section', 1, 1),
    (N'OtherExpensesinProject', N'TH', N'TH', N'01', N'Other Expenses in the Project Expenses Section', 1, 1);
GO

-- ----------------------------------------
-- Group: OtherFeeStatus (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'OtherFeeStatus', N'TH', N'EN', N'P', N'Pending Approval', 1, 1),
    (N'OtherFeeStatus', N'TH', N'TH', N'P', N'Pending Approval', 1, 1),
    (N'OtherFeeStatus', N'TH', N'EN', N'A', N'Approved', 1, 2),
    (N'OtherFeeStatus', N'TH', N'TH', N'A', N'Approved', 1, 2),
    (N'OtherFeeStatus', N'TH', N'EN', N'R', N'Rejected', 1, 3),
    (N'OtherFeeStatus', N'TH', N'TH', N'R', N'Rejected', 1, 3);
GO

-- ----------------------------------------
-- Group: PlotLocation (EN=18, TH=18)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'PlotLocation', N'TH', N'EN', N'01', N'Show House', 1, 1),
    (N'PlotLocation', N'TH', N'TH', N'01', N'บ้านตัวอย่าง', 1, 1),
    (N'PlotLocation', N'TH', N'EN', N'02', N'Corner Plot', 1, 2),
    (N'PlotLocation', N'TH', N'TH', N'02', N'แปลงหัวมุม', 1, 2),
    (N'PlotLocation', N'TH', N'EN', N'03', N'Near Clubhouse', 1, 3),
    (N'PlotLocation', N'TH', N'TH', N'03', N'ใกล้สโมสร', 1, 3),
    (N'PlotLocation', N'TH', N'EN', N'04', N'House not Facing Another', 1, 4),
    (N'PlotLocation', N'TH', N'TH', N'04', N'บ้านไม่หันหลังชนกับใคร', 1, 4),
    (N'PlotLocation', N'TH', N'EN', N'05', N'Edge Plot', 1, 5),
    (N'PlotLocation', N'TH', N'TH', N'05', N'แปลงริม', 1, 5),
    (N'PlotLocation', N'TH', N'EN', N'06', N'Corner with Window', 1, 6),
    (N'PlotLocation', N'TH', N'TH', N'06', N'มุมมีหน้าต่าง', 1, 6),
    (N'PlotLocation', N'TH', N'EN', N'07', N'Corner without Window', 1, 7),
    (N'PlotLocation', N'TH', N'TH', N'07', N'มุมไม่มีหน้าต่าง', 1, 7),
    (N'PlotLocation', N'TH', N'EN', N'08', N'Corner with U-Turn', 1, 8),
    (N'PlotLocation', N'TH', N'TH', N'08', N'มุมที่กลับรถ', 1, 8),
    (N'PlotLocation', N'TH', N'EN', N'09', N'Adjacent to Main Road', 1, 9),
    (N'PlotLocation', N'TH', N'TH', N'09', N'ติดถนนเมน', 1, 9),
    (N'PlotLocation', N'TH', N'EN', N'10', N'Adjacent to Park / Near Park / Opposite Park', 1, 10),
    (N'PlotLocation', N'TH', N'TH', N'10', N'ติดสวน/ใกล้สวน/ตรงข้ามสวน', 1, 10),
    (N'PlotLocation', N'TH', N'EN', N'11', N'Adjacent to Clubhouse', 1, 11),
    (N'PlotLocation', N'TH', N'TH', N'11', N'ติดสโมสร', 1, 11),
    (N'PlotLocation', N'TH', N'EN', N'12', N'Adjacent to Lake / Opposite Lake', 1, 12),
    (N'PlotLocation', N'TH', N'TH', N'12', N'ติดทะเลสาบ/ตรงข้ามทะเลทะเลสาบ', 1, 12),
    (N'PlotLocation', N'TH', N'EN', N'13', N'Front Zone of the Project', 1, 13),
    (N'PlotLocation', N'TH', N'TH', N'13', N'โซนหน้าโครงการ', 1, 13),
    (N'PlotLocation', N'TH', N'EN', N'14', N'House Not Facing Anyone', 1, 14),
    (N'PlotLocation', N'TH', N'TH', N'14', N'หน้าบ้านไม่ติดใคร', 1, 14),
    (N'PlotLocation', N'TH', N'EN', N'15', N'Private Zone', 1, 15),
    (N'PlotLocation', N'TH', N'TH', N'15', N'โซนส่วนตัว', 1, 15),
    (N'PlotLocation', N'TH', N'EN', N'16', N'Adjacent to / Near Transformer / High Voltage Power Lines', 1, 16),
    (N'PlotLocation', N'TH', N'TH', N'16', N'ติด/ใกล้หม้อแปลงไฟฟ้า/แนวสายไฟฟ้าแรงสูง', 1, 16),
    (N'PlotLocation', N'TH', N'EN', N'17', N'Adjacent to Sewage Treatment Plant / Garbage Disposal Area', 1, 17),
    (N'PlotLocation', N'TH', N'TH', N'17', N'ติดบ่อบำบัดน้ำเสีย/ที่ทิ้งขยะ', 1, 17),
    (N'PlotLocation', N'TH', N'EN', N'18', N'Middle Plot', 1, 18),
    (N'PlotLocation', N'TH', N'TH', N'18', N'แปลงกลาง', 1, 18),
    (N'PlotLocation', N'TH', N'EN', N'99', N'Other', 1, 19),
    (N'PlotLocation', N'TH', N'TH', N'99', N'อื่นๆ', 1, 19);
GO

-- ----------------------------------------
-- Group: PriceAnalysisApproach (EN=4, TH=4)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'PriceAnalysisApproach', N'TH', N'EN', N'01', N'Market Approach', 1, 1),
    (N'PriceAnalysisApproach', N'TH', N'TH', N'01', N'Market Approach', 1, 1),
    (N'PriceAnalysisApproach', N'TH', N'EN', N'02', N'Cost Approach', 1, 2),
    (N'PriceAnalysisApproach', N'TH', N'TH', N'02', N'Cost Approach', 1, 2),
    (N'PriceAnalysisApproach', N'TH', N'EN', N'03', N'Income Approach', 1, 3),
    (N'PriceAnalysisApproach', N'TH', N'TH', N'03', N'Income Approach', 1, 3),
    (N'PriceAnalysisApproach', N'TH', N'EN', N'04', N'Residual', 1, 4),
    (N'PriceAnalysisApproach', N'TH', N'TH', N'04', N'Residual', 1, 4);
GO

-- ----------------------------------------
-- Group: Priority (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Priority', N'TH', N'EN', N'HIGH', N'HIGH', 1, 1),
    (N'Priority', N'TH', N'TH', N'HIGH', N'ด่วน', 1, 1),
    (N'Priority', N'TH', N'EN', N'NORMAL', N'NORMAL', 1, 2),
    (N'Priority', N'TH', N'TH', N'NORMAL', N'ปรกติ', 1, 2);
GO

-- ----------------------------------------
-- Group: Profit Rent_Market Rental Fee (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Profit Rent_Market Rental Fee', N'TH', N'EN', N'01', N'Frequency', 1, 1),
    (N'Profit Rent_Market Rental Fee', N'TH', N'TH', N'01', N'Frequency', 1, 1),
    (N'Profit Rent_Market Rental Fee', N'TH', N'EN', N'02', N'Period', 1, 2),
    (N'Profit Rent_Market Rental Fee', N'TH', N'TH', N'02', N'Period', 1, 2);
GO

-- ----------------------------------------
-- Group: ProjectLand_Eviction (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ProjectLand_Eviction', N'TH', N'EN', N'01', N'Permanent Electricity', 1, 1),
    (N'ProjectLand_Eviction', N'TH', N'TH', N'01', N'Permanent Electricity', 1, 1),
    (N'ProjectLand_Eviction', N'TH', N'EN', N'02', N'Subway line', 1, 2),
    (N'ProjectLand_Eviction', N'TH', N'TH', N'02', N'Subway line', 1, 2),
    (N'ProjectLand_Eviction', N'TH', N'EN', N'03', N'Other', 1, 3),
    (N'ProjectLand_Eviction', N'TH', N'TH', N'03', N'Other', 1, 3);
GO

-- ----------------------------------------
-- Group: ProjectType (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ProjectType', N'TH', N'EN', N'01', N'Condominium', 1, 1),
    (N'ProjectType', N'TH', N'TH', N'01', N'คอนโด', 1, 1),
    (N'ProjectType', N'TH', N'EN', N'02', N'Land and Building', 1, 2),
    (N'ProjectType', N'TH', N'TH', N'02', N'ที่ดินและสิ่งปลูกสร้าง', 1, 2),
    (N'ProjectType', N'TH', N'EN', N'03', N'Land and Building (Construction)', 1, 3),
    (N'ProjectType', N'TH', N'TH', N'03', N'ที่ี่ดินและสิ่งปลูกสร้างกำลังก่อสร้าง', 1, 3);
GO

-- ----------------------------------------
-- Group: PropertyType (EN=11, TH=11)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'PropertyType', N'TH', N'EN', N'B', N'Building', 1, 1),
    (N'PropertyType', N'TH', N'TH', N'B', N'สิ่งปลูกสร้าง', 1, 1),
    (N'PropertyType', N'TH', N'EN', N'LB', N'Land and Building', 1, 2),
    (N'PropertyType', N'TH', N'TH', N'LB', N'ที่ดินและสิ่งปลูกสร้าง', 1, 2),
    (N'PropertyType', N'TH', N'EN', N'U', N'Condominium', 1, 3),
    (N'PropertyType', N'TH', N'TH', N'U', N'คอนโด', 1, 3),
    (N'PropertyType', N'TH', N'EN', N'L', N'Land', 1, 4),
    (N'PropertyType', N'TH', N'TH', N'L', N'ที่ดินเปล่า', 1, 4),
    (N'PropertyType', N'TH', N'EN', N'MAC', N'Machinery', 1, 5),
    (N'PropertyType', N'TH', N'TH', N'MAC', N'เครื่องจักร', 1, 5),
    (N'PropertyType', N'TH', N'EN', N'VEH', N'Vehicle', 1, 6),
    (N'PropertyType', N'TH', N'TH', N'VEH', N'รถยนต์', 1, 6),
    (N'PropertyType', N'TH', N'EN', N'VES', N'Vessel', 1, 7),
    (N'PropertyType', N'TH', N'TH', N'VES', N'เรือ', 1, 7),
    (N'PropertyType', N'TH', N'EN', N'LS', N'Lease Agreement (Land and Building)', 1, 8),
    (N'PropertyType', N'TH', N'TH', N'LS', N'สิทธิการเช่าที่ดินและสิ่งปลูกสร้าง', 1, 8),
    (N'PropertyType', N'TH', N'EN', N'LSL', N'Lease Agreement (Land )', 1, 9),
    (N'PropertyType', N'TH', N'TH', N'LSL', N'สิทธิการเช่าที่ดิน', 1, 9),
    (N'PropertyType', N'TH', N'EN', N'LSB', N'Lease Agreement (Building)', 1, 10),
    (N'PropertyType', N'TH', N'TH', N'LSB', N'สิทธิการเช่าสิ่งปลูกสร้าง', 1, 10),
    (N'PropertyType', N'TH', N'EN', N'LSU', N'Lease Agreement Condo', 1, 11),
    (N'PropertyType', N'TH', N'TH', N'LSU', N'สิทธิการเช่าคอนโด', 1, 11);
GO

-- ----------------------------------------
-- Group: PublicUtility (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'PublicUtility', N'TH', N'EN', N'01', N'Permanent Electricity', 1, 1),
    (N'PublicUtility', N'TH', N'TH', N'01', N'ไฟฟ้า', 1, 1),
    (N'PublicUtility', N'TH', N'EN', N'02', N'Tap Water/Ground Water', 1, 2),
    (N'PublicUtility', N'TH', N'TH', N'02', N'ประปา', 1, 2),
    (N'PublicUtility', N'TH', N'EN', N'03', N'Drainage Pipe/Sump', 1, 3),
    (N'PublicUtility', N'TH', N'TH', N'03', N'ท่อระบายน้ำ', 1, 3),
    (N'PublicUtility', N'TH', N'EN', N'04', N'Streetlight', 1, 4),
    (N'PublicUtility', N'TH', N'TH', N'04', N'ไฟฟ้าถนน', 1, 4),
    (N'PublicUtility', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'PublicUtility', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: QuotationFeedbackStatus (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'1', N'Assigned', 1, 1),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'1', N'Assigned', 1, 1),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'2', N'Pending', 1, 2),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'2', N'Pending', 1, 2),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'3', N'Pending Checker', 1, 3),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'3', N'Pending Checker', 1, 3),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'4', N'Quoted', 1, 4),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'4', N'Quoted', 1, 4),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'5', N'Not Participating', 1, 5),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'5', N'Not Participating', 1, 5),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'6', N'Quotation Revision', 1, 6),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'6', N'Quotation Revision', 1, 6),
    (N'QuotationFeedbackStatus', N'TH', N'EN', N'7', N'Awarded', 1, 7),
    (N'QuotationFeedbackStatus', N'TH', N'TH', N'7', N'Awarded', 1, 7);
GO

-- ----------------------------------------
-- Group: QuotationSearchBy (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'QuotationSearchBy', N'TH', N'EN', N'QI', N'Quotation ID', 1, 1),
    (N'QuotationSearchBy', N'TH', N'TH', N'QI', N'Quotation ID', 1, 1),
    (N'QuotationSearchBy', N'TH', N'EN', N'AR', N'Appraisal Report No', 1, 2),
    (N'QuotationSearchBy', N'TH', N'TH', N'AR', N'Appraisal Report No', 1, 2),
    (N'QuotationSearchBy', N'TH', N'EN', N'ST', N'Status', 1, 3),
    (N'QuotationSearchBy', N'TH', N'TH', N'ST', N'Status', 1, 3),
    (N'QuotationSearchBy', N'TH', N'EN', N'CU', N'Customer Name', 1, 4),
    (N'QuotationSearchBy', N'TH', N'TH', N'CU', N'Customer Name', 1, 4),
    (N'QuotationSearchBy', N'TH', N'EN', N'RT', N'Request Type', 1, 5),
    (N'QuotationSearchBy', N'TH', N'TH', N'RT', N'Request Type', 1, 5),
    (N'QuotationSearchBy', N'TH', N'EN', N'CO', N'Company Name', 1, 6),
    (N'QuotationSearchBy', N'TH', N'TH', N'CO', N'Company Name', 1, 6);
GO

-- ----------------------------------------
-- Group: QuotationStandaloneSearchBy (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'QuotationStandaloneSearchBy', N'TH', N'EN', N'1', N'Quotation ID', 1, 1),
    (N'QuotationStandaloneSearchBy', N'TH', N'TH', N'1', N'Quotation ID', 1, 1),
    (N'QuotationStandaloneSearchBy', N'TH', N'EN', N'2', N'Status', 1, 2),
    (N'QuotationStandaloneSearchBy', N'TH', N'TH', N'2', N'Status', 1, 2),
    (N'QuotationStandaloneSearchBy', N'TH', N'EN', N'3', N'Cut-Off Date', 1, 3),
    (N'QuotationStandaloneSearchBy', N'TH', N'TH', N'3', N'Cut-Off Date', 1, 3),
    (N'QuotationStandaloneSearchBy', N'TH', N'EN', N'4', N'Receive On', 1, 4),
    (N'QuotationStandaloneSearchBy', N'TH', N'TH', N'4', N'Receive On', 1, 4),
    (N'QuotationStandaloneSearchBy', N'TH', N'EN', N'5', N'Customer Name', 1, 5),
    (N'QuotationStandaloneSearchBy', N'TH', N'TH', N'5', N'Customer Name', 1, 5);
GO

-- ----------------------------------------
-- Group: QuotationStatus (EN=7, TH=7)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'QuotationStatus', N'TH', N'EN', N'PQ', N'New Quotation Created', 1, 1),
    (N'QuotationStatus', N'TH', N'TH', N'PQ', N'New Quotation Created', 1, 1),
    (N'QuotationStatus', N'TH', N'EN', N'PC', N'Pending Company Quote', 1, 2),
    (N'QuotationStatus', N'TH', N'TH', N'PC', N'Pending Company Quote', 1, 2),
    (N'QuotationStatus', N'TH', N'EN', N'MN', N'Quotation Revision Request Sent', 1, 3),
    (N'QuotationStatus', N'TH', N'TH', N'MN', N'Quotation Revision Request Sent', 1, 3),
    (N'QuotationStatus', N'TH', N'EN', N'PP', N'Pending Companies Shortlist', 1, 4),
    (N'QuotationStatus', N'TH', N'TH', N'PP', N'Pending Companies Shortlist', 1, 4),
    (N'QuotationStatus', N'TH', N'EN', N'PS', N'Pending Company Award', 1, 5),
    (N'QuotationStatus', N'TH', N'TH', N'PS', N'Pending Company Award', 1, 5),
    (N'QuotationStatus', N'TH', N'EN', N'C', N'Completed', 1, 6),
    (N'QuotationStatus', N'TH', N'TH', N'C', N'Completed', 1, 6),
    (N'QuotationStatus', N'TH', N'EN', N'CL', N'Cancel', 1, 7),
    (N'QuotationStatus', N'TH', N'TH', N'CL', N'Cancel', 1, 7);
GO

-- ----------------------------------------
-- Group: RegistrationStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RegistrationStatus', N'TH', N'EN', N'1', N'Registered', 1, 1),
    (N'RegistrationStatus', N'TH', N'TH', N'1', N'จดทะเบียน', 1, 1),
    (N'RegistrationStatus', N'TH', N'EN', N'2', N'Unregistered', 1, 2),
    (N'RegistrationStatus', N'TH', N'TH', N'2', N'ไม่จดทะเบียน', 1, 2);
GO

-- ----------------------------------------
-- Group: Remark (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Remark', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'Remark', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'Remark', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'Remark', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'Remark', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'Remark', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'Remark', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'Remark', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'Remark', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'Remark', N'TH', N'TH', N'05', N'05', 1, 5);
GO

-- ----------------------------------------
-- Group: Residential (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Residential', N'TH', N'EN', N'01', N'Can', 1, 1),
    (N'Residential', N'TH', N'TH', N'01', N'Can', 1, 1),
    (N'Residential', N'TH', N'EN', N'02', N'Can Not', 1, 2),
    (N'Residential', N'TH', N'TH', N'02', N'Can Not', 1, 2);
GO

-- ----------------------------------------
-- Group: Retail_IBG (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'BankingSegment', N'TH', N'EN', N'RETAIL', N'Retail', 1, 1),
    (N'BankingSegment', N'TH', N'TH', N'RETAIL', N'Retail', 1, 1),
    (N'BankingSegment', N'TH', N'EN', N'IBG', N'IBG', 1, 2),
    (N'BankingSegment', N'TH', N'TH', N'IBG', N'IBG', 1, 2);
GO

-- ----------------------------------------
-- Group: ReviewtType (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ReviewtType', N'TH', N'EN', N'Group 1', N'Normal Review', 1, 1),
    (N'ReviewtType', N'TH', N'TH', N'Group 1', N'Normal Review', 1, 1),
    (N'ReviewtType', N'TH', N'EN', N'Group 2', N'Before Stage 3', 1, 2),
    (N'ReviewtType', N'TH', N'TH', N'Group 2', N'Before Stage 3', 1, 2),
    (N'ReviewtType', N'TH', N'EN', N'Group 3', N'Stage 3', 1, 3),
    (N'ReviewtType', N'TH', N'TH', N'Group 3', N'Stage 3', 1, 3);
GO

-- ----------------------------------------
-- Group: RoadSurface (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoadSurface', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'RoadSurface', N'TH', N'TH', N'01', N'คอนกรีต', 1, 1),
    (N'RoadSurface', N'TH', N'EN', N'02', N'Gravel', 1, 2),
    (N'RoadSurface', N'TH', N'TH', N'02', N'ลูกรัง', 1, 2),
    (N'RoadSurface', N'TH', N'EN', N'03', N'Crushed Stone', 1, 3),
    (N'RoadSurface', N'TH', N'TH', N'03', N'หินคลุก', 1, 3),
    (N'RoadSurface', N'TH', N'EN', N'04', N'Soil', 1, 4),
    (N'RoadSurface', N'TH', N'TH', N'04', N'ดิน', 1, 4),
    (N'RoadSurface', N'TH', N'EN', N'05', N'Asphalt', 1, 5),
    (N'RoadSurface', N'TH', N'TH', N'05', N'ลาดยาง', 1, 5),
    (N'RoadSurface', N'TH', N'EN', N'99', N'Other', 1, 6),
    (N'RoadSurface', N'TH', N'TH', N'99', N'อื่นๆ', 1, 6);
GO

-- ----------------------------------------
-- Group: RollbackReason (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RollbackReason', N'TH', N'EN', N'1', N'1', 1, 1),
    (N'RollbackReason', N'TH', N'TH', N'1', N'1', 1, 1),
    (N'RollbackReason', N'TH', N'EN', N'2', N'2', 1, 2),
    (N'RollbackReason', N'TH', N'TH', N'2', N'2', 1, 2),
    (N'RollbackReason', N'TH', N'EN', N'3', N'3', 1, 3),
    (N'RollbackReason', N'TH', N'TH', N'3', N'3', 1, 3);
GO

-- ----------------------------------------
-- Group: Roof (EN=9, TH=9)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Roof', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'Roof', N'TH', N'TH', N'01', N'คอนกรีตเสริมเหล็ก', 1, 1),
    (N'Roof', N'TH', N'EN', N'02', N'Corrugated Tiles', 1, 2),
    (N'Roof', N'TH', N'TH', N'02', N'กระเบื้องลอนคู่', 1, 2),
    (N'Roof', N'TH', N'EN', N'03', N'Monier roof tiles', 1, 3),
    (N'Roof', N'TH', N'TH', N'03', N'ซีแพคโมเนีย', 1, 3),
    (N'Roof', N'TH', N'EN', N'04', N'Metal Sheet', 1, 4),
    (N'Roof', N'TH', N'TH', N'04', N'เมทัลชีท', 1, 4),
    (N'Roof', N'TH', N'EN', N'05', N'Vinyl', 1, 5),
    (N'Roof', N'TH', N'TH', N'05', N'ไวนิล', 1, 5),
    (N'Roof', N'TH', N'EN', N'06', N'Terracotta Tile', 1, 6),
    (N'Roof', N'TH', N'TH', N'06', N'กระเบื้องดินเผา', 1, 6),
    (N'Roof', N'TH', N'EN', N'07', N'Galvanized Iron', 1, 7),
    (N'Roof', N'TH', N'TH', N'07', N'สังกะสี', 1, 7),
    (N'Roof', N'TH', N'EN', N'08', N'Can''t Check', 1, 8),
    (N'Roof', N'TH', N'TH', N'08', N'ตรวจสอบไม่ได้', 1, 8),
    (N'Roof', N'TH', N'EN', N'09', N'Tile', 1, 9),
    (N'Roof', N'TH', N'TH', N'09', N'กระเบื้อง', 1, 9),
    (N'Roof', N'TH', N'EN', N'10', N'Duplex', 1, 10),
    (N'Roof', N'TH', N'TH', N'10', N'ดูเพล็กซ์', 1, 10),
    (N'Roof', N'TH', N'EN', N'99', N'Other', 1, 99),
    (N'Roof', N'TH', N'TH', N'99', N'อื่นๆ', 1, 99);
GO

-- ----------------------------------------
-- Group: RoofFrame (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoofFrame', N'TH', N'EN', N'01', N'Reinforced Concrete', 1, 1),
    (N'RoofFrame', N'TH', N'TH', N'01', N'คอนกรีตเสริมเหล็ก', 1, 1),
    (N'RoofFrame', N'TH', N'EN', N'02', N'Steel', 1, 2),
    (N'RoofFrame', N'TH', N'TH', N'02', N'เหล็ก', 1, 2),
    (N'RoofFrame', N'TH', N'EN', N'03', N'Wood', 1, 3),
    (N'RoofFrame', N'TH', N'TH', N'03', N'ไม้', 1, 3),
    (N'RoofFrame', N'TH', N'EN', N'04', N'Can''t Check', 1, 4),
    (N'RoofFrame', N'TH', N'TH', N'04', N'ตรวจสอบไม่ได้', 1, 4),
    (N'RoofFrame', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'RoofFrame', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: RoomLayout (EN=6, TH=6)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoomLayout', N'TH', N'EN', N'01', N'Studio', 1, 1),
    (N'RoomLayout', N'TH', N'TH', N'01', N'Studio', 1, 1),
    (N'RoomLayout', N'TH', N'EN', N'02', N'1 Bedroom', 1, 2),
    (N'RoomLayout', N'TH', N'TH', N'02', N'1 Bedroom', 1, 2),
    (N'RoomLayout', N'TH', N'EN', N'03', N'2 Bedroom', 1, 3),
    (N'RoomLayout', N'TH', N'TH', N'03', N'2 Bedroom', 1, 3),
    (N'RoomLayout', N'TH', N'EN', N'04', N'Duplex', 1, 4),
    (N'RoomLayout', N'TH', N'TH', N'04', N'Duplex', 1, 4),
    (N'RoomLayout', N'TH', N'EN', N'05', N'Penthouse', 1, 5),
    (N'RoomLayout', N'TH', N'TH', N'05', N'Penthouse', 1, 5),
    (N'RoomLayout', N'TH', N'EN', N'99', N'Other', 1, 6),
    (N'RoomLayout', N'TH', N'TH', N'99', N'Other', 1, 6);
GO

-- ----------------------------------------
-- Group: RoomType (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoomType', N'TH', N'EN', N'01', N'Standard', 1, 1),
    (N'RoomType', N'TH', N'TH', N'01', N'Standard', 1, 1),
    (N'RoomType', N'TH', N'EN', N'02', N'Deluxe', 1, 2),
    (N'RoomType', N'TH', N'TH', N'02', N'Deluxe', 1, 2),
    (N'RoomType', N'TH', N'EN', N'03', N'Superior', 1, 3),
    (N'RoomType', N'TH', N'TH', N'03', N'Superior', 1, 3),
    (N'RoomType', N'TH', N'EN', N'04', N'1 bed room', 1, 4),
    (N'RoomType', N'TH', N'TH', N'04', N'1 bed room', 1, 4),
    (N'RoomType', N'TH', N'EN', N'05', N'2 bed room', 1, 5),
    (N'RoomType', N'TH', N'TH', N'05', N'2 bed room', 1, 5),
    (N'RoomType', N'TH', N'EN', N'06', N'Other', 1, 6),
    (N'RoomType', N'TH', N'TH', N'06', N'Other', 1, 6),
    (N'RoomType', N'TH', N'EN', N'07', N'Air-conditioned room', 1, 7),
    (N'RoomType', N'TH', N'TH', N'07', N'Air-conditioned room', 1, 7),
    (N'RoomType', N'TH', N'EN', N'08', N'Fan room', 1, 8),
    (N'RoomType', N'TH', N'TH', N'08', N'Fan room', 1, 8);
GO

-- ----------------------------------------
-- Group: RoutebackReason (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoutebackReason', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'RoutebackReason', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'RoutebackReason', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'RoutebackReason', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'RoutebackReason', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'RoutebackReason', N'TH', N'TH', N'03', N'03', 1, 3);
GO

-- ----------------------------------------
-- Group: SaleAdj_CompareVal (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'SaleAdj_CompareVal', N'TH', N'EN', N'01', N'Equal', 1, 1),
    (N'SaleAdj_CompareVal', N'TH', N'TH', N'01', N'Equal', 1, 1),
    (N'SaleAdj_CompareVal', N'TH', N'EN', N'02', N'Better', 1, 2),
    (N'SaleAdj_CompareVal', N'TH', N'TH', N'02', N'Better', 1, 2),
    (N'SaleAdj_CompareVal', N'TH', N'EN', N'03', N'Inferior', 1, 3),
    (N'SaleAdj_CompareVal', N'TH', N'TH', N'03', N'Inferior', 1, 3);
GO

-- ----------------------------------------
-- Group: Sec_ReleaseRoutebackDecision (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'EN', N'01', N'Released', 1, 1),
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'TH', N'01', N'Released', 1, 1),
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'EN', N'02', N'Route Back', 1, 2),
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'TH', N'02', N'Route Back', 1, 2),
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'EN', N'03', N'Approved', 1, 3),
    (N'Sec_ReleaseRoutebackDecision', N'TH', N'TH', N'03', N'Approved', 1, 3);
GO

-- ----------------------------------------
-- Group: Selling/AdvExpenses (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Selling/AdvExpenses', N'TH', N'EN', N'01', N'Selling/Advertising and Public Relations Expenses', 1, 1),
    (N'Selling/AdvExpenses', N'TH', N'TH', N'01', N'Selling/Advertising and Public Relations Expenses', 1, 1);
GO

-- ----------------------------------------
-- Group: ServiceQualityEvaluation (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'ServiceQualityEvaluation', N'TH', N'EN', N'1', N'Retail', 1, 1),
    (N'ServiceQualityEvaluation', N'TH', N'TH', N'1', N'Retail', 1, 1),
    (N'ServiceQualityEvaluation', N'TH', N'EN', N'2', N'Retail', 1, 2),
    (N'ServiceQualityEvaluation', N'TH', N'TH', N'2', N'Retail', 1, 2),
    (N'ServiceQualityEvaluation', N'TH', N'EN', N'3', N'Retail', 1, 3),
    (N'ServiceQualityEvaluation', N'TH', N'TH', N'3', N'Retail', 1, 3),
    (N'ServiceQualityEvaluation', N'TH', N'EN', N'4', N'Retail', 1, 4),
    (N'ServiceQualityEvaluation', N'TH', N'TH', N'4', N'Retail', 1, 4),
    (N'ServiceQualityEvaluation', N'TH', N'EN', N'5', N'Retail', 1, 5),
    (N'ServiceQualityEvaluation', N'TH', N'TH', N'5', N'Retail', 1, 5);
GO

-- ----------------------------------------
-- Group: SourceofData (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'SourceofData', N'TH', N'EN', N'1', N'Bank', 1, 1),
    (N'SourceofData', N'TH', N'TH', N'1', N'ธนาคาร', 1, 1),
    (N'SourceofData', N'TH', N'EN', N'2', N'Appraisal Company', 1, 2),
    (N'SourceofData', N'TH', N'TH', N'2', N'บริษัทประเมิน', 1, 2);
GO

-- ----------------------------------------
-- Group: Sub Approve (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Sub Approve', N'TH', N'EN', N'Completed', N'Completed', 1, 10),
    (N'Sub Approve', N'TH', N'TH', N'Completed', N'Completed', 1, 10);
GO

-- ----------------------------------------
-- Group: SummaryOfAppraiserOpinions (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'SummaryOfAppraiserOpinions', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'SummaryOfAppraiserOpinions', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'SummaryOfAppraiserOpinions', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'SummaryOfAppraiserOpinions', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'SummaryOfAppraiserOpinions', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'SummaryOfAppraiserOpinions', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'SummaryOfAppraiserOpinions', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'SummaryOfAppraiserOpinions', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'SummaryOfAppraiserOpinions', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'SummaryOfAppraiserOpinions', N'TH', N'TH', N'05', N'05', 1, 5);
GO

-- ----------------------------------------
-- Group: SummaryOfCommitteeOpinions (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'SummaryOfCommitteeOpinions', N'TH', N'EN', N'01', N'01', 1, 1),
    (N'SummaryOfCommitteeOpinions', N'TH', N'TH', N'01', N'01', 1, 1),
    (N'SummaryOfCommitteeOpinions', N'TH', N'EN', N'02', N'02', 1, 2),
    (N'SummaryOfCommitteeOpinions', N'TH', N'TH', N'02', N'02', 1, 2),
    (N'SummaryOfCommitteeOpinions', N'TH', N'EN', N'03', N'03', 1, 3),
    (N'SummaryOfCommitteeOpinions', N'TH', N'TH', N'03', N'03', 1, 3),
    (N'SummaryOfCommitteeOpinions', N'TH', N'EN', N'04', N'04', 1, 4),
    (N'SummaryOfCommitteeOpinions', N'TH', N'TH', N'04', N'04', 1, 4),
    (N'SummaryOfCommitteeOpinions', N'TH', N'EN', N'05', N'05', 1, 5),
    (N'SummaryOfCommitteeOpinions', N'TH', N'TH', N'05', N'05', 1, 5);
GO

-- ----------------------------------------
-- Group: SupportingDataStatus (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'SupportingDataStatus', N'TH', N'EN', N'p', N'PendingApproval', 1, 1),
    (N'SupportingDataStatus', N'TH', N'TH', N'p', N'PendingApproval', 1, 1),
    (N'SupportingDataStatus', N'TH', N'EN', N'A', N'Approved', 1, 2),
    (N'SupportingDataStatus', N'TH', N'TH', N'A', N'Approved', 1, 2);
GO

-- ----------------------------------------
-- Group: Survey data (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Survey data', N'TH', N'EN', N'0', N'1', 1, 5),
    (N'Survey data', N'TH', N'TH', N'0', N'02', 1, 5);
GO

-- ----------------------------------------
-- Group: TaskType (EN=9, TH=9)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'TaskType', N'TH', N'EN', N'01', N'Draft', 1, 1),
    (N'TaskType', N'TH', N'TH', N'01', N'Draft', 1, 1),
    (N'TaskType', N'TH', N'EN', N'02', N'Route Back Follow Up', 1, 2),
    (N'TaskType', N'TH', N'TH', N'02', N'Route Back Follow Up', 1, 2),
    (N'TaskType', N'TH', N'EN', N'03', N'Document Follow Up', 1, 3),
    (N'TaskType', N'TH', N'TH', N'03', N'Document Follow Up', 1, 3),
    (N'TaskType', N'TH', N'EN', N'04', N'Pending Check', 1, 4),
    (N'TaskType', N'TH', N'TH', N'04', N'Pending Check', 1, 4),
    (N'TaskType', N'TH', N'EN', N'05', N'Pending Assignment', 1, 5),
    (N'TaskType', N'TH', N'TH', N'05', N'Pending Assignment', 1, 5),
    (N'TaskType', N'TH', N'EN', N'06', N'Pending Fee/Appointment Approval', 1, 6),
    (N'TaskType', N'TH', N'TH', N'06', N'Pending Fee/Appointment Approval', 1, 6),
    (N'TaskType', N'TH', N'EN', N'07', N'Route Back', 1, 7),
    (N'TaskType', N'TH', N'TH', N'07', N'Route Back', 1, 7),
    (N'TaskType', N'TH', N'EN', N'08', N'Pending Check Appraisal Book', 1, 8),
    (N'TaskType', N'TH', N'TH', N'08', N'Pending Check Appraisal Book', 1, 8),
    (N'TaskType', N'TH', N'EN', N'09', N'Pending Appraisal', 1, 9),
    (N'TaskType', N'TH', N'TH', N'09', N'Pending Appraisal', 1, 9);
GO

-- ----------------------------------------
-- Group: Topic (EN=9, TH=9)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Topic', N'TH', N'EN', N'01', N'ภาพถ่ายบริเวณหน้าโครงการ', 1, 1),
    (N'Topic', N'TH', N'TH', N'01', N'ภาพถ่ายบริเวณหน้าโครงการ', 1, 1),
    (N'Topic', N'TH', N'EN', N'02', N'ภาพถ่ายบริเวณถนนผ่านหน้าทรัพย์สิน', 1, 2),
    (N'Topic', N'TH', N'TH', N'02', N'ภาพถ่ายบริเวณถนนผ่านหน้าทรัพย์สิน', 1, 2),
    (N'Topic', N'TH', N'EN', N'03', N'ภาพถ่ายทรัพย์สิน', 1, 3),
    (N'Topic', N'TH', N'TH', N'03', N'ภาพถ่ายทรัพย์สิน', 1, 3),
    (N'Topic', N'TH', N'EN', N'04', N'ภาพถ่ายบริเวณถนนผ่านหน้าโครงการ', 1, 4),
    (N'Topic', N'TH', N'TH', N'04', N'ภาพถ่ายบริเวณถนนผ่านหน้าโครงการ', 1, 4),
    (N'Topic', N'TH', N'EN', N'05', N'ภาพถ่ายโครงการ', 1, 5),
    (N'Topic', N'TH', N'TH', N'05', N'ภาพถ่ายโครงการ', 1, 5),
    (N'Topic', N'TH', N'EN', N'06', N'ภาพถ่าย LOBBY', 1, 6),
    (N'Topic', N'TH', N'TH', N'06', N'ภาพถ่าย LOBBY', 1, 6),
    (N'Topic', N'TH', N'EN', N'07', N'ภาพถ่าย โถงลิฟท์', 1, 7),
    (N'Topic', N'TH', N'TH', N'07', N'ภาพถ่าย โถงลิฟท์', 1, 7),
    (N'Topic', N'TH', N'EN', N'08', N'ภาพถ่ายโถงทางเดินหน้าห้องพัก', 1, 8),
    (N'Topic', N'TH', N'TH', N'08', N'ภาพถ่ายโถงทางเดินหน้าห้องพัก', 1, 8),
    (N'Topic', N'TH', N'EN', N'09', N'ภาพถ่ายหน้าห้องพัก', 1, 9),
    (N'Topic', N'TH', N'TH', N'09', N'ภาพถ่ายหน้าห้องพัก', 1, 9);
GO

-- ----------------------------------------
-- Group: TowerModelType (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'TowerModelType', N'TH', N'EN', N'01', N'M1', 1, 1),
    (N'TowerModelType', N'TH', N'TH', N'01', N'M1', 1, 1),
    (N'TowerModelType', N'TH', N'EN', N'02', N'M2', 1, 2),
    (N'TowerModelType', N'TH', N'TH', N'02', N'M2', 1, 2),
    (N'TowerModelType', N'TH', N'EN', N'03', N'M3', 1, 3),
    (N'TowerModelType', N'TH', N'TH', N'03', N'M3', 1, 3);
GO

-- ----------------------------------------
-- Group: Tower_RoadSurface (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Tower_RoadSurface', N'TH', N'EN', N'01', N'concrete', 1, 1),
    (N'Tower_RoadSurface', N'TH', N'TH', N'01', N'concrete', 1, 1),
    (N'Tower_RoadSurface', N'TH', N'EN', N'02', N'Asphalt', 1, 2),
    (N'Tower_RoadSurface', N'TH', N'TH', N'02', N'Asphalt', 1, 2),
    (N'Tower_RoadSurface', N'TH', N'EN', N'03', N'Gravel/Crushed Stone', 1, 3),
    (N'Tower_RoadSurface', N'TH', N'TH', N'03', N'Gravel/Crushed Stone', 1, 3),
    (N'Tower_RoadSurface', N'TH', N'EN', N'04', N'Soil', 1, 4),
    (N'Tower_RoadSurface', N'TH', N'TH', N'04', N'Soil', 1, 4),
    (N'Tower_RoadSurface', N'TH', N'EN', N'05', N'Other', 1, 5),
    (N'Tower_RoadSurface', N'TH', N'TH', N'05', N'Other', 1, 5);
GO

-- ----------------------------------------
-- Group: TransferFee (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'TransferFee', N'TH', N'EN', N'01', N'TransferFee', 1, 1),
    (N'TransferFee', N'TH', N'TH', N'01', N'TransferFee', 1, 1);
GO

-- ----------------------------------------
-- Group: Transportation (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Transportation', N'TH', N'EN', N'01', N'Car', 1, 1),
    (N'Transportation', N'TH', N'TH', N'01', N'รถยนต์', 1, 1),
    (N'Transportation', N'TH', N'EN', N'02', N'Bus', 1, 2),
    (N'Transportation', N'TH', N'TH', N'02', N'รถประจำทาง', 1, 2),
    (N'Transportation', N'TH', N'EN', N'03', N'Ship', 1, 3),
    (N'Transportation', N'TH', N'TH', N'03', N'เรือ', 1, 3),
    (N'Transportation', N'TH', N'EN', N'04', N'Footpath', 1, 4),
    (N'Transportation', N'TH', N'TH', N'04', N'ทางเดิน', 1, 4),
    (N'Transportation', N'TH', N'EN', N'99', N'Other', 1, 5),
    (N'Transportation', N'TH', N'TH', N'99', N'อื่นๆ', 1, 5);
GO

-- ----------------------------------------
-- Group: TypeOfFee (EN=3, TH=3)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'TypeOfFee', N'TH', N'EN', N'01', N'Appraisal Fee', 1, 1),
    (N'TypeOfFee', N'TH', N'TH', N'01', N'ค่าประเมิน', 1, 1),
    (N'TypeOfFee', N'TH', N'EN', N'02', N'Inspection fee', 1, 2),
    (N'TypeOfFee', N'TH', N'TH', N'02', N'ค่าตรวจงวดงาน', 1, 2),
    (N'TypeOfFee', N'TH', N'EN', N'99', N'Other', 1, 3),
    (N'TypeOfFee', N'TH', N'TH', N'99', N'อื่นๆ', 1, 3);
GO

-- ----------------------------------------
-- Group: TypeOfUrbanPlanning (EN=10, TH=10)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'01', N'Commerce or commercial district', 1, 1),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'01', N'ย่านพานิชยกรรม', 1, 1),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'02', N'A very dense residential area', 1, 2),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'02', N'ย่านที่อยู่อาศัยหนาแน่นมาก', 1, 2),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'03', N'Medium density residential district', 1, 3),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'03', N'ย่านที่อยู่อาศัยหนาแน่นปานกลาง', 1, 3),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'04', N'The residential area of less dense', 1, 4),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'04', N'ย่านที่อยู่อาศัยหนาแน่นน้อย', 1, 4),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'05', N'The other difficulty/ restrictions to development', 1, 5),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'05', N'อื่นๆ ที่ยาก / มีข้อจำกัดต่อการพัฒนา', 1, 5),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'06', N'Industrial and warehouse land', 1, 6),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'06', N'ที่ดินประเภทอุตสาหกรรมและคลังสินค้า', 1, 6),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'07', N'Rural and agricultural conservation land', 1, 7),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'07', N'ที่ดินประเภทอนุรักษ์ชนบทและเกษตรกรรม', 1, 7),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'08', N'Rural and agricultural land', 1, 8),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'08', N'ที่ดินประเภทชนบทและเกตรกรรม', 1, 8),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'09', N'Land for the conservation and promotion of Thai arts and culture', 1, 9),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'09', N'ที่ดินประเภทอนุรักษ์และส่งเสริมศิลปวัฒนธรรมไทย', 1, 9),
    (N'TypeOfUrbanPlanning', N'TH', N'EN', N'10', N'Land type: Government agency, public utilities/public utilities', 1, 10),
    (N'TypeOfUrbanPlanning', N'TH', N'TH', N'10', N'ที่ดินประเภทหน่วยงานราชการ สาธารณูปโภค/สาธารณูปการ', 1, 10);
GO

-- ----------------------------------------
-- Group: UnderConstruction (EN=2, TH=2)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'UnderConstruction', N'TH', N'EN', N'01', N'Yes', 1, 1),
    (N'UnderConstruction', N'TH', N'TH', N'01', N'Yes', 1, 1),
    (N'UnderConstruction', N'TH', N'EN', N'02', N'No', 1, 2),
    (N'UnderConstruction', N'TH', N'TH', N'02', N'No', 1, 2);
GO

-- ----------------------------------------
-- Group: UpperFlooringMaterials (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'UpperFlooringMaterials', N'TH', N'EN', N'01', N'Polished concrete', 1, 1),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'01', N'Polished concrete', 1, 1),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'02', N'Glazed tiles', 1, 2),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'02', N'Glazed tiles', 1, 2),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'03', N'Marble', 1, 3),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'03', N'Marble', 1, 3),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'04', N'Granite', 1, 4),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'04', N'Granite', 1, 4),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'05', N'Laminate', 1, 5),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'05', N'Laminate', 1, 5),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'06', N'Parquet wood floor', 1, 6),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'06', N'Parquet wood floor', 1, 6),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'07', N'Rubber tiles', 1, 7),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'07', N'Rubber tiles', 1, 7),
    (N'UpperFlooringMaterials', N'TH', N'EN', N'99', N'Other', 1, 8),
    (N'UpperFlooringMaterials', N'TH', N'TH', N'99', N'Other', 1, 8);
GO

-- ----------------------------------------
-- Group: Urgent Approve (EN=1, TH=1)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Urgent Approve', N'TH', N'EN', N'Completed', N'Completed', 1, 9),
    (N'Urgent Approve', N'TH', N'TH', N'Completed', N'Completed', 1, 9);
GO

-- ----------------------------------------
-- Group: Utilization (EN=5, TH=5)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Utilization', N'TH', N'EN', N'01', N'Used For Living', 1, 1),
    (N'Utilization', N'TH', N'TH', N'01', N'Used For Living', 1, 1),
    (N'Utilization', N'TH', N'EN', N'02', N'Residing at least 30%', 1, 2),
    (N'Utilization', N'TH', N'TH', N'02', N'Residing at least 30%', 1, 2),
    (N'Utilization', N'TH', N'EN', N'03', N'Residing less than 30%', 1, 3),
    (N'Utilization', N'TH', N'TH', N'03', N'Residing less than 30%', 1, 3),
    (N'Utilization', N'TH', N'EN', N'04', N'Used for Rent Sharing', 1, 4),
    (N'Utilization', N'TH', N'TH', N'04', N'Used for Rent Sharing', 1, 4),
    (N'Utilization', N'TH', N'EN', N'99', N'Used For Other Purposes', 1, 5),
    (N'Utilization', N'TH', N'TH', N'99', N'Used For Other Purposes', 1, 5);
GO

-- ----------------------------------------
-- Group: VehicleType (EN=8, TH=8)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'VehicleType', N'TH', N'EN', N'1', N'Sedan', 1, 1),
    (N'VehicleType', N'TH', N'TH', N'1', N'รถเก๋ง', 1, 1),
    (N'VehicleType', N'TH', N'EN', N'2', N'Pickup Truck', 1, 2),
    (N'VehicleType', N'TH', N'TH', N'2', N'รถกระบะ', 1, 2),
    (N'VehicleType', N'TH', N'EN', N'3', N'Truck', 1, 3),
    (N'VehicleType', N'TH', N'TH', N'3', N'รถบันทุก', 1, 3),
    (N'VehicleType', N'TH', N'EN', N'4', N'4 wheels drive', 1, 4),
    (N'VehicleType', N'TH', N'TH', N'4', N'รถ 4 wheel drive', 1, 4),
    (N'VehicleType', N'TH', N'EN', N'5', N'Motorcycle', 1, 5),
    (N'VehicleType', N'TH', N'TH', N'5', N'รถ motocycle', 1, 5),
    (N'VehicleType', N'TH', N'EN', N'6', N'Van', 1, 6),
    (N'VehicleType', N'TH', N'TH', N'6', N'รถตู้', 1, 6),
    (N'VehicleType', N'TH', N'EN', N'7', N'Bus', 1, 7),
    (N'VehicleType', N'TH', N'TH', N'7', N'รถบัส', 1, 7),
    (N'VehicleType', N'TH', N'EN', N'8', N'Tuk-tuk', 1, 8),
    (N'VehicleType', N'TH', N'TH', N'8', N'รถตูุ๊กตุ๊ก', 1, 8);
GO

-- ----------------------------------------
-- Group: Country (EN=246, TH=246)
-- ----------------------------------------
INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Country', N'TH', N'EN', N'AD', N'Andorra', 1, 1),
    (N'Country', N'TH', N'TH', N'AD', N'อันดอร์รา', 1, 1),
    (N'Country', N'TH', N'EN', N'AE', N'United Arab Emirates', 1, 2),
    (N'Country', N'TH', N'TH', N'AE', N'สหรัฐอาหรับเอมิเรตส์', 1, 2),
    (N'Country', N'TH', N'EN', N'AF', N'Afghanistan', 1, 3),
    (N'Country', N'TH', N'TH', N'AF', N'อัฟกานิสถาน', 1, 3),
    (N'Country', N'TH', N'EN', N'AG', N'Antigua And Barbuda', 1, 4),
    (N'Country', N'TH', N'TH', N'AG', N'แอนติกาและบาร์บูดา', 1, 4),
    (N'Country', N'TH', N'EN', N'AI', N'Anguilla', 1, 5),
    (N'Country', N'TH', N'TH', N'AI', N'แองกวิลลา', 1, 5),
    (N'Country', N'TH', N'EN', N'AL', N'Albania', 1, 6),
    (N'Country', N'TH', N'TH', N'AL', N'แอลเบเนีย', 1, 6),
    (N'Country', N'TH', N'EN', N'AM', N'Armenia', 1, 7),
    (N'Country', N'TH', N'TH', N'AM', N'อาร์เมเนีย', 1, 7),
    (N'Country', N'TH', N'EN', N'AN', N'Netherlands Antilles', 1, 8),
    (N'Country', N'TH', N'TH', N'AN', N'เนเธอร์แลนด์แอนทิลลิส', 1, 8),
    (N'Country', N'TH', N'EN', N'AO', N'Angola', 1, 9),
    (N'Country', N'TH', N'TH', N'AO', N'แองโกลา', 1, 9),
    (N'Country', N'TH', N'EN', N'AQ', N'Antartica', 1, 10),
    (N'Country', N'TH', N'TH', N'AQ', N'แอนตาร์กติกา', 1, 10),
    (N'Country', N'TH', N'EN', N'AR', N'Argentina', 1, 11),
    (N'Country', N'TH', N'TH', N'AR', N'อาร์เจนตินา', 1, 11),
    (N'Country', N'TH', N'EN', N'AS', N'American Samoa', 1, 12),
    (N'Country', N'TH', N'TH', N'AS', N'อเมริกันซามัว', 1, 12),
    (N'Country', N'TH', N'EN', N'AT', N'Austria', 1, 13),
    (N'Country', N'TH', N'TH', N'AT', N'ออสเตรีย', 1, 13),
    (N'Country', N'TH', N'EN', N'AU', N'Australia', 1, 14),
    (N'Country', N'TH', N'TH', N'AU', N'ออสเตรเลีย', 1, 14),
    (N'Country', N'TH', N'EN', N'AW', N'Aruba, Bonaire And Curacao', 1, 15),
    (N'Country', N'TH', N'TH', N'AW', N'อารูบา', 1, 15),
    (N'Country', N'TH', N'EN', N'AZ', N'Azerbaijan', 1, 16),
    (N'Country', N'TH', N'TH', N'AZ', N'อาเซอร์ไบจาน', 1, 16),
    (N'Country', N'TH', N'EN', N'BA', N'Bosnia-Herzegovina', 1, 17),
    (N'Country', N'TH', N'TH', N'BA', N'บอสเนียและเฮอร์เซโกวีนา', 1, 17),
    (N'Country', N'TH', N'EN', N'BB', N'Barbados', 1, 18),
    (N'Country', N'TH', N'TH', N'BB', N'บาร์เบโดส', 1, 18),
    (N'Country', N'TH', N'EN', N'BD', N'Bangladesh', 1, 19),
    (N'Country', N'TH', N'TH', N'BD', N'บังกลาเทศ', 1, 19),
    (N'Country', N'TH', N'EN', N'BE', N'Belgium', 1, 20),
    (N'Country', N'TH', N'TH', N'BE', N'เบลเยียม', 1, 20),
    (N'Country', N'TH', N'EN', N'BF', N'Burkina Faso', 1, 21),
    (N'Country', N'TH', N'TH', N'BF', N'บูร์กินาฟาโซ', 1, 21),
    (N'Country', N'TH', N'EN', N'BG', N'Bulgaria', 1, 22),
    (N'Country', N'TH', N'TH', N'BG', N'บัลแกเรีย', 1, 22),
    (N'Country', N'TH', N'EN', N'BH', N'Bahrain', 1, 23),
    (N'Country', N'TH', N'TH', N'BH', N'บาห์เรน', 1, 23),
    (N'Country', N'TH', N'EN', N'BI', N'Burundi', 1, 24),
    (N'Country', N'TH', N'TH', N'BI', N'บุรุนดี', 1, 24),
    (N'Country', N'TH', N'EN', N'BJ', N'Benin (Dahomey)', 1, 25),
    (N'Country', N'TH', N'TH', N'BJ', N'เบนิน', 1, 25),
    (N'Country', N'TH', N'EN', N'BM', N'Bermuda', 1, 26),
    (N'Country', N'TH', N'TH', N'BM', N'เบอร์มิวดา', 1, 26),
    (N'Country', N'TH', N'EN', N'BN', N'Brunei Darussalam', 1, 27),
    (N'Country', N'TH', N'TH', N'BN', N'บรูไนดารุสซาลาม', 1, 27),
    (N'Country', N'TH', N'EN', N'BO', N'Bolivia', 1, 28),
    (N'Country', N'TH', N'TH', N'BO', N'โบลิเวีย', 1, 28),
    (N'Country', N'TH', N'EN', N'BR', N'Brazil', 1, 29),
    (N'Country', N'TH', N'TH', N'BR', N'บราซิล', 1, 29),
    (N'Country', N'TH', N'EN', N'BS', N'Bahamas', 1, 30),
    (N'Country', N'TH', N'TH', N'BS', N'บาฮามาส', 1, 30),
    (N'Country', N'TH', N'EN', N'BT', N'Bhutan', 1, 31),
    (N'Country', N'TH', N'TH', N'BT', N'ภูฏาน', 1, 31),
    (N'Country', N'TH', N'EN', N'BV', N'Bouvet Island', 1, 32),
    (N'Country', N'TH', N'TH', N'BV', N'เกาะบูเวต', 1, 32),
    (N'Country', N'TH', N'EN', N'BW', N'Botswana', 1, 33),
    (N'Country', N'TH', N'TH', N'BW', N'บอตสวานา', 1, 33),
    (N'Country', N'TH', N'EN', N'BY', N'Belarus Region', 1, 34),
    (N'Country', N'TH', N'TH', N'BY', N'เบลารุส', 1, 34),
    (N'Country', N'TH', N'EN', N'BZ', N'Belize (British Honduras)', 1, 35),
    (N'Country', N'TH', N'TH', N'BZ', N'เบลีซ', 1, 35),
    (N'Country', N'TH', N'EN', N'CA', N'Canada', 1, 36),
    (N'Country', N'TH', N'TH', N'CA', N'แคนาดา', 1, 36),
    (N'Country', N'TH', N'EN', N'CC', N'Cocos (Keeling) Islands', 1, 37),
    (N'Country', N'TH', N'TH', N'CC', N'หมู่เกาะโคโคส (คีลิง)', 1, 37),
    (N'Country', N'TH', N'EN', N'CD', N'Congo, The Democratic Republic Of The', 1, 38),
    (N'Country', N'TH', N'TH', N'CD', N'สาธารณรัฐประชาธิปไตยคองโก', 1, 38),
    (N'Country', N'TH', N'EN', N'CF', N'Central African Republic', 1, 39),
    (N'Country', N'TH', N'TH', N'CF', N'สาธารณรัฐแอฟริกากลาง', 1, 39),
    (N'Country', N'TH', N'EN', N'CG', N'Congo', 1, 40),
    (N'Country', N'TH', N'TH', N'CG', N'คองโก', 1, 40),
    (N'Country', N'TH', N'EN', N'CH', N'Switzerland', 1, 41),
    (N'Country', N'TH', N'TH', N'CH', N'สวิตเซอร์แลนด์', 1, 41),
    (N'Country', N'TH', N'EN', N'CI', N'Cote D''Ivoire', 1, 42),
    (N'Country', N'TH', N'TH', N'CI', N'โกตดิวัวร์', 1, 42),
    (N'Country', N'TH', N'EN', N'CK', N'Cook Islands', 1, 43),
    (N'Country', N'TH', N'TH', N'CK', N'หมู่เกาะคุก', 1, 43),
    (N'Country', N'TH', N'EN', N'CL', N'Chile', 1, 44),
    (N'Country', N'TH', N'TH', N'CL', N'ชิลี', 1, 44),
    (N'Country', N'TH', N'EN', N'CM', N'Cameroon', 1, 45),
    (N'Country', N'TH', N'TH', N'CM', N'แคเมอรูน', 1, 45),
    (N'Country', N'TH', N'EN', N'CN', N'China', 1, 46),
    (N'Country', N'TH', N'TH', N'CN', N'จีน', 1, 46),
    (N'Country', N'TH', N'EN', N'CO', N'Colombia', 1, 47),
    (N'Country', N'TH', N'TH', N'CO', N'โคลอมเบีย', 1, 47),
    (N'Country', N'TH', N'EN', N'CR', N'Costa Rica', 1, 48),
    (N'Country', N'TH', N'TH', N'CR', N'คอสตาริกา', 1, 48),
    (N'Country', N'TH', N'EN', N'CU', N'Cuba', 1, 49),
    (N'Country', N'TH', N'TH', N'CU', N'คิวบา', 1, 49),
    (N'Country', N'TH', N'EN', N'CV', N'Cape Verde', 1, 50),
    (N'Country', N'TH', N'TH', N'CV', N'เคปเวิร์ด', 1, 50),
    (N'Country', N'TH', N'EN', N'CX', N'Christmas Island', 1, 51),
    (N'Country', N'TH', N'TH', N'CX', N'เกาะคริสต์มาส', 1, 51),
    (N'Country', N'TH', N'EN', N'CY', N'Cyprus', 1, 52),
    (N'Country', N'TH', N'TH', N'CY', N'ไซปรัส', 1, 52),
    (N'Country', N'TH', N'EN', N'CZ', N'Czech Republic', 1, 53),
    (N'Country', N'TH', N'TH', N'CZ', N'สาธารณรัฐเช็ก', 1, 53),
    (N'Country', N'TH', N'EN', N'DE', N'Germany', 1, 54),
    (N'Country', N'TH', N'TH', N'DE', N'เยอรมนี', 1, 54),
    (N'Country', N'TH', N'EN', N'DJ', N'Djibouti', 1, 55),
    (N'Country', N'TH', N'TH', N'DJ', N'จิบูตี', 1, 55),
    (N'Country', N'TH', N'EN', N'DK', N'Denmark', 1, 56),
    (N'Country', N'TH', N'TH', N'DK', N'เดนมาร์ก', 1, 56),
    (N'Country', N'TH', N'EN', N'DM', N'Dominica', 1, 57),
    (N'Country', N'TH', N'TH', N'DM', N'โดมินิกา', 1, 57),
    (N'Country', N'TH', N'EN', N'DO', N'Dominican Republic', 1, 58),
    (N'Country', N'TH', N'TH', N'DO', N'สาธารณรัฐโดมินิกัน', 1, 58),
    (N'Country', N'TH', N'EN', N'DZ', N'Algeria', 1, 59),
    (N'Country', N'TH', N'TH', N'DZ', N'แอลจีเรีย', 1, 59),
    (N'Country', N'TH', N'EN', N'EC', N'Ecuador', 1, 60),
    (N'Country', N'TH', N'TH', N'EC', N'เอกวาดอร์', 1, 60),
    (N'Country', N'TH', N'EN', N'EE', N'Estonia', 1, 61),
    (N'Country', N'TH', N'TH', N'EE', N'เอสโตเนีย', 1, 61),
    (N'Country', N'TH', N'EN', N'EG', N'Egypt', 1, 62),
    (N'Country', N'TH', N'TH', N'EG', N'อียิปต์', 1, 62),
    (N'Country', N'TH', N'EN', N'EH', N'Western Sahara', 1, 63),
    (N'Country', N'TH', N'TH', N'EH', N'ซาฮาราตะวันตก', 1, 63),
    (N'Country', N'TH', N'EN', N'ER', N'Eritrea', 1, 64),
    (N'Country', N'TH', N'TH', N'ER', N'เอริเทรีย', 1, 64),
    (N'Country', N'TH', N'EN', N'ES', N'Spain', 1, 65),
    (N'Country', N'TH', N'TH', N'ES', N'สเปน', 1, 65),
    (N'Country', N'TH', N'EN', N'ET', N'Ethiopia', 1, 66),
    (N'Country', N'TH', N'TH', N'ET', N'เอธิโอเปีย', 1, 66),
    (N'Country', N'TH', N'EN', N'FI', N'Finland', 1, 67),
    (N'Country', N'TH', N'TH', N'FI', N'ฟินแลนด์', 1, 67),
    (N'Country', N'TH', N'EN', N'FJ', N'Fiji', 1, 68),
    (N'Country', N'TH', N'TH', N'FJ', N'ฟิจิ', 1, 68),
    (N'Country', N'TH', N'EN', N'FK', N'Falkland Islands', 1, 69),
    (N'Country', N'TH', N'TH', N'FK', N'หมู่เกาะฟอล์กแลนด์', 1, 69),
    (N'Country', N'TH', N'EN', N'FM', N'Micronesia', 1, 70),
    (N'Country', N'TH', N'TH', N'FM', N'ไมโครนีเซีย', 1, 70),
    (N'Country', N'TH', N'EN', N'FO', N'Faeroe Islands', 1, 71),
    (N'Country', N'TH', N'TH', N'FO', N'หมู่เกาะแฟโร', 1, 71),
    (N'Country', N'TH', N'EN', N'FR', N'France', 1, 72),
    (N'Country', N'TH', N'TH', N'FR', N'ฝรั่งเศส', 1, 72),
    (N'Country', N'TH', N'EN', N'GA', N'Gabon', 1, 73),
    (N'Country', N'TH', N'TH', N'GA', N'กาบอง', 1, 73),
    (N'Country', N'TH', N'EN', N'GB', N'United Kingdom', 1, 74),
    (N'Country', N'TH', N'TH', N'GB', N'สหราชอาณาจักร', 1, 74),
    (N'Country', N'TH', N'EN', N'GD', N'Grenada', 1, 75),
    (N'Country', N'TH', N'TH', N'GD', N'เกรเนดา', 1, 75),
    (N'Country', N'TH', N'EN', N'GE', N'Georgia', 1, 76),
    (N'Country', N'TH', N'TH', N'GE', N'จอร์เจีย', 1, 76),
    (N'Country', N'TH', N'EN', N'GF', N'French Guiana', 1, 77),
    (N'Country', N'TH', N'TH', N'GF', N'เฟรนช์เกียนา', 1, 77),
    (N'Country', N'TH', N'EN', N'GG', N'Guernsey, C.I.', 1, 78),
    (N'Country', N'TH', N'TH', N'GG', N'เกิร์นซีย์', 1, 78),
    (N'Country', N'TH', N'EN', N'GH', N'Ghana', 1, 79),
    (N'Country', N'TH', N'TH', N'GH', N'กานา', 1, 79),
    (N'Country', N'TH', N'EN', N'GI', N'Gibraltar', 1, 80),
    (N'Country', N'TH', N'TH', N'GI', N'ยิบรอลตาร์', 1, 80),
    (N'Country', N'TH', N'EN', N'GL', N'Greenland', 1, 81),
    (N'Country', N'TH', N'TH', N'GL', N'กรีนแลนด์', 1, 81),
    (N'Country', N'TH', N'EN', N'GM', N'Gambia', 1, 82),
    (N'Country', N'TH', N'TH', N'GM', N'แกมเบีย', 1, 82),
    (N'Country', N'TH', N'EN', N'GN', N'Guinea', 1, 83),
    (N'Country', N'TH', N'TH', N'GN', N'กินี', 1, 83),
    (N'Country', N'TH', N'EN', N'GP', N'Guadeloupe', 1, 84),
    (N'Country', N'TH', N'TH', N'GP', N'กวาเดอลุป', 1, 84),
    (N'Country', N'TH', N'EN', N'GQ', N'Equatorial Guinea', 1, 85),
    (N'Country', N'TH', N'TH', N'GQ', N'อิเควทอเรียลกินี', 1, 85),
    (N'Country', N'TH', N'EN', N'GR', N'Greece', 1, 86),
    (N'Country', N'TH', N'TH', N'GR', N'กรีซ', 1, 86),
    (N'Country', N'TH', N'EN', N'GS', N'South Georgia And South Sandwich Islands', 1, 87),
    (N'Country', N'TH', N'TH', N'GS', N'เกาะเซาท์จอร์เจียและหมู่เกาะเซาท์แซนด์วิช', 1, 87),
    (N'Country', N'TH', N'EN', N'GT', N'Guatemala', 1, 88),
    (N'Country', N'TH', N'TH', N'GT', N'กัวเตมาลา', 1, 88),
    (N'Country', N'TH', N'EN', N'GU', N'Guam', 1, 89),
    (N'Country', N'TH', N'TH', N'GU', N'กวม', 1, 89),
    (N'Country', N'TH', N'EN', N'GW', N'Guinea-Bissau', 1, 90),
    (N'Country', N'TH', N'TH', N'GW', N'กินี-บิสเซา', 1, 90),
    (N'Country', N'TH', N'EN', N'GY', N'Guyana', 1, 91),
    (N'Country', N'TH', N'TH', N'GY', N'กายอานา', 1, 91),
    (N'Country', N'TH', N'EN', N'HK', N'Hong Kong', 1, 92),
    (N'Country', N'TH', N'TH', N'HK', N'ฮ่องกง', 1, 92),
    (N'Country', N'TH', N'EN', N'HM', N'Heard And Mcdonald Islands', 1, 93),
    (N'Country', N'TH', N'TH', N'HM', N'เกาะเฮิร์ดและหมู่เกาะแมกดอนัลด์', 1, 93),
    (N'Country', N'TH', N'EN', N'HN', N'Honduras', 1, 94),
    (N'Country', N'TH', N'TH', N'HN', N'ฮอนดูรัส', 1, 94),
    (N'Country', N'TH', N'EN', N'HR', N'Croatia', 1, 95),
    (N'Country', N'TH', N'TH', N'HR', N'โครเอเชีย', 1, 95),
    (N'Country', N'TH', N'EN', N'HT', N'Haiti', 1, 96),
    (N'Country', N'TH', N'TH', N'HT', N'เฮติ', 1, 96),
    (N'Country', N'TH', N'EN', N'HU', N'Hungary', 1, 97),
    (N'Country', N'TH', N'TH', N'HU', N'ฮังการี', 1, 97),
    (N'Country', N'TH', N'EN', N'ID', N'Indonesia', 1, 98),
    (N'Country', N'TH', N'TH', N'ID', N'อินโดนีเซีย', 1, 98),
    (N'Country', N'TH', N'EN', N'IE', N'Ireland', 1, 99),
    (N'Country', N'TH', N'TH', N'IE', N'ไอร์แลนด์', 1, 99),
    (N'Country', N'TH', N'EN', N'IL', N'Israel', 1, 100),
    (N'Country', N'TH', N'TH', N'IL', N'อิสราเอล', 1, 100),
    (N'Country', N'TH', N'EN', N'IM', N'Isle Of Man', 1, 101),
    (N'Country', N'TH', N'TH', N'IM', N'เกาะแมน', 1, 101),
    (N'Country', N'TH', N'EN', N'IN', N'India', 1, 102),
    (N'Country', N'TH', N'TH', N'IN', N'อินเดีย', 1, 102),
    (N'Country', N'TH', N'EN', N'IO', N'British Indian Ocean Territory', 1, 103),
    (N'Country', N'TH', N'TH', N'IO', N'บริติชอินเดียนโอเชียนเทร์ริทอรี', 1, 103),
    (N'Country', N'TH', N'EN', N'IQ', N'Iraq', 1, 104),
    (N'Country', N'TH', N'TH', N'IQ', N'อิรัก', 1, 104),
    (N'Country', N'TH', N'EN', N'IR', N'Iran (Islamic Republic Of)', 1, 105),
    (N'Country', N'TH', N'TH', N'IR', N'อิหร่าน', 1, 105),
    (N'Country', N'TH', N'EN', N'IS', N'Iceland', 1, 106),
    (N'Country', N'TH', N'TH', N'IS', N'ไอซ์แลนด์', 1, 106),
    (N'Country', N'TH', N'EN', N'IT', N'Italy', 1, 107),
    (N'Country', N'TH', N'TH', N'IT', N'อิตาลี', 1, 107),
    (N'Country', N'TH', N'EN', N'JE', N'Jersey, C.I.', 1, 108),
    (N'Country', N'TH', N'TH', N'JE', N'เจอร์ซีย์', 1, 108),
    (N'Country', N'TH', N'EN', N'JM', N'Jamaica', 1, 109),
    (N'Country', N'TH', N'TH', N'JM', N'จาเมกา', 1, 109),
    (N'Country', N'TH', N'EN', N'JO', N'Jordan', 1, 110),
    (N'Country', N'TH', N'TH', N'JO', N'จอร์แดน', 1, 110),
    (N'Country', N'TH', N'EN', N'JP', N'Japan', 1, 111),
    (N'Country', N'TH', N'TH', N'JP', N'ญี่ปุ่น', 1, 111),
    (N'Country', N'TH', N'EN', N'KE', N'Kenya', 1, 112),
    (N'Country', N'TH', N'TH', N'KE', N'เคนยา', 1, 112),
    (N'Country', N'TH', N'EN', N'KG', N'Kyrgyzstan', 1, 113),
    (N'Country', N'TH', N'TH', N'KG', N'คีร์กีซสถาน', 1, 113),
    (N'Country', N'TH', N'EN', N'KH', N'Cambodia', 1, 114),
    (N'Country', N'TH', N'TH', N'KH', N'กัมพูชา', 1, 114),
    (N'Country', N'TH', N'EN', N'KI', N'Kiribati', 1, 115),
    (N'Country', N'TH', N'TH', N'KI', N'คิริบาส', 1, 115),
    (N'Country', N'TH', N'EN', N'KM', N'Comoros', 1, 116),
    (N'Country', N'TH', N'TH', N'KM', N'คอโมโรส', 1, 116),
    (N'Country', N'TH', N'EN', N'KN', N'Saint Kitts And Nevis', 1, 117),
    (N'Country', N'TH', N'TH', N'KN', N'เซนต์คิตส์และเนวิส', 1, 117),
    (N'Country', N'TH', N'EN', N'KP', N'Korea, Democratic People''s Republic Of', 1, 118),
    (N'Country', N'TH', N'TH', N'KP', N'เกาหลีเหนือ', 1, 118),
    (N'Country', N'TH', N'EN', N'KR', N'Korea, Republic Of (South)', 1, 119),
    (N'Country', N'TH', N'TH', N'KR', N'เกาหลีใต้', 1, 119),
    (N'Country', N'TH', N'EN', N'KW', N'Kuwait', 1, 120),
    (N'Country', N'TH', N'TH', N'KW', N'คูเวต', 1, 120),
    (N'Country', N'TH', N'EN', N'KY', N'Cayman Islands', 1, 121),
    (N'Country', N'TH', N'TH', N'KY', N'หมู่เกาะเคย์แมน', 1, 121),
    (N'Country', N'TH', N'EN', N'KZ', N'Kazakhstan', 1, 122),
    (N'Country', N'TH', N'TH', N'KZ', N'คาซัคสถาน', 1, 122),
    (N'Country', N'TH', N'EN', N'LA', N'Lao People''s Democratic Republic', 1, 123),
    (N'Country', N'TH', N'TH', N'LA', N'ลาว', 1, 123),
    (N'Country', N'TH', N'EN', N'LB', N'Lebanon', 1, 124),
    (N'Country', N'TH', N'TH', N'LB', N'เลบานอน', 1, 124),
    (N'Country', N'TH', N'EN', N'LC', N'Saint Lucia', 1, 125),
    (N'Country', N'TH', N'TH', N'LC', N'เซนต์ลูเซีย', 1, 125),
    (N'Country', N'TH', N'EN', N'LI', N'Liechtenstein', 1, 126),
    (N'Country', N'TH', N'TH', N'LI', N'ลิกเตนสไตน์', 1, 126),
    (N'Country', N'TH', N'EN', N'LK', N'Sri Lanka', 1, 127),
    (N'Country', N'TH', N'TH', N'LK', N'ศรีลังกา', 1, 127),
    (N'Country', N'TH', N'EN', N'LR', N'Liberia', 1, 128),
    (N'Country', N'TH', N'TH', N'LR', N'ไลบีเรีย', 1, 128),
    (N'Country', N'TH', N'EN', N'LS', N'Lesotho', 1, 129),
    (N'Country', N'TH', N'TH', N'LS', N'เลโซโท', 1, 129),
    (N'Country', N'TH', N'EN', N'LT', N'Lithuania', 1, 130),
    (N'Country', N'TH', N'TH', N'LT', N'ลิทัวเนีย', 1, 130),
    (N'Country', N'TH', N'EN', N'LU', N'Luxembourg', 1, 131),
    (N'Country', N'TH', N'TH', N'LU', N'ลักเซมเบิร์ก', 1, 131),
    (N'Country', N'TH', N'EN', N'LV', N'Latvia', 1, 132),
    (N'Country', N'TH', N'TH', N'LV', N'ลัตเวีย', 1, 132),
    (N'Country', N'TH', N'EN', N'LY', N'Libyan Arab Jamahiriya', 1, 133),
    (N'Country', N'TH', N'TH', N'LY', N'ลิเบีย', 1, 133),
    (N'Country', N'TH', N'EN', N'MA', N'Morocco', 1, 134),
    (N'Country', N'TH', N'TH', N'MA', N'โมร็อกโก', 1, 134),
    (N'Country', N'TH', N'EN', N'MC', N'Monaco', 1, 135),
    (N'Country', N'TH', N'TH', N'MC', N'โมนาโก', 1, 135),
    (N'Country', N'TH', N'EN', N'MD', N'Moldova', 1, 136),
    (N'Country', N'TH', N'TH', N'MD', N'มอลโดวา', 1, 136),
    (N'Country', N'TH', N'EN', N'ME', N'Montenegro', 1, 137),
    (N'Country', N'TH', N'TH', N'ME', N'มอนเตเนโกร', 1, 137),
    (N'Country', N'TH', N'EN', N'MG', N'Madagascar', 1, 138),
    (N'Country', N'TH', N'TH', N'MG', N'มาดากัสการ์', 1, 138),
    (N'Country', N'TH', N'EN', N'MH', N'Marshall Islands', 1, 139),
    (N'Country', N'TH', N'TH', N'MH', N'หมู่เกาะมาร์แชลล์', 1, 139),
    (N'Country', N'TH', N'EN', N'MK', N'Macedonia', 1, 140),
    (N'Country', N'TH', N'TH', N'MK', N'มาซิโดเนีย', 1, 140),
    (N'Country', N'TH', N'EN', N'ML', N'Mali', 1, 141),
    (N'Country', N'TH', N'TH', N'ML', N'มาลี', 1, 141),
    (N'Country', N'TH', N'EN', N'MM', N'Myanmar', 1, 142),
    (N'Country', N'TH', N'TH', N'MM', N'เมียนมา', 1, 142),
    (N'Country', N'TH', N'EN', N'MN', N'Mongolia', 1, 143),
    (N'Country', N'TH', N'TH', N'MN', N'มองโกเลีย', 1, 143),
    (N'Country', N'TH', N'EN', N'MO', N'Macau', 1, 144),
    (N'Country', N'TH', N'TH', N'MO', N'มาเก๊า', 1, 144),
    (N'Country', N'TH', N'EN', N'MP', N'Northern Mariana Islands', 1, 145),
    (N'Country', N'TH', N'TH', N'MP', N'หมู่เกาะนอร์เทิร์นมาเรียนา', 1, 145),
    (N'Country', N'TH', N'EN', N'MQ', N'Martinique', 1, 146),
    (N'Country', N'TH', N'TH', N'MQ', N'มาร์ตินีก', 1, 146),
    (N'Country', N'TH', N'EN', N'MR', N'Mauritania', 1, 147),
    (N'Country', N'TH', N'TH', N'MR', N'มอริเตเนีย', 1, 147),
    (N'Country', N'TH', N'EN', N'MS', N'Montserrat', 1, 148),
    (N'Country', N'TH', N'TH', N'MS', N'มอนต์เซอร์รัต', 1, 148),
    (N'Country', N'TH', N'EN', N'MT', N'Malta', 1, 149),
    (N'Country', N'TH', N'TH', N'MT', N'มอลตา', 1, 149),
    (N'Country', N'TH', N'EN', N'MU', N'Mauritius', 1, 150),
    (N'Country', N'TH', N'TH', N'MU', N'มอริเชียส', 1, 150),
    (N'Country', N'TH', N'EN', N'MV', N'Maldives', 1, 151),
    (N'Country', N'TH', N'TH', N'MV', N'มัลดีฟส์', 1, 151),
    (N'Country', N'TH', N'EN', N'MW', N'Malawi', 1, 152),
    (N'Country', N'TH', N'TH', N'MW', N'มาลาวี', 1, 152),
    (N'Country', N'TH', N'EN', N'MX', N'Mexico', 1, 153),
    (N'Country', N'TH', N'TH', N'MX', N'เม็กซิโก', 1, 153),
    (N'Country', N'TH', N'EN', N'MY', N'Malaysia', 1, 154),
    (N'Country', N'TH', N'TH', N'MY', N'มาเลเซีย', 1, 154),
    (N'Country', N'TH', N'EN', N'MZ', N'Mozambique', 1, 155),
    (N'Country', N'TH', N'TH', N'MZ', N'โมซัมบิก', 1, 155),
    (N'Country', N'TH', N'EN', N'NA', N'Namibia', 1, 156),
    (N'Country', N'TH', N'TH', N'NA', N'นามิเบีย', 1, 156),
    (N'Country', N'TH', N'EN', N'NC', N'New Caledonia', 1, 157),
    (N'Country', N'TH', N'TH', N'NC', N'นิวแคลิโดเนีย', 1, 157),
    (N'Country', N'TH', N'EN', N'NE', N'Niger', 1, 158),
    (N'Country', N'TH', N'TH', N'NE', N'ไนเจอร์', 1, 158),
    (N'Country', N'TH', N'EN', N'NF', N'Norfolk Island', 1, 159),
    (N'Country', N'TH', N'TH', N'NF', N'เกาะนอร์ฟอล์ก', 1, 159),
    (N'Country', N'TH', N'EN', N'NG', N'Nigeria', 1, 160),
    (N'Country', N'TH', N'TH', N'NG', N'ไนจีเรีย', 1, 160),
    (N'Country', N'TH', N'EN', N'NI', N'Nicaragua', 1, 161),
    (N'Country', N'TH', N'TH', N'NI', N'นิการากัว', 1, 161),
    (N'Country', N'TH', N'EN', N'NL', N'Netherlands', 1, 162),
    (N'Country', N'TH', N'TH', N'NL', N'เนเธอร์แลนด์', 1, 162),
    (N'Country', N'TH', N'EN', N'NO', N'Norway', 1, 163),
    (N'Country', N'TH', N'TH', N'NO', N'นอร์เวย์', 1, 163),
    (N'Country', N'TH', N'EN', N'NP', N'Nepal', 1, 164),
    (N'Country', N'TH', N'TH', N'NP', N'เนปาล', 1, 164),
    (N'Country', N'TH', N'EN', N'NR', N'Nauru', 1, 165),
    (N'Country', N'TH', N'TH', N'NR', N'นาอูรู', 1, 165),
    (N'Country', N'TH', N'EN', N'NU', N'Niue', 1, 166),
    (N'Country', N'TH', N'TH', N'NU', N'นีอูเอ', 1, 166),
    (N'Country', N'TH', N'EN', N'NZ', N'New Zealand', 1, 167),
    (N'Country', N'TH', N'TH', N'NZ', N'นิวซีแลนด์', 1, 167),
    (N'Country', N'TH', N'EN', N'OM', N'Oman', 1, 168),
    (N'Country', N'TH', N'TH', N'OM', N'โอมาน', 1, 168),
    (N'Country', N'TH', N'EN', N'PA', N'Panama', 1, 169),
    (N'Country', N'TH', N'TH', N'PA', N'ปานามา', 1, 169),
    (N'Country', N'TH', N'EN', N'PE', N'Peru', 1, 170),
    (N'Country', N'TH', N'TH', N'PE', N'เปรู', 1, 170),
    (N'Country', N'TH', N'EN', N'PF', N'French Polynesia', 1, 171),
    (N'Country', N'TH', N'TH', N'PF', N'เฟรนช์โปลินีเซีย', 1, 171),
    (N'Country', N'TH', N'EN', N'PG', N'Papua New Guinea', 1, 172),
    (N'Country', N'TH', N'TH', N'PG', N'ปาปัวนิวกินี', 1, 172),
    (N'Country', N'TH', N'EN', N'PH', N'Philippines', 1, 173),
    (N'Country', N'TH', N'TH', N'PH', N'ฟิลิปปินส์', 1, 173),
    (N'Country', N'TH', N'EN', N'PK', N'Pakistan', 1, 174),
    (N'Country', N'TH', N'TH', N'PK', N'ปากีสถาน', 1, 174),
    (N'Country', N'TH', N'EN', N'PL', N'Poland', 1, 175),
    (N'Country', N'TH', N'TH', N'PL', N'โปแลนด์', 1, 175),
    (N'Country', N'TH', N'EN', N'PM', N'St. Pierre And Miquelon', 1, 176),
    (N'Country', N'TH', N'TH', N'PM', N'แซงปีแยร์และมีเกอลง', 1, 176),
    (N'Country', N'TH', N'EN', N'PN', N'Pitcairn', 1, 177),
    (N'Country', N'TH', N'TH', N'PN', N'พิตแคร์น', 1, 177),
    (N'Country', N'TH', N'EN', N'PR', N'Puerto Rico', 1, 178),
    (N'Country', N'TH', N'TH', N'PR', N'เปอร์โตริโก', 1, 178),
    (N'Country', N'TH', N'EN', N'PS', N'Palestinian Territory, Occupied', 1, 179),
    (N'Country', N'TH', N'TH', N'PS', N'ปาเลสไตน์', 1, 179),
    (N'Country', N'TH', N'EN', N'PT', N'Portugal', 1, 180),
    (N'Country', N'TH', N'TH', N'PT', N'โปรตุเกส', 1, 180),
    (N'Country', N'TH', N'EN', N'PW', N'Palau', 1, 181),
    (N'Country', N'TH', N'TH', N'PW', N'ปาเลา', 1, 181),
    (N'Country', N'TH', N'EN', N'PY', N'Paraguay', 1, 182),
    (N'Country', N'TH', N'TH', N'PY', N'ปารากวัย', 1, 182),
    (N'Country', N'TH', N'EN', N'PZ', N'Panama Canal Zone', 1, 183),
    (N'Country', N'TH', N'TH', N'PZ', N'เขตคลองปานามา', 1, 183),
    (N'Country', N'TH', N'EN', N'QA', N'Qatar', 1, 184),
    (N'Country', N'TH', N'TH', N'QA', N'กาตาร์', 1, 184),
    (N'Country', N'TH', N'EN', N'RE', N'Reunion', 1, 185),
    (N'Country', N'TH', N'TH', N'RE', N'เรอูนียง', 1, 185),
    (N'Country', N'TH', N'EN', N'RO', N'Romania', 1, 186),
    (N'Country', N'TH', N'TH', N'RO', N'โรมาเนีย', 1, 186),
    (N'Country', N'TH', N'EN', N'RS', N'Serbia', 1, 187),
    (N'Country', N'TH', N'TH', N'RS', N'เซอร์เบีย', 1, 187),
    (N'Country', N'TH', N'EN', N'RU', N'Russian Federation', 1, 188),
    (N'Country', N'TH', N'TH', N'RU', N'รัสเซีย', 1, 188),
    (N'Country', N'TH', N'EN', N'RW', N'Rwanda', 1, 189),
    (N'Country', N'TH', N'TH', N'RW', N'รวันดา', 1, 189),
    (N'Country', N'TH', N'EN', N'SA', N'Saudi Arabia', 1, 190),
    (N'Country', N'TH', N'TH', N'SA', N'ซาอุดีอาระเบีย', 1, 190),
    (N'Country', N'TH', N'EN', N'SB', N'Solomon Islands', 1, 191),
    (N'Country', N'TH', N'TH', N'SB', N'หมู่เกาะโซโลมอน', 1, 191),
    (N'Country', N'TH', N'EN', N'SC', N'Seychelles', 1, 192),
    (N'Country', N'TH', N'TH', N'SC', N'เซเชลส์', 1, 192),
    (N'Country', N'TH', N'EN', N'SD', N'Sudan', 1, 193),
    (N'Country', N'TH', N'TH', N'SD', N'ซูดาน', 1, 193),
    (N'Country', N'TH', N'EN', N'SE', N'Sweden', 1, 194),
    (N'Country', N'TH', N'TH', N'SE', N'สวีเดน', 1, 194),
    (N'Country', N'TH', N'EN', N'SG', N'Singapore', 1, 195),
    (N'Country', N'TH', N'TH', N'SG', N'สิงคโปร์', 1, 195),
    (N'Country', N'TH', N'EN', N'SH', N'St. Helena', 1, 196),
    (N'Country', N'TH', N'TH', N'SH', N'เซนต์เฮเลนา', 1, 196),
    (N'Country', N'TH', N'EN', N'SI', N'Slovenia', 1, 197),
    (N'Country', N'TH', N'TH', N'SI', N'สโลวีเนีย', 1, 197),
    (N'Country', N'TH', N'EN', N'SJ', N'Svalbard And Jan Mayen Islands', 1, 198),
    (N'Country', N'TH', N'TH', N'SJ', N'สฟาลบาร์และยันไมเอน', 1, 198),
    (N'Country', N'TH', N'EN', N'SK', N'Slovakia', 1, 199),
    (N'Country', N'TH', N'TH', N'SK', N'สโลวาเกีย', 1, 199),
    (N'Country', N'TH', N'EN', N'SL', N'Sierra Leone', 1, 200),
    (N'Country', N'TH', N'TH', N'SL', N'เซียร์ราลีโอน', 1, 200),
    (N'Country', N'TH', N'EN', N'SM', N'San Marino', 1, 201),
    (N'Country', N'TH', N'TH', N'SM', N'ซานมารีโน', 1, 201),
    (N'Country', N'TH', N'EN', N'SN', N'Senegal', 1, 202),
    (N'Country', N'TH', N'TH', N'SN', N'เซเนกัล', 1, 202),
    (N'Country', N'TH', N'EN', N'SO', N'Somalia', 1, 203),
    (N'Country', N'TH', N'TH', N'SO', N'โซมาเลีย', 1, 203),
    (N'Country', N'TH', N'EN', N'SR', N'Suriname', 1, 204),
    (N'Country', N'TH', N'TH', N'SR', N'ซูรินาเม', 1, 204),
    (N'Country', N'TH', N'EN', N'SS', N'South Sudan', 1, 205),
    (N'Country', N'TH', N'TH', N'SS', N'เซาท์ซูดาน', 1, 205),
    (N'Country', N'TH', N'EN', N'ST', N'Sao Tome And Principe Islands', 1, 206),
    (N'Country', N'TH', N'TH', N'ST', N'เซาตูเมและปรินซิปี', 1, 206),
    (N'Country', N'TH', N'EN', N'SV', N'El Salvador', 1, 207),
    (N'Country', N'TH', N'TH', N'SV', N'เอลซัลวาดอร์', 1, 207),
    (N'Country', N'TH', N'EN', N'SY', N'Syrian Arab Republic', 1, 208),
    (N'Country', N'TH', N'TH', N'SY', N'ซีเรีย', 1, 208),
    (N'Country', N'TH', N'EN', N'SZ', N'Swaziland', 1, 209),
    (N'Country', N'TH', N'TH', N'SZ', N'สวาซิแลนด์', 1, 209),
    (N'Country', N'TH', N'EN', N'TC', N'Turks And Caicos Ialands', 1, 210),
    (N'Country', N'TH', N'TH', N'TC', N'หมู่เกาะเติกส์และเคคอส', 1, 210),
    (N'Country', N'TH', N'EN', N'TD', N'Chad', 1, 211),
    (N'Country', N'TH', N'TH', N'TD', N'ชาด', 1, 211),
    (N'Country', N'TH', N'EN', N'TF', N'French Southern Territories', 1, 212),
    (N'Country', N'TH', N'TH', N'TF', N'เฟรนช์เซาเทิร์นเทร์ริทอรีส์', 1, 212),
    (N'Country', N'TH', N'EN', N'TG', N'Togo', 1, 213),
    (N'Country', N'TH', N'TH', N'TG', N'โตโก', 1, 213),
    (N'Country', N'TH', N'EN', N'TH', N'Thailand', 1, 214),
    (N'Country', N'TH', N'TH', N'TH', N'ไทย', 1, 214),
    (N'Country', N'TH', N'EN', N'TJ', N'Tajikistan', 1, 215),
    (N'Country', N'TH', N'TH', N'TJ', N'ทาจิกิสถาน', 1, 215),
    (N'Country', N'TH', N'EN', N'TK', N'Tokelau', 1, 216),
    (N'Country', N'TH', N'TH', N'TK', N'โตเกเลา', 1, 216),
    (N'Country', N'TH', N'EN', N'TL', N'Timor-Leste', 1, 217),
    (N'Country', N'TH', N'TH', N'TL', N'ติมอร์-เลสเต', 1, 217),
    (N'Country', N'TH', N'EN', N'TM', N'Turkmenistan Region', 1, 218),
    (N'Country', N'TH', N'TH', N'TM', N'เติร์กเมนิสถาน', 1, 218),
    (N'Country', N'TH', N'EN', N'TN', N'Tunisia', 1, 219),
    (N'Country', N'TH', N'TH', N'TN', N'ตูนิเซีย', 1, 219),
    (N'Country', N'TH', N'EN', N'TO', N'Tonga', 1, 220),
    (N'Country', N'TH', N'TH', N'TO', N'ตองงา', 1, 220),
    (N'Country', N'TH', N'EN', N'TP', N'East Timor', 1, 221),
    (N'Country', N'TH', N'TH', N'TP', N'ติมอร์ตะวันออก', 1, 221),
    (N'Country', N'TH', N'EN', N'TR', N'Turkey', 1, 222),
    (N'Country', N'TH', N'TH', N'TR', N'ตุรกี', 1, 222),
    (N'Country', N'TH', N'EN', N'TT', N'Trinidad And Tobago', 1, 223),
    (N'Country', N'TH', N'TH', N'TT', N'ตรินิแดดและโตเบโก', 1, 223),
    (N'Country', N'TH', N'EN', N'TV', N'Tuvalu', 1, 224),
    (N'Country', N'TH', N'TH', N'TV', N'ตูวาลู', 1, 224),
    (N'Country', N'TH', N'EN', N'TW', N'Taiwan, Province Of China', 1, 225),
    (N'Country', N'TH', N'TH', N'TW', N'ไต้หวัน', 1, 225),
    (N'Country', N'TH', N'EN', N'TZ', N'Tanzania, United Republic Of', 1, 226),
    (N'Country', N'TH', N'TH', N'TZ', N'แทนซาเนีย', 1, 226),
    (N'Country', N'TH', N'EN', N'UA', N'Ukraine', 1, 227),
    (N'Country', N'TH', N'TH', N'UA', N'ยูเครน', 1, 227),
    (N'Country', N'TH', N'EN', N'UG', N'Uganda', 1, 228),
    (N'Country', N'TH', N'TH', N'UG', N'ยูกันดา', 1, 228),
    (N'Country', N'TH', N'EN', N'UM', N'United States Minor Outlying Islands', 1, 229),
    (N'Country', N'TH', N'TH', N'UM', N'หมู่เกาะเล็กรอบนอกของสหรัฐอเมริกา', 1, 229),
    (N'Country', N'TH', N'EN', N'US', N'United States Of America', 1, 230),
    (N'Country', N'TH', N'TH', N'US', N'สหรัฐอเมริกา', 1, 230),
    (N'Country', N'TH', N'EN', N'UY', N'Uruguay', 1, 231),
    (N'Country', N'TH', N'TH', N'UY', N'อุรุกวัย', 1, 231),
    (N'Country', N'TH', N'EN', N'UZ', N'Uzbekistan', 1, 232),
    (N'Country', N'TH', N'TH', N'UZ', N'อุซเบกิสถาน', 1, 232),
    (N'Country', N'TH', N'EN', N'VA', N'Vatican City State', 1, 233),
    (N'Country', N'TH', N'TH', N'VA', N'นครวาติกัน', 1, 233),
    (N'Country', N'TH', N'EN', N'VC', N'Saint Vincent And Grenadines', 1, 234),
    (N'Country', N'TH', N'TH', N'VC', N'เซนต์วินเซนต์และเกรนาดีนส์', 1, 234),
    (N'Country', N'TH', N'EN', N'VE', N'Venezuela', 1, 235),
    (N'Country', N'TH', N'TH', N'VE', N'เวเนซุเอลา', 1, 235),
    (N'Country', N'TH', N'EN', N'VG', N'Virgin Islands (British)', 1, 236),
    (N'Country', N'TH', N'TH', N'VG', N'หมู่เกาะบริติชเวอร์จิน', 1, 236),
    (N'Country', N'TH', N'EN', N'VI', N'Virgin Islands, U.S.', 1, 237),
    (N'Country', N'TH', N'TH', N'VI', N'หมู่เกาะเวอร์จินของสหรัฐอเมริกา', 1, 237),
    (N'Country', N'TH', N'EN', N'VN', N'Vietnam', 1, 238),
    (N'Country', N'TH', N'TH', N'VN', N'เวียดนาม', 1, 238),
    (N'Country', N'TH', N'EN', N'VU', N'Vanuatu (New Hebrides)', 1, 239),
    (N'Country', N'TH', N'TH', N'VU', N'วานูอาตู', 1, 239),
    (N'Country', N'TH', N'EN', N'WF', N'Wallis And Futuna Islands', 1, 240),
    (N'Country', N'TH', N'TH', N'WF', N'หมู่เกาะวาลลิสและฟุตูนา', 1, 240),
    (N'Country', N'TH', N'EN', N'WS', N'Western Samoa', 1, 241),
    (N'Country', N'TH', N'TH', N'WS', N'ซามัว', 1, 241),
    (N'Country', N'TH', N'EN', N'YE', N'Yemen, Democratic (South)', 1, 242),
    (N'Country', N'TH', N'TH', N'YE', N'เยเมน', 1, 242),
    (N'Country', N'TH', N'EN', N'YT', N'Mayotte', 1, 243),
    (N'Country', N'TH', N'TH', N'YT', N'มายอต', 1, 243),
    (N'Country', N'TH', N'EN', N'ZA', N'South Africa', 1, 244),
    (N'Country', N'TH', N'TH', N'ZA', N'แอฟริกาใต้', 1, 244),
    (N'Country', N'TH', N'EN', N'ZM', N'Zambia', 1, 245),
    (N'Country', N'TH', N'TH', N'ZM', N'แซมเบีย', 1, 245),
    (N'Country', N'TH', N'EN', N'ZW', N'Zimbabwe', 1, 246),
    (N'Country', N'TH', N'TH', N'ZW', N'ซิมบับเว', 1, 246);
GO

-- ============================================================
-- END OF SCRIPT
-- ============================================================