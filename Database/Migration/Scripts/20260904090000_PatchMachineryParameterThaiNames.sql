-- Correct the Thai wording of the parameter groups the machinery appraisal form reads.
--
-- Two defects shipped in 20260317002600_SeedData_GeneralParameter.sql:
--
--   ConditionUse  every TH-language row carried the ENGLISH string — 'In Used', 'Not In Used',
--                 'Not Found' — so the สภาพการใช้งาน buttons on the machinery form read in
--                 English no matter which language the user picked. Code '03' is the one the
--                 machinery summary report prints as "(สำรวจไม่พบ)", which is where its Thai
--                 wording comes from; the other two follow it.
--
--   MachineType   code '2' was misspelt twice — 'อุตสหกรรม' for อุตสาหกรรม and 'การผลิด' for
--                 การผลิต — and code '3' lost the space before its slash.
--
-- MachineStatus and Country were already correct and are not touched.
--
-- The seed script itself is corrected in place for fresh databases, but DbUp journals one-time
-- scripts by file name with no checksum, so that edit is a no-op wherever it already ran. This
-- patch is what reaches those databases.
--
-- Joining on the stale description as well as the code makes this idempotent AND stops it
-- overwriting a description an administrator has since edited through the Parameter screen.

UPDATE p
SET [Description] = c.NewDescription,
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
FROM [parameter].[Parameters] p
JOIN (VALUES
        (N'ConditionUse', N'01', N'In Used',     N'ใช้งานอยู่'),
        (N'ConditionUse', N'02', N'Not In Used', N'ไม่ได้ใช้งาน'),
        (N'ConditionUse', N'03', N'Not Found',   N'สำรวจไม่พบ'),
        (N'MachineType',  N'2',  N'อุตสหกรรม / เครื่องจักรการผลิด',  N'อุตสาหกรรม / เครื่องจักรการผลิต'),
        (N'MachineType',  N'3',  N'พลังงาน/ เครื่องจักรสาธารณูปโภค', N'พลังงาน / เครื่องจักรสาธารณูปโภค')
     ) AS c ([Group], Code, StaleDescription, NewDescription)
  ON  c.[Group]          = p.[Group]
  AND c.Code             = p.[Code]
  AND c.StaleDescription = p.[Description]
WHERE p.[Language] = N'TH';

PRINT CONCAT('Patched machinery parameter Thai names: ', @@ROWCOUNT, ' row(s).');
