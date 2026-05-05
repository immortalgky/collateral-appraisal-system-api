using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectTowerIdToProjectModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectModels_ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels",
                column: "ProjectTowerId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectModels_ProjectTowers_ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels",
                column: "ProjectTowerId",
                principalSchema: "appraisal",
                principalTable: "ProjectTowers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjectModels_ProjectTowers_ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropIndex(
                name: "IX_ProjectModels_ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropColumn(
                name: "ProjectTowerId",
                schema: "appraisal",
                table: "ProjectModels");
        }
    }
}
