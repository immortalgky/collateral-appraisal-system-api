using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UnitPurchaseByToText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Convert ProjectUnits.PurchaseBy from int (1=Cash, 2=Loan) to nvarchar(10)
            // holding the enum name. The int → name backfill was removed;
            // only safe on an empty table.

            // 1. Add new string column alongside the int column.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.ProjectUnits ADD PurchaseBy_New nvarchar(10) NULL;
                """);

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.

            // 2. Drop the int column.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.ProjectUnits DROP COLUMN PurchaseBy;
                """);

            // 3. Rename the new column back to PurchaseBy.
            migrationBuilder.Sql("""
                EXEC sp_rename 'appraisal.ProjectUnits.PurchaseBy_New', 'PurchaseBy', 'COLUMN';
                """);

            // 4. Column stays NULL (nullable in the domain); no ALTER ... NOT NULL.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse: text codes back to int. Both 'Cash' and 'Loan' have int equivalents,
            // so no fail-fast guard is needed (no Land-like new value here).

            // 1. Add int column alongside text column.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.ProjectUnits ADD PurchaseBy_Old int NULL;
                """);

            // 2. Backfill: convert enum-name strings back to int values.
            migrationBuilder.Sql("""
                UPDATE appraisal.ProjectUnits
                SET PurchaseBy_Old = CASE PurchaseBy
                    WHEN N'Cash' THEN 1
                    WHEN N'Loan' THEN 2
                    ELSE NULL
                END;
                """);

            // 3. Drop the text column.
            migrationBuilder.Sql("""
                ALTER TABLE appraisal.ProjectUnits DROP COLUMN PurchaseBy;
                """);

            // 4. Rename the int column back to PurchaseBy.
            migrationBuilder.Sql("""
                EXEC sp_rename 'appraisal.ProjectUnits.PurchaseBy_Old', 'PurchaseBy', 'COLUMN';
                """);
        }
    }
}
