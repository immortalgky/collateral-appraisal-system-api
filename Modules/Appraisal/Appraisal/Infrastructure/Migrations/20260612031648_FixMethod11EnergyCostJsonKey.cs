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
            // Method 11 (Income) detail JSON historically used a misspelled key 'totalEnegyCost'.
            // The C# model (Method11Detail) now serializes/deserializes 'totalEnergyCost', so any
            // row persisted before the rename would deserialize that key to an empty array (data
            // orphaned). Data-only migration: rename the key in place for existing rows.
            //
            // The two tokens are not substrings of one another ('Enegy' lacks the 'r' of 'Energy'),
            // so REPLACE is unambiguous and the statement is idempotent (the WHERE re-guards).
            migrationBuilder.Sql(
                "UPDATE appraisal.IncomeAssumptions " +
                "SET Method_DetailJson = REPLACE(Method_DetailJson, 'totalEnegyCost', 'totalEnergyCost') " +
                "WHERE Method_DetailJson LIKE '%totalEnegyCost%';");
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
