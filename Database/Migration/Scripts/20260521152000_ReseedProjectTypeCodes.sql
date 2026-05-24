-- Re-seed ProjectType parameter group to align with short text code storage:
-- U = Condo, LB = LandAndBuilding, L = Land and Building (Construction)
-- Replaces old 01/02 codes with U/LB/L codes to match what the DB now stores.

DELETE FROM parameter.Parameters WHERE [Group] = N'ProjectType';

INSERT INTO parameter.Parameters ([Group], Country, Language, Code, Description, IsActive, SeqNo)
VALUES
    (N'ProjectType', N'TH', N'EN', N'U',  N'Condominium',                                      1, 1),
    (N'ProjectType', N'TH', N'TH', N'U',  N'คอนโดมิเนียม',                                      1, 1),
    (N'ProjectType', N'TH', N'EN', N'LB', N'Land and Building',                                1, 2),
    (N'ProjectType', N'TH', N'TH', N'LB', N'ที่ดินและสิ่งปลูกสร้าง',                              1, 2),
    (N'ProjectType', N'TH', N'EN', N'L',  N'Land and Building (Construction)',                  1, 3),
    (N'ProjectType', N'TH', N'TH', N'L',  N'ที่ดินและสิ่งปลูกสร้าง (อยู่ระหว่างก่อสร้าง)',          1, 3);
