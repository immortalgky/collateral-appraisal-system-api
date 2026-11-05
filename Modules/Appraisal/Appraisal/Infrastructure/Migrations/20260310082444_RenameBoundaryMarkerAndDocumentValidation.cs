using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameBoundaryMarkerAndDocumentValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // LandTitles.HasBoundaryMarker is already nvarchar(max) — just rename
            migrationBuilder.RenameColumn(
                name: "HasBoundaryMarker",
                schema: "appraisal",
                table: "LandTitles",
                newName: "BoundaryMarkerType");

            // LandTitles.IsDocumentValidated: bit → nvarchar(max). The value copy was removed;
            // only safe on an empty table.
            migrationBuilder.AddColumn<string>(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(max)",
                nullable: true);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.DropColumn(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "LandTitles");

            // CondoAppraisalDetails.IsDocumentValidated: bit → nvarchar(max). Same: no value copy.
            migrationBuilder.AddColumn<string>(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(max)",
                nullable: true);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.DropColumn(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "CondoAppraisalDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse LandTitles.BoundaryMarkerType rename
            migrationBuilder.RenameColumn(
                name: "BoundaryMarkerType",
                schema: "appraisal",
                table: "LandTitles",
                newName: "HasBoundaryMarker");

            // Reverse LandTitles.DocumentValidationResultType → IsDocumentValidated (bit)
            migrationBuilder.AddColumn<bool>(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "LandTitles",
                type: "bit",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE appraisal.LandTitles SET IsDocumentValidated = CASE WHEN DocumentValidationResultType IS NOT NULL THEN CAST(DocumentValidationResultType AS BIT) ELSE NULL END");

            migrationBuilder.DropColumn(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "LandTitles");

            // Reverse CondoAppraisalDetails.DocumentValidationResultType → IsDocumentValidated (bit)
            migrationBuilder.AddColumn<bool>(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "bit",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE appraisal.CondoAppraisalDetails SET IsDocumentValidated = CASE WHEN DocumentValidationResultType IS NOT NULL THEN CAST(DocumentValidationResultType AS BIT) ELSE NULL END");

            migrationBuilder.DropColumn(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "CondoAppraisalDetails");
        }
    }
}
