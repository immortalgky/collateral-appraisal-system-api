namespace Document.Documents.Features.UpdateDocument;

public class UpdateDocumentCommandValidator : AbstractValidator<UpdateDocumentCommand>
{
    public UpdateDocumentCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Id is required.")
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.");

        RuleFor(x => x.NewComment)
            .NotNull()
            .WithMessage("Comment is required.");
    }
}