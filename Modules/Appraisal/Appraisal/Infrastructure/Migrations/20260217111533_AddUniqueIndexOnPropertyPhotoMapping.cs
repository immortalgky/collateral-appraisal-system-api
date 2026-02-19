using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnPropertyPhotoMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotoMappings_GalleryPhotoId",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_GalleryPhotoId_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                columns: new[] { "GalleryPhotoId", "AppraisalPropertyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PropertyPhotoMappings_GalleryPhotoId_AppraisalPropertyId",
                schema: "appraisal",
                table: "PropertyPhotoMappings");

            migrationBuilder.CreateIndex(
                name: "IX_PropertyPhotoMappings_GalleryPhotoId",
                schema: "appraisal",
                table: "PropertyPhotoMappings",
                column: "GalleryPhotoId");
        }
    }
}
