using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameOutboxDeliveryLockToBackgroundServiceLease : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "appraisal",
                table: "OutboxDeliveryLock");

            migrationBuilder.RenameTable(
                name: "OutboxDeliveryLock",
                schema: "appraisal",
                newName: "BackgroundServiceLease",
                newSchema: "appraisal");

            migrationBuilder.AddPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "appraisal",
                table: "BackgroundServiceLease",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_BackgroundServiceLease",
                schema: "appraisal",
                table: "BackgroundServiceLease");

            migrationBuilder.RenameTable(
                name: "BackgroundServiceLease",
                schema: "appraisal",
                newName: "OutboxDeliveryLock",
                newSchema: "appraisal");

            migrationBuilder.AddPrimaryKey(
                name: "PK_OutboxDeliveryLock",
                schema: "appraisal",
                table: "OutboxDeliveryLock",
                column: "Id");
        }
    }
}
