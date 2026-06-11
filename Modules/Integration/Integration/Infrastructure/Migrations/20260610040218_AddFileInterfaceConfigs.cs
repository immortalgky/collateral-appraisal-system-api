using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Integration.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFileInterfaceConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FileInterfaceConfigs",
                schema: "integration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InterfaceCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FileNamePrefix = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FileNameDateFormat = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    FileExtension = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Directory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProcessedDirectory = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FilePattern = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FileInterfaceConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FileInterfaceConfigs_InterfaceCode",
                schema: "integration",
                table: "FileInterfaceConfigs",
                column: "InterfaceCode",
                unique: true);

            // Seed dev-default rows for the three file interfaces.
            migrationBuilder.InsertData(
                schema: "integration",
                table: "FileInterfaceConfigs",
                columns: ["Id", "InterfaceCode", "Direction", "FileNamePrefix", "FileNameDateFormat", "FileExtension", "Directory", "ProcessedDirectory", "FilePattern", "IsActive"],
                values: new object[,]
                {
                    // REGULATORY: outbound monthly Basel/RDT snapshot
                    { Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890"), "REGULATORY", "Out", "REGULATORY_", "yyyyMMdd", "txt", "./outbound", null!, null!, true },
                    // COLLATERAL_RESULT: outbound completed-appraisal prices
                    { Guid.Parse("b2c3d4e5-f6a7-8901-bcde-f12345678901"), "COLLATERAL_RESULT", "Out", "COLLATERAL_RESULT_", "yyyyMMddHHmmss", "txt", "./outbound", null!, null!, true },
                    // REAPPRAISAL: inbound AS400 COLLATREV file
                    { Guid.Parse("c3d4e5f6-a7b8-9012-cdef-012345678902"), "REAPPRAISAL", "In", null!, null!, null!, "./reappraisal/inbox", "./reappraisal/processed", "AS400_COLLATREV_*.txt", true },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                schema: "integration",
                table: "FileInterfaceConfigs",
                keyColumn: "InterfaceCode",
                keyValues: new object[] { "REGULATORY", "COLLATERAL_RESULT", "REAPPRAISAL" });

            migrationBuilder.DropTable(
                name: "FileInterfaceConfigs",
                schema: "integration");
        }
    }
}
