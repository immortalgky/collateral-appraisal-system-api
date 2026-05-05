using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveProjectImageThumbnailUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectTowerImages_ProjectTowerId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectTowerImages");

            migrationBuilder.DropIndex(
                name: "IX_ProjectModelImages_ProjectModelId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectModelImages");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectTowerImages_ProjectTowerId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectTowerImages",
                column: "ProjectTowerId",
                unique: true,
                filter: "[IsThumbnail] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelImages_ProjectModelId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectModelImages",
                column: "ProjectModelId",
                unique: true,
                filter: "[IsThumbnail] = 1");
        }
    }
}
