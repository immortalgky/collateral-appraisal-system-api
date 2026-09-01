using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitUploadOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AddedUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "AutoSoldUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSystemGenerated",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MatchedUnsoldUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UpdatedUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads",
                type: "int",
                nullable: true);
            // Existing history would otherwise read "0 units" for every past batch. Two of the five
            // columns can be recovered honestly: the seed batch is identifiable by the literal the
            // seeder writes, and how many units a batch brought in is still countable from the units
            // that point at it. The re-match counters cannot be reconstructed and stay NULL, which
            // the screen renders as "not recorded" rather than as zero.
            //
            // Two literals, not one: the seeder said "Seeded from prior appraisal" before it was
            // renamed, and rows written under the old wording are still in the databases.
            migrationBuilder.Sql(
                """
                UPDATE appraisal.ProjectUnitUploads
                SET IsSystemGenerated = 1
                WHERE FileName IN (N'Seeded from collateral master',
                                   N'Seeded from prior appraisal');
                """);

            migrationBuilder.Sql(
                """
                UPDATE u
                SET u.AddedUnits = x.Cnt
                FROM appraisal.ProjectUnitUploads u
                CROSS APPLY (
                    SELECT COUNT(*) AS Cnt
                    FROM appraisal.ProjectUnits pu
                    WHERE pu.UploadBatchId = u.Id
                ) x
                WHERE x.Cnt > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AddedUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads");

            migrationBuilder.DropColumn(
                name: "AutoSoldUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads");

            migrationBuilder.DropColumn(
                name: "IsSystemGenerated",
                schema: "appraisal",
                table: "ProjectUnitUploads");

            migrationBuilder.DropColumn(
                name: "MatchedUnsoldUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads");

            migrationBuilder.DropColumn(
                name: "UpdatedUnits",
                schema: "appraisal",
                table: "ProjectUnitUploads");
        }
    }
}
