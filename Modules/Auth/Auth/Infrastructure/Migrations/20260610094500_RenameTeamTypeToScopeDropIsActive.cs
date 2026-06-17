using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTeamTypeToScopeDropIsActive : Migration
    {
        // auth.Teams / auth.TeamMembers are mapped with ExcludeFromMigrations(), so EF does
        // not scaffold ops for them. This migration owns their creation (final schema) and
        // migrates any pre-existing old-schema (Type/IsActive) tables. Authored as guarded raw SQL.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // EF now owns creation of these tables. Create them in their FINAL shape if
            // missing (fresh DB), since the coordinator runs EF migrations before the DbUp
            // seed script. On existing DBs the tables already exist, so this no-ops and the
            // transform blocks below migrate the old Type/IsActive schema to Scope/Description.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams')
    CREATE TABLE auth.Teams (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWSEQUENTIALID(),
        Name NVARCHAR(200) NOT NULL,
        Scope NVARCHAR(50) NOT NULL CONSTRAINT DF_Teams_Scope DEFAULT 'Bank',
        Description NVARCHAR(500) NULL,
        CONSTRAINT PK_Teams PRIMARY KEY (Id)
    );");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'TeamMembers')
    CREATE TABLE auth.TeamMembers (
        TeamId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        CONSTRAINT PK_TeamMembers PRIMARY KEY (TeamId, UserId),
        CONSTRAINT FK_TeamMembers_Teams FOREIGN KEY (TeamId) REFERENCES auth.Teams(Id),
        CONSTRAINT FK_TeamMembers_Users FOREIGN KEY (UserId) REFERENCES auth.AspNetUsers(Id)
    );");

            // Rename Type -> Scope (dropping its auto-named default constraint first)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Type')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Scope')
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Type';
    IF @df IS NOT NULL EXEC('ALTER TABLE auth.Teams DROP CONSTRAINT ' + @df);
    EXEC sp_rename 'auth.Teams.Type', 'Scope', 'COLUMN';
END");

            // Backfill values
            migrationBuilder.Sql(@"
UPDATE auth.Teams
SET Scope = CASE Scope WHEN 'Internal' THEN 'Bank' WHEN 'External' THEN 'Company' ELSE Scope END
WHERE Scope IN ('Internal', 'External');");

            // Default 'Bank' on Scope
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Scope')
    ALTER TABLE auth.Teams ADD CONSTRAINT DF_Teams_Scope DEFAULT 'Bank' FOR Scope;");

            // Add Description column
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Description')
    ALTER TABLE auth.Teams ADD Description NVARCHAR(500) NULL;");

            // Drop IsActive column (and its auto-named default constraint)
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'IsActive')
BEGIN
    DECLARE @dfa sysname;
    SELECT @dfa = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'IsActive';
    IF @dfa IS NOT NULL EXEC('ALTER TABLE auth.Teams DROP CONSTRAINT ' + @dfa);
    ALTER TABLE auth.Teams DROP COLUMN IsActive;
END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore IsActive
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'IsActive')
    ALTER TABLE auth.Teams ADD IsActive BIT NOT NULL CONSTRAINT DF_Teams_IsActive DEFAULT 1;");

            // Drop Description
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Description')
    ALTER TABLE auth.Teams DROP COLUMN Description;");

            // Rename Scope -> Type
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Scope')
   AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
           WHERE TABLE_SCHEMA = 'auth' AND TABLE_NAME = 'Teams' AND COLUMN_NAME = 'Type')
BEGIN
    DECLARE @df sysname;
    SELECT @df = dc.name
    FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Scope';
    IF @df IS NOT NULL EXEC('ALTER TABLE auth.Teams DROP CONSTRAINT ' + @df);
    EXEC sp_rename 'auth.Teams.Scope', 'Type', 'COLUMN';
END");

            migrationBuilder.Sql(@"
UPDATE auth.Teams
SET Type = CASE Type WHEN 'Bank' THEN 'Internal' WHEN 'Company' THEN 'External' ELSE Type END
WHERE Type IN ('Bank', 'Company');");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM sys.default_constraints dc
    JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
    WHERE dc.parent_object_id = OBJECT_ID('auth.Teams') AND c.name = 'Type')
    ALTER TABLE auth.Teams ADD CONSTRAINT DF_Teams_Type DEFAULT 'Internal' FOR Type;");
        }
    }
}
