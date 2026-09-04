using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelTowerUniqueConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ProjectModels_ProjectTowerId_ModelName",
                schema: "appraisal",
                table: "ProjectModels",
                columns: new[] { "ProjectTowerId", "ModelName" },
                unique: true,
                filter: "[ProjectTowerId] IS NOT NULL AND [ModelName] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjectModels_ProjectTowerId_ModelName",
                schema: "appraisal",
                table: "ProjectModels");
        }
    }
}
