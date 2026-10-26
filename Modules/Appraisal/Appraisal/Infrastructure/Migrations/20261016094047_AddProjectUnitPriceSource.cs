using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitPriceSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UnitPriceImportFileName",
                schema: "appraisal",
                table: "Projects",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnitPriceImportedAt",
                schema: "appraisal",
                table: "Projects",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPriceImportedBy",
                schema: "appraisal",
                table: "Projects",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnitPriceSource",
                schema: "appraisal",
                table: "Projects",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Calculated");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitPriceImportFileName",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UnitPriceImportedAt",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UnitPriceImportedBy",
                schema: "appraisal",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "UnitPriceSource",
                schema: "appraisal",
                table: "Projects");
        }
    }
}
