using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDeliveryLockAndCleanupIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxDeliveryLock",
                schema: "appraisal",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstanceId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LeasedUntil = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcquiredAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveryLock", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationEventOutbox_Cleanup",
                schema: "appraisal",
                table: "IntegrationEventOutbox",
                columns: new[] { "Status", "ProcessedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxDeliveryLock",
                schema: "appraisal");

            migrationBuilder.DropIndex(
                name: "IX_IntegrationEventOutbox_Cleanup",
                schema: "appraisal",
                table: "IntegrationEventOutbox");
        }
    }
}
