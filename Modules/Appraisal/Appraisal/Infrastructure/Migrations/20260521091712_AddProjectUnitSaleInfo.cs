using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectUnitSaleInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsSold",
                schema: "appraisal",
                table: "ProjectUnits",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LoanBankName",
                schema: "appraisal",
                table: "ProjectUnits",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PurchaseBy",
                schema: "appraisal",
                table: "ProjectUnits",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSold",
                schema: "appraisal",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "LoanBankName",
                schema: "appraisal",
                table: "ProjectUnits");

            migrationBuilder.DropColumn(
                name: "PurchaseBy",
                schema: "appraisal",
                table: "ProjectUnits");
        }
    }
}
