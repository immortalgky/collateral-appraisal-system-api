-- ============================================================
-- Revoke the appraisal workspace from the credit-side roles
--
-- ⚠ THIS CHANGES WHAT ~1,250 REAL PEOPLE CAN OPEN. Read the blast radius below before
--   deploying. It runs automatically as part of `migrate`, journalled once per database,
--   immediately after 20260914090000_SeedData_AppraisalTrackingForCredit.sql.
--
-- Why it is a journalled migration and not a hand-run maintenance script: the migrator only
-- executes Migration/Scripts (one-time) and Scripts/Views | StoredProcedures | Functions
-- (repeatable). Anything under Scripts/Maintenance is embedded in the assembly but never
-- executed by anything -- so a file there would have been silently skipped on UAT and
-- production, leaving the feature deployed and doing nothing at all. That is a worse failure
-- than the change being automatic: the code would ship, no one would see any difference, and
-- the cause would be a script nobody remembered to run.
--
-- WHAT IT DOES
-- ------------
-- Removes APPRAISAL_VIEW -- plus the nine menu-only APPRAISAL_*_VIEW section codes -- from the
-- Inquiry and Report roles. Nothing else is touched.
--
-- WHY IT IS NEEDED
-- ----------------
-- The previous script grants APPRAISAL_TRACKING_VIEW, but on its own that changes nothing:
--
--     AppraisalFieldScope.IsTrackingOnly(user) =>
--         !user.HasPermission("APPRAISAL_VIEW") && user.HasPermission("APPRAISAL_TRACKING_VIEW")
--
-- APPRAISAL_VIEW deliberately wins, so while it is still attached no column is masked,
-- /appraisals/export is not refused, and the route guard keeps letting those users into the
-- appraisal workspace. This script is what actually switches them over.
--
-- BLAST RADIUS
-- ------------
-- On the development database: Inquiry 1,043 users, Report 203. Step 0 prints the live
-- numbers for whatever database this runs against.
--
-- After this runs, those users:
--   * LOSE the appraisal workspace (/appraisals/{id}). Typing the URL redirects to '/', and
--     the "View details" link is hidden rather than shown and then bounced.
--   * LOSE the Export button on the appraisal list; the endpoint returns 403.
--   * KEEP the appraisal list, with the internal columns blanked server-side.
--   * KEEP the "ค้นหา/ติดตามงานประเมิน" menu and the tracking panel, which is now their
--     whole view of an appraisal.
--   * Report KEEPS report access (REPORT_VIEW, REPORT_OP_VIEW) -- untouched here.
--   * Inquiry KEEPS history search (HISTORY_SEARCH_VIEW) -- untouched here.
--
-- REVERSIBLE: step 3 prints the exact INSERT that puts everything back.
--
-- After deploying: RESTART every API instance (the menu tree is cached with no TTL) and have
-- the affected users RE-LOGIN -- permissions ride in the access token as a claim. Tell them
-- first; ~1,250 people losing a screen on the same morning is a service-desk event.
--
-- No explicit transaction here: the migrator already wraps the whole upgrade in one
-- (DeployChanges...WithTransaction()), so a failure anywhere rolls this back with it.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

DECLARE @Roles TABLE (Name NVARCHAR(256));
INSERT INTO @Roles (Name) VALUES (N'Inquiry'), (N'Report');

DECLARE @Codes TABLE (Code NVARCHAR(100));
INSERT INTO @Codes (Code) VALUES
    (N'APPRAISAL_VIEW'),
    (N'APPRAISAL_360_VIEW'),
    (N'APPRAISAL_REQUEST_VIEW'),
    (N'APPRAISAL_ADMINISTRATION_VIEW'),
    (N'APPRAISAL_APPOINTMENT_VIEW'),
    (N'APPRAISAL_PROPERTY_VIEW'),
    (N'APPRAISAL_BLOCK_CONDO_VIEW'),
    (N'APPRAISAL_BLOCK_VILLAGE_VIEW'),
    (N'APPRAISAL_DOCUMENTS_VIEW'),
    (N'APPRAISAL_SUMMARY_VIEW');

-- ---------- 0. Look before you leap ----------
PRINT '--- users affected ---';
SELECT r.Name AS RoleName, COUNT(ur.UserId) AS UserCount
FROM auth.AspNetRoles r
         LEFT JOIN auth.AspNetUserRoles ur ON ur.RoleId = r.Id
WHERE r.Name IN (SELECT Name FROM @Roles)
GROUP BY r.Name;

PRINT '--- permissions that will be removed ---';
SELECT r.Name AS RoleName, p.PermissionCode
FROM auth.AspNetRoles r
         JOIN auth.RolePermissions rp ON rp.RoleId = r.Id
         JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
WHERE r.Name IN (SELECT Name FROM @Roles)
  AND p.PermissionCode IN (SELECT Code FROM @Codes)
ORDER BY r.Name, p.PermissionCode;

-- ---------- 1. Safety: refuse unless the replacement is already in place ----------
-- Running this before the grant script would leave those users with no appraisal access at
-- all rather than switching them onto the tracking screen.
-- Counted per ROLE, not "at least one row anywhere". The previous form grouped inside an
-- EXISTS, which is a no-op: it passed as soon as EITHER role held the grant, and then revoked
-- from BOTH. On a database where the grant had reached Inquiry but not Report — a renamed role,
-- a hand-edited role map, a partial re-run — the ~203 Report users would have been left with no
-- appraisal access at all, which is the one outcome this gate exists to prevent.
-- Counted over the target roles that EXIST on this database, not over the whole wish list.
-- Inquiry and Report are created by nobody in this repository — not by AuthDataSeed, not by any
-- script — so a fresh database has neither, and a gate demanding both would RAISERROR there.
-- DbUp wraps the whole run in one transaction (DatabaseMigrator.WithTransaction), so that single
-- error rolled back every script in the run and `migrate` failed permanently, on every retry.
-- Every other seed script in this folder simply matches zero rows when a role is missing.
DECLARE @RolesNeeding INT = (
    SELECT COUNT(*) FROM @Roles r
    WHERE EXISTS (SELECT 1 FROM auth.AspNetRoles ar WHERE ar.Name = r.Name));
DECLARE @RolesGranted INT = (
    SELECT COUNT(DISTINCT r.Name)
    FROM auth.AspNetRoles r
             JOIN auth.RolePermissions rp ON rp.RoleId = r.Id
             JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
    WHERE r.Name IN (SELECT Name FROM @Roles)
      AND p.PermissionCode = N'APPRAISAL_TRACKING_VIEW');

-- @RolesNeeding = 0 means none of the target roles is on this database: nothing to revoke and
-- nothing to protect. Proceed and let the revoke statements match zero rows.
IF (@RolesNeeding > 0 AND @RolesGranted < @RolesNeeding)
BEGIN
    RAISERROR (N'APPRAISAL_TRACKING_VIEW is granted to only %d of %d target roles. Run 20260914090000_SeedData_AppraisalTrackingForCredit.sql first.', 16, 1, @RolesGranted, @RolesNeeding);
    RETURN;
END

-- ---------- 2. Revoke ----------
DELETE rp
FROM auth.RolePermissions rp
         JOIN auth.AspNetRoles r ON r.Id = rp.RoleId
         JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
WHERE r.Name IN (SELECT Name FROM @Roles)
  AND p.PermissionCode IN (SELECT Code FROM @Codes);

PRINT CONCAT(N'Revoked ', @@ROWCOUNT, N' role-permission link(s) from Inquiry + Report.');

-- ---------- 3. How to undo ----------
PRINT '--- to restore, run: ---';
PRINT 'INSERT INTO auth.RolePermissions (RoleId, PermissionId)';
PRINT 'SELECT r.Id, p.PermissionId FROM auth.AspNetRoles r CROSS JOIN auth.Permissions p';
PRINT 'WHERE r.Name IN (N''Inquiry'', N''Report'')';
PRINT '  AND p.PermissionCode IN (N''APPRAISAL_VIEW'', N''APPRAISAL_360_VIEW'', N''APPRAISAL_REQUEST_VIEW'',';
PRINT '        N''APPRAISAL_ADMINISTRATION_VIEW'', N''APPRAISAL_APPOINTMENT_VIEW'', N''APPRAISAL_PROPERTY_VIEW'',';
PRINT '        N''APPRAISAL_BLOCK_CONDO_VIEW'', N''APPRAISAL_BLOCK_VILLAGE_VIEW'', N''APPRAISAL_DOCUMENTS_VIEW'',';
PRINT '        N''APPRAISAL_SUMMARY_VIEW'')';
PRINT '  AND NOT EXISTS (SELECT 1 FROM auth.RolePermissions x WHERE x.RoleId = r.Id AND x.PermissionId = p.PermissionId);';
PRINT '';
PRINT 'Then RESTART every API instance and have the affected users re-login.';
GO
