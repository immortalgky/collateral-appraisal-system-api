using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCondoUnitModelAndTowerForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CondoModelId",
                schema: "appraisal",
                table: "CondoUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_CondoModelId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoModelId");

            migrationBuilder.CreateIndex(
                name: "IX_CondoUnits_CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoTowerId");

            migrationBuilder.AddForeignKey(
                name: "FK_CondoUnits_CondoModels_CondoModelId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoModelId",
                principalSchema: "appraisal",
                principalTable: "CondoModels",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CondoUnits_CondoTowers_CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits",
                column: "CondoTowerId",
                principalSchema: "appraisal",
                principalTable: "CondoTowers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CondoUnits_CondoModels_CondoModelId",
                schema: "appraisal",
                table: "CondoUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_CondoUnits_CondoTowers_CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits");

            migrationBuilder.DropIndex(
                name: "IX_CondoUnits_CondoModelId",
                schema: "appraisal",
                table: "CondoUnits");

            migrationBuilder.DropIndex(
                name: "IX_CondoUnits_CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits");

            migrationBuilder.DropColumn(
                name: "CondoModelId",
                schema: "appraisal",
                table: "CondoUnits");

            migrationBuilder.DropColumn(
                name: "CondoTowerId",
                schema: "appraisal",
                table: "CondoUnits");
        }
    }
}
