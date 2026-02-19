namespace Appraisal.Application.Features.Fees.AddFeeItem;

public record AddFeeItemRequest(
    string FeeCode,
    string FeeDescription,
    decimal FeeAmount);
