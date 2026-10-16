using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequireTwoFactorForExternal",
                schema: "auth",
                table: "PasswordPolicy",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "TwoFactorRememberHours",
                schema: "auth",
                table: "PasswordPolicy",
                type: "int",
                nullable: false,
                defaultValue: 12);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequireTwoFactorForExternal",
                schema: "auth",
                table: "PasswordPolicy");

            migrationBuilder.DropColumn(
                name: "TwoFactorRememberHours",
                schema: "auth",
                table: "PasswordPolicy");
        }
    }
}
