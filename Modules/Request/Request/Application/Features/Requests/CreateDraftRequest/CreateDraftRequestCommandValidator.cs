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

        // Priority is optional for a draft (omitted → Normal at Save), but an explicitly
        // supplied value must be in-set so it fails as a 400 instead of throwing in Save.
        RuleFor(x => x.Priority)
            .Must(Priority.IsValid)
            .WithMessage("Priority must be 'Normal' or 'High'.");
    }
}