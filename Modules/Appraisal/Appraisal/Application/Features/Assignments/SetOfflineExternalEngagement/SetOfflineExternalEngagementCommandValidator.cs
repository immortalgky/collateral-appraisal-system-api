using Shared.Time;

namespace Appraisal.Application.Features.Assignments.SetOfflineExternalEngagement;

/// <summary>
/// BookDate is a non-nullable DateTime, so an omitted or null JSON field binds to
/// DateTime.MinValue rather than failing. That value would be written straight to
/// ValuationAnalyses.ValuationDate and then PINNED there — AppraisalValuationSummaryService stops
/// re-deriving the date once the assignment is Offline — so the appraisal would render as
/// 01/01/0001 on the report and in the AS400/LOS feed with no code path able to correct it.
/// Reject it at the edge instead.
/// </summary>
public class SetOfflineExternalEngagementCommandValidator
    : AbstractValidator<SetOfflineExternalEngagementCommand>
{
    public SetOfflineExternalEngagementCommandValidator(IDateTimeProvider dateTimeProvider)
    {
        RuleFor(c => c.AppraisalId)
            .NotEmpty().WithMessage("AppraisalId is required.");

        RuleFor(c => c.CompanyId)
            .NotEmpty().WithMessage("The external company is required.");

        RuleFor(c => c.BookDate)
            .Must(d => d != default)
            .WithMessage("The appraisal date from the company's book is required.")
            // The date is read off a book the bank has already received, so it cannot be in the
            // future. This also catches a client that sends a parseable but nonsensical value.
            .Must(d => d.Date <= dateTimeProvider.ApplicationNow.Date)
            .WithMessage("The appraisal date from the book cannot be in the future.");
    }
}
