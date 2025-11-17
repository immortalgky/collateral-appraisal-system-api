using System;

namespace Request.Requests.Features.UpdateDraftRequest;

public class UpdateDraftRequestCommandValidator : AbstractValidator<UpdateDraftRequestCommand>
{
    public UpdateDraftRequestCommandValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Empty Information");
    }
}
