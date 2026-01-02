namespace Request.Application.Features.Requests.CreateRequest;

public class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator()
    {
        RuleFor(x => x.Purpose)
            .NotNull()
            .NotEmpty()
            .WithMessage("Purpose is required.");

        RuleFor(x => x.Channel)
            .NotNull()
            .NotEmpty()
            .WithMessage("Channel is required.");

        RuleFor(x => x.Requestor)
            .NotNull()
            .WithMessage("Requestor is required.");

        RuleFor(x => x.Creator)
            .NotNull()
            .WithMessage("Creator is required.");

        RuleFor(x => x.Priority)
            .NotNull()
            .NotEmpty()
            .WithMessage("Priority is required.");

        RuleFor(x => x.IsPma)
            .NotNull()
            .WithMessage("IsPma is required.");

        RuleFor(x => x.Detail)
            .NotEmpty()
            .WithMessage("Detail is required.");

        RuleFor(x => x.Customers)
            .NotNull()
            .WithMessage("Customers is required.");

        RuleFor(x => x.Properties)
            .NotNull()
            .WithMessage("Properties is required.");
    }
}