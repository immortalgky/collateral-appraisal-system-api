namespace Request.Application.Features.Requests.UpdateDraftRequest;

public class UpdateDraftRequestCommandValidator : AbstractValidator<UpdateDraftRequestCommand>
{
    public UpdateDraftRequestCommandValidator()
    {
        // A draft is intentionally incomplete: only the fields the handler dereferences
        // unconditionally are required. Everything else (Purpose, Channel, Detail, Customers,
        // Properties, ...) is validated on Submit, not on Save-draft.
        RuleFor(x => x.RequestorEmployeeId)
            .NotNull()
            .NotEmpty()
            .WithMessage("Requestor is required.");

        RuleFor(x => x.Creator)
            .NotNull()
            .WithMessage("Creator is required.");

        // Priority is optional for a draft (omitted → Normal at Save), but an explicitly
        // supplied value must be in-set so it fails as a 400 instead of throwing in Save.
        RuleFor(x => x.Priority)
            .Must(Priority.IsValid)
            .WithMessage("Priority must be 'Normal' or 'High'.");
    }
}