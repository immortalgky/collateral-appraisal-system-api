-- ============================================================
--  Appraisal side-nav permissions, activity menu overrides, and menu paths
--  Schema: auth
--
--  AuthDataSeed.SeedRoleWithPermissionsAsync is CREATE-ONLY and
--  MenuSeedData's UpsertTreeAsync is INSERT-ONLY for menu items, so none of the
--  C# changes in this release reach a database that already has these roles and
--  menu rows. This script applies them.
--
--  Four parts:
--    1. Role -> permission DELTA for 11 roles (only the codes that actually
--       changed; unrelated grants, including anything added by hand through
--       /admin/roles, are left alone).
--    2. auth.ActivityMenuOverrides rebuilt for the 15 activities the seeder
--       now defines (115 rows). Delete-then-insert, because the override set
--       SHRINKS as well as grows and a pure upsert would leave stale hidden
--       tabs behind.
--    3. Five action tabs gain EditPermissionCode = their view code.
--    4. Appraisal Search path /appraisals/search -> /appraisals/list.
--
--  Idempotent: inserts are NOT EXISTS-guarded, updates pin the old value in the
--  WHERE, and part 2 is a deterministic rebuild. Safe to re-run.
--
--  Notes:
--    * auth.Permissions PK is PermissionId; the code column is PermissionCode.
--    * auth.MenuItems PK is MenuItemId; ActivityMenuOverrides references it by
--      MenuItemId, so every lookup joins through ItemKey.
--    * SET QUOTED_IDENTIFIER ON is REQUIRED -- auth.MenuItems has a filtered index.
--    * Keep Database/Scripts/Maintenance/RestoreAllRolePermissions.sql in sync.
--    * MenuTreeCache ("auth:menu:full") has NO TTL -- RESTART EVERY API INSTANCE
--      after this runs, or the side-nav will serve the old tree.
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

-- ============================================================
-- PART 1 -- Role permission delta
-- ============================================================

DECLARE @RoleGrant TABLE (RoleName NVARCHAR(256), PermissionCode NVARCHAR(100));
DECLARE @RoleRevoke TABLE (RoleName NVARCHAR(256), PermissionCode NVARCHAR(100));

-- IntAdmin
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'IntAdmin', N'APPRAISAL_ADMINISTRATION_EDIT'),
    (N'IntAdmin', N'APPRAISAL_SUMMARY_EDIT'),
    (N'IntAdmin', N'REQUEST_CREATE');
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'IntAdmin', N'APPRAISAL_360_VIEW'),
    (N'IntAdmin', N'APPRAISAL_APPOINTMENT_VIEW'),
    (N'IntAdmin', N'APPRAISAL_PROPERTY_VIEW'),
    (N'IntAdmin', N'APPRAISAL_BLOCK_CONDO_VIEW'),
    (N'IntAdmin', N'APPRAISAL_BLOCK_VILLAGE_VIEW'),
    (N'IntAdmin', N'APPRAISAL_DOCUMENTS_VIEW'),
    (N'IntAdmin', N'TASK_QUOTATION_REVIEW'),
    (N'IntAdmin', N'TASK_QUOTATION_FINALIZE');

-- ExtAdmin
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'ExtAdmin', N'APPRAISAL_APPOINTMENT_EDIT'),
    (N'ExtAdmin', N'APPRAISAL_SUMMARY_EDIT');
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'ExtAdmin', N'APPRAISAL_360_VIEW'),
    (N'ExtAdmin', N'APPRAISAL_ADMINISTRATION_VIEW'),
    (N'ExtAdmin', N'APPRAISAL_PROPERTY_VIEW'),
    (N'ExtAdmin', N'APPRAISAL_BLOCK_CONDO_VIEW'),
    (N'ExtAdmin', N'APPRAISAL_BLOCK_VILLAGE_VIEW'),
    (N'ExtAdmin', N'APPRAISAL_DOCUMENTS_VIEW');

-- RequestMaker
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'RequestMaker', N'APPRAISAL_360_VIEW'),
    (N'RequestMaker', N'APPRAISAL_ADMINISTRATION_VIEW'),
    (N'RequestMaker', N'APPRAISAL_APPOINTMENT_VIEW'),
    (N'RequestMaker', N'APPRAISAL_PROPERTY_VIEW'),
    (N'RequestMaker', N'APPRAISAL_BLOCK_CONDO_VIEW'),
    (N'RequestMaker', N'APPRAISAL_BLOCK_VILLAGE_VIEW'),
    (N'RequestMaker', N'APPRAISAL_DOCUMENTS_VIEW'),
    (N'RequestMaker', N'APPRAISAL_ADMINISTRATION_EDIT'),
    (N'RequestMaker', N'APPRAISAL_APPOINTMENT_EDIT'),
    (N'RequestMaker', N'APPRAISAL_PROPERTY_EDIT'),
    (N'RequestMaker', N'APPRAISAL_BLOCK_CONDO_EDIT'),
    (N'RequestMaker', N'APPRAISAL_BLOCK_VILLAGE_EDIT'),
    (N'RequestMaker', N'APPRAISAL_DOCUMENTS_EDIT');

-- RequestChecker
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'RequestChecker', N'APPRAISAL_REQUEST_VIEW'),
    (N'RequestChecker', N'APPRAISAL_SUMMARY_VIEW'),
    (N'RequestChecker', N'APPRAISAL_SUMMARY_EDIT');

-- IntAppraisalStaff
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'IntAppraisalStaff', N'APPRAISAL_REQUEST_EDIT'),
    (N'IntAppraisalStaff', N'APPRAISAL_ADMINISTRATION_EDIT');

-- IntAppraisalChecker
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'IntAppraisalChecker', N'APPRAISAL_DOCUMENTS_EDIT'),
    (N'IntAppraisalChecker', N'APPRAISAL_SUMMARY_EDIT');

-- IntAppraisalVerifier
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'IntAppraisalVerifier', N'APPRAISAL_DOCUMENTS_EDIT'),
    (N'IntAppraisalVerifier', N'APPRAISAL_SUMMARY_EDIT');

-- ExtAppraisalStaff
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'ExtAppraisalStaff', N'APPRAISAL_ADMINISTRATION_VIEW'),
    (N'ExtAppraisalStaff', N'APPRAISAL_REQUEST_EDIT'),
    (N'ExtAppraisalStaff', N'APPRAISAL_ADMINISTRATION_EDIT');

-- ExtAppraisalChecker
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'ExtAppraisalChecker', N'APPRAISAL_SUMMARY_EDIT');
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'ExtAppraisalChecker', N'APPRAISAL_ADMINISTRATION_VIEW');

-- ExtAppraisalVerifier
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'ExtAppraisalVerifier', N'APPRAISAL_SUMMARY_EDIT');
INSERT INTO @RoleRevoke (RoleName, PermissionCode) VALUES
    (N'ExtAppraisalVerifier', N'APPRAISAL_ADMINISTRATION_VIEW');

-- AppraisalCommittee
INSERT INTO @RoleGrant (RoleName, PermissionCode) VALUES
    (N'AppraisalCommittee', N'APPRAISAL_SUMMARY_EDIT');

INSERT INTO auth.RolePermissions (RoleId, PermissionId)
SELECT r.Id, p.PermissionId
FROM @RoleGrant g
JOIN auth.AspNetRoles r ON r.Name = g.RoleName
JOIN auth.Permissions p ON p.PermissionCode = g.PermissionCode
WHERE NOT EXISTS (
    SELECT 1 FROM auth.RolePermissions rp
    WHERE rp.RoleId = r.Id AND rp.PermissionId = p.PermissionId);
PRINT CONCAT('Granted   : ', @@ROWCOUNT);

DELETE rp
FROM auth.RolePermissions rp
JOIN auth.AspNetRoles r ON r.Id = rp.RoleId
JOIN auth.Permissions p ON p.PermissionId = rp.PermissionId
JOIN @RoleRevoke x ON x.RoleName = r.Name AND x.PermissionCode = p.PermissionCode;
PRINT CONCAT('Revoked   : ', @@ROWCOUNT);

-- ============================================================
-- PART 2 -- Rebuild ActivityMenuOverrides
-- ============================================================

DECLARE @Ovr TABLE (ActivityId NVARCHAR(100), ItemKey NVARCHAR(200), IsVisible BIT, CanEdit BIT);

INSERT INTO @Ovr (ActivityId, ItemKey, IsVisible, CanEdit) VALUES
    (N'appraisal-initiation', N'appraisal.360', 1, 0),
    (N'appraisal-initiation', N'appraisal.administration', 0, 0),
    (N'int-appraisal-execution', N'appraisal.property-pma', 0, 0),
    (N'appraisal-book-verification', N'appraisal.property-pma', 0, 0),
    (N'int-offline-book-keyin', N'appraisal.property-pma', 0, 0),
    (N'appraisal-assignment', N'appraisal.fee-appointment-approval', 0, 0),
    (N'int-appraisal-check', N'appraisal.fee-appointment-approval', 0, 0),
    (N'appraisal-book-verification', N'appraisal.appointment', 1, 0),
    (N'appraisal-book-verification', N'appraisal.property', 1, 0),
    (N'appraisal-book-verification', N'appraisal.block-condo', 1, 0),
    (N'appraisal-book-verification', N'appraisal.block-village', 1, 0),
    (N'ext-appraisal-assignment', N'appraisal.quotation-submit', 0, 0),
    (N'ext-appraisal-check', N'appraisal.quotation-submit', 0, 0),
    (N'ext-appraisal-assignment', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'ext-appraisal-check', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'appraisal-initiation-check', N'appraisal.request', 1, 0),
    (N'appraisal-initiation', N'appraisal.quotation-pick-winner', 0, 0),
    (N'appraisal-initiation-check', N'appraisal.quotation-pick-winner', 0, 0),
    (N'appraisal-initiation', N'appraisal.document-followup', 0, 0),
    (N'appraisal-initiation-check', N'appraisal.document-followup', 0, 0),
    (N'ext-collect-submissions', N'appraisal.360', 0, 0),
    (N'ext-collect-submissions', N'appraisal.request', 1, 0),
    (N'ext-collect-submissions', N'appraisal.administration', 0, 0),
    (N'ext-collect-submissions', N'appraisal.appointment', 0, 0),
    (N'ext-collect-submissions', N'appraisal.fee-appointment-approval', 0, 0),
    (N'ext-collect-submissions', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'ext-collect-submissions', N'appraisal.quotation-review', 0, 0),
    (N'ext-collect-submissions', N'appraisal.quotation-pick-winner', 0, 0),
    (N'ext-collect-submissions', N'appraisal.quotation-finalize', 0, 0),
    (N'ext-collect-submissions', N'appraisal.property', 0, 0),
    (N'ext-collect-submissions', N'appraisal.block-condo', 0, 0),
    (N'ext-collect-submissions', N'appraisal.block-village', 0, 0),
    (N'ext-collect-submissions', N'appraisal.property-pma', 0, 0),
    (N'ext-collect-submissions', N'appraisal.documents', 0, 0),
    (N'ext-collect-submissions', N'appraisal.document-followup', 0, 0),
    (N'ext-collect-submissions', N'appraisal.summary', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.360', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.request', 1, 0),
    (N'ext-respond-negotiation', N'appraisal.administration', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.appointment', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.fee-appointment-approval', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.quotation-submit', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.quotation-review', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.quotation-pick-winner', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.quotation-finalize', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.property', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.block-condo', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.block-village', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.property-pma', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.documents', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.document-followup', 0, 0),
    (N'ext-respond-negotiation', N'appraisal.summary', 0, 0),
    (N'rm-pick-winner', N'appraisal.360', 0, 0),
    (N'rm-pick-winner', N'appraisal.request', 1, 0),
    (N'rm-pick-winner', N'appraisal.administration', 0, 0),
    (N'rm-pick-winner', N'appraisal.appointment', 0, 0),
    (N'rm-pick-winner', N'appraisal.fee-appointment-approval', 0, 0),
    (N'rm-pick-winner', N'appraisal.quotation-submit', 0, 0),
    (N'rm-pick-winner', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'rm-pick-winner', N'appraisal.quotation-review', 0, 0),
    (N'rm-pick-winner', N'appraisal.quotation-finalize', 0, 0),
    (N'rm-pick-winner', N'appraisal.property', 0, 0),
    (N'rm-pick-winner', N'appraisal.block-condo', 0, 0),
    (N'rm-pick-winner', N'appraisal.block-village', 0, 0),
    (N'rm-pick-winner', N'appraisal.property-pma', 0, 0),
    (N'rm-pick-winner', N'appraisal.documents', 0, 0),
    (N'rm-pick-winner', N'appraisal.document-followup', 0, 0),
    (N'rm-pick-winner', N'appraisal.summary', 0, 0),
    (N'provide-additional-documents', N'appraisal.360', 0, 0),
    (N'provide-additional-documents', N'appraisal.request', 1, 0),
    (N'provide-additional-documents', N'appraisal.administration', 0, 0),
    (N'provide-additional-documents', N'appraisal.appointment', 0, 0),
    (N'provide-additional-documents', N'appraisal.fee-appointment-approval', 0, 0),
    (N'provide-additional-documents', N'appraisal.quotation-submit', 0, 0),
    (N'provide-additional-documents', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'provide-additional-documents', N'appraisal.quotation-review', 0, 0),
    (N'provide-additional-documents', N'appraisal.quotation-pick-winner', 0, 0),
    (N'provide-additional-documents', N'appraisal.quotation-finalize', 0, 0),
    (N'provide-additional-documents', N'appraisal.property', 0, 0),
    (N'provide-additional-documents', N'appraisal.block-condo', 0, 0),
    (N'provide-additional-documents', N'appraisal.block-village', 0, 0),
    (N'provide-additional-documents', N'appraisal.property-pma', 0, 0),
    (N'provide-additional-documents', N'appraisal.documents', 0, 0),
    (N'provide-additional-documents', N'appraisal.summary', 0, 0),
    (N'fee-appointment-approval', N'appraisal.360', 0, 0),
    (N'fee-appointment-approval', N'appraisal.request', 1, 0),
    (N'fee-appointment-approval', N'appraisal.administration', 0, 0),
    (N'fee-appointment-approval', N'appraisal.appointment', 0, 0),
    (N'fee-appointment-approval', N'appraisal.quotation-submit', 0, 0),
    (N'fee-appointment-approval', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'fee-appointment-approval', N'appraisal.quotation-review', 0, 0),
    (N'fee-appointment-approval', N'appraisal.quotation-pick-winner', 0, 0),
    (N'fee-appointment-approval', N'appraisal.quotation-finalize', 0, 0),
    (N'fee-appointment-approval', N'appraisal.property', 0, 0),
    (N'fee-appointment-approval', N'appraisal.block-condo', 0, 0),
    (N'fee-appointment-approval', N'appraisal.block-village', 0, 0),
    (N'fee-appointment-approval', N'appraisal.property-pma', 0, 0),
    (N'fee-appointment-approval', N'appraisal.documents', 0, 0),
    (N'fee-appointment-approval', N'appraisal.document-followup', 0, 0),
    (N'fee-appointment-approval', N'appraisal.summary', 0, 0),
    (N'int-pma-input', N'appraisal.360', 0, 0),
    (N'int-pma-input', N'appraisal.request', 1, 0),
    (N'int-pma-input', N'appraisal.administration', 0, 0),
    (N'int-pma-input', N'appraisal.appointment', 0, 0),
    (N'int-pma-input', N'appraisal.fee-appointment-approval', 0, 0),
    (N'int-pma-input', N'appraisal.quotation-submit', 0, 0),
    (N'int-pma-input', N'appraisal.quotation-respond-negotiation', 0, 0),
    (N'int-pma-input', N'appraisal.quotation-review', 0, 0),
    (N'int-pma-input', N'appraisal.quotation-pick-winner', 0, 0),
    (N'int-pma-input', N'appraisal.quotation-finalize', 0, 0),
    (N'int-pma-input', N'appraisal.property', 0, 0),
    (N'int-pma-input', N'appraisal.block-condo', 0, 0),
    (N'int-pma-input', N'appraisal.block-village', 0, 0),
    (N'int-pma-input', N'appraisal.documents', 0, 0),
    (N'int-pma-input', N'appraisal.document-followup', 0, 0);

-- Only these activities are managed here; any other activity's overrides are untouched.
DELETE o
FROM auth.ActivityMenuOverrides o
WHERE o.ActivityId IN (
    N'appraisal-assignment',
    N'appraisal-book-verification',
    N'appraisal-initiation',
    N'appraisal-initiation-check',
    N'ext-appraisal-assignment',
    N'ext-appraisal-check',
    N'ext-collect-submissions',
    N'ext-respond-negotiation',
    N'fee-appointment-approval',
    N'int-appraisal-check',
    N'int-appraisal-execution',
    N'int-offline-book-keyin',
    N'int-pma-input',
    N'provide-additional-documents',
    N'rm-pick-winner');
PRINT CONCAT('Overrides removed : ', @@ROWCOUNT);

INSERT INTO auth.ActivityMenuOverrides (ActivityMenuOverrideId, ActivityId, MenuItemId, IsVisible, CanEdit)
SELECT NEWID(), v.ActivityId, m.MenuItemId, v.IsVisible, v.CanEdit
FROM @Ovr v
JOIN auth.MenuItems m ON m.ItemKey = v.ItemKey;
PRINT CONCAT('Overrides inserted: ', @@ROWCOUNT);

-- Surface any tab key that did not resolve, rather than silently under-applying.
IF EXISTS (SELECT 1 FROM @Ovr v WHERE NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = v.ItemKey))
BEGIN
    PRINT 'WARNING: some appraisal tab keys had no matching auth.MenuItems row:';
    SELECT DISTINCT v.ItemKey
    FROM @Ovr v
    WHERE NOT EXISTS (SELECT 1 FROM auth.MenuItems m WHERE m.ItemKey = v.ItemKey);
END

-- ============================================================
-- PART 3 -- Action tabs become editable (EditPermissionCode = view code)
-- ============================================================

UPDATE auth.MenuItems SET EditPermissionCode = N'TASK_FEE_APPOINTMENT_APPROVAL'
WHERE ItemKey = N'appraisal.fee-appointment-approval' AND EditPermissionCode IS NULL;
UPDATE auth.MenuItems SET EditPermissionCode = N'TASK_QUOTATION_SUBMIT'
WHERE ItemKey = N'appraisal.quotation-submit' AND EditPermissionCode IS NULL;
UPDATE auth.MenuItems SET EditPermissionCode = N'TASK_QUOTATION_NEGOTIATE'
WHERE ItemKey = N'appraisal.quotation-respond-negotiation' AND EditPermissionCode IS NULL;
UPDATE auth.MenuItems SET EditPermissionCode = N'TASK_QUOTATION_PICK_WINNER'
WHERE ItemKey = N'appraisal.quotation-pick-winner' AND EditPermissionCode IS NULL;
UPDATE auth.MenuItems SET EditPermissionCode = N'TASK_PROVIDE_ADDITIONAL_DOCS'
WHERE ItemKey = N'appraisal.document-followup' AND EditPermissionCode IS NULL;

-- ============================================================
-- PART 4 -- Appraisal Search path
-- ============================================================

UPDATE auth.MenuItems SET [Path] = N'/appraisals/list'
WHERE ItemKey = N'main.appraisal.search' AND [Path] = N'/appraisals/search';

COMMIT TRANSACTION;

PRINT 'Done. RESTART EVERY API INSTANCE -- MenuTreeCache has no TTL.';
