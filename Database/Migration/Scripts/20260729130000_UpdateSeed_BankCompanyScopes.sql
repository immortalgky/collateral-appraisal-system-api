-- ============================================================
-- Backfill Bank/Company Scope on seeded roles and workflow groups
-- Schema: auth
--
-- Scope decides which users a role/group can be assigned to: 'Bank' for the host bank's own
-- staff (AspNetUsers.CompanyId IS NULL), 'Company' for external appraisal-company users. The
-- admin UI compares it exactly, so any other value drops the row out of every picker.
--
-- Two seed paths produced such rows:
--   * auth.Groups   — AuthDataSeed passed the literal 'System' as the scope for all 12
--                     workflow-assignment groups.
--   * auth.AspNetRoles — 20260323201000_SeedData_WorkflowUsersAndRoles.sql inserted its 10
--                     roles without a Scope column, leaving them NULL. Both seeds are fixed
--                     going forward; this repairs databases already built from them.
--
-- Idempotent, and deliberately narrow: only rows still holding NULL / '' / 'System' are
-- touched, so a scope an admin set on purpose through /admin/roles or /admin/groups survives.
-- ============================================================

SET NOCOUNT ON;

-- ------------------------------------------------------------
-- Roles
-- ------------------------------------------------------------

DECLARE @RoleScopes TABLE (Name NVARCHAR(256), Scope NVARCHAR(50));
INSERT INTO @RoleScopes (Name, Scope) VALUES
    (N'Admin',                N'Bank'),
    (N'MeetingSecretary',     N'Bank'),
    (N'IntAdmin',             N'Bank'),
    (N'RequestMaker',         N'Bank'),
    (N'RequestChecker',       N'Bank'),
    (N'IntAppraisalStaff',    N'Bank'),
    (N'IntAppraisalChecker',  N'Bank'),
    (N'IntAppraisalVerifier', N'Bank'),
    (N'AppraisalCommittee',   N'Bank'),
    (N'ExtAdmin',             N'Company'),
    (N'ExtAppraisalStaff',    N'Company'),
    (N'ExtAppraisalChecker',  N'Company'),
    (N'ExtAppraisalVerifier', N'Company');

UPDATE r
SET r.Scope = s.Scope
FROM auth.AspNetRoles r
INNER JOIN @RoleScopes s ON s.Name = r.Name
WHERE r.Scope IS NULL OR r.Scope = N'';

PRINT CONCAT('Backfilled Scope on ', @@ROWCOUNT, ' seeded role(s).');

-- ------------------------------------------------------------
-- Workflow assignment groups
-- ------------------------------------------------------------
-- MeetingSecretary is intentionally absent: SeedWorkflowGroupsAsync never creates a group for
-- it. CompanyId is left untouched — the Company-scoped groups are shared across every external
-- company.

DECLARE @GroupScopes TABLE (Name NVARCHAR(256), Scope NVARCHAR(50));
INSERT INTO @GroupScopes (Name, Scope) VALUES
    (N'Admin',                N'Bank'),
    (N'IntAdmin',             N'Bank'),
    (N'RequestMaker',         N'Bank'),
    (N'RequestChecker',       N'Bank'),
    (N'IntAppraisalStaff',    N'Bank'),
    (N'IntAppraisalChecker',  N'Bank'),
    (N'IntAppraisalVerifier', N'Bank'),
    (N'AppraisalCommittee',   N'Bank'),
    (N'ExtAdmin',             N'Company'),
    (N'ExtAppraisalStaff',    N'Company'),
    (N'ExtAppraisalChecker',  N'Company'),
    (N'ExtAppraisalVerifier', N'Company');

UPDATE g
SET g.Scope = s.Scope
FROM auth.Groups g
INNER JOIN @GroupScopes s ON s.Name = g.Name
WHERE g.Scope IS NULL OR g.Scope = N'' OR g.Scope = N'System';

PRINT CONCAT('Backfilled Scope on ', @@ROWCOUNT, ' workflow group(s).');
GO
