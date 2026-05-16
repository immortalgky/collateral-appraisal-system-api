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
       -- Sourced from AppraisalFee.TotalPaidAmount (customer-side aggregate, maintained
       -- by AppraisalFee.RecordPayment / RecalculateFromPayments).
       af.TotalPaidAmount                                              AS PayPartialAmount,
       -- RemainingFee: outstanding fee after subtracting customer payments and the
       -- bank-absorb commitment. = TotalFeeAfterVAT - PayPartialAmount - BankAbsorbAmount.
       af.TotalFeeAfterVAT - af.TotalPaidAmount - af.BankAbsorbAmount  AS RemainingFee,
       -- SubmittedDate: when the appraiser handed the book to the bank for review.
       -- Sourced from aa.SubmittedAt, stamped by AppraisalAssignment.MarkUnderReview()
       -- at the ext-appraisal-verification → appraisal-book-verification handoff.
       aa.SubmittedAt                                                 AS SubmittedDate,
       -- LastPaymentDate: most recent payment recorded against this fee in the
       -- AppraisalFeePaymentHistory ledger.
       (SELECT MAX(h.PaymentDate)
        FROM appraisal.AppraisalFeePaymentHistory h
        WHERE h.AppraisalFeeId = af.Id)                               AS LastPaymentDate
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
