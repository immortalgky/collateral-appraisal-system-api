namespace Appraisal.Application.Features.Fees.RejectFeeItem;

public record RejectFeeItemRequest(Guid RejectedBy, string Reason);
