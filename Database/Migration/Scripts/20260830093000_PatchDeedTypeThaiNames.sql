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
-- Each UPDATE is guarded on the exact stale value, so it is idempotent AND will not overwrite a
-- description an administrator has since edited through the Parameter maintenance screen.

UPDATE [parameter].[Parameters]
SET [Description] = N'โฉนดที่ดิน / อ.ช.2',
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
WHERE [Group] = N'DeedType' AND [Language] = N'TH' AND [Code] = N'DEED'
  AND [Description] = N'Title deed';

UPDATE [parameter].[Parameters]
SET [Description] = N'น.ส.3',
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
WHERE [Group] = N'DeedType' AND [Language] = N'TH' AND [Code] = N'NS3'
  AND [Description] = N'นส 3';

UPDATE [parameter].[Parameters]
SET [Description] = N'น.ส.3 ก.',
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
WHERE [Group] = N'DeedType' AND [Language] = N'TH' AND [Code] = N'NS3K'
  AND [Description] = N'นส 3 ก';

UPDATE [parameter].[Parameters]
SET [Description] = N'น.ส.3 ข.',
    [UpdatedAt]   = GETDATE(),
    [UpdatedBy]   = N'SYSTEM'
WHERE [Group] = N'DeedType' AND [Language] = N'TH' AND [Code] = N'NS3KO'
  AND [Description] = N'นส 3 ข';
