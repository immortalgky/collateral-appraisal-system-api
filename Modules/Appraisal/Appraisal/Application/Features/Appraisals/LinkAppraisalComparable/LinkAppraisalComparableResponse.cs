namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public record LinkAppraisalComparableResponse(
    Guid Id,
    int SequenceNumber,
    decimal OriginalPricePerUnit,
    decimal Weight
);
