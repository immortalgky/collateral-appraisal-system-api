-- ============================================================
-- FAILED_MESSAGE_VIEW / FAILED_MESSAGE_MANAGE permissions + menu entry
-- Schema: auth
--
-- Failed Messages Monitor (docs/failed-messages/design.md): consumer *_error/*_skipped messages,
-- outbox Failed/Stuck rows, and per-node queue health, at GET/POST /admin/failed-messages and
-- /admin/outbox-messages. FAILED_MESSAGE_VIEW gates the read screen; FAILED_MESSAGE_MANAGE additionally
-- gates retry/discard/resend.
--
-- Mirrors AuthDataSeed.SeedPermissionsAsync + MenuSeedData.GetMainMenuSeed. SeedAdminRoleAsync is
-- create-only, so an existing Admin role only gets these new codes from this script (same pattern as
-- 20260925120100_SeedData_WebhookSecretRevealPermission.sql). AuthDataSeed.UpsertTreeAsync is
-- insert-only for menu items, so an existing database needs this script for the menu node too (same
-- pattern as 20260913120000_SeedData_HangfireDashboardMenu.sql).
--
-- MenuTreeCache ("auth:menu:full") has NO TTL -- RESTART EVERY API INSTANCE after this runs.
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

-- auth.MenuItems carries a FILTERED index (IX_MenuItems_Scope_Path, WHERE [Path] IS NOT NULL) and SQL
-- Server refuses INSERT/UPDATE against it unless QUOTED_IDENTIFIER and ANSI_NULLS are ON. DbUp sets
-- them, but production applies a plain-SQL bundle through sqlcmd (QUOTED_IDENTIFIER OFF by default),
-- so state them explicitly.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- Permissions ----------
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'FAILED_MESSAGE_VIEW', N'View Failed Messages',
       N'View failed/skipped consumer messages, outbox failures, and per-node queue health',
       N'Integration', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'FAILED_MESSAGE_VIEW');

INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'FAILED_MESSAGE_MANAGE', N'Manage Failed Messages',
       N'Retry/discard failed messages and resend failed outbox rows',
       N'Integration', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'FAILED_MESSAGE_MANAGE');

-- ---------- Grant to Admin (same role WEBHOOK_SECRET_REVEAL was granted to) ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'Admin'
  AND p.PermissionCode IN (N'FAILED_MESSAGE_VIEW', N'FAILED_MESSAGE_MANAGE')
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- The menu node (under main.system, right after main.webhook-deliveries) ----------
-- Matches MenuSeedData.cs's Development order (webhook-deliveries, THEN failed-messages, THEN
-- job-schedules) rather than appending at the end. If webhook's SortOrder + 1 is already taken by
-- another sibling, shift every sibling after webhook up by one first; guarded by the same
-- NOT EXISTS(main.failed-messages) as the INSERT below so re-running this script is a no-op.
-- The webhook SortOrder is only read while that node still sits directly under main.system (an admin may
-- have moved it); otherwise nothing is shifted and the node is appended at MAX(sibling SortOrder) + 1.
DECLARE @SystemMenuId UNIQUEIDENTIFIER = (SELECT MenuItemId FROM auth.MenuItems WHERE ItemKey = N'main.system');
DECLARE @WebhookSortOrder INT = (SELECT SortOrder FROM auth.MenuItems
                                 WHERE ItemKey = N'main.webhook-deliveries' AND ParentId = @SystemMenuId);
DECLARE @TargetSortOrder INT = ISNULL(@WebhookSortOrder, 0) + 1;

IF NOT EXISTS (SELECT 1 FROM auth.MenuItems WHERE ItemKey = N'main.failed-messages')
   AND @WebhookSortOrder IS NOT NULL
   AND EXISTS (SELECT 1 FROM auth.MenuItems WHERE ParentId = @SystemMenuId AND SortOrder = @TargetSortOrder)
BEGIN
    UPDATE auth.MenuItems
    SET SortOrder = SortOrder + 1
    WHERE ParentId = @SystemMenuId AND SortOrder > @WebhookSortOrder;
END

DECLARE @FailedMessagesMenuId UNIQUEIDENTIFIER = 'A3E5C6F1-8B2D-4A97-9C3E-6F1B0D2E7A45';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @FailedMessagesMenuId, N'main.failed-messages', 0, p.MenuItemId, N'/admin/failed-messages',
       N'triangle-exclamation', 0, N'text-slate-500',
       -- Falls back to MAX(sibling)+1 if main.webhook-deliveries doesn't exist or no longer sits under
       -- main.system (no shift happens in that case, so +1 never collides).
       ISNULL(@WebhookSortOrder + 1,
              ISNULL((SELECT MAX(c.SortOrder) FROM auth.MenuItems c WHERE c.ParentId = p.MenuItemId), 0) + 1),
       N'FAILED_MESSAGE_VIEW', NULL, NULL,
       1, SYSDATETIME()
FROM auth.MenuItems p
WHERE p.ItemKey = N'main.system'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.failed-messages');

-- zh mirrors English (matching AuthDataSeed.BuildTranslations for a node declared without an explicit
-- LabelZh, same as main.webhook-deliveries); th has its own label.
INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Failed Messages'),
    (N'th', N'ข้อความที่ประมวลผลไม่สำเร็จ'),
    (N'zh', N'Failed Messages')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.failed-messages'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

PRINT 'Failed Messages Monitor: FAILED_MESSAGE_VIEW / FAILED_MESSAGE_MANAGE granted to Admin, main.failed-messages menu added under main.system';
GO
