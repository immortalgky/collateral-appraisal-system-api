-- ============================================================
-- Seed: Internal Teams + TeamMembers
-- Depends on: 20260323201000_SeedData_WorkflowUsersAndRoles.sql
-- Idempotent — safe to re-run.
-- ============================================================

SET NOCOUNT ON;

-- ============================================================
-- Section 1: Create auth.Teams table (if not exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams')
BEGIN
    CREATE TABLE auth.Teams (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Name NVARCHAR(200) NOT NULL,
        Type NVARCHAR(50) NOT NULL DEFAULT 'Internal',
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT PK_Teams PRIMARY KEY (Id)
    );
END

-- ============================================================
-- Section 2: Create auth.TeamMembers table (if not exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'TeamMembers')
BEGIN
    CREATE TABLE auth.TeamMembers (
        TeamId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_TeamMembers PRIMARY KEY (TeamId, UserId),
        CONSTRAINT FK_TeamMembers_Teams FOREIGN KEY (TeamId) REFERENCES auth.Teams(Id),
        CONSTRAINT FK_TeamMembers_Users FOREIGN KEY (UserId) REFERENCES auth.AspNetUsers(Id)
    );
END

-- ============================================================
-- Section 3: Seed two internal teams
-- ============================================================

-- Appraisal Team Alpha
IF NOT EXISTS (SELECT 1 FROM auth.Teams WHERE Name = N'Appraisal Team Alpha')
BEGIN
    DECLARE @AlphaId UNIQUEIDENTIFIER = NEWID();

    INSERT INTO auth.Teams (Id, Name, Type, IsActive)
    VALUES (@AlphaId, N'Appraisal Team Alpha', N'Internal', 1);

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

    INSERT INTO auth.Teams (Id, Name, Type, IsActive)
    VALUES (@BetaId, N'Appraisal Team Beta', N'Internal', 1);

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
