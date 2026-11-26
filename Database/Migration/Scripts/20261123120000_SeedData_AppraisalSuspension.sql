-- ============================================================
-- APPRAISAL_SUSPENSION permission + Appraisal Suspension menu entry
-- Schema: auth
--
-- A Completed appraisal of a pre-appraisal project is reused by LOS for every unit sold later, with no
-- per-unit appraisal. When a later unit appraisal shows the price has moved too far, the bank needs to
-- stop LOS from retrieving that result. /standalone/appraisal-suspension is the sanctioned path: it
-- flips appraisal.Appraisals.IsSuspended and writes one row to appraisal.AppraisalSuspensionLogs.
-- Status stays Completed, so nothing else in CAS changes; only the LOS result endpoints withhold it.
--
-- Same two seeder gaps as the appraisal-data-correction and failed-messages scripts:
--   1. SeedRoleWithPermissionsAsync is CREATE-ONLY, so a newly added permission never reaches an
--      existing IntAdmin role. Step 2 grants it.
--   2. UpsertTreeAsync is INSERT-ONLY for menu items. Step 3 adds the node.
-- SeedPermissionsAsync IS additive, so step 1 only matters for databases upgraded before the next
-- boot — and seeding does not run outside Development at all, which is why this script exists.
--
-- Notes:
--   * auth.Permissions PK is PermissionId; the code column is PermissionCode.
--   * auth.MenuItems PK is MenuItemId. IconStyle 0 = Solid, Scope 0 = Main.
--   * MenuItemId is NEWID(): nothing looks a menu node up by id (this script and the seeder key on
--     ItemKey) and the seeder generates its own ids, so there is no fixed value to match.
--   * AspNetRoles lives in the auth schema.
--   * Node is appended under main.standalone after main.standalone.appraisal-data-correction.
--     No existing sibling moves.
--   * The holder ALSO needs STANDALONE_USE: GetMyMenuQueryHandler hides the children of an
--     invisible parent, so without it the new node never renders. Step 4 covers IntAdmin; anyone
--     granted APPRAISAL_SUSPENSION per user (UserPermissions) needs STANDALONE_USE granted too.
--   * Keep in sync with AuthDataSeed.cs (permission + IntAdmin grant) and MenuSeedData.cs (node +
--     LabelTh): those are what a fresh Development database gets, this is what UAT/production get.
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
SELECT NEWID(), N'APPRAISAL_SUSPENSION', N'Suspend Appraisals',
       N'Suspend appraisals from external retrieval',
       N'Appraisal', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'APPRAISAL_SUSPENSION');

-- ---------- 2. Grant it to IntAdmin ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'IntAdmin'
  AND p.PermissionCode = N'APPRAISAL_SUSPENSION'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- 3. The menu node ----------
INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT NEWID(), N'main.standalone.appraisal-suspension', 0, p.MenuItemId,
       N'/standalone/appraisal-suspension',
       N'circle-pause', 0, N'text-teal-500',
       -- One step past the current last child so the node lands at the end of the group.
       ISNULL((SELECT MAX(c.SortOrder) FROM auth.MenuItems c WHERE c.ParentId = p.MenuItemId), 0) + 10,
       N'APPRAISAL_SUSPENSION', NULL, N'APPRAISAL_SUSPENSION', 1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.standalone'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.standalone.appraisal-suspension');

-- th carries the node's LabelTh and zh mirrors English, matching AuthDataSeed.BuildTranslations for a
-- node declared with an explicit LabelTh.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Appraisal Suspension'),
    (N'th', N'ระงับการใช้ผลประเมิน'),
    (N'zh', N'Appraisal Suspension')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.standalone.appraisal-suspension'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

-- ---------- 4. IntAdmin also needs STANDALONE_USE to see the parent group ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'IntAdmin'
  AND p.PermissionCode = N'STANDALONE_USE'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

PRINT 'Appraisal suspension: APPRAISAL_SUSPENSION granted to IntAdmin, main.standalone.appraisal-suspension menu added';
GO
