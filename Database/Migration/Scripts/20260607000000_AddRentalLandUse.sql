-- ----------------------------------------
-- Add LandUse parameter '05' Rental
-- Ported from release/uat/v3 (commit af352bc4). Authored as a NEW DbUp script
-- instead of editing the already-applied 20260317002600_SeedData_GeneralParameter.sql,
-- which is journaled run-once and will not re-execute on existing databases.
--
-- Idempotent: inserts the Rental option only if missing, and shifts the existing
-- '99' Other entries to seqno 6 so Rental (seqno 5) sorts before Other. Safe to re-run.
-- ----------------------------------------

-- EN: Rental
IF NOT EXISTS (SELECT 1 FROM parameter.Parameters
               WHERE [group] = N'LandUse' AND [country] = N'TH' AND [language] = N'EN' AND [code] = N'05')
BEGIN
    INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
    VALUES (N'LandUse', N'TH', N'EN', N'05', N'Rental', 1, 5);
END
GO

-- TH: แบ่งเช่า
IF NOT EXISTS (SELECT 1 FROM parameter.Parameters
               WHERE [group] = N'LandUse' AND [country] = N'TH' AND [language] = N'TH' AND [code] = N'05')
BEGIN
    INSERT INTO parameter.Parameters ([group], [country], [language], [code], [description], [isactive], [seqno])
    VALUES (N'LandUse', N'TH', N'TH', N'05', N'แบ่งเช่า', 1, 5);
END
GO

-- Re-sequence '99' Other to sit after Rental (5 -> 6); no-op once already shifted
UPDATE parameter.Parameters
SET [seqno] = 6
WHERE [group] = N'LandUse' AND [country] = N'TH' AND [code] = N'99' AND [seqno] = 5;
GO
