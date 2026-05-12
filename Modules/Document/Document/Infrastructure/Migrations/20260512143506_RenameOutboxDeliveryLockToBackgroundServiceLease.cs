using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOutboxDeliveryLockToBackgroundServiceLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "document",
                table: "OutboxDeliveryLock");

            migrationBuilder.RenameTable(
                name: "OutboxDeliveryLock",
                schema: "document",
                newName: "BackgroundServiceLease",
                newSchema: "document");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "document",
                table: "BackgroundServiceLease",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "document",
                table: "BackgroundServiceLease");

            migrationBuilder.RenameTable(
                name: "BackgroundServiceLease",
                schema: "document",
                newName: "OutboxDeliveryLock",
                newSchema: "document");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "document",
                table: "OutboxDeliveryLock",
                column: "Id");
        }
    }
}
