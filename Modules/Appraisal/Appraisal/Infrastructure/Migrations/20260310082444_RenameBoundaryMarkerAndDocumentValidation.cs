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

            // LandTitles.IsDocumentValidated: bit → nvarchar(max) with data migration
            migrationBuilder.AddColumn<string>(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "LandTitles",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE appraisal.LandTitles SET DocumentValidationResultType = CAST(IsDocumentValidated AS NVARCHAR(10))");

            migrationBuilder.DropColumn(
                name: "IsDocumentValidated",
                schema: "appraisal",
                table: "LandTitles");

            // CondoAppraisalDetails.IsDocumentValidated: bit → nvarchar(max) with data migration
            migrationBuilder.AddColumn<string>(
                name: "DocumentValidationResultType",
                schema: "appraisal",
                table: "CondoAppraisalDetails",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql(
                "UPDATE appraisal.CondoAppraisalDetails SET DocumentValidationResultType = CAST(IsDocumentValidated AS NVARCHAR(10))");

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
