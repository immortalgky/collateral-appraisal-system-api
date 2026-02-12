CREATE
OR ALTER
VIEW appraisal.vw_AppraisalFeeList AS
SELECT f.Id,
       f.AssignmentId,
       aa.AppraisalId,
       f.TotalFeeBeforeVAT,
       f.VATRate,
       f.VATAmount,
       f.TotalFeeAfterVAT,
       f.BankAbsorbAmount,
       f.CustomerPayableAmount,
       f.TotalPaidAmount,
       f.OutstandingAmount,
       f.PaymentStatus,
       f.InspectionFeeAmount,
       f.CreatedAt
FROM appraisal.AppraisalFees f
         INNER JOIN appraisal.AppraisalAssignments aa ON aa.Id = f.AssignmentId
