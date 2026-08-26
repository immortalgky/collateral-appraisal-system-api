using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundFileLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundFileLogs",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterfaceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileDate = table.Column<DateOnly>(type: "date", nullable: true),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ContentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RowsReceived = table.Column<int>(type: "int", nullable: false),
                    RowsUpdated = table.Column<int>(type: "int", nullable: false),
                    RowsUnchanged = table.Column<int>(type: "int", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundFileLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundFileLogs_Interface_CompletedAt",
                schema: "integration",
                table: "InboundFileLogs",
                columns: new[] { "InterfaceCode", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InboundFileLogs_Interface_File_Size",
                schema: "integration",
                table: "InboundFileLogs",
                columns: new[] { "InterfaceCode", "FileName", "SizeBytes" });

            migrationBuilder.CreateIndex(
                name: "UX_InboundFileLogs_Interface_File_Hash",
                schema: "integration",
                table: "InboundFileLogs",
                columns: new[] { "InterfaceCode", "FileName", "ContentHash" },
                unique: true,
                filter: "[ContentHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InboundFileLogs",
                schema: "integration");
        }
    }
}
