using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVillageUnitModelForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "VillageModelId",
                schema: "appraisal",
                table: "VillageUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_VillageModelId",
                schema: "appraisal",
                table: "VillageUnits",
                column: "VillageModelId");

            migrationBuilder.AddForeignKey(
                name: "FK_VillageUnits_VillageModels_VillageModelId",
                schema: "appraisal",
                table: "VillageUnits",
                column: "VillageModelId",
                principalSchema: "appraisal",
                principalTable: "VillageModels",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VillageUnits_VillageModels_VillageModelId",
                schema: "appraisal",
                table: "VillageUnits");

            migrationBuilder.DropIndex(
                name: "IX_VillageUnits_VillageModelId",
                schema: "appraisal",
                table: "VillageUnits");

            migrationBuilder.DropColumn(
                name: "VillageModelId",
                schema: "appraisal",
                table: "VillageUnits");
        }
    }
}
