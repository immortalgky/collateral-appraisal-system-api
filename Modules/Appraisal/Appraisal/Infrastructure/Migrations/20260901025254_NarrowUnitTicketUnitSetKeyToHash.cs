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
            // Tickets issued while the key was the caller's spelling cannot be honoured under the
            // hash: the same rooms would fingerprint differently and mint a second ticket, which is
            // the duplicate this change exists to stop. They are dropped rather than migrated —
            // the value cannot be recomputed without the units, and the table has never shipped, so
            // the only rows this can reach are on a developer's machine.
            migrationBuilder.Sql(
                """
                DELETE FROM appraisal.UnitTickets
                WHERE LEN(RTRIM(UnitSetKey)) <> 64
                   OR RTRIM(UnitSetKey) LIKE '%[^0-9a-f]%';
                """);

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
