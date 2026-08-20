-- =============================================================================
-- Restrict the "Property Information (PMA)" tab permission to IntAppraisalStaff.
--
-- Why: APPRAISAL_PROPERTY_PMA_VIEW/EDIT used to be part of the shared appraisal
-- section permission set granted to EVERY appraisal role, so the PMA property tab
-- could surface on any activity. The PMA tab is now exclusive to the int-pma-input
-- activity (performed by IntAppraisalStaff); the C# seeder was changed to grant the
-- permission to IntAppraisalStaff only, and to hide it on that role's other
-- activities via ActivityMenuOverrides.
--
-- The role-permission seeder is ADD-ONLY (it never revokes), so on databases that
-- were already seeded the broad grant lingers. This one-off script removes it from
-- every role EXCEPT IntAppraisalStaff and Admin (Admin intentionally keeps all
-- permissions). Idempotent: re-running is a no-op once the rows are gone.
--
-- The complementary additive changes to permissions and menu items (new
-- TASK_INT_PMA_INPUT permission, the int-pma-input task menu item) are INSERT-ONLY
-- in the seeder and apply automatically on the next application boot.
--
-- The ActivityMenuOverride rows are NOT in that group: that seeder is fresh-install
-- only (it skips entirely once auth.ActivityMenuOverrides has any row), because
-- "inherit" is stored as the absence of a row and re-inserting missing pairs would
-- undo admin edits on every app-pool recycle. On an already-seeded database the
-- override rows must be added by an explicit script in Database/Migration/Scripts/.
-- =============================================================================

DELETE rp
FROM auth.RolePermissions rp
INNER JOIN auth.Permissions   p ON p.Id = rp.PermissionId
INNER JOIN auth.AspNetRoles   r ON r.Id = rp.RoleId
WHERE p.PermissionCode IN ('APPRAISAL_PROPERTY_PMA_VIEW', 'APPRAISAL_PROPERTY_PMA_EDIT')
  AND r.Name NOT IN ('IntAppraisalStaff', 'Admin');

-- Verify (optional): rows that remain should be IntAppraisalStaff + Admin only.
-- SELECT r.Name, p.PermissionCode
-- FROM auth.RolePermissions rp
-- INNER JOIN auth.Permissions p ON p.Id = rp.PermissionId
-- INNER JOIN auth.AspNetRoles r ON r.Id = rp.RoleId
-- WHERE p.PermissionCode IN ('APPRAISAL_PROPERTY_PMA_VIEW', 'APPRAISAL_PROPERTY_PMA_EDIT')
-- ORDER BY r.Name, p.PermissionCode;
