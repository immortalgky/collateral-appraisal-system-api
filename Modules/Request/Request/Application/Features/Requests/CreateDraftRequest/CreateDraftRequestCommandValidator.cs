namespace Request.Application.Features.Requests.CreateDraftRequest;

public class CreateDraftRequestCommandValidator : AbstractValidator<CreateDraftRequestCommand>
{
    public CreateDraftRequestCommandValidator()
    {
        RuleFor(x => x.RequestorEmployeeId)
            .NotNull()
            .NotEmpty()
            .WithMessage("Requestor information is required.");

        RuleFor(x => x.Creator)
            .NotNull()
            .WithMessage("Creator information is required.");
    }
}