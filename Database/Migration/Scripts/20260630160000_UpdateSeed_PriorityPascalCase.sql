-- ============================================================
-- Standardize the Priority parameter group to canonical Pascal-case
-- codes/descriptions (Normal / High), matching the Priority value object.
-- ============================================================

DELETE FROM parameter.Parameters WHERE [group] = N'Priority';
GO

INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
VALUES
    (N'Priority', N'TH', N'EN', N'High',   N'High',  1, 1),
    (N'Priority', N'TH', N'TH', N'High',   N'ด่วน',   1, 1),
    (N'Priority', N'TH', N'EN', N'Normal', N'Normal', 1, 2),
    (N'Priority', N'TH', N'TH', N'Normal', N'ปรกติ',  1, 2);
GO
