using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSubscriptionEventType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode_IsActive",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.AddColumn<string>(
                name: "EventType",
                schema: "integration",
                table: "WebhookSubscriptions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode_EventType",
                schema: "integration",
                table: "WebhookSubscriptions",
                columns: new[] { "SystemCode", "EventType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode_EventType_IsActive",
                schema: "integration",
                table: "WebhookSubscriptions",
                columns: new[] { "SystemCode", "EventType", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode_EventType",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_WebhookSubscription_SystemCode_EventType_IsActive",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.DropColumn(
                name: "EventType",
                schema: "integration",
                table: "WebhookSubscriptions");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode",
                schema: "integration",
                table: "WebhookSubscriptions",
                column: "SystemCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscription_SystemCode_IsActive",
                schema: "integration",
                table: "WebhookSubscriptions",
                columns: new[] { "SystemCode", "IsActive" });
        }
    }
}
