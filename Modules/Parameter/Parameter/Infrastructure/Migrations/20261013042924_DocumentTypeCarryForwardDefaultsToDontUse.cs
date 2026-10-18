using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DocumentTypeCarryForwardDefaultsToDontUse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "CarryForwardByDefault",
                schema: "parameter",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "CarryForwardByDefault",
                schema: "parameter",
                table: "DocumentTypes",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }
    }
}
