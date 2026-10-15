-- ============================================================
-- Remove the view permission from the menu GROUP nodes
-- Schema: auth
--
-- GetMyMenuQueryHandler now shows a node that has no view permission only when at
-- least one of its children is visible. The six groups below carried a gate purely
-- so they could exist (MenuItem.Create used to demand one), and because a hidden
-- parent hides its whole subtree, admins had to grant e.g. LOGS_VIEW just so a user
-- with JOB_SCHEDULE_MANAGE could see "Scheduled Jobs".
--
-- A row is changed only while it is still exactly as seeded: the seeded gate AND no
-- Path (or, for main.standalone, its seeded '/standalone', which has no route in the
-- SPA and is cleared too). MenuItem.Create/Update now require a view permission on any
-- item that has a Path, so ungating a row an admin had given a Path would leave it
-- invisible to everyone and impossible to edit. A row an admin changed is left alone.
-- One statement, so the gate and the Path can never be cleared independently.
-- Idempotent.
--
-- BEHAVIOUR CHANGE FOR ADMINS: removing LOGS_VIEW, SLA_CONFIG_MANAGE, COLLATERAL_ADMIN or USER_MANAGE
-- no longer hides the System / Business Rules / Master Data / Users & Access group, and STANDALONE_USE
-- no longer gates anything (still seeded and granted). To hide a group, remove the permissions of the
-- entries inside it. Full list and deploy steps: deploy/README.md "Menu parents ungated".
--
-- DEPLOY / ROLLBACK: NOT backward-compatible with the previous app build. The previous
-- GetMyMenuQueryHandler hides an ungated node AND its whole subtree, so an old API node that restarts
-- after this ran — or an app rollback — hides these six groups from everyone, Admin included
-- (/admin/menus sits under Users & Access). Run with the db/ bundle as usual and deploy the new app
-- straight after (deploy/README.md "Menu parents ungated"). DbUp scripts have no Down; if the app is
-- rolled back, run deploy/rollback/Restore_MenuParentGates.sql FIRST, then roll the app back — and
-- delete this script's journal row before deploying again (same zip or later) (deploy/README.md "Menu parents
-- ungated").
--
-- MenuSeedData.cs matches this for fresh databases (UpsertTreeAsync is insert-only).
-- The menu tree is cached in memory; the new build's start picks this up. Never restart an OLD-build
-- API node after this ran (see DEPLOY above).
-- ============================================================

-- auth.MenuItems carries a FILTERED index (IX_MenuItems_Scope_Path, WHERE [Path] IS NOT NULL)
-- and SQL Server refuses UPDATE on it unless QUOTED_IDENTIFIER and ANSI_NULLS are ON. DbUp sets
-- them, but production applies a plain-SQL bundle through sqlcmd (QUOTED_IDENTIFIER OFF by default,
-- deploy/Invoke-SqlDeploy.ps1 passes no -I), so state them explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Keep these seeded values in sync with deploy/rollback/Restore_MenuParentGates.sql.
UPDATE m
SET    m.ViewPermissionCode = NULL,
       m.ViewPermissionPrefix = NULL,
       m.[Path] = NULL,
       m.UpdatedAt = SYSDATETIME()
FROM   auth.MenuItems m
JOIN  (VALUES
        (N'main.master-data',    N'COLLATERAL_ADMIN',  NULL),
        (N'main.workflow',       NULL,                 N'WORKFLOW_'),
        (N'main.business-rules', N'SLA_CONFIG_MANAGE', NULL),
        (N'main.access',         N'USER_MANAGE',       NULL),
        (N'main.system',         N'LOGS_VIEW',         NULL),
        (N'main.standalone',     N'STANDALONE_USE',    NULL)
      ) AS seeded (ItemKey, ViewPermissionCode, ViewPermissionPrefix)
       ON  m.ItemKey = seeded.ItemKey
       AND m.Scope = 0 -- Main
       AND ISNULL(m.ViewPermissionCode, N'')   = ISNULL(seeded.ViewPermissionCode, N'')
       AND ISNULL(m.ViewPermissionPrefix, N'') = ISNULL(seeded.ViewPermissionPrefix, N'')
       AND (m.[Path] IS NULL
            OR (m.ItemKey = N'main.standalone' AND m.[Path] = N'/standalone'))
       -- A row WITH a Path must still be a parent: if an admin moved all its children away it is a
       -- working link, so keep its gate. A Path-less container needs no such check — on a fresh
       -- database it has no children yet when this runs (the seeder attaches them at boot).
       AND (m.[Path] IS NULL OR EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = m.MenuItemId));
