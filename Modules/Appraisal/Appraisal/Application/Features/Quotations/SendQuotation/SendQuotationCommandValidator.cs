using FluentValidation;

namespace Appraisal.Application.Features.Quotations.SendQuotation;

public class SendQuotationCommandValidator : AbstractValidator<SendQuotationCommand>
{
    public SendQuotationCommandValidator()
    {
        RuleFor(x => x.QuotationRequestId).NotEmpty();
        RuleFor(x => x.From).NotEmpty().MaximumLength(500);
        // To is optional — recipients may all be in Cc/Bcc (bank hides recipients).
        RuleFor(x => x.To).MaximumLength(500).When(x => x.To is not null);
        RuleFor(x => x.Cc).MaximumLength(500).When(x => x.Cc is not null);
        RuleFor(x => x.Bcc).MaximumLength(500).When(x => x.Bcc is not null);
        RuleFor(x => x)
            .Must(c => !string.IsNullOrWhiteSpace(c.To)
                       || !string.IsNullOrWhiteSpace(c.Cc)
                       || !string.IsNullOrWhiteSpace(c.Bcc))
            .WithMessage("At least one recipient (To, Cc, or Bcc) is required.");
        RuleFor(x => x.Subject).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Content).MaximumLength(4000).When(x => x.Content is not null);
    }
}
