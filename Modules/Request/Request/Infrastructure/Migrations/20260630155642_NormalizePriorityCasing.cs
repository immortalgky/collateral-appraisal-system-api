using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Request.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizePriorityCasing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Normalize any non-canonical casing (e.g. lowercase values written by the
            // request-page priority toggle) to the canonical Priority value-object form.
            migrationBuilder.Sql(@"
                UPDATE request.Requests SET Priority = 'Normal' WHERE LOWER(Priority) = 'normal';
                UPDATE request.Requests SET Priority = 'High'   WHERE LOWER(Priority) = 'high';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: casing normalization is not meaningfully reversible.
        }
    }
}
