using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFireInsuranceRatesToAppraisal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FireInsuranceRates",
                schema: "appraisal",
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
                    table.PrimaryKey("PK_FireInsuranceRates", x => x.Code);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FireInsuranceRates_Condition",
                schema: "appraisal",
                table: "FireInsuranceRates",
                column: "Condition",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FireInsuranceRates",
                schema: "appraisal");
        }
    }
}
