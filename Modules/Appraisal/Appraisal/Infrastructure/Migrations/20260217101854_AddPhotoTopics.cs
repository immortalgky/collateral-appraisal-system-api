using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoTopics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PhotoTopics",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    AppraisalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TopicName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    DisplayColumns = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    CreatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    UpdatedWorkstation = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhotoTopics", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppraisalGallery_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery",
                column: "PhotoTopicId");

            migrationBuilder.CreateIndex(
                name: "IX_PhotoTopics_AppraisalId",
                schema: "appraisal",
                table: "PhotoTopics",
                column: "AppraisalId");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AppraisalGallery_PhotoTopics_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropTable(
                name: "PhotoTopics",
                schema: "appraisal");

            migrationBuilder.DropIndex(
                name: "IX_AppraisalGallery_PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");

            migrationBuilder.DropColumn(
                name: "PhotoTopicId",
                schema: "appraisal",
                table: "AppraisalGallery");
        }
    }
}
