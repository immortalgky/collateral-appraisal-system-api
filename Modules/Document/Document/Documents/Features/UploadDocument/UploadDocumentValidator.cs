namespace Document.Documents.Features.UploadDocument;

public class UploadDocumentCommandValidator : AbstractValidator<UploadDocumentCommand>
{
    public UploadDocumentCommandValidator()
    {
        RuleFor(x => x.Documents)
            .NotNull()
            .WithMessage("Document is required.")
            .Must(document => document.Count > 0)
            .WithMessage("At least one document is required.");

        RuleFor(x => x.RelateRequest)
            .NotNull()
            .WithMessage("RelateRequest is required.");

        RuleFor(x => x.RelateId)
            .NotNull()
            .WithMessage("RelateId is required.")
            .GreaterThan(0)
            .WithMessage("RelateId must be greater than 0.");
    }
}