using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class DropLastConstructionInspectionIdFromLandDetail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastConstructionInspectionId",
                schema: "collateral",
                table: "LandDetails");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LastConstructionInspectionId",
                schema: "collateral",
                table: "LandDetails",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
