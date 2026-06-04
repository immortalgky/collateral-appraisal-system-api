using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockReappraisalDue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlockReappraisalDue",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollateralMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ProjectType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OldAppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ProjectSellingPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalUnits = table.Column<int>(type: "int", nullable: false),
                    RemainingUnits = table.Column<int>(type: "int", nullable: false),
                    LastAppraisedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlockReappraisalDue", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlockReappraisalDue_Status",
                schema: "collateral",
                table: "BlockReappraisalDue",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "UX_BlockReappraisalDue_CollateralMasterId",
                schema: "collateral",
                table: "BlockReappraisalDue",
                column: "CollateralMasterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockReappraisalDue",
                schema: "collateral");
        }
    }
}
