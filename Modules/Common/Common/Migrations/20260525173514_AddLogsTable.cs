using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLogsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "Logs",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeStamp = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    Level = table.Column<string>(type: "nvarchar(16)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Exception = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrelationId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    EntityId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    AppraisalId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    RequestId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    WorkflowInstanceId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    CollateralId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    DocumentId = table.Column<string>(type: "nvarchar(64)", nullable: true),
                    MachineName = table.Column<string>(type: "nvarchar(128)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_AppraisalId",
                schema: "dbo",
                table: "Logs",
                column: "AppraisalId",
                filter: "[AppraisalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_CollateralId",
                schema: "dbo",
                table: "Logs",
                column: "CollateralId",
                filter: "[CollateralId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_CorrelationId",
                schema: "dbo",
                table: "Logs",
                column: "CorrelationId",
                filter: "[CorrelationId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_DocumentId",
                schema: "dbo",
                table: "Logs",
                column: "DocumentId",
                filter: "[DocumentId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_EntityId",
                schema: "dbo",
                table: "Logs",
                column: "EntityId",
                filter: "[EntityId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_RequestId",
                schema: "dbo",
                table: "Logs",
                column: "RequestId",
                filter: "[RequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TimeStamp",
                schema: "dbo",
                table: "Logs",
                column: "TimeStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_WorkflowInstanceId",
                schema: "dbo",
                table: "Logs",
                column: "WorkflowInstanceId",
                filter: "[WorkflowInstanceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Logs",
                schema: "dbo");
        }
    }
}
