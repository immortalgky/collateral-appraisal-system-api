using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NarrowUnitTicketUnitSetKeyToHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            migrationBuilder.AlterColumn<string>(
                name: "UnitSetKey",
                schema: "appraisal",
                table: "UnitTickets",
                type: "nchar(64)",
                fixedLength: true,
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(400)",
                oldMaxLength: 400);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "UnitSetKey",
                schema: "appraisal",
                table: "UnitTickets",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nchar(64)",
                oldFixedLength: true,
                oldMaxLength: 64);
        }
    }
}
