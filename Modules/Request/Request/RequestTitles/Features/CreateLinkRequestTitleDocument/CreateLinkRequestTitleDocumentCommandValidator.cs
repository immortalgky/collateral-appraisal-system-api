namespace Request.RequestTitles.Features.CreateLinkRequestTitleDocument;

public class CreateLinkRequestTitleDocumentCommandValidator : AbstractValidator<CreateLinkRequestTitleDocumentCommand>
{
    public CreateLinkRequestTitleDocumentCommandValidator()
    {
        RuleFor(x => x.DocumentType)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x =>x.DocumentDescription)
            .MaximumLength(500)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");

        
        RuleFor(x => x.UploadedBy)
            .MaximumLength(10)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
        
        RuleFor(x => x.UploadedByName)
            .MaximumLength(100)
            .WithMessage("'{PropertyName}' must be {MaxLength} characters or fewer. You entered {TotalLength} characters.");
    }   
}