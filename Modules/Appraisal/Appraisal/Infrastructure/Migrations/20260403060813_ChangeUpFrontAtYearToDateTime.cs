using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeUpFrontAtYearToDateTime : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add new column, convert existing int year to Jan 1 of that year, drop old, rename
            migrationBuilder.AddColumn<DateTime>(
                name: "AtYearNew",
                schema: "appraisal",
                table: "RentalUpFrontEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1));

            migrationBuilder.Sql(
                "UPDATE [appraisal].[RentalUpFrontEntries] SET [AtYearNew] = DATEFROMPARTS([AtYear], 1, 1) WHERE [AtYear] > 0");

            migrationBuilder.DropColumn(
                name: "AtYear",
                schema: "appraisal",
                table: "RentalUpFrontEntries");

            migrationBuilder.RenameColumn(
                name: "AtYearNew",
                schema: "appraisal",
                table: "RentalUpFrontEntries",
                newName: "AtYear");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "AtYear",
                schema: "appraisal",
                table: "RentalUpFrontEntries",
                type: "int",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }
    }
}
