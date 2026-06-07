using FluentValidation;

namespace Integration.Application.Features.Quotations.ApproveQuotation;

public class ApproveQuotationCommandValidator : AbstractValidator<ApproveQuotationCommand>
{
    public ApproveQuotationCommandValidator()
    {
        RuleFor(x => x.QuotationId).NotEmpty();
        // ApprovalReason is free text with no DB column to mirror; boundary cap.
        RuleFor(x => x.ApprovalReason).MaximumLength(4000);
    }
}
