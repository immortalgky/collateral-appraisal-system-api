using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EncryptWebhookSecrets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "WebhookSecretRevealLogs",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Field = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RevealedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RevealedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSecretRevealLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSecretRevealLogs_SubscriptionId_RevealedAt",
                schema: "integration",
                table: "WebhookSecretRevealLogs",
                columns: new[] { "SubscriptionId", "RevealedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back means code that cannot decrypt, and an ENC:v1: value does not fit in 256
            // chars: clear the encrypted secrets (the old code could not use them anyway) so the
            // narrowing succeeds. They must be re-entered after the rollback.
            migrationBuilder.Sql(
                // BIN2: case-sensitive like SecretProtector.IsProtected — a plaintext "enc:v1:..." stays.
                "UPDATE integration.WebhookSubscriptions SET SecretKey = NULL " +
                "WHERE SecretKey LIKE 'ENC:v1:%' COLLATE Latin1_General_BIN2; " +
                "UPDATE integration.WebhookSubscriptions SET ClientSecret = NULL " +
                "WHERE ClientSecret LIKE 'ENC:v1:%' COLLATE Latin1_General_BIN2;");

            migrationBuilder.DropTable(
                name: "WebhookSecretRevealLogs",
                schema: "integration");

            migrationBuilder.AlterColumn<string>(
                name: "SecretKey",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClientSecret",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);
        }
    }
}
