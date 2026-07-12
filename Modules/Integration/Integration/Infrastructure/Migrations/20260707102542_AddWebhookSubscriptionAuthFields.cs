using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSubscriptionAuthFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);

            migrationBuilder.AddColumn<string>(
                name: "AuthType",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "HMAC");

            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HttpMethod",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "POST");

            migrationBuilder.AddColumn<string>(
                name: "TokenEndpoint",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AuthType",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "ClientId",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "HttpMethod",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "TokenEndpoint",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);
        }
    }
}
