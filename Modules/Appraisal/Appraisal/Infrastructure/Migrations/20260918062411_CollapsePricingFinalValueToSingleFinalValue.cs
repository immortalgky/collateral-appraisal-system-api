using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollapsePricingFinalValueToSingleFinalValue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Hand-written, deliberately. EF scaffolded a bare DropColumn("FinalValueRounded"):
            // the model lost one of two decimals and it picked the wrong one to delete. That would
            // have thrown away every rounded figure — the number the whole application prices
            // against — and kept the pre-rounding one under the surviving name.
            //
            // What we want is the opposite: drop the raw column, then let the rounded one take over
            // its name. Order matters — the name has to be free before the rename, and a rename
            // (not an insert-select) is what keeps every stored figure intact.
            migrationBuilder.DropColumn(
                name: "FinalValue",
                schema: "appraisal",
                table: "PricingFinalValues");

            migrationBuilder.RenameColumn(
                name: "FinalValueRounded",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "FinalValue");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FinalValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                newName: "FinalValueRounded");

            // The pre-rounding figure is not recoverable — nothing kept a copy of it. Rolling back
            // restores the column's shape, not its old contents.
            migrationBuilder.AddColumn<decimal>(
                name: "FinalValue",
                schema: "appraisal",
                table: "PricingFinalValues",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }
    }
}
