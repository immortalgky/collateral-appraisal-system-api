using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Parameter.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFireInsuranceRateTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PricingParameterFireInsuranceRates",
                schema: "parameter",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PropertyKind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RatePerSqm = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DisplaySeq = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingParameterFireInsuranceRates", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PricingParameterFireInsuranceRates_Condition",
                schema: "parameter",
                table: "PricingParameterFireInsuranceRates",
                column: "Condition",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PricingParameterFireInsuranceRates",
                schema: "parameter");
        }
    }
}
