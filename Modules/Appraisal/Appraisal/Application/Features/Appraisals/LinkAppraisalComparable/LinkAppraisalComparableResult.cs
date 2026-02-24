namespace Appraisal.Application.Features.Appraisals.LinkAppraisalComparable;

public record LinkAppraisalComparableResult(
    Guid Id,
    int SequenceNumber,
    decimal OriginalPricePerUnit,
    decimal Weight
);
