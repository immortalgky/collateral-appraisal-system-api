-- ============================================================
-- APPRAISAL_DATA_CORRECTION permission + Appraisal Data Correction menu entry
-- Schema: auth
--
-- Property data on a Completed/Cancelled appraisal is read-only on the normal screens, so wrong
-- values entered during the appraisal had no supported way back out — they were fixed with hand
-- written UPDATE statements, or not at all. /standalone/appraisal-data-correction is the sanctioned
-- path: it requires a reason and writes a field-level before/after row to
-- appraisal.AppraisalPropertyCorrectionLogs.
--
-- Same two seeder gaps as the address-master and job-schedule scripts:
--   1. SeedAdminRoleAsync is CREATE-ONLY, so a newly added permission never reaches an existing
--      Admin role. Step 2 grants it.
--   2. UpsertTreeAsync is INSERT-ONLY for menu items. Step 3 adds the node.
-- SeedPermissionsAsync IS additive, so step 1 only matters for databases upgraded before the next
-- boot — and seeding does not run outside Development at all, which is why this script exists.
--
-- Notes:
--   * auth.Permissions PK is PermissionId; the code column is PermissionCode.
--   * auth.MenuItems PK is MenuItemId. IconStyle 0 = Solid, Scope 0 = Main.
--   * AspNetRoles lives in the auth schema.
--   * Node is appended under main.standalone after main.standalone.block-reappraisal.
--     No existing sibling moves.
--   * The holder ALSO needs STANDALONE_USE: GetMyMenuQueryHandler hides the children of an
--     invisible parent, so without it the new node never renders. Step 4 covers Admin.
--   * Older scripts in this folder tell you to keep
--     Database/Scripts/Maintenance/RestoreAllRolePermissions.sql in sync. That file no longer
--     exists in the repository, so there is nothing to update — do not go looking for it.
--   * MenuTreeCache ("auth:menu:full") has NO TTL — RESTART THE API after this runs (every
--     instance; it is a per-node IMemoryCache). Users also need to re-login: permissions are a
--     token claim.
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The permission ----------
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'APPRAISAL_DATA_CORRECTION', N'Correct Appraisal Property Data',
       N'Correct descriptive property data on Completed/Cancelled appraisals',
       N'Appraisal', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'APPRAISAL_DATA_CORRECTION');

-- ---------- 2. Grant it to Admin ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode = N'APPRAISAL_DATA_CORRECTION'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- 3. The menu node ----------
DECLARE @CorrectionMenuId UNIQUEIDENTIFIER = '3F5B2A17-9C4D-4E88-B1A6-7D0E4C2F8B31';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @CorrectionMenuId, N'main.standalone.appraisal-data-correction', 0, p.MenuItemId,
       N'/standalone/appraisal-data-correction',
       N'pen-to-square', 0, N'text-teal-500',
       -- One step past the current last child so the node lands at the end of the group.
       ISNULL((SELECT MAX(c.SortOrder) FROM auth.MenuItems c WHERE c.ParentId = p.MenuItemId), 0) + 10,
       N'APPRAISAL_DATA_CORRECTION', NULL, N'APPRAISAL_DATA_CORRECTION', 1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.standalone'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.standalone.appraisal-data-correction');

-- th and zh mirror English, matching AuthDataSeed.BuildTranslations for a node declared without an
-- explicit LabelTh.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Appraisal Data Correction'),
    (N'th', N'Appraisal Data Correction'),
    (N'zh', N'Appraisal Data Correction')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.standalone.appraisal-data-correction'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

-- ---------- 4. Admin also needs STANDALONE_USE to see the parent group ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode = N'STANDALONE_USE'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

PRINT 'Appraisal data correction: APPRAISAL_DATA_CORRECTION granted to Admin, main.standalone.appraisal-data-correction menu added';
GO
