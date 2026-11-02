using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeProjectSaleLaunchDateToPartialDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert datetime2 → nvarchar(10). EF's plain AlterColumn would fail on an implicit
            // datetime2-to-string cast, so we stage the new column and then swap names. The
            // CONVERT(..., 23) value copy was removed; only safe on an empty table.
            migrationBuilder.Sql(@"
                ALTER TABLE [appraisal].[Projects]
                ADD [ProjectSaleLaunchDate_New] nvarchar(10) NULL;
            ");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.Sql(@"
                ALTER TABLE [appraisal].[Projects]
                DROP COLUMN [ProjectSaleLaunchDate];
            ");

            migrationBuilder.Sql(@"
                EXEC sp_rename
                    N'[appraisal].[Projects].[ProjectSaleLaunchDate_New]',
                    N'ProjectSaleLaunchDate',
                    N'COLUMN';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort reverse for dev rollback only.
            // Year-only ('YYYY') and year-month ('YYYY-MM') strings cannot round-trip to a
            // datetime2 — TRY_CONVERT yields NULL for those rows. Full-date strings
            // ('YYYY-MM-DD') round-trip cleanly.
            migrationBuilder.Sql(@"
                ALTER TABLE [appraisal].[Projects]
                ADD [ProjectSaleLaunchDate_Old] datetime2 NULL;
            ");

            migrationBuilder.Sql(@"
                UPDATE [appraisal].[Projects]
                SET [ProjectSaleLaunchDate_Old] = TRY_CONVERT(datetime2, [ProjectSaleLaunchDate], 23)
                WHERE [ProjectSaleLaunchDate] IS NOT NULL;
            ");

            migrationBuilder.Sql(@"
                ALTER TABLE [appraisal].[Projects]
                DROP COLUMN [ProjectSaleLaunchDate];
            ");

            migrationBuilder.Sql(@"
                EXEC sp_rename
                    N'[appraisal].[Projects].[ProjectSaleLaunchDate_Old]',
                    N'ProjectSaleLaunchDate',
                    N'COLUMN';
            ");
        }
    }
}
