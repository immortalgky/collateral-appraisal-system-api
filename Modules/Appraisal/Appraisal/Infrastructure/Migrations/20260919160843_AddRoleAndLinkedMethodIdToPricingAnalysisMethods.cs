using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleAndLinkedMethodIdToPricingAnalysisMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingAnalysisMethods_LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                column: "LinkedMethodId");

            migrationBuilder.AddForeignKey(
                name: "FK_PricingAnalysisMethods_PricingAnalysisMethods_LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods",
                column: "LinkedMethodId",
                principalSchema: "appraisal",
                principalTable: "PricingAnalysisMethods",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PricingAnalysisMethods_PricingAnalysisMethods_LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropIndex(
                name: "IX_PricingAnalysisMethods_LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "LinkedMethodId",
                schema: "appraisal",
                table: "PricingAnalysisMethods");

            migrationBuilder.DropColumn(
                name: "Role",
                schema: "appraisal",
                table: "PricingAnalysisMethods");
        }
    }
}
