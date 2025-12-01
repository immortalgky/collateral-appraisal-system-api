namespace Request.Requests.Features.DeleteRequest;

public class DeleteRequestCommandValidator : AbstractValidator<DeleteRequestCommand>
{
    public DeleteRequestCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Id is required.");
        // .GreaterThan(0)
        // .WithMessage("Id must be greater than 0.");
    }
}