using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ProjectTypeToTextCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Two-step column-type change: add new column, backfill, drop old, rename, alter NOT NULL.
            // Direct AlterColumn on SQL Server cannot convert int → nvarchar when data exists.

            // 1. Add the new nullable text column alongside the existing int column
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects ADD ProjectType_New nvarchar(2) NULL;
                """);

            // 2. Backfill: convert existing int values to short text codes
            migrationBuilder.Sql("""
                UPDATE appraisal.Projects
                SET ProjectType_New = CASE ProjectType
                    WHEN 1 THEN N'U'
                    WHEN 2 THEN N'LB'
                    ELSE NULL
                END;
                """);

            // 3. Drop the old int column
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects DROP COLUMN ProjectType;
                """);

            // 4. Rename the new column to ProjectType
            migrationBuilder.Sql("""
                EXEC sp_rename 'appraisal.Projects.ProjectType_New', 'ProjectType', 'COLUMN';
                """);

            // 5. Make the column NOT NULL now that all rows have a value
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects ALTER COLUMN ProjectType nvarchar(2) NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: text codes back to int. 'L' has no int equivalent — fail-fast BEFORE
            // mutating any schema so an aborted Down leaves the table untouched.

            // 1. Guard: refuse to roll back if any 'L' rows exist (cannot convert back to int)
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM appraisal.Projects WHERE ProjectType = N'L')
                    THROW 51000, 'Cannot roll back ProjectTypeToTextCodes: rows with ProjectType=''L'' exist and have no int equivalent.', 1;
                """);

            // 2. Add int column alongside text column
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects ADD ProjectType_Old int NULL;
                """);

            // 3. Backfill: convert text codes back to int values
            migrationBuilder.Sql("""
                UPDATE appraisal.Projects
                SET ProjectType_Old = CASE ProjectType
                    WHEN N'U'  THEN 1
                    WHEN N'LB' THEN 2
                    ELSE NULL
                END;
                """);

            // 4. Drop the text column
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects DROP COLUMN ProjectType;
                """);

            // 5. Rename the int column back to ProjectType
            migrationBuilder.Sql("""
                EXEC sp_rename 'appraisal.Projects.ProjectType_Old', 'ProjectType', 'COLUMN';
                """);

            // 6. Make the column NOT NULL
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.Projects ALTER COLUMN ProjectType int NOT NULL;
                """);
        }
    }
}
