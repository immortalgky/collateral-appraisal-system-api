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

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
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
