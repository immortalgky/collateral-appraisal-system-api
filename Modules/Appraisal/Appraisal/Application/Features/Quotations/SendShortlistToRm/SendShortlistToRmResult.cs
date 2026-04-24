namespace Appraisal.Application.Features.Quotations.SendShortlistToRm;

public record SendShortlistToRmResult(Guid QuotationRequestId, string Status, DateTime SentAt);
