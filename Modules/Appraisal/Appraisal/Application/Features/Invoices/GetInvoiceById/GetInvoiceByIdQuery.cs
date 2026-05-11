using Appraisal.Application.Features.Invoices.GetInvoiceById;

namespace Appraisal.Application.Features.Invoices.GetInvoiceById;

public record GetInvoiceByIdQuery(Guid InvoiceId, Guid? CallerCompanyId) : IQuery<InvoiceDetailDto?>;
