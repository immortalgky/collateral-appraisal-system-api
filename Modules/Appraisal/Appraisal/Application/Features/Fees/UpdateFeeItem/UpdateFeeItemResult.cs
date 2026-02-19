namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public record UpdateFeeItemResult(
    Guid FeeItemId,
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount
);