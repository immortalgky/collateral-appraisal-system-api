-- ============================================================
-- ADDRESS_MASTER_MANAGE permission + Address Masters menu entry
-- Schema: auth
--
-- The Thai address masters (parameter.TitleProvinces/Districts/SubDistricts and the
-- Dopa* equivalents) were seed-only: read endpoints existed, but there was no write
-- path at all, so adding a missing province meant hand-written SQL.
-- /admin/address-masters now maintains both hierarchies.
--
-- Same two seeder gaps as the job-schedule script:
--   1. SeedAdminRoleAsync is CREATE-ONLY, so a newly added permission never reaches
--      an existing Admin role. Step 2 grants it.
--   2. UpsertTreeAsync is INSERT-ONLY for menu items. Step 3 adds the node.
-- SeedPermissionsAsync IS additive, so step 1 is only needed for databases upgraded
-- before the next boot.
--
-- Notes:
--   * auth.Permissions PK is PermissionId; the code column is PermissionCode.
--   * auth.MenuItems PK is MenuItemId. IconStyle 0 = Solid, Scope 0 = Main.
--   * AspNetRoles lives in the auth schema.
--   * Node is appended under main.business-rules at SortOrder 100, after
--     main.system-configurations (90). No existing sibling moves.
--   * Keep Database/Scripts/Maintenance/RestoreAllRolePermissions.sql in sync.
--   * MenuTreeCache ("auth:menu:full") has NO TTL — RESTART THE API after this runs
--     (every instance; it is a per-node IMemoryCache).
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The permission ----------
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'ADDRESS_MASTER_MANAGE', N'Manage Address Masters',
       N'Create, rename, and remove Title/DOPA provinces, districts, and sub-districts',
       N'Common', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'ADDRESS_MASTER_MANAGE');

-- ---------- 2. Grant it to Admin ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode = N'ADDRESS_MASTER_MANAGE'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- 3. The menu node ----------
DECLARE @AddressMenuId UNIQUEIDENTIFIER = '7C9E0105-4E1A-4C7B-9A01-4D2F5B6E0105';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @AddressMenuId, N'main.address-masters', 0, p.MenuItemId, N'/admin/address-masters',
       N'map-location-dot', 0, N'text-rose-500', 100, N'ADDRESS_MASTER_MANAGE', NULL,
       N'ADDRESS_MASTER_MANAGE', 1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.business-rules'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.address-masters');

-- th and zh mirror English, matching AuthDataSeed.BuildTranslations for a node
-- declared without an explicit LabelTh.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Address Masters'),
    (N'th', N'Address Masters'),
    (N'zh', N'Address Masters')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.address-masters'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

PRINT 'Address master admin: ADDRESS_MASTER_MANAGE granted to Admin, main.address-masters menu added';
GO
