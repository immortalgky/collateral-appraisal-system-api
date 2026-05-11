CREATE OR ALTER VIEW appraisal.vw_InvoiceList AS
SELECT i.Id,
       i.InvoiceNumber,
       i.Status,
       i.TotalAmount,
       COUNT(ii.Id)         AS ItemCount,
       MIN(ii.ReceivedDate) AS PeriodStartDate,
       MAX(ii.ReceivedDate) AS PeriodEndDate,
       i.SubmittedAt,
       i.ApprovedAt,
       i.ApprovedBy,
       i.PaymentReference,
       i.PaymentMethod,
       i.PaymentDate,
       i.CompanyId,
       c.Name               AS CompanyName,
       i.CreatedAt
FROM appraisal.Invoices i
         LEFT JOIN appraisal.InvoiceItems ii ON ii.InvoiceId = i.Id
         LEFT JOIN auth.Companies c ON c.Id = i.CompanyId
GROUP BY i.Id, i.InvoiceNumber, i.Status, i.TotalAmount,
         i.SubmittedAt, i.ApprovedAt, i.ApprovedBy,
         i.PaymentReference, i.PaymentMethod, i.PaymentDate,
         i.CompanyId, c.Name, i.CreatedAt
