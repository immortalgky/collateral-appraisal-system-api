namespace Request.Requests.Features.CreateRequest;

public class CreateRequestCommandValidator : AbstractValidator<CreateRequestCommand>
{
    public CreateRequestCommandValidator()
    {
        RuleFor(x => x.Purpose)
            .NotEmpty()
            .WithMessage("Purpose is required.");

        RuleFor(x => x.Priority)
            .NotEmpty()
            .WithMessage("Priority is required.");

        // RuleFor(x => x.Channel)
        //     .NotEmpty()
        //     .WithMessage("Channel is required.");

        RuleFor(x => x.Address)
            .NotNull()
            .WithMessage("Address is required.");

        RuleFor(x => x.Contact)
            .NotNull()
            .WithMessage("Contact is required.");

        RuleFor(x => x.Fee)
            .NotNull()
            .WithMessage("Fee is required.");

        // RuleFor(x => x.Requestor)
        //     .NotNull()
        //     .WithMessage("Requestor is required.");

        RuleFor(x => x.Customers)
            .NotNull()
            .WithMessage("Customers is required.")
            .Must(customers => customers.Count > 0)
            .WithMessage("At least one customer is required.");

        RuleFor(x => x.Properties)
            .NotNull()
            .WithMessage("Properties is required.")
            .Must(properties => properties.Count > 0)
            .WithMessage("At least one property is required.");
    }
}