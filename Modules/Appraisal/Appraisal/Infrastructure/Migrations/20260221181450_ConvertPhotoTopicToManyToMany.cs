using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertPhotoTopicToManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create the join table first
            migrationBuilder.CreateTable(
                name: "GalleryPhotoTopicMappings",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    GalleryPhotoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PhotoTopicId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalleryPhotoTopicMappings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GalleryPhotoTopicMappings_AppraisalGallery_GalleryPhotoId",
                        column: x => x.GalleryPhotoId,
                        principalSchema: "appraisal",
                        principalTable: "AppraisalGallery",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GalleryPhotoTopicMappings_PhotoTopics_PhotoTopicId",
                        column: x => x.PhotoTopicId,
                        principalSchema: "appraisal",
                        principalTable: "PhotoTopics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalleryPhotoTopicMappings_GalleryPhotoId_PhotoTopicId",
                schema: "appraisal",
                table: "GalleryPhotoTopicMappings",
                columns: new[] { "GalleryPhotoId", "PhotoTopicId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GalleryPhotoTopicMappings_PhotoTopicId",
                schema: "appraisal",
                table: "GalleryPhotoTopicMappings",
                column: "PhotoTopicId");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // 2. Drop the old FK, index, and column. The copy into the join table was removed;
            // only safe on an empty table.
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalGallery_PhotoTopics_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalGallery_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalleryPhotoTopicMappings",
                schema: "appraisal");

            migrationBuilder.AddColumn<Guid>(
                name: "PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGallery_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery",
                column: "PhotoTopicId");

            migrationBuilder.AddForeignKey(
                name: "FK_AppraisalGallery_PhotoTopics_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery",
                column: "PhotoTopicId",
                principalSchema: "appraisal",
                principalTable: "PhotoTopics",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
