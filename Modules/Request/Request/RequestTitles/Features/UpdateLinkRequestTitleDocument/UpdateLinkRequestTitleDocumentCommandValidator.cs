using System;

namespace Request.RequestTitles.Features.UpdateLinkRequestTitleDocument;

public class UpdateLinkRequestTitleDocumentCommandValidator : AbstractValidator<UpdateLinkRequestTitleDocumentCommand>
{
    public UpdateLinkRequestTitleDocumentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("'{PropertyName}' must not be empty.");

        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("'{PropertyName}' must not be empty.");

        RuleFor(x => x.TitleId)
            .NotEmpty()
            .WithMessage("'{PropertyName}' must not be empty.");

        RuleFor(x => x.DocumentType)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.Filename)
            .MaximumLength(255)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.Prefix)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.DocumentDescription)
            .MaximumLength(500)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.FilePath)
            .MaximumLength(500)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.CreatedWorkstation)
            .MaximumLength(50)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.UploadedBy)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        RuleFor(x => x.UploadedByName)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
    }
}
