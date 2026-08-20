using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddHostCollateralLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HostCollateralLinks",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AppraisalNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    HostCollateralId = table.Column<string>(type: "nvarchar(19)", maxLength: 19, nullable: true),
                    IsRedeemed = table.Column<bool>(type: "bit", nullable: false),
                    RecordDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostCollateralLinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HostCollateralLinks_IsRedeemed",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "IsRedeemed");

            migrationBuilder.CreateIndex(
                name: "UX_HostCollateralLinks_AppraisalNumber",
                schema: "collateral",
                table: "HostCollateralLinks",
                column: "AppraisalNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HostCollateralLinks",
                schema: "collateral");
        }
    }
}
