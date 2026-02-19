namespace Appraisal.Application.Features.Fees.UpdateFeeItem;

public record UpdateFeeItemRequest(string FeeCode, string FeeDescription, decimal FeeAmount);