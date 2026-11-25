-- =============================================================================
--  CREATE BANK ROLES  (from CAS Security Matrix, 28/07/2026)
--  Adds the 3 roles the bank needs that our seed does not have, and grants each
--  the permissions the matrix specifies. ADDITIVE + idempotent: creates roles by
--  NormalizedName and grants by NOT EXISTS; never edits existing roles or menus.
--
--  Roles created:
--    Inquiry      read-only inquiry/monitor (view appraisal book + history search)
--    Report       report viewer (print + operational reports)
--    IT Security  identity administration (users/roles/groups/permissions/menu)
--
--  NOTE ON TRANSLATION: the matrix models all appraisal side-tabs as a single
--  "APPRAISAL_VIEW" and uses "All" rows; our system gates each tab with a granular
--  APPRAISAL_*_VIEW code. So "Inquiry can view the book read-only" is expanded below
--  into the per-tab view codes. Review/trim the @Grant block if the bank refines
--  these 3 role definitions. Does NOT touch AppraisalCommittee/Sub-Committee — those
--  are handled as committee membership in CreateBankUsers.sql.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- 1) Roles (insert-only by NormalizedName). Description is NOT NULL.
DECLARE @Roles TABLE (Name NVARCHAR(256), Descr NVARCHAR(MAX), Scope NVARCHAR(50));
INSERT INTO @Roles (Name, Descr, Scope) VALUES
    (N'Inquiry',     N'Read-only inquiry / monitor — view appraisal book and history search', N'Bank'),
    (N'Report',      N'Report viewer — print reports and operational reports',                 N'Bank'),
    (N'IT Security', N'IT Security — identity administration (users, roles, groups, permissions, menu)', N'Bank');

INSERT INTO auth.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp, Description, Scope)
SELECT NEWID(), x.Name, UPPER(x.Name), CAST(NEWID() AS NVARCHAR(36)), x.Descr, x.Scope
FROM @Roles x
WHERE NOT EXISTS (SELECT 1 FROM auth.AspNetRoles r WHERE r.NormalizedName = UPPER(x.Name));

-- 2) Grants (matrix-derived). Edit here if the bank refines a role.
DECLARE @Grant TABLE (RoleName NVARCHAR(256), PermissionCode NVARCHAR(450));
INSERT INTO @Grant (RoleName, PermissionCode) VALUES
    -- Inquiry: read-only viewer of the appraisal book + history search
    (N'Inquiry', N'DASHBOARD_VIEW'),
    (N'Inquiry', N'APPRAISAL_VIEW'),
    (N'Inquiry', N'STANDALONE_USE'),
    (N'Inquiry', N'HISTORY_SEARCH_VIEW'),
    (N'Inquiry', N'APPRAISAL_360_VIEW'),
    (N'Inquiry', N'APPRAISAL_REQUEST_VIEW'),
    (N'Inquiry', N'APPRAISAL_ADMINISTRATION_VIEW'),
    (N'Inquiry', N'APPRAISAL_APPOINTMENT_VIEW'),
    (N'Inquiry', N'APPRAISAL_PROPERTY_VIEW'),
    (N'Inquiry', N'APPRAISAL_BLOCK_CONDO_VIEW'),
    (N'Inquiry', N'APPRAISAL_BLOCK_VILLAGE_VIEW'),
    (N'Inquiry', N'APPRAISAL_DOCUMENTS_VIEW'),
    (N'Inquiry', N'APPRAISAL_SUMMARY_VIEW'),
    -- Report: report viewer
    (N'Report', N'DASHBOARD_VIEW'),
    (N'Report', N'APPRAISAL_VIEW'),
    (N'Report', N'STANDALONE_USE'),
    (N'Report', N'REPORT_VIEW'),
    (N'Report', N'REPORT_OP_VIEW'),
    -- IT Security: identity administration
    (N'IT Security', N'DASHBOARD_VIEW'),
    (N'IT Security', N'APPRAISAL_VIEW'),
    (N'IT Security', N'HISTORY_SEARCH_VIEW'),
    (N'IT Security', N'USER_MANAGE'),
    (N'IT Security', N'USER_CHANGE_PASSWORD'),
    (N'IT Security', N'USER_RESET_PASSWORD'),
    (N'IT Security', N'ROLE_MANAGE'),
    (N'IT Security', N'GROUP_MANAGE'),
    (N'IT Security', N'PERMISSION_MANAGE'),
    (N'IT Security', N'MENU_MANAGE');

-- Guard: every referenced permission code must exist.
IF EXISTS (SELECT 1 FROM @Grant g WHERE NOT EXISTS
           (SELECT 1 FROM auth.Permissions p WHERE p.PermissionCode = g.PermissionCode))
BEGIN
    RAISERROR('A referenced PermissionCode does not exist in auth.Permissions. Rolling back.', 16, 1);
    ROLLBACK TRANSACTION;
    RETURN;
END

INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM @Grant g
JOIN auth.AspNetRoles r ON r.NormalizedName = UPPER(g.RoleName)
JOIN auth.Permissions  p ON p.PermissionCode = g.PermissionCode
WHERE NOT EXISTS (SELECT 1 FROM auth.RolePermissions rp
                  WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);

COMMIT TRANSACTION;

-- Verify — expect Inquiry 13, Report 5, IT Security 10.
SELECT r.Name AS Role, COUNT(*) AS Permissions
FROM auth.RolePermissions rp JOIN auth.AspNetRoles r ON r.Id = rp.RoleId
WHERE r.Name IN (N'Inquiry', N'Report', N'IT Security')
GROUP BY r.Name ORDER BY r.Name;
