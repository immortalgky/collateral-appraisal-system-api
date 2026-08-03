-- ============================================================
-- JOB_SCHEDULE_MANAGE permission + Scheduled Jobs menu entry
-- Schema: auth
--
-- The per-module {schema}.JobSchedules tables (cron / timezone / enabled for every
-- Hangfire recurring job) had no API and no screen; changing a schedule meant
-- hand-editing SQL and restarting. /admin/job-schedules now covers them.
--
-- AuthDataSeed.SeedPermissionsAsync IS additive, so the permission row itself
-- appears on the next boot without help. Two things are NOT covered by the seeder:
--
--   1. SeedAdminRoleAsync is CREATE-ONLY -- it grants every permission only when
--      the Admin role is first created. On an existing database a newly added
--      permission never reaches Admin. Step 2 below does that.
--   2. AuthDataSeed.UpsertTreeAsync is INSERT-ONLY for menu items. Step 3 adds
--      the menu node to databases that already have the menu.
--
-- Both steps are written to work on a fresh database too: the migrate tool runs
-- DbUp before the seeder, so on a fresh DB step 1 inserts the permission, step 2
-- finds no Admin role yet and matches nothing (the seeder then grants it as part
-- of the initial all-permissions grant), and step 3 inserts the menu row that the
-- seeder subsequently finds already present.
--
-- Notes:
--   * auth.MenuItems' PK column is MenuItemId; auth.Permissions' is PermissionId
--     and its code column is PermissionCode (both remapped from the entity's Id/
--     PermissionCode -- see PermissionConfiguration).
--   * IconStyle 0 = Solid, Scope 0 = Main.
--   * RolePermissions is a plain (RoleId, PermissionId) link table.
--   * AspNetRoles lives in the auth schema.
--   * Keep Database/Scripts/Maintenance/RestoreAllRolePermissions.sql in sync --
--     it is the rebuild-from-scratch artifact for role grants.
--   * MenuTreeCache ("auth:menu:full") has NO TTL, so RESTART THE API after this
--     runs (every instance -- it is a per-node IMemoryCache).
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The permission ----------
-- Mirrors the entry added to AuthDataSeed.SeedPermissionsAsync, so a database
-- that is upgraded before the next boot is not left without it.
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'JOB_SCHEDULE_MANAGE', N'Manage Scheduled Jobs',
       N'Change the cron schedule, timezone, and enabled state of recurring background jobs',
       N'Common', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'JOB_SCHEDULE_MANAGE');

-- ---------- 2. Grant it to Admin ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode = N'JOB_SCHEDULE_MANAGE'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- 3. The menu node ----------
-- Appended under the existing System container (main.system), after
-- main.webhook-deliveries at SortOrder 30.
DECLARE @JobSchedulesMenuId UNIQUEIDENTIFIER = '7C9E0104-4E1A-4C7B-9A01-4D2F5B6E0104';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @JobSchedulesMenuId, N'main.job-schedules', 0, p.MenuItemId, N'/admin/job-schedules',
       N'clock', 0, N'text-slate-500', 40, N'JOB_SCHEDULE_MANAGE', NULL, N'JOB_SCHEDULE_MANAGE',
       1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.system'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.job-schedules');

-- th and zh mirror English, matching AuthDataSeed.BuildTranslations for a node
-- declared without an explicit LabelTh.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Scheduled Jobs'),
    (N'th', N'Scheduled Jobs'),
    (N'zh', N'Scheduled Jobs')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.job-schedules'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

PRINT 'Job schedule admin: JOB_SCHEDULE_MANAGE permission granted to Admin, main.job-schedules menu added';
GO
