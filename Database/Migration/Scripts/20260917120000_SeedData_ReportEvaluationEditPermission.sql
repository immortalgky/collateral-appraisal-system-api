-- ============================================================
-- REPORT_EVALUATION_EDIT permission — split Service Quality Evaluation
-- view/edit access
-- Schema: auth
--
-- Previously a single permission, REPORT_EVALUATION_VIEW, gated the whole
-- Service Quality Evaluation feature (list + detail + scoring). This adds a
-- second permission so scoring (create/update an evaluation) can be
-- restricted separately from read-only access.
--
-- REPORT_EVALUATION_VIEW today is held by IntAdmin, IntAppraisalVerifier,
-- and AppraisalCommittee — they keep read-only access and are NOT granted
-- REPORT_EVALUATION_EDIT here. Only IntAppraisalStaff gets edit rights
-- (plus view, which it did not previously hold at all).
--
-- Same two seeder gaps as the address-master script:
--   1. SeedRoleWithPermissionsAsync is CREATE-ONLY, so a newly added
--      permission never reaches an existing IntAppraisalStaff role. Step 2
--      grants it.
--   2. UpsertTreeAsync is INSERT-ONLY for menu items — it won't update an
--      existing node's EditPermissionCode. Step 3 does that directly.
-- SeedPermissionsAsync IS additive, so step 1 is only needed for databases
-- upgraded before the next application boot.
--
-- Notes:
--   * auth.Permissions PK column is PermissionId; the code column is
--     PermissionCode (see PermissionConfiguration.HasColumnName).
--   * auth.MenuItems PK column is MenuItemId.
--   * AspNetRoles lives in the auth schema.
--   * MenuTreeCache ("auth:menu:full") has NO TTL — RESTART THE API after
--     this runs (every instance; it is a per-node IMemoryCache).
--
-- Idempotent: every statement is guarded by a NOT EXISTS.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ---------- 1. The permission ----------
INSERT INTO auth.Permissions (PermissionId, PermissionCode, DisplayName, [Description], Module, CreatedAt)
SELECT NEWID(), N'REPORT_EVALUATION_EDIT', N'Edit Service Quality Evaluation',
       N'Submit and complete service quality evaluation scores for external appraisal companies',
       N'Common', SYSDATETIME()
WHERE NOT EXISTS (SELECT 1 FROM auth.Permissions WHERE PermissionCode = N'REPORT_EVALUATION_EDIT');

-- ---------- 2. Grant REPORT_EVALUATION_VIEW + REPORT_EVALUATION_EDIT to IntAppraisalStaff ----------
INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM auth.AspNetRoles r
CROSS JOIN auth.Permissions p
WHERE r.Name = N'IntAppraisalStaff'
  AND p.PermissionCode IN (N'REPORT_EVALUATION_VIEW', N'REPORT_EVALUATION_EDIT')
  AND NOT EXISTS (
      SELECT 1 FROM auth.RolePermissions rp
      WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

-- ---------- 3. Menu: give the existing node an EditPermissionCode ----------
UPDATE m
SET EditPermissionCode = N'REPORT_EVALUATION_EDIT'
FROM auth.MenuItems m
WHERE m.ItemKey = N'main.standalone.service-quality-evaluation'
  AND (m.EditPermissionCode IS NULL OR m.EditPermissionCode <> N'REPORT_EVALUATION_EDIT');

PRINT 'REPORT_EVALUATION_EDIT permission created, granted to IntAppraisalStaff, and wired to the Service Quality Evaluation menu item.';
GO
