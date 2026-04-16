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
            // Rename legacy AppraisalType string values to new canonical names.
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'New' WHERE AppraisalType = 'Initial'");
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'ReAppraisal' WHERE AppraisalType = 'Revaluation'");
            migrationBuilder.Sql("UPDATE [appraisal].[Appraisals] SET AppraisalType = 'PreAppraisal' WHERE AppraisalType = 'Special'");
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
