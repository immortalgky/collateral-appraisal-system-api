using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertDraftEvaluationStatusToPending : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Draft is no longer a valid status — partial saves now persist as Pending.
            migrationBuilder.Sql(@"
                UPDATE appraisal.AppraisalEvaluations
                SET EvaluationStatus = 'Pending'
                WHERE EvaluationStatus = 'Draft';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restoring Draft is not safe — there is no way to distinguish which
            // 'Pending' rows came from the old 'Draft' state. Leaving Down empty.
        }
    }
}
