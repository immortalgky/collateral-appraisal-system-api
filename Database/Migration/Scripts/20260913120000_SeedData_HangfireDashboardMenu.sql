-- ============================================================
-- Hangfire Dashboard menu entry (System group)
-- Schema: auth
--
-- The Hangfire dashboard at /hangfire could only be reached by typing the URL. The
-- SPA now embeds it in an iframe on its own page, /admin/hangfire, so it behaves
-- like any other menu entry. This script adds that menu node.
--
-- No new permission: the entry reuses JOB_SCHEDULE_MANAGE (seeded and granted to
-- Admin by 20260802120100_SeedData_JobScheduleAdmin.sql), which is the same area --
-- background jobs.
--
-- READ BEFORE GRANTING THIS PERMISSION TO ANYONE ELSE: it is not merely a sidebar
-- entry. The "HangfireDashboard" policy in AuthModule now authorises /hangfire on
-- exactly this code (it used to require the Admin role), so whoever holds it can
-- open the dashboard and delete, requeue or trigger any background job and read the
-- arguments those jobs were queued with.
--
-- To make the entry actually appear for a non-Admin, that user also needs LOGS_VIEW:
-- the parent "System" group is gated on it, and a hidden parent hides its children.
-- Without it the permission still grants /hangfire access, just with no way in from
-- the menu.
--
-- AuthDataSeed.UpsertTreeAsync is INSERT-ONLY for menu items, so a database that
-- already has the menu never receives a newly seeded node -- hence this script.
--
-- On a FRESH database this script is a no-op, not the source of the node: DbUp runs
-- before the seeder, so main.system does not exist yet, the parent lookup below
-- matches nothing and nothing is inserted. The seeder then creates both. That is
-- fine here because the parent is pre-existing everywhere this script matters --
-- but do not copy the pattern for a node whose PARENT is new in the same release,
-- or the script will silently do nothing on fresh installs.
--
-- Notes:
--   * auth.MenuItems' PK column is MenuItemId; IconStyle 0 = Solid, Scope 0 = Main.
--   * The fixed MenuItemId below is a fresh GUID -- do NOT derive one by nudging a
--     digit of a neighbouring script's id: '7C9E0105-...-4D2F5B6E0105' already
--     belongs to main.address-masters, and reusing it fails on PK_MenuItems.
--   * SortOrder is computed as MAX(sibling) + 10, never hardcoded, because the
--     right value depends on the database's history. AuthDataSeed numbers per
--     parent today (10/20/30/40 under main.system), but long-lived databases still
--     carry values from an older build whose counter ran across the whole tree --
--     the dev database has this group at 1140-1170. A literal is wrong on one shape
--     or the other: 50 would have rendered this entry at the TOP of the group there,
--     above Application Logs, instead of after Scheduled Jobs. Menus are ordered by
--     SortOrder alone (GetMyMenuQueryHandler), with no tiebreak.
--   * MenuTreeCache ("auth:menu:full") has NO TTL, so RESTART THE API after this
--     runs (every instance -- it is a per-node IMemoryCache).
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- The menu node ----------
DECLARE @HangfireMenuId UNIQUEIDENTIFIER = 'FC86152F-04C1-43F9-96A8-87CEE6CDD584';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @HangfireMenuId, N'main.hangfire-dashboard', 0, p.MenuItemId, N'/admin/hangfire',
       N'gauge-high', 0, N'text-slate-500',
       ISNULL((SELECT MAX(c.SortOrder) FROM auth.MenuItems c WHERE c.ParentId = p.MenuItemId), 0) + 10,
       N'JOB_SCHEDULE_MANAGE', NULL, N'JOB_SCHEDULE_MANAGE',
       1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.system'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.hangfire-dashboard');

-- th and zh mirror English, matching AuthDataSeed.BuildTranslations for a node
-- declared without an explicit LabelTh.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Hangfire Dashboard'),
    (N'th', N'Hangfire Dashboard'),
    (N'zh', N'Hangfire Dashboard')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.hangfire-dashboard'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

PRINT 'Hangfire dashboard: main.hangfire-dashboard menu added under main.system';
GO
