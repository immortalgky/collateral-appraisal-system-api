-- ============================================================
-- Seed: Internal Teams + TeamMembers
-- Depends on: 20260323201000_SeedData_WorkflowUsersAndRoles.sql
-- Table creation is owned by the Auth EF migration
-- (20260610094500_RenameTeamTypeToScopeDropIsActive); this script only seeds.
-- Idempotent — safe to re-run.
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- Seed two internal teams
-- ============================================================

-- Appraisal Team Alpha
IF NOT EXISTS (SELECT 1 FROM auth.Teams WHERE Name = N'Appraisal Team Alpha')
BEGIN
    DECLARE @AlphaId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO auth.Teams (Id, Name, Scope)
    VALUES (@AlphaId, N'Appraisal Team Alpha', N'Bank');

    -- Members: int.staff1, int.staff2, int.checker1, int.verifier1, committee1
    INSERT INTO auth.TeamMembers (TeamId, UserId)
    SELECT @AlphaId, u.Id
    FROM auth.AspNetUsers u
    WHERE u.NormalizedUserName IN (
        N'INT.STAFF1', N'INT.STAFF2',
        N'INT.CHK1',
        N'INT.VRF1',
        N'COMMITTEE1'
    )
    AND NOT EXISTS (
        SELECT 1 FROM auth.TeamMembers tm WHERE tm.TeamId = @AlphaId AND tm.UserId = u.Id
    );
END

-- Appraisal Team Beta
IF NOT EXISTS (SELECT 1 FROM auth.Teams WHERE Name = N'Appraisal Team Beta')
BEGIN
    DECLARE @BetaId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO auth.Teams (Id, Name, Scope)
    VALUES (@BetaId, N'Appraisal Team Beta', N'Bank');

    -- Members: int.staff3, int.checker2, int.verifier2, committee2
    INSERT INTO auth.TeamMembers (TeamId, UserId)
    SELECT @BetaId, u.Id
    FROM auth.AspNetUsers u
    WHERE u.NormalizedUserName IN (
        N'INT.STAFF3',
        N'INT.CHK2',
        N'INT.VRF2',
        N'COMMITTEE2'
    )
    AND NOT EXISTS (
        SELECT 1 FROM auth.TeamMembers tm WHERE tm.TeamId = @BetaId AND tm.UserId = u.Id
    );
END

PRINT 'Seed data for internal teams completed.';
