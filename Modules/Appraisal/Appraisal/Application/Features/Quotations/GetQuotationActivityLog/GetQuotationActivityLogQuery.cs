using Shared.CQRS;

namespace Appraisal.Application.Features.Quotations.GetQuotationActivityLog;

public record GetQuotationActivityLogQuery(Guid QuotationRequestId) : IQuery<List<QuotationActivityLogRow>>;
