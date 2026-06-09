using FluentValidation;
using Integration.Application.Validation;

namespace Integration.Application.Features.AppraisalRequests.ResubmitRequest;

public class ResubmitRequestCommandValidator : AbstractValidator<ResubmitRequestCommand>
{
    // Mode is parsed case-insensitively by the handler; null defaults to DataFix.
    private static readonly string[] AllowedModes = { "DataFix", "Followup" };

    public ResubmitRequestCommandValidator()
    {
        RuleFor(x => x.RequestId).NotEmpty();

        RuleFor(x => x.Mode)
            .Must(m => m is null || AllowedModes.Any(a => a.Equals(m, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Mode must be 'DataFix' or 'Followup'.");

        // Presence of these fields is mode-dependent and enforced in the handler (DataFix requires
        // them). Here we only bound length/shape for whatever is supplied.
        RuleFor(x => x.Purpose).MaximumLength(10);
        RuleFor(x => x.Channel).MaximumLength(10);
        RuleFor(x => x.Priority).MaximumLength(255);

        RuleFor(x => x.Requestor!).SetValidator(new UserInfoDtoValidator());
        RuleFor(x => x.Creator!).SetValidator(new UserInfoDtoValidator());

        RuleFor(x => x.Detail!).SetValidator(new RequestDetailDtoValidator());

        RuleForEach(x => x.Customers).SetValidator(new RequestCustomerDtoValidator());
        RuleForEach(x => x.Properties).SetValidator(new RequestPropertyDtoValidator());
        RuleForEach(x => x.Titles).SetValidator(new RequestTitleDtoValidator());
        RuleForEach(x => x.Documents).SetValidator(new RequestDocumentDtoValidator());
    }
}
