-- ============================================================
-- Credit-side appraisal tracking: permission + menu entry
-- Schema: auth
--
-- Credit (สินเชื่อ) staff need to follow an appraisal's progress and, once the price
-- is approved, read its value and documents -- without reaching the appraisal
-- workspace, which stays gated on APPRAISAL_VIEW.
--
-- NO NEW ROLES. The bank's access matrix (Inquiry / Report / Inquiry+Report) maps onto
-- roles that ALREADY EXIST in this database:
--     Inquiry  -- "Read-only inquiry / monitor"   (1,042 users on the dev database)
--     Report   -- "Report viewer"                 (203 users)
-- and the third column is simply a user holding BOTH roles -- ASP.NET Identity allows
-- several roles per user, so no combined role is needed.
--
-- ⚠ THIS SCRIPT ONLY GRANTS. It does not revoke anything, and on its own it therefore
--   changes NOTHING that a credit user sees. Both roles currently hold APPRAISAL_VIEW,
--   and AppraisalFieldScope.IsTrackingOnly() deliberately lets APPRAISAL_VIEW win, so
--   the masking never fires while they still hold it. Removing APPRAISAL_VIEW from
--   ~1,250 users is a separate step, and it happens in the very next script:
--       20260914090100_Revoke_AppraisalWorkspaceFromCreditRoles.sql
--   The two are sequenced so the grant always lands first; read that file's header for the
--   blast radius before deploying either of them.
--
-- The node points at /appraisals/search -- the SAME list page the appraisal team uses. One
-- page, masked server-side per caller; the separate /appraisals/tracking route was dropped
-- rather than keep two URLs for one screen.
--
-- Why a separate node at all, rather than relaxing main.appraisal.search's own gating:
-- a menu item can be gated by an exact code OR a prefix, and APPRAISAL_VIEW and
-- APPRAISAL_TRACKING_VIEW share no prefix beyond "APPRAISAL_" -- which would also match
-- RequestChecker (it holds APPRAISAL_REQUEST_VIEW and APPRAISAL_SUMMARY_VIEW but NOT
-- APPRAISAL_VIEW), newly exposing the appraisal list to a role that cannot see it today.
--
-- Why the menu node is TOP LEVEL and not a child of main.appraisal:
-- GetMyMenuQueryHandler runs `if (!isVisible) continue;` BEFORE recursing into a node's
-- children, so a child of a parent gated on APPRAISAL_VIEW would never render for a
-- credit user once that permission is revoked. Relaxing that parent to
-- ViewPermissionPrefix = 'APPRAISAL_' is not an option either: it would also match the
-- menu-only APPRAISAL_*_VIEW section codes that other roles hold.
--
-- UpsertTreeAsync never inserts into a database that already has a menu, and
-- SeedData:RunSeeders is false outside Development. Hence this script for UAT/production.
--
-- Notes:
--   * auth.MenuItems' PK column is MenuItemId; IconStyle 0 = Solid, Scope 0 = Main.
--   * The link table is auth.RolePermissions (plural).
--   * SortOrder is taken from the old main.appraisal group so the new node lands in its
--     place (step 3 runs before step 4 deletes it). Only when that group is missing does it
--     fall back to MAX(root sibling) + 10 -- never a literal: long-lived databases carry
--     values from an older build whose counter ran across the whole tree.
--   * MenuTreeCache ("auth:menu:full") has NO TTL, so RESTART THE API after this runs
--     (every instance -- it is a per-node IMemoryCache). The affected users must also
--     RE-LOGIN: permissions ride in the token as a claim, so an existing session keeps
--     the old set until its token is reissued.
--   * Older scripts in this folder tell you to keep
--     Database/Scripts/Maintenance/RestoreAllRolePermissions.sql in sync. That file no
--     longer exists in the repository -- there is nothing to update, do not go looking.
--
-- Idempotent: every statement is guarded by NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The permission ----------
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'APPRAISAL_TRACKING_VIEW', N'Track Appraisal Progress',
       N'Follow an appraisal''s progress and holder, and read the approved value and documents '
       + N'once the price is approved, without access to the appraisal workspace',
       N'Appraisal', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'APPRAISAL_TRACKING_VIEW');

-- ---------- 2. Grant it to the credit-side roles ----------
-- Both roles already hold DASHBOARD_VIEW, so only the new code is added here.
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name IN (N'Inquiry', N'Report')
  AND p.PermissionCode = N'APPRAISAL_TRACKING_VIEW'
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions x
      WHERE x.RoleId = r.Id AND x.PermissionId = p.PermissionId);

-- ---------- 2b. And to EVERY role that may already reach the appraisal list ----------
-- The new menu node replaces the old "Appraisal > Search" entry, and it is gated on the new
-- code. Without this grant, the 11 appraisal-side roles would keep APPRAISAL_VIEW but lose
-- their only menu route to the list. Written as "whoever holds APPRAISAL_VIEW" rather than a
-- hardcoded list so it stays correct on a database whose role/permission mapping was tuned by
-- hand -- which this one was: the seeder says RequestMaker has no APPRAISAL_VIEW, the live
-- database says it does.
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, tracking.PermissionId
FROM auth.AspNetRoles r
         JOIN auth.RolePermissions rp ON rp.RoleId = r.Id
         JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId AND p.PermissionCode = N'APPRAISAL_VIEW'
         CROSS JOIN (SELECT PermissionId FROM auth.Permissions
                     WHERE PermissionCode = N'APPRAISAL_TRACKING_VIEW') tracking
WHERE NOT EXISTS (
    SELECT 1 FROM auth.RolePermissions x
    WHERE x.RoleId = r.Id AND x.PermissionId = tracking.PermissionId);

-- ---------- 3. The menu node (root level) ----------
DECLARE @TrackingMenuId UNIQUEIDENTIFIER = '2B7A61D4-9C33-4E58-B0A7-14F5C8E9A7D1';

INSERT INTO auth.MenuItems
    (MenuItemId, ItemKey, Scope, ParentId, [Path], IconName, IconStyle, IconColor,
     SortOrder, ViewPermissionCode, ViewPermissionPrefix, EditPermissionCode, IsSystem, CreatedAt)
SELECT @TrackingMenuId, N'main.appraisal-tracking', 0, NULL, N'/appraisals/search',
       N'radar', 0, N'text-sky-500',
       COALESCE(
           (SELECT o.SortOrder FROM auth.MenuItems o WHERE o.ItemKey = N'main.appraisal'),
           ISNULL((SELECT MAX(c.SortOrder) FROM auth.MenuItems c WHERE c.ParentId IS NULL AND c.Scope = 0), 0) + 10),
       N'APPRAISAL_TRACKING_VIEW', NULL, NULL,
       1, SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = N'main.appraisal-tracking');

INSERT INTO auth.MenuItemTranslations (MenuItemId, LanguageCode, Label, CreatedAt)
SELECT m.MenuItemId, t.LanguageCode, t.Label, SYSDATETIME()
FROM auth.MenuItems m
CROSS APPLY (VALUES
    (N'en', N'Appraisal Search / Tracking'),
    (N'th', N'ค้นหา/ติดตามงานประเมิน'),
    (N'zh', N'Appraisal Search / Tracking')
) AS t(LanguageCode, Label)
WHERE m.ItemKey = N'main.appraisal-tracking'
  AND NOT EXISTS (
      SELECT 1 FROM auth.MenuItemTranslations x
      WHERE x.MenuItemId = m.MenuItemId AND x.LanguageCode = t.LanguageCode);

-- ---------- 4. Retire the old Appraisal menu group ----------
-- "Search" pointed at the same page the new node now points at. "My Appraisals" and
-- "Pending Review" never had a route or a page component -- clicking either only ever
-- reached the not-found page -- so nothing working is being removed here.
-- Translations cascade on delete; the parent goes last because MenuItems self-references.
DELETE t FROM auth.MenuItemTranslations t
    JOIN auth.MenuItems m ON m.MenuItemId = t.MenuItemId
WHERE m.ItemKey IN (N'main.appraisal.search', N'main.appraisal.my-appraisals', N'main.appraisal.pending-review');

DELETE FROM auth.MenuItems
WHERE ItemKey IN (N'main.appraisal.search', N'main.appraisal.my-appraisals', N'main.appraisal.pending-review');

DELETE t FROM auth.MenuItemTranslations t
    JOIN auth.MenuItems m ON m.MenuItemId = t.MenuItemId
WHERE m.ItemKey = N'main.appraisal'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = m.MenuItemId);

DELETE FROM auth.MenuItems
WHERE ItemKey = N'main.appraisal'
  AND NOT EXISTS (SELECT 1 FROM auth.MenuItems c WHERE c.ParentId = auth.MenuItems.MenuItemId);

PRINT 'Credit appraisal tracking: APPRAISAL_TRACKING_VIEW granted, old Appraisal menu group retired, main.appraisal-tracking menu ensured';
PRINT 'NOTE: nothing changes for those users until APPRAISAL_VIEW is revoked -- the next script (20260914090100) does that.';
GO
