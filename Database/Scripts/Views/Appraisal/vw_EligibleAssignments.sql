CREATE OR ALTER VIEW appraisal.vw_EligibleAssignments AS
SELECT aa.Id                          AS AssignmentId,
       aa.AssigneeCompanyId,
       af.Id                          AS AppraisalFeeId,
       a.AppraisalNumber,
       rc.Name                        AS CustomerName,
       ap.PropertyType                AS ProductType,
       af.TotalFeeBeforeVAT           AS FeeBeforeVAT,
       af.VATRate,
       af.VATAmount,
       af.TotalFeeAfterVAT,
       af.BankAbsorbAmount,
       aa.CompletedAt                 AS ReceivedDate
FROM appraisal.AppraisalAssignments aa
         INNER JOIN appraisal.AppraisalFees af ON af.AssignmentId = aa.Id
         INNER JOIN appraisal.Appraisals a ON a.Id = aa.AppraisalId
         LEFT JOIN request.RequestCustomers rc ON rc.RequestId = a.RequestId
         LEFT JOIN (SELECT ap2.AppraisalId,
                           ap2.PropertyType,
                           ROW_NUMBER() OVER (PARTITION BY ap2.AppraisalId ORDER BY ap2.SequenceNumber) AS rn
                    FROM appraisal.AppraisalProperties ap2) ap
                   ON ap.AppraisalId = a.Id AND ap.rn = 1
WHERE aa.AssignmentStatus = 'Completed'
  AND af.FeePaymentType IN ('04', '05', '06', '07')
  AND af.BankAbsorbAmount > 0
  AND NOT EXISTS (SELECT 1 FROM appraisal.InvoiceItems ii WHERE ii.AssignmentId = aa.Id)
