using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailSendLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmailSendLogs",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CcAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BccAddresses = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FromAddress = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RecipientCount = table.Column<int>(type: "int", nullable: false),
                    AttachmentCount = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailSendLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_CreatedAt",
                schema: "notification",
                table: "EmailSendLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_ReferenceId",
                schema: "notification",
                table: "EmailSendLogs",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailSendLogs_Status",
                schema: "notification",
                table: "EmailSendLogs",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailSendLogs",
                schema: "notification");
        }
    }
}
