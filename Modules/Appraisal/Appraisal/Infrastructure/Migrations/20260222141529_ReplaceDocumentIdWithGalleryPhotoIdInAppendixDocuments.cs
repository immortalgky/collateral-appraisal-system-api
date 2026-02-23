using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceDocumentIdWithGalleryPhotoIdInAppendixDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FileName",
                schema: "appraisal",
                table: "AppendixDocuments");

            migrationBuilder.DropColumn(
                name: "FilePath",
                schema: "appraisal",
                table: "AppendixDocuments");

            migrationBuilder.RenameColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "AppendixDocuments",
                newName: "GalleryPhotoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "GalleryPhotoId",
                schema: "appraisal",
                table: "AppendixDocuments",
                newName: "DocumentId");

            migrationBuilder.AddColumn<string>(
                name: "FileName",
                schema: "appraisal",
                table: "AppendixDocuments",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                schema: "appraisal",
                table: "AppendixDocuments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");
        }
    }
}
