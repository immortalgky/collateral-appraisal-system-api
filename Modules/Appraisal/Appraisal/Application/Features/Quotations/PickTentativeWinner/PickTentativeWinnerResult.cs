namespace Appraisal.Application.Features.Quotations.PickTentativeWinner;

public record PickTentativeWinnerResult(
    Guid QuotationRequestId,
    Guid CompanyQuotationId,
    string Status);
