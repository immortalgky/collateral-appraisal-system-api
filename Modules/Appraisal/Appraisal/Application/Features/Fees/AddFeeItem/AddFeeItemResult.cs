namespace Appraisal.Application.Features.Fees.AddFeeItem;

/// <param name="FeeId">The fee the item landed on — new when the admin's first item created the fee row.</param>
public record AddFeeItemResult(Guid ItemId, Guid FeeId);
