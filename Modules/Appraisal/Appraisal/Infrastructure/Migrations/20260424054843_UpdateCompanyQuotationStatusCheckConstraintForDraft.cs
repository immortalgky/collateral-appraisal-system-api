using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateCompanyQuotationStatusCheckConstraintForDraft : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add 'Draft' and 'PendingCheckerReview' to the allowed set so the
            // Maker/Checker flow can persist a CompanyQuotation before final submit.
            migrationBuilder.Sql("ALTER TABLE [appraisal].[CompanyQuotations] DROP CONSTRAINT IF EXISTS [CK_CompanyQuotations_Status];");
            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[CompanyQuotations]
                ADD CONSTRAINT [CK_CompanyQuotations_Status]
                CHECK ([Status] IN (
                    'Draft', 'PendingCheckerReview',
                    'Submitted', 'UnderReview', 'Tentative',
                    'Negotiating', 'Accepted', 'Rejected', 'Withdrawn', 'Declined'
                ));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the pre-Maker/Checker constraint.
            migrationBuilder.Sql("ALTER TABLE [appraisal].[CompanyQuotations] DROP CONSTRAINT IF EXISTS [CK_CompanyQuotations_Status];");
            migrationBuilder.Sql("""
                ALTER TABLE [appraisal].[CompanyQuotations]
                ADD CONSTRAINT [CK_CompanyQuotations_Status]
                CHECK ([Status] IN (
                    'Submitted', 'UnderReview', 'Tentative',
                    'Negotiating', 'Accepted', 'Rejected', 'Withdrawn', 'Declined'
                ));
                """);
        }
    }
}
