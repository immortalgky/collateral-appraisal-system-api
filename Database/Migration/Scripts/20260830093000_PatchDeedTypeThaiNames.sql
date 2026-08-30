-- CA-609: correct the Thai wording of the DeedType parameter group.
--
-- The original seed (20260317002600_SeedData_GeneralParameter.sql) shipped three defects in the
-- TH-language rows of this group:
--   DEED  held the English string 'Title deed' instead of any Thai text at all
--   NS3 / NS3K / NS3KO used a non-standard spelling without the abbreviation periods ('นส 3'),
--         where the Department of Lands writes 'น.ส.3', 'น.ส.3 ก.' and 'น.ส.3 ข.'
-- The DEED caption also has to cover a condominium unit deed, which shares this one dropdown, so it
-- reads 'โฉนดที่ดิน / อ.ช.2' rather than naming only the land document.
--
-- No new codes: the value set stays DEED / NS3 / NS3K / NS3KO / POSR / OTHER, matching
-- TitleDeedInfo.ValidDeedTypes. POSR and OTHER were already correct and are left alone.
--
-- The seed script itself was corrected in place for fresh databases, but DbUp journals one-time
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
        (N'DEED',  N'Title deed', N'โฉนดที่ดิน / อ.ช.2'),
        (N'NS3',   N'นส 3',       N'น.ส.3'),
        (N'NS3K',  N'นส 3 ก',     N'น.ส.3 ก.'),
        (N'NS3KO', N'นส 3 ข',     N'น.ส.3 ข.')
     ) AS c (Code, StaleDescription, NewDescription)
  ON  c.Code             = p.[Code]
  AND c.StaleDescription = p.[Description]
WHERE p.[Group]    = N'DeedType'
  AND p.[Language] = N'TH';
