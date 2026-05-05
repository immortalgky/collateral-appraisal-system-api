using FluentValidation;

namespace Appraisal.Application.Features.Quotations.SendQuotation;

public class SendQuotationCommandValidator : AbstractValidator<SendQuotationCommand>
{
    public SendQuotationCommandValidator()
    {
        RuleFor(x => x.QuotationRequestId).NotEmpty();
        RuleFor(x => x.From).NotEmpty().MaximumLength(500);
        RuleFor(x => x.To).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Cc).MaximumLength(500).When(x => x.Cc is not null);
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).MaximumLength(4000).When(x => x.Content is not null);
    }
}
