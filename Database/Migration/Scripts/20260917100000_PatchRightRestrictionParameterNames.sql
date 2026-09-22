-- Correct the option wording of the land "Eviction" parameter groups, which are really
-- การรอนสิทธิ์ (right restriction): part of the plot given over to a state utility.
--
-- Defects shipped in 20260317002600_SeedData_GeneralParameter.sql:
--
--   Eviction              the EN rows described different things from their TH rows —
--                         '01' Permanent Electricity vs อยู่ในแนวสายไฟฟ้าแรงสูง,
--                         '02' Tap Water/Ground Water vs แนวรถไฟฟ้าใต้ดิน — and TH '99' carried a
--                         stray leading dot ('.อื่นๆ').
--
--   ProjectLand_Eviction  every TH row carried the ENGLISH string, and the EN wording did not name
--                         the restriction ('Permanent Electricity', 'Subway line').
--
-- Only descriptions change. The group names and codes stay as they are — they are what saved
-- appraisals store and what the forms look up — so no stored value moves.
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
        (N'Eviction',             N'EN', N'01', N'Permanent Electricity',  N'High-Voltage Power Line'),
        (N'Eviction',             N'EN', N'02', N'Tap Water/Ground Water', N'Underground Railway Line'),
        (N'Eviction',             N'TH', N'99', N'.อื่นๆ',                  N'อื่นๆ'),
        (N'ProjectLand_Eviction', N'EN', N'01', N'Permanent Electricity',  N'High-Voltage Power Line'),
        (N'ProjectLand_Eviction', N'TH', N'01', N'Permanent Electricity',  N'อยู่ในแนวสายไฟฟ้าแรงสูง'),
        (N'ProjectLand_Eviction', N'EN', N'02', N'Subway line',            N'Underground Railway Line'),
        (N'ProjectLand_Eviction', N'TH', N'02', N'Subway line',            N'แนวรถไฟฟ้าใต้ดิน'),
        (N'ProjectLand_Eviction', N'TH', N'03', N'Other',                  N'อื่นๆ')
     ) AS c ([Group], [Language], Code, StaleDescription, NewDescription)
  ON  c.[Group]          = p.[Group]
  AND c.[Language]       = p.[Language]
  AND c.Code             = p.[Code]
  AND c.StaleDescription = p.[Description];

PRINT CONCAT('Patched right-restriction parameter names: ', @@ROWCOUNT, ' row(s).');
