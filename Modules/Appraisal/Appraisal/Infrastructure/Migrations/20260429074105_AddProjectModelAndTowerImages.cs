using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectModelAndTowerImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageDocumentIds",
                schema: "appraisal",
                table: "ProjectTowers");

            migrationBuilder.DropColumn(
                name: "ImageDocumentIds",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.CreateTable(
                name: "ProjectModelImages",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProjectModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GalleryPhotoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsThumbnail = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectModelImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectModelImages_ProjectModels_ProjectModelId",
                        column: x => x.ProjectModelId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectModels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjectTowerImages",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    ProjectTowerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GalleryPhotoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DisplaySequence = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsThumbnail = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTowerImages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTowerImages_ProjectTowers_ProjectTowerId",
                        column: x => x.ProjectTowerId,
                        principalSchema: "appraisal",
                        principalTable: "ProjectTowers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelImages_GalleryPhotoId",
                schema: "appraisal",
                table: "ProjectModelImages",
                column: "GalleryPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelImages_ProjectModelId_DisplaySequence",
                schema: "appraisal",
                table: "ProjectModelImages",
                columns: new[] { "ProjectModelId", "DisplaySequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModelImages_ProjectModelId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectModelImages",
                column: "ProjectModelId",
                unique: true,
                filter: "[IsThumbnail] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTowerImages_GalleryPhotoId",
                schema: "appraisal",
                table: "ProjectTowerImages",
                column: "GalleryPhotoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTowerImages_ProjectTowerId_DisplaySequence",
                schema: "appraisal",
                table: "ProjectTowerImages",
                columns: new[] { "ProjectTowerId", "DisplaySequence" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTowerImages_ProjectTowerId_SingleThumbnail",
                schema: "appraisal",
                table: "ProjectTowerImages",
                column: "ProjectTowerId",
                unique: true,
                filter: "[IsThumbnail] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectModelImages",
                schema: "appraisal");

            migrationBuilder.DropTable(
                name: "ProjectTowerImages",
                schema: "appraisal");

            migrationBuilder.AddColumn<string>(
                name: "ImageDocumentIds",
                schema: "appraisal",
                table: "ProjectTowers",
                type: "nvarchar(2000)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageDocumentIds",
                schema: "appraisal",
                table: "ProjectModels",
                type: "nvarchar(2000)",
                nullable: true);
        }
    }
}
