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
            // Data-only migration (no schema change) — bring existing rows onto the new vocabulary.

            // Defensive: rename any confirmed 'Approved' rows (no-op where the auto-approve
            // path never ran, e.g. before the approval feature was launched).
            migrationBuilder.Sql(
                "UPDATE appraisal.Appointments SET Status = 'Appointed' WHERE Status = 'Approved';");

            // The approval feature was not live when these rows were created, so a persisted
            // 'Pending' row is a confirmed appointment, not one awaiting a bank decision.
            // Promote it to 'Appointed', but guard on the approval markers so a genuine
            // pending-approval row (if any exists) is never silently auto-confirmed.
            migrationBuilder.Sql(
                "UPDATE appraisal.Appointments SET Status = 'Appointed' " +
                "WHERE Status = 'Pending' AND RequiresApproval = 0 AND ApprovalSubmittedAt IS NULL;");
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
