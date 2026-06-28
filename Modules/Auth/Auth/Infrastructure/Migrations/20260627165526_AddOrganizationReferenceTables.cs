using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Auth.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationReferenceTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CostCenters",
                schema: "auth",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostCenters", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                schema: "auth",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    DivisionCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Officers",
                schema: "auth",
                columns: table => new
                {
                    OfficerCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    BranchNumber = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    OfficerId = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    ShortName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    CostCenterCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: true),
                    DepartmentCode = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Officers", x => x.OfficerCode);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Officers_CostCenterCode",
                schema: "auth",
                table: "Officers",
                column: "CostCenterCode");

            migrationBuilder.CreateIndex(
                name: "IX_Officers_DepartmentCode",
                schema: "auth",
                table: "Officers",
                column: "DepartmentCode");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CostCenters",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Departments",
                schema: "auth");

            migrationBuilder.DropTable(
                name: "Officers",
                schema: "auth");
        }
    }
}
