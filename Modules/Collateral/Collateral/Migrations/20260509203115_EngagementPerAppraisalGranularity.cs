using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class EngagementPerAppraisalGranularity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CollateralEngagements_AppraisalProperty",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "AppraisedValue",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "PropertyId",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.CreateIndex(
                name: "UX_CollateralEngagements_Appraisal",
                schema: "collateral",
                table: "CollateralEngagements",
                column: "AppraisalId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_CollateralEngagements_Appraisal",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.AddColumn<decimal>(
                name: "AppraisedValue",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropertyId",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "UX_CollateralEngagements_AppraisalProperty",
                schema: "collateral",
                table: "CollateralEngagements",
                columns: new[] { "AppraisalId", "PropertyId" },
                unique: true);
        }
    }
}
