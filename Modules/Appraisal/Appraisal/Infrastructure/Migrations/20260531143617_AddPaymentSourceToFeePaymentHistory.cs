using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Appraisal.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentSourceToFeePaymentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Source",
                schema: "appraisal",
                table: "AppraisalFeePaymentHistory",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Customer");

            // ----------------------------------------------------------------
            // Backfill step 1: insert a BankAbsorb settlement row for every
            // AppraisalFee that:
            //   - has BankAbsorbAmount > 0
            //   - belongs to an assignment on a Paid invoice
            //   - does not already have a Source='BankAbsorb' row (idempotent)
            // ----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                INSERT INTO appraisal.AppraisalFeePaymentHistory
                    (AppraisalFeeId, PaymentAmount, PaymentDate, PaymentReference, Source, CreatedAt, CreatedBy)
                SELECT af.Id,
                       af.BankAbsorbAmount,
                       COALESCE(CAST(inv.PaidDate AS datetime2), GETDATE()),
                       inv.PaymentOrderNo,
                       'BankAbsorb',
                       GETDATE(),
                       'system'
                FROM appraisal.AppraisalFees af
                INNER JOIN appraisal.InvoiceItems ii  ON ii.AppraisalFeeId = af.Id
                INNER JOIN appraisal.Invoices     inv ON inv.Id = ii.InvoiceId
                WHERE inv.Status = 'Paid'
                  AND af.BankAbsorbAmount > 0
                  AND NOT EXISTS (
                      SELECT 1
                      FROM appraisal.AppraisalFeePaymentHistory h
                      WHERE h.AppraisalFeeId = af.Id
                        AND h.Source = 'BankAbsorb'
                  );
                """);

            // ----------------------------------------------------------------
            // Backfill step 2: recompute PaymentStatus for all affected fees
            // using the new total-fee-basis logic.
            //
            //   customerPaid = SUM of Source<>'BankAbsorb' rows
            //   totalPaid    = SUM of all rows
            //   bankSettled  = EXISTS(Source='BankAbsorb' row)
            //
            // Status rules (mirror UpdatePaymentStatus() in C#):
            //   TotalFeeAfterVAT <= 0              → NotPaid
            //   totalPaid >= TotalFeeAfterVAT      → Paid
            //   customerPaid >= CustomerPayableAmount
            //     AND BankAbsorbAmount > 0
            //     AND NOT bankSettled               → PendingInvoice
            //   totalPaid > 0                      → Partial
            //   else                               → NotPaid
            // ----------------------------------------------------------------
            migrationBuilder.Sql(
                """
                WITH fee_totals AS (
                    SELECT h.AppraisalFeeId,
                           SUM(h.PaymentAmount)
                               AS TotalPaid,
                           SUM(CASE WHEN h.Source <> 'BankAbsorb' THEN h.PaymentAmount ELSE 0 END)
                               AS CustomerPaid,
                           MAX(CASE WHEN h.Source = 'BankAbsorb' THEN 1 ELSE 0 END)
                               AS BankSettled
                    FROM appraisal.AppraisalFeePaymentHistory h
                    GROUP BY h.AppraisalFeeId
                )
                UPDATE af
                SET af.TotalPaidAmount   = COALESCE(ft.TotalPaid, 0),
                    af.OutstandingAmount = af.TotalFeeAfterVAT - COALESCE(ft.TotalPaid, 0),
                    af.PaymentStatus     =
                        CASE
                            WHEN af.TotalFeeAfterVAT <= 0
                                THEN 'NotPaid'
                            WHEN COALESCE(ft.TotalPaid, 0) >= af.TotalFeeAfterVAT
                                THEN 'Paid'
                            WHEN COALESCE(ft.CustomerPaid, 0) >= af.CustomerPayableAmount
                             AND af.BankAbsorbAmount > 0
                             AND COALESCE(ft.BankSettled, 0) = 0
                                THEN 'PendingInvoice'
                            WHEN COALESCE(ft.TotalPaid, 0) > 0
                                THEN 'Partial'
                            ELSE 'NotPaid'
                        END
                FROM appraisal.AppraisalFees af
                LEFT JOIN fee_totals ft ON ft.AppraisalFeeId = af.Id;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Source",
                schema: "appraisal",
                table: "AppraisalFeePaymentHistory");
        }
    }
}
