using Appraisal.Application.Configurations;

namespace Appraisal.Application.Features.Quotations.SubmitQuotation;

public record SubmitQuotationItemRequest(
    Guid QuotationRequestItemId,
    Guid AppraisalId,
    int ItemNumber,
    decimal QuotedPrice,
    int EstimatedDays,
    // Optional fee-breakdown fields — sent by the Checker path so the draft's
    // breakdown is preserved on final submit. Legacy callers omit them.
    decimal? FeeAmount = null,
    decimal? Discount = null,
    decimal? NegotiatedDiscount = null,
    decimal? VatPercent = null);

public record SubmitQuotationCommand(
    Guid QuotationRequestId,
    Guid CompanyId,
    string QuotationNumber,
    int EstimatedDays,
    List<SubmitQuotationItemRequest> Items,
    DateTime? ValidUntil = null,
    DateTime? ProposedStartDate = null,
    DateTime? ProposedCompletionDate = null,
    string? Remarks = null,
    string? TermsAndConditions = null,
    string? ContactName = null,
    string? ContactEmail = null,
    string? ContactPhone = null)
    : ICommand<SubmitQuotationResult>, ITransactionalCommand<IAppraisalUnitOfWork>;
