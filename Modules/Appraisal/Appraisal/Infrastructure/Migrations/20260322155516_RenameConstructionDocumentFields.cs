using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameConstructionDocumentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DocumentFilePath",
                schema: "appraisal",
                table: "ConstructionInspections",
                newName: "FilePath");

            migrationBuilder.RenameColumn(
                name: "DocumentFileName",
                schema: "appraisal",
                table: "ConstructionInspections",
                newName: "FileName");

            migrationBuilder.AddColumn<string>(
                name: "FileExtension",
                schema: "appraisal",
                table: "ConstructionInspections",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                schema: "appraisal",
                table: "ConstructionInspections",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                schema: "appraisal",
                table: "ConstructionInspections",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileExtension",
                schema: "appraisal",
                table: "ConstructionInspections");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                schema: "appraisal",
                table: "ConstructionInspections");

            migrationBuilder.DropColumn(
                name: "MimeType",
                schema: "appraisal",
                table: "ConstructionInspections");

            migrationBuilder.RenameColumn(
                name: "FilePath",
                schema: "appraisal",
                table: "ConstructionInspections",
                newName: "DocumentFilePath");

            migrationBuilder.RenameColumn(
                name: "FileName",
                schema: "appraisal",
                table: "ConstructionInspections",
                newName: "DocumentFileName");
        }
    }
}
