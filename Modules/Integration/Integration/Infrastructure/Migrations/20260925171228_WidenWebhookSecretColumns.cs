using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenWebhookSecretColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // A secret encrypted with a very large RSA key does not fit back into 2000 chars: clear it
            // (it must be re-entered) rather than let the narrowing fail on truncation.
            migrationBuilder.Sql(
                "UPDATE integration.WebhookSubscriptions SET SecretKey = NULL WHERE LEN(SecretKey) > 2000; " +
                "UPDATE integration.WebhookSubscriptions SET ClientSecret = NULL WHERE LEN(ClientSecret) > 2000;");

            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);
        }
    }
}
