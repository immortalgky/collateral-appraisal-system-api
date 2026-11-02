using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMethod11EnergyCostJsonKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Cleanly reversible: restore the old key so the rolled-back code (which reads
            // 'totalEnegyCost') can deserialize these rows again.
            migrationBuilder.Sql(
                "UPDATE appraisal.IncomeAssumptions " +
                "SET Method_DetailJson = REPLACE(Method_DetailJson, 'totalEnergyCost', 'totalEnegyCost') " +
                "WHERE Method_DetailJson LIKE '%totalEnergyCost%';");
        }
    }
}
