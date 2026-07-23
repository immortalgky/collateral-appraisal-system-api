using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FieldLengthCustomerName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "request",
                table: "RequestCustomers",
                type: "nvarchar(260)",
                maxLength: 260,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "request",
                table: "RequestCustomers",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(260)",
                oldMaxLength: 260,
                oldNullable: true);
        }
    }
}
