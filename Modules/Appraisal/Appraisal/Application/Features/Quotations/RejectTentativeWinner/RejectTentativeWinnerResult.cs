namespace Appraisal.Application.Features.Quotations.RejectTentativeWinner;

public record RejectTentativeWinnerResult(Guid QuotationRequestId, string Status);
