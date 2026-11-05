using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAppraisalTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the rename back to legacy values.
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'Initial' WHERE AppraisalType = 'New'");
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'Revaluation' WHERE AppraisalType = 'ReAppraisal'");
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'Special' WHERE AppraisalType = 'PreAppraisal'");
        }
    }
}
