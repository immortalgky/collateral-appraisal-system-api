using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeAppointmentStatusToAppointed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Appointment status lifecycle simplified to: Appointed, Pending, Cancelled.
            // Was a data-only migration (no schema change): the row remap onto the new vocabulary has been
            // removed, so Up is now a no-op.

            // Data patch removed 2026-10-28: already applied on every environment (2026-10-27 PRD deploy) and a no-op on a fresh DB.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally irreversible: once normalized, an 'Appointed' row cannot be
            // distinguished as originally 'Approved' vs 'Pending', so any blind rollback
            // would wrongly demote genuinely-appointed rows. No-op by design.
        }
    }
}
