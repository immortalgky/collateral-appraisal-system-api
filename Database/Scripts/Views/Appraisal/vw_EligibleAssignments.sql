CREATE
OR ALTER
VIEW appraisal.vw_EligibleAssignments AS
SELECT aa.Id                                                          AS AssignmentId,
       aa.AssigneeCompanyId,
       af.Id                                                          AS AppraisalFeeId,
       a.AppraisalNumber,
       rc.Name                                                        AS CustomerName,
       ap.PropertyType                                                AS ProductType,
       af.FeePaymentType,
       af.TotalFeeBeforeVAT                                          AS FeeBeforeVAT,
       af.VATRate,
       af.VATAmount,
       af.TotalFeeAfterVAT,
       af.BankAbsorbAmount,
       -- PayPartialAmount: amount the customer has already paid against this fee.
       -- Excludes BankAbsorb synthetic rows (inserted when invoice is marked Paid)
       -- so that the display amount only reflects real cash/transfer payments.
       (SELECT COALESCE(SUM(h.PaymentAmount), 0)
        FROM appraisal.AppraisalFeePaymentHistory h
        WHERE h.AppraisalFeeId = af.Id
          AND h.Source <> 'BankAbsorb')                               AS PayPartialAmount,
       -- RemainingFee: outstanding customer portion after their payments.
       -- = TotalFeeAfterVAT - customer-paid-amount - BankAbsorbAmount.
       af.TotalFeeAfterVAT
           - (SELECT COALESCE(SUM(h.PaymentAmount), 0)
              FROM appraisal.AppraisalFeePaymentHistory h
              WHERE h.AppraisalFeeId = af.Id
                AND h.Source <> 'BankAbsorb')
           - af.BankAbsorbAmount                                       AS RemainingFee,
       -- SubmittedDate: when the appraiser handed the book to the bank for review.
       -- Sourced from aa.SubmittedAt, stamped by AppraisalAssignment.MarkUnderReview()
       -- at the ext-appraisal-verification → appraisal-book-verification handoff.
       aa.SubmittedAt                                                 AS SubmittedDate,
       -- LastPaymentDate: most recent real customer payment (Source<>'BankAbsorb').
       -- BankAbsorb rows are excluded because their date is the invoice paid date,
       -- not a cash/transfer payment from the customer.
       (SELECT MAX(h.PaymentDate)
        FROM appraisal.AppraisalFeePaymentHistory h
        WHERE h.AppraisalFeeId = af.Id
          AND h.Source <> 'BankAbsorb')                               AS LastPaymentDate
FROM appraisal.AppraisalAssignments aa
         INNER JOIN appraisal.AppraisalFees af ON af.AssignmentId = aa.Id
         INNER JOIN appraisal.Appraisals a ON a.Id = aa.AppraisalId
         LEFT JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
         LEFT JOIN (SELECT ap2.AppraisalId,
                           ap2.PropertyType,
                           ROW_NUMBER() OVER (PARTITION BY ap2.AppraisalId ORDER BY ap2.SequenceNumber) AS rn
                    FROM appraisal.AppraisalProperties ap2) ap
                   ON ap.AppraisalId = a.Id AND ap.rn = 1
WHERE aa.AssignmentStatus IN ('Verified', 'Completed')
  AND af.BankAbsorbAmount > 0
  -- Invoice gate: assignment must have passed bank verification (Verified) OR be terminal Completed.
  -- Verified is set by the workflow handler when int-appraisal-verification completes — that is the
  -- moment the bank accepted the book. Mirrors AppraisalAssignment.IsInvoiceEligible.
  -- FSD criterion: all fee items must be approved (no Pending items remain)
  AND NOT EXISTS (SELECT 1
                  FROM appraisal.AppraisalFeeItems fi
                  WHERE fi.AppraisalFeeId = af.Id
                    AND fi.ApprovalStatus = 'Pending')
  -- Dedup against InvoiceItems is applied per-consumer (see GetEligibleAssignmentsQueryHandler /
  -- CreateInvoiceCommandHandler / UpdateInvoiceDraftCommandHandler) so the current draft can be
  -- excluded from the dedup when editing.
