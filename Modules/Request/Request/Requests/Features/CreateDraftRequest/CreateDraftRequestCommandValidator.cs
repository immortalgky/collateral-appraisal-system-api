using System;

namespace Request.Requests.Features.CreateDraftRequest;

public class CreateDraftRequestCommandValidator : AbstractValidator<CreateDraftRequestCommand>
{
    public CreateDraftRequestCommandValidator()
    {
        RuleFor(x => x)
            .NotEmpty()
            .WithMessage("Empty Information");
    }
}

