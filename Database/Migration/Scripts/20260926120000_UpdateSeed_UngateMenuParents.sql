-- ============================================================
-- Turn the remaining menu PARENTS into plain groups (no Path, no view permission)
-- Schema: auth
--
-- Follows 20260925120000_UpdateSeed_UngateMenuGroups.sql. Every remaining menu PARENT still carried
-- a Path and a gate because it looked like a page of its own. None is: the sidebar only
-- expands/collapses an item that has children and never navigates to its Path; '/users', '/reports'
-- and '/reports/operational' have no route at all; the other Paths just repeat their first child's,
-- which the breadcrumb lets the child overwrite. Their gates therefore did nothing except hide the
-- whole subtree — e.g. a role holding MENU_MANAGE but not USER_MANAGE never saw "Menus", and the
-- seeded ExtAdmin (QUOTATION_EXT_VIEW / INVOICE_EXT_VIEW, without QUOTATION_VIEW / INVOICE_VIEW)
-- never saw its own "External Co. Portal" entries.
--
--   main.request              '/requests'                   REQUEST_VIEW
--   main.task                 '/tasks'                      TASK_LIST_VIEW
--   main.quotation            '/quotations'                 QUOTATION_VIEW
--   main.invoice              '/admin/invoices'             INVOICE_VIEW
--   main.meetings             '/meetings'                   MEETING_MANAGE
--   main.reports              '/reports'                    REPORT_VIEW
--   main.reports.operational  '/reports/operational'        REPORT_OP_VIEW
--
--   main.user-management      '/users'                      USER_MANAGE
--   main.oauth                '/admin/oauth-clients'        prefix OAUTH_
--   main.collateral-master    '/admin/collateral-masters'   COLLATERAL_ADMIN
--   main.template-management  '/market-comparable-factors'  TEMPLATE_MANAGE  (edit TEMPLATE_MANAGE)
--   main.workflow-builder     '/workflow-builder'           WORKFLOW_MANAGE  (edit WORKFLOW_MANAGE)
--
-- The repair UPDATE at the end also finishes main.standalone on databases that ran the FIRST version
-- of the earlier script (gate cleared, Path '/standalone' left).
--
-- A row is changed only while it still holds exactly its seeded Path, view gate and edit code, so
-- anything an admin changed is left alone. The edit code is cleared too: a group has nothing to edit.
-- Idempotent.
--
-- BEHAVIOUR CHANGE FOR ADMINS: a parent's permission no longer switches off its whole section.
-- Removing (or denying per user) REQUEST_VIEW, TASK_LIST_VIEW, QUOTATION_VIEW, INVOICE_VIEW,
-- REPORT_VIEW or USER_MANAGE now hides only the entries gated on that code. (The other parents here —
-- meetings, reports.operational, oauth, collateral-master, template-management, workflow-builder —
-- gated on the same code as all their children, so nothing changes for them.) To hide a whole
-- section, remove the permissions of the entries inside it. Full list: deploy/README.md.
--
-- DEPLOY: this is NOT backward-compatible with the previous app build (deploy/README.md says schema
-- changes normally are). It runs with the db/ bundle in step 3 as usual, then the new API and SPA
-- (step 4) must follow straight away: old API nodes keep their cached menu until they restart, but an
-- old node that restarts in between would hide every ungated section, and the old SPA Sidebar keys
-- path-less siblings by href ('#' twice). See deploy/README.md "Menu parents ungated".
-- ROLLING THE APP BACK after this ran: run deploy/rollback/Restore_MenuParentGates.sql FIRST, then
-- roll the app back, and delete this script's journal row before deploying again (same zip or later)
-- (deploy/README.md "Menu parents ungated").
--
-- MenuSeedData.cs matches this for fresh databases (UpsertTreeAsync is insert-only).
-- The menu tree is cached in memory; the new build's start picks this up. Never restart an OLD-build
-- API node after this ran (see DEPLOY above).
-- ============================================================

-- auth.MenuItems carries a FILTERED index (IX_MenuItems_Scope_Path, WHERE [Path] IS NOT NULL) and
-- SQL Server refuses UPDATE on it unless QUOTED_IDENTIFIER and ANSI_NULLS are ON. Production runs
-- this through sqlcmd without -I, so state them explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- Keep these seeded values in sync with deploy/rollback/Restore_MenuParentGates.sql.
UPDATE m
SET    m.[Path] = NULL,
       m.ViewPermissionCode = NULL,
       m.ViewPermissionPrefix = NULL,
       m.EditPermissionCode = NULL,
       m.UpdatedAt = SYSDATETIME()
FROM   auth.MenuItems m
JOIN  (VALUES
        (N'main.request',             N'/requests',                  N'REQUEST_VIEW',     NULL,      NULL),
        (N'main.task',                N'/tasks',                     N'TASK_LIST_VIEW',   NULL,      NULL),
        (N'main.quotation',           N'/quotations',                N'QUOTATION_VIEW',   NULL,      NULL),
        (N'main.invoice',             N'/admin/invoices',            N'INVOICE_VIEW',     NULL,      NULL),
        (N'main.meetings',            N'/meetings',                  N'MEETING_MANAGE',   NULL,      NULL),
        (N'main.reports',             N'/reports',                   N'REPORT_VIEW',      NULL,      NULL),
        (N'main.reports.operational', N'/reports/operational',       N'REPORT_OP_VIEW',   NULL,      NULL),
        (N'main.user-management',     N'/users',                     N'USER_MANAGE',      NULL,      NULL),
        (N'main.oauth',               N'/admin/oauth-clients',       NULL,                N'OAUTH_', NULL),
        (N'main.collateral-master',   N'/admin/collateral-masters',  N'COLLATERAL_ADMIN', NULL,      NULL),
        (N'main.template-management', N'/market-comparable-factors', N'TEMPLATE_MANAGE',  NULL,      N'TEMPLATE_MANAGE'),
        (N'main.workflow-builder',    N'/workflow-builder',          N'WORKFLOW_MANAGE',  NULL,      N'WORKFLOW_MANAGE')
      ) AS seeded (ItemKey, [Path], ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode)
       ON  m.ItemKey = seeded.ItemKey
       AND m.Scope = 0 -- Main
       AND m.[Path] = seeded.[Path]
       AND ISNULL(m.ViewPermissionCode, N'')   = ISNULL(seeded.ViewPermissionCode, N'')
       AND ISNULL(m.ViewPermissionPrefix, N'') = ISNULL(seeded.ViewPermissionPrefix, N'')
       AND ISNULL(m.EditPermissionCode, N'')   = ISNULL(seeded.EditPermissionCode, N'')
       -- Still a parent: if an admin moved all its children away, it is a working link — keep its gate.
       -- (Every row here has a seeded Path. On a fresh database none of them exists yet when this
       -- runs; the seeder inserts them already ungated.)
       AND EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = m.MenuItemId);

-- Repair: a parent (has children) left with a Path but no gate — e.g. main.standalone after the first
-- version of the earlier script on a row an admin had customised. MenuItem.Update refuses such a row,
-- and ReorderMenuItems updates every sibling, so one of these would break reordering at /admin/menus.
-- A parent's Path is never navigated to (see above), so clearing it loses nothing.
UPDATE m
SET    m.[Path] = NULL,
       m.UpdatedAt = SYSDATETIME()
FROM   auth.MenuItems m
WHERE  m.Scope = 0 -- Main
  AND  m.ViewPermissionCode IS NULL
  AND  m.ViewPermissionPrefix IS NULL
  AND  m.[Path] IS NOT NULL
  AND  EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = m.MenuItemId);

PRINT CONCAT('Repaired ungated parents with a leftover Path: ', @@ROWCOUNT);
