-- ----------------------------------------
-- Replace placeholder RoutebackReason parameters with real bilingual reasons.
-- Used by the Summary & Decision screen routeback (movement "B") reason dropdown.
-- ----------------------------------------
DELETE FROM parameter.Parameters WHERE [group] = N'RoutebackReason';
GO

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'RoutebackReason', N'TH', N'EN', N'01', N'Incomplete or missing documents', 1, 1),
    (N'RoutebackReason', N'TH', N'TH', N'01', N'เอกสารไม่ครบถ้วนหรือขาดหาย', 1, 1),
    (N'RoutebackReason', N'TH', N'EN', N'02', N'Incorrect request information', 1, 2),
    (N'RoutebackReason', N'TH', N'TH', N'02', N'ข้อมูลคำขอไม่ถูกต้อง', 1, 2),
    (N'RoutebackReason', N'TH', N'EN', N'03', N'Additional information required', 1, 3),
    (N'RoutebackReason', N'TH', N'TH', N'03', N'ต้องการข้อมูลเพิ่มเติม', 1, 3),
    (N'RoutebackReason', N'TH', N'EN', N'04', N'Incorrect collateral/property details', 1, 4),
    (N'RoutebackReason', N'TH', N'TH', N'04', N'ข้อมูลหลักประกันไม่ถูกต้อง', 1, 4),
    (N'RoutebackReason', N'TH', N'EN', N'05', N'Title document issue', 1, 5),
    (N'RoutebackReason', N'TH', N'TH', N'05', N'เอกสารสิทธิ์มีปัญหา', 1, 5),
    (N'RoutebackReason', N'TH', N'EN', N'06', N'Pending customer confirmation', 1, 6),
    (N'RoutebackReason', N'TH', N'TH', N'06', N'รอการยืนยันจากลูกค้า', 1, 6),
    (N'RoutebackReason', N'TH', N'EN', N'99', N'Other', 1, 7),
    (N'RoutebackReason', N'TH', N'TH', N'99', N'อื่นๆ', 1, 7);
GO
