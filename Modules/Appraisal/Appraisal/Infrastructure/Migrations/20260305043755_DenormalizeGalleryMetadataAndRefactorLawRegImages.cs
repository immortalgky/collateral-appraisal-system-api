using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DenormalizeGalleryMetadataAndRefactorLawRegImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "appraisal",
                table: "LawAndRegulationImages");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "appraisal",
                table: "LawAndRegulationImages");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                newName: "GalleryPhotoId");

            migrationBuilder.AddColumn<string>(
                name: "FileExtension",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "FileSizeBytes",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MimeType",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UploadedByName",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LawAndRegulationImages_GalleryPhotoId",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                column: "GalleryPhotoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LawAndRegulationImages_GalleryPhotoId",
                schema: "appraisal",
                table: "LawAndRegulationImages");

            migrationBuilder.DropColumn(
                name: "FileExtension",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "FileSizeBytes",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "MimeType",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "UploadedByName",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.RenameColumn(
                name: "GalleryPhotoId",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                newName: "DocumentId");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "appraisal",
                table: "LawAndRegulationImages",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
