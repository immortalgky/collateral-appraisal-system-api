-- ============================================================
-- Regroup the main navigation menu into five admin groups
-- Schema: auth
--
-- The main sidebar had ~32 top-level items, two-thirds of them single-purpose
-- admin/config screens. This script introduces five CONTAINER menu items
-- (Path = NULL, so the sidebar renders them as expand-only toggles) and moves
-- 20 existing roots underneath them, taking the top level from 32 to 17.
--
-- It also repairs a real ordering bug: AuthDataSeed.UpsertTreeAsync derives
-- SortOrder from list position at FIRST INSERT and advances its counter even
-- for rows it skips. After many mid-list insertions the live roots collided
-- (40x2, 50x3, 60x2, 120x2, 180x3, 190x2, 220x2, 230x2), and
-- GetMyMenuQueryHandler orders by SortOrder alone -- so ties broke
-- non-deterministically in SQL Server and the on-screen order did not match
-- the seed file. Step 4 gives all 17 roots a unique SortOrder.
--
-- MenuSeedData.cs has been restructured to match, which covers FRESH databases.
-- Because UpsertTreeAsync is INSERT-ONLY (on an ItemKey match it leaves
-- ParentId/SortOrder untouched), this script is what applies the change to
-- databases that already have the flat menu.
--
-- Both paths converge: on a fresh DB the migrate tool runs DbUp first, so the
-- inserts below land and the updates match nothing; the seeder then finds the
-- container ItemKeys already present and parents the children onto these exact
-- GUIDs. On an existing DB the inserts add the containers and the updates
-- re-parent. Container GUIDs are fixed literals so they are identical in every
-- environment.
--
-- Notes:
--   * auth.MenuItems' PK column is MenuItemId (not Id).
--   * IconStyle 0 = Solid (Auth.Domain.Menu.IconStyle).
--   * Scope 0 = Main (Auth.Domain.Menu.MenuScope).
--   * Audit columns (CreatedAt/By/Workstation, Updated*) are all nullable.
--   * Container gates are chosen so no seeded role loses a screen it can reach
--     today -- see the rationale comment in MenuSeedData.cs.
--   * MenuTreeCache ("auth:menu:full") has NO TTL and is only invalidated
--     in-process by the admin menu handlers, so RESTART THE API after this runs
--     (every instance -- it is a per-node IMemoryCache).
--
-- Idempotent: re-running is a no-op once the containers exist and the
-- ParentId/SortOrder values already match.
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

-- ---------- 1. The five container rows ----------
DECLARE @Containers TABLE (
    MenuItemId           UNIQUEIDENTIFIER NOT NULL,
    ItemKey              NVARCHAR(200)    NOT NULL,
    LabelEn              NVARCHAR(500)    NOT NULL,
    LabelTh              NVARCHAR(500)    NOT NULL,
    IconName             NVARCHAR(100)    NOT NULL,
    IconColor            NVARCHAR(100)    NULL,
    SortOrder            INT              NOT NULL,
    ViewPermissionCode   NVARCHAR(100)    NULL,
    ViewPermissionPrefix NVARCHAR(200)    NULL
);

INSERT INTO @Containers
    (MenuItemId, ItemKey, LabelEn, LabelTh, IconName, IconColor, SortOrder, ViewPermissionCode, ViewPermissionPrefix)
VALUES
    ('7C9E0001-4E1A-4C7B-9A01-4D2F5B6E0001', N'main.master-data',    N'Master Data',    N'ข้อมูลหลัก',    N'database',         N'text-cyan-500',   130, N'COLLATERAL_ADMIN', NULL),
    ('7C9E0002-4E1A-4C7B-9A01-4D2F5B6E0002', N'main.workflow',       N'Workflow',       N'เวิร์กโฟลว์',      N'diagram-project',  N'text-orange-500', 140, NULL,                N'WORKFLOW_'),
    ('7C9E0003-4E1A-4C7B-9A01-4D2F5B6E0003', N'main.business-rules', N'Business Rules', N'กฎเกณฑ์ธุรกิจ',   N'sliders',          N'text-rose-500',   150, N'SLA_CONFIG_MANAGE', NULL),
    ('7C9E0004-4E1A-4C7B-9A01-4D2F5B6E0004', N'main.access',         N'Users & Access', N'ผู้ใช้และสิทธิ์',    N'shield-halved',    N'text-violet-500', 160, N'USER_MANAGE',      NULL),
    ('7C9E0005-4E1A-4C7B-9A01-4D2F5B6E0005', N'main.system',         N'System',         N'ระบบ',          N'server',           N'text-slate-500',  170, N'LOGS_VIEW',        NULL);

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, Path, IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT c.MenuItemId, c.ItemKey, 0, NULL, NULL, c.IconName, 0, c.IconColor,
       c.SortOrder, c.ViewPermissionCode, c.ViewPermissionPrefix, NULL, 1, SYSDATETIME()
FROM @Containers c
WHERE NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = c.ItemKey);

-- ---------- 2. Container translations (en / th / zh) ----------
-- zh mirrors the English label, matching AuthDataSeed.BuildTranslations.
-- Resolve ids from the table (not the literals) so this still works if a row
-- was created by the seeder first.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM @Containers c
INNER JOIN auth.MenuItems m ON m.ItemKey = c.ItemKey
CROSS APPLY (VALUES
    (N'en', c.LabelEn),
    (N'th', c.LabelTh),
    (N'zh', c.LabelEn)
) AS t(LanguageCode, Label)
WHERE NOT EXISTS (
    SELECT 1 FROM auth.MenuItemTranslations x
    WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

-- ---------- 3. Re-parent the 20 moved items ----------
DECLARE @Moves TABLE (
    ItemKey       NVARCHAR(200) NOT NULL,
    ParentItemKey NVARCHAR(200) NOT NULL,
    SortOrder     INT           NOT NULL
);

INSERT INTO @Moves (ItemKey, ParentItemKey, SortOrder) VALUES
    -- Master Data
    (N'main.collateral-master',          N'main.master-data',    10),
    (N'main.template-management',        N'main.master-data',    20),
    -- Workflow
    (N'main.workflow-builder',           N'main.workflow',       10),
    (N'main.workflow-step-validation',   N'main.workflow',       20),
    (N'main.workflow-assignment-config', N'main.workflow',       30),
    (N'main.workflow-roundrobin-config', N'main.workflow',       40),
    -- Business Rules
    (N'main.parameter',                  N'main.business-rules', 10),
    (N'main.document-requirements',      N'main.business-rules', 20),
    (N'main.fee-structures',             N'main.business-rules', 30),
    (N'main.fee-approval-tiers',         N'main.business-rules', 40),
    (N'main.appointment-approval-rule',  N'main.business-rules', 50),
    (N'main.evaluation-config',          N'main.business-rules', 60),
    (N'main.sla-config',                 N'main.business-rules', 70),
    -- Users & Access
    (N'main.user-management',            N'main.access',         10),
    (N'main.oauth',                      N'main.access',         20),
    (N'main.audit-log',                  N'main.access',         30),
    (N'main.access-report',              N'main.access',         40),
    -- System
    (N'main.logs',                       N'main.system',         10),
    (N'main.webhook-subscriptions',      N'main.system',         20),
    (N'main.webhook-deliveries',         N'main.system',         30);

UPDATE m
SET m.ParentId  = p.MenuItemId,
    m.SortOrder = mv.SortOrder,
    m.UpdatedAt = SYSDATETIME()
FROM auth.MenuItems m
INNER JOIN @Moves mv       ON mv.ItemKey = m.ItemKey
INNER JOIN auth.MenuItems p ON p.ItemKey = mv.ParentItemKey
WHERE m.ParentId IS NULL
   OR m.ParentId <> p.MenuItemId
   OR m.SortOrder <> mv.SortOrder;

-- ---------- 4. Unique SortOrder for all 17 roots ----------
-- This is the duplicate-ordering fix; it must cover the twelve untouched
-- operational roots too, not just the new groups.
DECLARE @Roots TABLE (ItemKey NVARCHAR(200) NOT NULL, SortOrder INT NOT NULL);
INSERT INTO @Roots (ItemKey, SortOrder) VALUES
    (N'main.dashboard',      10),
    (N'main.request',        20),
    (N'main.task',           30),
    (N'main.task-monitor',   40),
    (N'main.monitoring',     50),
    (N'main.appraisal',      60),
    (N'main.quotation',      70),
    (N'main.invoice',        80),
    (N'main.meetings',       90),
    (N'main.reports',       100),
    (N'main.notification',  110),
    (N'main.standalone',    120),
    (N'main.master-data',   130),
    (N'main.workflow',      140),
    (N'main.business-rules',150),
    (N'main.access',        160),
    (N'main.system',        170);

UPDATE m
SET m.SortOrder = r.SortOrder,
    m.UpdatedAt = SYSDATETIME()
FROM auth.MenuItems m
INNER JOIN @Roots r ON r.ItemKey = m.ItemKey
WHERE m.SortOrder <> r.SortOrder;

PRINT 'Regrouped main menu: 5 container groups added, 20 items re-parented, 17 roots renumbered 10-170';
GO
