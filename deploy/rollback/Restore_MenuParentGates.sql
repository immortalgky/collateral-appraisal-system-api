-- ============================================================
-- ROLLBACK ONLY - restore the menu parent gates removed by
--   20260925120000_UpdateSeed_UngateMenuGroups.sql
--   20260926120000_UpdateSeed_UngateMenuParents.sql
-- Run this BEFORE rolling the app back to a build older than those scripts. That build hides an
-- ungated menu node and its whole subtree, so every section would vanish for everyone.
-- Lives outside Database/Migration/Scripts so it can never run automatically.
--
-- What it does, in ONE transaction:
--   1. gives each seeded parent back its original Path / gate / edit code - only while the row is
--      still ungated (an admin's own gate is left alone);
--   2. refuses to finish (rolls back and raises an error listing them) if ANY item is still ungated -
--      e.g. a group an admin created after go-live, or a seeded parent whose children were all moved
--      away. The previous build hides an ungated parent with its whole subtree, and its MenuItem.Update
--      rejects an ungated item, so a single one breaks every drag-reorder at /admin/menus. Give each a
--      view permission (or delete an empty one), then run this again.
--   3. after committing, lists every restored gate that NO role holds any more - e.g. an admin removed
--      STANDALONE_USE during the release because it no longer gated anything. Under the previous build
--      that section is hidden from everyone until the code is granted again at /admin/roles.
--
-- The seeded values below must match the VALUES lists in both forward scripts - change all three
-- together.
--
-- Afterwards: roll the app back, then - before deploying again, the same zip or any later build
-- (they all contain these scripts) - delete both scripts' journal rows (deploy/README.md "Menu parents ungated") so the
-- bundle applies them again.
-- Idempotent.
-- ============================================================

SET QUOTED_IDENTIFIER ON; -- auth.MenuItems has a filtered index; sqlcmd defaults this OFF
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
GO

BEGIN TRAN;

UPDATE m
SET    m.[Path] = seeded.[Path],
       m.ViewPermissionCode = seeded.ViewPermissionCode,
       m.ViewPermissionPrefix = seeded.ViewPermissionPrefix,
       m.EditPermissionCode = COALESCE(m.EditPermissionCode, seeded.EditPermissionCode),
       m.UpdatedAt = SYSDATETIME()
FROM   auth.MenuItems m
JOIN  (VALUES
        -- 20260925120000_UpdateSeed_UngateMenuGroups.sql
        (N'main.master-data',         NULL,                          N'COLLATERAL_ADMIN',  NULL,        NULL),
        (N'main.workflow',            NULL,                          NULL,                 N'WORKFLOW_', NULL),
        (N'main.business-rules',      NULL,                          N'SLA_CONFIG_MANAGE', NULL,        NULL),
        (N'main.access',              NULL,                          N'USER_MANAGE',       NULL,        NULL),
        (N'main.system',              NULL,                          N'LOGS_VIEW',         NULL,        NULL),
        (N'main.standalone',          N'/standalone',                N'STANDALONE_USE',    NULL,        NULL),
        -- 20260926120000_UpdateSeed_UngateMenuParents.sql
        (N'main.request',             N'/requests',                  N'REQUEST_VIEW',      NULL,        NULL),
        (N'main.task',                N'/tasks',                     N'TASK_LIST_VIEW',    NULL,        NULL),
        (N'main.quotation',           N'/quotations',                N'QUOTATION_VIEW',    NULL,        NULL),
        (N'main.invoice',             N'/admin/invoices',            N'INVOICE_VIEW',      NULL,        NULL),
        (N'main.meetings',            N'/meetings',                  N'MEETING_MANAGE',    NULL,        NULL),
        (N'main.reports',             N'/reports',                   N'REPORT_VIEW',       NULL,        NULL),
        (N'main.reports.operational', N'/reports/operational',       N'REPORT_OP_VIEW',    NULL,        NULL),
        (N'main.user-management',     N'/users',                     N'USER_MANAGE',       NULL,        NULL),
        (N'main.oauth',               N'/admin/oauth-clients',       NULL,                 N'OAUTH_',   NULL),
        (N'main.collateral-master',   N'/admin/collateral-masters',  N'COLLATERAL_ADMIN',  NULL,        NULL),
        (N'main.template-management', N'/market-comparable-factors', N'TEMPLATE_MANAGE',   NULL,        N'TEMPLATE_MANAGE'),
        (N'main.workflow-builder',    N'/workflow-builder',          N'WORKFLOW_MANAGE',   NULL,        N'WORKFLOW_MANAGE')
      ) AS seeded (ItemKey, [Path], ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode)
       ON  m.ItemKey = seeded.ItemKey
       AND m.Scope = 0 -- Main
       -- Blank counts as ungated, as it does for the previous build (IsNullOrEmpty).
       AND NULLIF(LTRIM(RTRIM(m.ViewPermissionCode)), N'') IS NULL
       AND NULLIF(LTRIM(RTRIM(m.ViewPermissionPrefix)), N'') IS NULL
       -- Only a row that is still a parent: one whose children were moved away would come back as a
       -- link to a Path that has no route.
       AND EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = m.MenuItemId);

-- FOR XML PATH rather than STRING_AGG: works on every SQL Server version.
DECLARE @stillUngated NVARCHAR(MAX) = STUFF((
    SELECT N', ' + m.ItemKey FROM auth.MenuItems m
    WHERE NULLIF(LTRIM(RTRIM(m.ViewPermissionCode)), N'') IS NULL
      AND NULLIF(LTRIM(RTRIM(m.ViewPermissionPrefix)), N'') IS NULL
    FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, N'');

IF @stillUngated IS NOT NULL
BEGIN
    ROLLBACK;
    DECLARE @msg NVARCHAR(2048) = LEFT(
        N'Nothing restored: the previous app build cannot show these ungated menu items - give each a '
        + N'view permission first, then run this again: ' + @stillUngated, 2048);
    THROW 50000, @msg, 1;
END

COMMIT;
PRINT 'Menu parent gates restored. Restart the API after rolling the app back.';

-- Report (does not change anything): restored gates no role holds - those sections will be hidden
-- from everyone under the previous build until the code is granted again.
SELECT m.ItemKey, COALESCE(m.ViewPermissionCode, m.ViewPermissionPrefix + N'*') AS Gate
FROM   auth.MenuItems m
WHERE  m.ItemKey IN (N'main.master-data', N'main.workflow', N'main.business-rules', N'main.access',
                     N'main.system', N'main.standalone', N'main.request', N'main.task', N'main.quotation',
                     N'main.invoice', N'main.meetings', N'main.reports', N'main.reports.operational',
                     N'main.user-management', N'main.oauth', N'main.collateral-master',
                     N'main.template-management', N'main.workflow-builder')
  AND  NOT EXISTS (
         SELECT 1 FROM auth.RolePermissions rp
         JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
         WHERE (m.ViewPermissionCode IS NOT NULL AND p.PermissionCode = m.ViewPermissionCode)
            OR (m.ViewPermissionPrefix IS NOT NULL AND p.PermissionCode LIKE m.ViewPermissionPrefix + N'%'));
GO
