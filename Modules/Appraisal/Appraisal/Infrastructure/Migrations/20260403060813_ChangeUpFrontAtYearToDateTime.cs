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
            // Add new column, drop old, rename. The int-year → Jan 1 conversion was removed;
            // only safe on an empty table.
            migrationBuilder.AddColumn<DateTime>(
                name: "AtYearNew",
                schema: "appraisal",
                table: "RentalUpFrontEntries",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1));

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

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
