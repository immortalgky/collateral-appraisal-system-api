using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Workflow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUpdateAppraisalStatusActivityConfigs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // One-way migration: restoring the removed rows would require knowing the original
            // Ids and SortOrders. On a fresh database the seeder will re-create them, but since
            // the seeder now omits these rows the Down() is intentionally a no-op.
        }
    }
}
