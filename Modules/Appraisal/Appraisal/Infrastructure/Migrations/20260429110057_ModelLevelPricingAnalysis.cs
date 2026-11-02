using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ModelLevelPricingAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis");

            migrationBuilder.DropColumn(
                name: "StandardPrice",
                schema: "appraisal",
                table: "ProjectModels");

            migrationBuilder.DropColumn(
                name: "StandardPrice",
                schema: "appraisal",
                table: "ProjectModelAssumptions");

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.AlterColumn<Guid>(
                name: "PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SubjectType",
                schema: "appraisal",
                table: "PricingAnalysis",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "ProjectModelId",
                unique: true,
                filter: "[ProjectModelId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysis_PropertyGroupId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "PropertyGroupId",
                unique: true,
                filter: "[PropertyGroupId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PricingAnalysis_SubjectXor",
                schema: "appraisal",
                table: "PricingAnalysis",
                sql: "([PropertyGroupId] IS NOT NULL AND [ProjectModelId] IS NULL) OR ([PropertyGroupId] IS NULL AND [ProjectModelId] IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_PricingAnalysis_ProjectModels_ProjectModelId",
                schema: "appraisal",
                table: "PricingAnalysis",
                column: "ProjectModelId",
                principalSchema: "appraisal",
                principalTable: "ProjectModels",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "ModelLevelPricingAnalysis migration is not reversible — " +
                "model-level analyses cannot be safely re-mapped to PropertyGroups.");
        }
    }
}
