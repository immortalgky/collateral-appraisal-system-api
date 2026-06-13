using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WebhookSubscriptionSystemCodeUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions",
                column: "SystemCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions",
                column: "SystemCode");
        }
    }
}
