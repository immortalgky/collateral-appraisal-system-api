using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFeeStructureFeeName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fee name is no longer stored — it is resolved from the TypeOfFee parameter group by
            // code. Existing FeeStructure rows (incl. the legacy 02/03 tiers) are left untouched;
            // the Travel/Urgent rows are only removed from the C# seed, not deleted from the DB.
            migrationBuilder.DropColumn(
                name: "FeeName",
                schema: "appraisal",
                table: "FeeStructures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeeName",
                schema: "appraisal",
                table: "FeeStructures",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new System.Guid("00000000-0000-0000-0000-000000000001"),
                column: "FeeName",
                value: "Appraisal Fee");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new System.Guid("00000000-0000-0000-0000-000000000004"),
                column: "FeeName",
                value: "Appraisal Fee");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new System.Guid("00000000-0000-0000-0000-000000000005"),
                column: "FeeName",
                value: "Appraisal Fee");

            // 02/03 still exist in the DB (never deleted) — restore their names on rollback too.
            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new System.Guid("00000000-0000-0000-0000-000000000002"),
                column: "FeeName",
                value: "Travel Fee");

            migrationBuilder.UpdateData(
                schema: "appraisal",
                table: "FeeStructures",
                keyColumn: "Id",
                keyValue: new System.Guid("00000000-0000-0000-0000-000000000003"),
                column: "FeeName",
                value: "Urgent Fee");
        }
    }
}
