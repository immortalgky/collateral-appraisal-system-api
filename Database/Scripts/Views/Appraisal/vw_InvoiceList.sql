CREATE OR ALTER VIEW appraisal.vw_InvoiceList AS
SELECT i.Id,
       i.InvoiceNumber,
       i.Status,
       i.TotalAmount,
       i.Notes,
       COUNT(ii.Id)            AS ItemCount,
       MIN(ii.SubmittedDate)   AS PeriodStartDate,
       MAX(ii.SubmittedDate)   AS PeriodEndDate,
       i.SubmittedAt,
       i.ApprovedAt,
       i.ApprovedBy,
       i.PaymentOrderNo,
       i.PaidDate,
       -- SentDate: the date the invoice was submitted (transitioned to Sent status)
       CAST(i.SubmittedAt AS DATE) AS SentDate,
       i.CompanyId,
       c.Name                  AS CompanyName,
       c.BankAccountNo,
       c.BankAccountName,
       i.CreatedAt
FROM appraisal.Invoices i
         LEFT JOIN appraisal.InvoiceItems ii ON ii.InvoiceId = i.Id
         LEFT JOIN auth.Companies c ON c.Id = i.CompanyId
GROUP BY i.Id, i.InvoiceNumber, i.Status, i.TotalAmount, i.Notes,
         i.SubmittedAt, i.ApprovedAt, i.ApprovedBy,
         i.PaymentOrderNo, i.PaidDate,
         i.CompanyId, c.Name, c.BankAccountNo, c.BankAccountName, i.CreatedAt
