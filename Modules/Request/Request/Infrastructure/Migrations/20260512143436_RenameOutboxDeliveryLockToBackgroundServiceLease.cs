using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOutboxDeliveryLockToBackgroundServiceLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "request",
                table: "OutboxDeliveryLock");

            migrationBuilder.RenameTable(
                name: "OutboxDeliveryLock",
                schema: "request",
                newName: "BackgroundServiceLease",
                newSchema: "request");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "request",
                table: "BackgroundServiceLease",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "request",
                table: "BackgroundServiceLease");

            migrationBuilder.RenameTable(
                name: "BackgroundServiceLease",
                schema: "request",
                newName: "OutboxDeliveryLock",
                newSchema: "request");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "request",
                table: "OutboxDeliveryLock",
                column: "Id");
        }
    }
}
