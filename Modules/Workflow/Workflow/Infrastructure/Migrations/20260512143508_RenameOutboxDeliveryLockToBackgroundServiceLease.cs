using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOutboxDeliveryLockToBackgroundServiceLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "workflow",
                table: "OutboxDeliveryLock");

            migrationBuilder.RenameTable(
                name: "OutboxDeliveryLock",
                schema: "workflow",
                newName: "BackgroundServiceLease",
                newSchema: "workflow");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "workflow",
                table: "BackgroundServiceLease",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "workflow",
                table: "BackgroundServiceLease");

            migrationBuilder.RenameTable(
                name: "BackgroundServiceLease",
                schema: "workflow",
                newName: "OutboxDeliveryLock",
                newSchema: "workflow");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "workflow",
                table: "OutboxDeliveryLock",
                column: "Id");
        }
    }
}
