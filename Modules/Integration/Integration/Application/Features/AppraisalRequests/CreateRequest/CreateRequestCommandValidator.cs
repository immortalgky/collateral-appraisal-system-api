using FluentValidation;
using Integration.Application.Validation;
using Request.Domain.Requests;

namespace Integration.Application.Features.AppraisalRequests.CreateRequest;

public class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator()
    {
        RuleFor(x => x.Purpose).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Channel).NotEmpty().MaximumLength(10);
        RuleFor(x => x.Priority)
            .NotEmpty()
            .Must(Priority.IsValid)
            .WithMessage("Priority must be 'Normal' or 'High'.");
        RuleFor(x => x.ExternalCaseKey).MaximumLength(100);

        RuleFor(x => x.Requestor).NotNull().SetValidator(new UserInfoDtoValidator());
        RuleFor(x => x.Creator).NotNull().SetValidator(new UserInfoDtoValidator());

        RuleFor(x => x.Detail!).SetValidator(new RequestDetailDtoValidator());

        RuleForEach(x => x.Customers).SetValidator(new RequestCustomerDtoValidator());
        RuleForEach(x => x.Properties).SetValidator(new RequestPropertyDtoValidator());
        RuleForEach(x => x.Titles).SetValidator(new RequestTitleDtoValidator());
        RuleForEach(x => x.Documents).SetValidator(new RequestDocumentDtoValidator());
        RuleForEach(x => x.Comments).SetValidator(new RequestCommentDtoValidator());
    }
}
