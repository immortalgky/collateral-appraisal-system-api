-- ============================================================
-- Menu entries for the newly-reachable configuration screens
-- Schema: auth
--
-- Three config screens had no way in from the sidebar:
--
--   * /admin/committees              -- the CommitteeAdminPage has existed and
--                                       worked for a while, but MenuSeedData.cs
--                                       never had a node for it, so it was
--                                       reachable only by typing the URL.
--   * /admin/auto-assignment-rules   -- new screen over the existing
--                                       /api/workflow/auto-assignment-rules CRUD.
--   * /admin/system-configurations   -- new screen over the existing
--                                       /system-configurations read+update API.
--
-- It also renames main.sla-config from "OLA / SLA Targets" to "SLA Configuration",
-- because that screen now hosts three tabs (targets, holidays, business hours)
-- rather than targets alone.
--
-- MenuSeedData.cs has been updated to match, which covers FRESH databases.
-- AuthDataSeed.UpsertTreeAsync is INSERT-ONLY -- on an ItemKey match it leaves
-- ParentId/SortOrder/translations untouched -- so this script is what applies
-- the change to databases that already have the menu.
--
-- Notes:
--   * auth.MenuItems' PK column is MenuItemId (not Id).
--   * IconStyle 0 = Solid (Auth.Domain.Menu.IconStyle).
--   * Scope 0 = Main (Auth.Domain.Menu.MenuScope).
--   * Child nodes carry no Thai label in the seed, so BuildTranslations stores
--     th = en (and zh = en). These rows follow that same convention.
--   * SortOrders continue their group's existing sequence: Workflow ends at 40
--     (round-robin), Business Rules ends at 70 (sla-config). Nothing is
--     renumbered, so no existing sibling moves.
--   * MEETING_ADMIN gates Committees: the committee endpoints are login-only
--     (RequireAuthorization() with no policy), so the menu gate plus the
--     frontend RoleProtectedRoute are what restrict the screen. MEETING_ADMIN
--     is held by Admin and IntAdmin.
--   * MenuTreeCache ("auth:menu:full") has NO TTL and is only invalidated
--     in-process by the admin menu handlers, so RESTART THE API after this runs
--     (every instance -- it is a per-node IMemoryCache).
--
-- Idempotent: re-running is a no-op once the rows exist and the label matches.
-- ============================================================

-- auth.MenuItems carries a FILTERED index -- IX_MenuItems_Scope_Path,
-- WHERE [Path] IS NOT NULL -- and SQL Server refuses INSERT/UPDATE/DELETE on
-- such a table unless QUOTED_IDENTIFIER and ANSI_NULLS are ON. DbUp already
-- sets them, but sqlcmd defaults QUOTED_IDENTIFIER OFF, so state them
-- explicitly: production applies a generated plain-SQL bundle by hand
-- (deploy/README.md) and must not depend on the caller's session defaults.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The three new leaf rows ----------
DECLARE @NewItems TABLE (
    MenuItemId         UNIQUEIDENTIFIER NOT NULL,
    ItemKey            NVARCHAR(200)    NOT NULL,
    ParentItemKey      NVARCHAR(200)    NOT NULL,
    LabelEn            NVARCHAR(500)    NOT NULL,
    [Path]             NVARCHAR(500)    NOT NULL,
    IconName           NVARCHAR(100)    NOT NULL,
    IconColor          NVARCHAR(100)    NULL,
    SortOrder          INT              NOT NULL,
    ViewPermissionCode NVARCHAR(100)    NULL,
    EditPermissionCode NVARCHAR(100)    NULL
);

INSERT INTO @NewItems
    (MenuItemId, ItemKey, ParentItemKey, LabelEn, [Path], IconName, IconColor, SortOrder,
     ViewPermissionCode, EditPermissionCode)
VALUES
    ('7C9E0101-4E1A-4C7B-9A01-4D2F5B6E0101', N'main.auto-assignment-rules', N'main.workflow',
     N'Auto Assignment Rules', N'/admin/auto-assignment-rules', N'route', N'text-orange-500', 50,
     N'WORKFLOW_ADMIN', N'WORKFLOW_ADMIN'),
    ('7C9E0102-4E1A-4C7B-9A01-4D2F5B6E0102', N'main.committees', N'main.business-rules',
     N'Committees', N'/admin/committees', N'users-line', N'text-orange-500', 80,
     N'MEETING_ADMIN', N'MEETING_ADMIN'),
    ('7C9E0103-4E1A-4C7B-9A01-4D2F5B6E0103', N'main.system-configurations', N'main.business-rules',
     N'System Configuration', N'/admin/system-configurations', N'gears', N'text-rose-500', 90,
     N'PARAMETER_MANAGE', N'PARAMETER_MANAGE');

-- Parent containers were created by 20260729120000_UpdateSeed_RegroupMainMenu.sql.
-- Skip any row whose parent is missing rather than inserting an orphan root.
INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT n.MenuItemId, n.ItemKey, 0, p.MenuItemId, n.[Path], n.IconName, 0, n.IconColor,
       n.SortOrder, n.ViewPermissionCode, NULL, n.EditPermissionCode, 1, SYSDATETIME()
FROM @NewItems n
INNER JOIN auth.MenuItems p ON p.ItemKey = n.ParentItemKey
WHERE NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = n.ItemKey);

-- ---------- 2. Translations (en / th / zh) ----------
-- th and zh mirror the English label, matching AuthDataSeed.BuildTranslations
-- for a node declared without an explicit LabelTh.
-- Resolve ids from the table (not the literals) so this still works if a row
-- was created by the seeder first.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM @NewItems n
INNER JOIN auth.MenuItems m ON m.ItemKey = n.ItemKey
CROSS APPLY (VALUES
    (N'en', n.LabelEn),
    (N'th', n.LabelEn),
    (N'zh', n.LabelEn)
) AS t(LanguageCode, Label)
WHERE NOT EXISTS (
    SELECT 1 FROM auth.MenuItemTranslations x
    WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

-- ---------- 3. Rename main.sla-config ----------
-- The screen gained Holidays and Business Hours tabs, so "OLA / SLA Targets"
-- now names only one third of it. All three languages carried the English
-- label, so all three are updated.
UPDATE x
SET x.Label     = N'SLA Configuration',
    x.UpdatedAt = SYSDATETIME()
FROM auth.MenuItemTranslations x
INNER JOIN auth.MenuItems m ON m.MenuItemId = x.MenuItemId
WHERE m.ItemKey = N'main.sla-config'
  AND x.Label = N'OLA / SLA Targets';

PRINT 'Config maintenance menus: +main.auto-assignment-rules, +main.committees, +main.system-configurations; main.sla-config renamed';
GO
