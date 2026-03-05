using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorGalleryPhotoIsInUseAndMarketComparableImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename IsUsedInReport → IsInUse (preserves existing boolean data)
            migrationBuilder.RenameColumn(
                name: "IsUsedInReport",
                schema: "appraisal",
                table: "AppraisalGallery",
                newName: "IsInUse");

            // Add DEFAULT constraint — required because EF Core omits the value
            // when it matches HasDefaultValue(false), expecting the DB to fill it in.
            migrationBuilder.AlterColumn<bool>(
                name: "IsInUse",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.DropColumn(
                name: "ReportSection",
                schema: "appraisal",
                table: "AppraisalGallery");

            // Rename DocumentId → GalleryPhotoId on MarketComparableImages
            migrationBuilder.RenameColumn(
                name: "DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages",
                newName: "GalleryPhotoId");
            
            migrationBuilder.RenameIndex(
                name: "IX_MarketComparableImages_DocumentId",
                schema: "appraisal",
                table: "MarketComparableImages",
                newName: "IX_MarketComparableImages_GalleryPhotoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsInUse",
                schema: "appraisal",
                table: "AppraisalGallery",
                newName: "IsUsedInReport");

            migrationBuilder.AddColumn<string>(
                name: "ReportSection",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.RenameColumn(
                name: "GalleryPhotoId",
                schema: "appraisal",
                table: "MarketComparableImages",
                newName: "DocumentId");

            migrationBuilder.RenameIndex(
                name: "IX_MarketComparableImages_GalleryPhotoId",
                schema: "appraisal",
                table: "MarketComparableImages",
                newName: "IX_MarketComparableImages_DocumentId");
        }
    }
}
