using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WebhookDeliveryDropRetryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Terminate any rows stuck in the defunct Retrying state before this refactor.
            migrationBuilder.Sql(
                "UPDATE integration.WebhookDeliveries SET Status = 'Failed' WHERE Status = 'Retrying'");

            migrationBuilder.DropIndex(
                name: "IX_WebhookDelivery_Status_NextRetryAt",
                schema: "integration",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "NextRetryAt",
                schema: "integration",
                table: "WebhookDeliveries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "NextRetryAt",
                schema: "integration",
                table: "WebhookDeliveries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDelivery_Status_NextRetryAt",
                schema: "integration",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextRetryAt" });
        }
    }
}
