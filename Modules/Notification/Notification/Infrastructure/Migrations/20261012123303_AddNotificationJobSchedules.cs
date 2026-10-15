using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Notification.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationJobSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_CreatedAt",
                schema: "notification",
                table: "UserNotifications");

            migrationBuilder.CreateTable(
                name: "JobSchedules",
                schema: "notification",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false, defaultValueSql: "NEWSEQUENTIALID()"),
                    JobId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CronExpression = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobSchedules", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_CreatedAt",
                schema: "notification",
                table: "UserNotifications",
                column: "CreatedAt")
                .Annotation("SqlServer:Include", new[] { "IsRead" });

            migrationBuilder.CreateIndex(
                name: "UX_JobSchedules_JobId",
                schema: "notification",
                table: "JobSchedules",
                column: "JobId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobSchedules",
                schema: "notification");

            migrationBuilder.DropIndex(
                name: "IX_UserNotifications_CreatedAt",
                schema: "notification",
                table: "UserNotifications");

            migrationBuilder.CreateIndex(
                name: "IX_UserNotifications_CreatedAt",
                schema: "notification",
                table: "UserNotifications",
                column: "CreatedAt");
        }
    }
}
