using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedMessages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BrokerSnapshots",
                schema: "integration",
                columns: table => new
                {
                    Node = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ManagementStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    QueuesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BrokerSnapshots", x => x.Node);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessageAuditLogs",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OutboxModule = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ActorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    At = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessageAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FailedMessages",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Node = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceQueue = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MessageId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConversationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ConsumerType = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExceptionType = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ExceptionMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    StackTrace = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    FaultedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RefType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RefId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RefNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Body = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Headers = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ActionBy = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ActionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RetryClaimedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FailedMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessageAuditLogs_Target_At",
                schema: "integration",
                table: "FailedMessageAuditLogs",
                columns: new[] { "TargetId", "At" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Node_Status",
                schema: "integration",
                table: "FailedMessages",
                columns: new[] { "Node", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FailedMessages_Status_FaultedAt",
                schema: "integration",
                table: "FailedMessages",
                columns: new[] { "Status", "FaultedAt" });

            migrationBuilder.CreateIndex(
                name: "UX_FailedMessages_Dedup",
                schema: "integration",
                table: "FailedMessages",
                columns: new[] { "MessageId", "SourceQueue", "Kind", "FaultedAt" },
                unique: true,
                filter: "[MessageId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BrokerSnapshots",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "FailedMessageAuditLogs",
                schema: "integration");

            migrationBuilder.DropTable(
                name: "FailedMessages",
                schema: "integration");
        }
    }
}
