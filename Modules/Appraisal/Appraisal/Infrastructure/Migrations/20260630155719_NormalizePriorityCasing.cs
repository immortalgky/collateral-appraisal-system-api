using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePriorityCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize any non-canonical casing to the canonical Priority value-object form.
            migrationBuilder.Sql(@"
                UPDATE appraisal.Appraisals SET Priority = 'Normal' WHERE LOWER(Priority) = 'normal';
                UPDATE appraisal.Appraisals SET Priority = 'High'   WHERE LOWER(Priority) = 'high';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: casing normalization is not meaningfully reversible.
        }
    }
}
