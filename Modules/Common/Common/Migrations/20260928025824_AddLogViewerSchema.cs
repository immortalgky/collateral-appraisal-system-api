using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Common.Migrations
{
    /// <inheritdoc />
    public partial class AddLogViewerSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Logs_TimeStamp",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.AddColumn<string>(
                name: "MessageTemplate",
                schema: "dbo",
                table: "Logs",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestPath",
                schema: "dbo",
                table: "Logs",
                type: "nvarchar(400)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceContext",
                schema: "dbo",
                table: "Logs",
                type: "nvarchar(256)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserName",
                schema: "dbo",
                table: "Logs",
                type: "nvarchar(128)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SystemMetricSamples",
                schema: "common",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TimeStamp = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(128)", nullable: false),
                    ProcessStartedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    CpuPercent = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    MachineMemoryPercent = table.Column<decimal>(type: "decimal(5,1)", nullable: false),
                    WorkingSetMb = table.Column<int>(type: "int", nullable: false),
                    GcHeapMb = table.Column<int>(type: "int", nullable: false),
                    Gen2Collections = table.Column<int>(type: "int", nullable: false),
                    ThreadCount = table.Column<int>(type: "int", nullable: false),
                    ThreadPoolQueue = table.Column<int>(type: "int", nullable: false),
                    RequestsPerMin = table.Column<int>(type: "int", nullable: false),
                    Http5xxPerMin = table.Column<int>(type: "int", nullable: false),
                    P95Ms = table.Column<int>(type: "int", nullable: true),
                    ExceptionsPerMin = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemMetricSamples", x => x.Id);
                });

            // Won't fix: these index operations aren't marked .Annotation("SqlServer:CreatedOnline", true)
            // — IsCreatedOnline() hard-fails on editions without Enterprise/Online indexing (e.g. Standard),
            // so forcing it here would break the migration for some deployments. Handled instead by the
            // DBA guidance in deploy/README.md (run in a low-traffic window; use ONLINE=ON manually if the
            // target edition supports it).
            migrationBuilder.CreateIndex(
                name: "IX_Logs_Level_TimeStamp",
                schema: "dbo",
                table: "Logs",
                columns: new[] { "Level", "TimeStamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TimeStamp",
                schema: "dbo",
                table: "Logs",
                column: "TimeStamp")
                .Annotation("SqlServer:Include", new[] { "Level" });

            migrationBuilder.CreateIndex(
                name: "IX_Logs_UserName_TimeStamp",
                schema: "dbo",
                table: "Logs",
                columns: new[] { "UserName", "TimeStamp" },
                filter: "[UserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SystemMetricSamples_TimeStamp",
                schema: "common",
                table: "SystemMetricSamples",
                column: "TimeStamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemMetricSamples",
                schema: "common");

            migrationBuilder.DropIndex(
                name: "IX_Logs_Level_TimeStamp",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Logs_TimeStamp",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropIndex(
                name: "IX_Logs_UserName_TimeStamp",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "MessageTemplate",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "RequestPath",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "SourceContext",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.DropColumn(
                name: "UserName",
                schema: "dbo",
                table: "Logs");

            migrationBuilder.CreateIndex(
                name: "IX_Logs_TimeStamp",
                schema: "dbo",
                table: "Logs",
                column: "TimeStamp");
        }
    }
}
