using System;

namespace Request.Requests.Features.UpdateDraftRequest;

public class UpdateDraftRequestCommandValidator : AbstractValidator<UpdateDraftRequestCommand>
{
    public UpdateDraftRequestCommandValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Empty Information");

        RuleFor(x => x.SourceSystem.CreatedDate)
            .NotEmpty()
            .WithMessage("CreatedDate is required.");

        RuleFor(x => x.SourceSystem.Creator)
            .NotEmpty()
            .WithMessage("Creator is required.");

        RuleFor(x => x.SourceSystem.CreatorName)
            .NotEmpty()
            .WithMessage("CreatorName is required.");
    }
}
