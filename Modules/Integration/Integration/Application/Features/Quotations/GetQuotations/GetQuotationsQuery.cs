using Shared.CQRS;

namespace Integration.Application.Features.Quotations.GetQuotations;

public record GetQuotationsQuery(Guid RequestId) : IQuery<GetQuotationsResult>;

public record GetQuotationsResult(List<QuotationDto> Quotations);

public record QuotationDto(
    Guid Id,
    Guid CompanyId,
    string CompanyName,
    decimal TotalAmount,
    string Status,
    DateTime SubmittedAt,
    bool IsWinner
);
