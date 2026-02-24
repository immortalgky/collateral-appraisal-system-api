namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public record LinkAppraisalComparableRequest(
    Guid MarketComparableId,
    int SequenceNumber,
    decimal OriginalPricePerUnit,
    decimal? Weight = null,
    string? SelectionReason = null,
    string? Notes = null
);
