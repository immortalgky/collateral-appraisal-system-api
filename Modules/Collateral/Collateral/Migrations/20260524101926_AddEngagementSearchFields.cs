using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Collateral.Migrations
{
    /// <inheritdoc />
    public partial class AddEngagementSearchFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppraisedCollateralType",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaInSqWa",
                schema: "collateral",
                table: "CollateralEngagements",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CollateralEngagementBuildings",
                schema: "collateral",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngagementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BuildingTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    BuildingArea = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    BuildingValue = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Sequence = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollateralEngagementBuildings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollateralEngagementBuildings_Engagement",
                        column: x => x.EngagementId,
                        principalSchema: "collateral",
                        principalTable: "CollateralEngagements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagementBuildings_Engagement_TypeCode",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                columns: new[] { "EngagementId", "BuildingTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_CollateralEngagementBuildings_EngagementId",
                schema: "collateral",
                table: "CollateralEngagementBuildings",
                column: "EngagementId");

            // Data migration: rename CollateralType values to match Appraisal's PropertyType.Code vocabulary.
            // L=bare land, LB=land+building, U=condo, LSL=bare leasehold, MAC=machinery.
            // Existing rows carry the old verbose strings; new rows use the short codes directly.
            migrationBuilder.Sql("""
                UPDATE collateral.CollateralMasters
                SET CollateralType = CASE CollateralType
                    WHEN 'Land'      THEN 'L'
                    WHEN 'Condo'     THEN 'U'
                    WHEN 'Leasehold' THEN 'LSL'
                    WHEN 'Machine'   THEN 'MAC'
                    ELSE CollateralType
                END
                WHERE CollateralType IN ('Land', 'Condo', 'Leasehold', 'Machine');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CollateralEngagementBuildings",
                schema: "collateral");

            migrationBuilder.DropColumn(
                name: "AppraisedCollateralType",
                schema: "collateral",
                table: "CollateralEngagements");

            migrationBuilder.DropColumn(
                name: "LandAreaInSqWa",
                schema: "collateral",
                table: "CollateralEngagements");

            // Reverse data migration: restore old verbose strings from short codes.
            migrationBuilder.Sql("""
                UPDATE collateral.CollateralMasters
                SET CollateralType = CASE CollateralType
                    WHEN 'L'   THEN 'Land'
                    WHEN 'LB'  THEN 'Land'
                    WHEN 'U'   THEN 'Condo'
                    WHEN 'LSL' THEN 'Leasehold'
                    WHEN 'LSB' THEN 'Leasehold'
                    WHEN 'LS'  THEN 'Leasehold'
                    WHEN 'MAC' THEN 'Machine'
                    ELSE CollateralType
                END
                WHERE CollateralType IN ('L', 'LB', 'U', 'LSL', 'LSB', 'LS', 'MAC');
                """);
        }
    }
}
