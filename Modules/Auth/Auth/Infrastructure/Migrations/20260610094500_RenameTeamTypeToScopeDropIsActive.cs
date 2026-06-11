using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameTeamTypeToScopeDropIsActive : Migration
    {
        // auth.Teams is DbUp-created and mapped with ExcludeFromMigrations(), so EF does
        // not scaffold column ops for it. Authored explicitly as guarded raw SQL.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
