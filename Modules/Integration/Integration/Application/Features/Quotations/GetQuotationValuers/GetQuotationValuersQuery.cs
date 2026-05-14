using Shared.CQRS;

namespace Integration.Application.Features.Quotations.GetQuotationValuers;

public record GetQuotationValuersQuery(Guid QuotationId) : IQuery<GetQuotationValuersResult>;

public record GetQuotationValuersResult(
    Guid QuotationId,
    string? QuotationNumber,
    List<QuotationAppraisalItem> Appraisals,
    List<QuotationValuerItem> Valuers);

public record QuotationAppraisalItem(
    string AppraisalNumber,
    string? PropertyType,
    string? PropertyLocation);

public record QuotationValuerItem(
    string CompanyQuotationId,
    string ValuerName,
    decimal TotalAppraisalFee,
    int? EstimatedDays,
    List<ValuerAppraisalFeeItem> Items);

public record ValuerAppraisalFeeItem(
    string AppraisalNumber,
    decimal QuotedPrice,
    int? EstimatedDays);