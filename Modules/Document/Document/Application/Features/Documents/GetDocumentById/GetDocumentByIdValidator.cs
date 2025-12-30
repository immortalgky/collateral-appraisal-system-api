namespace Document.Domain.Documents.Features.GetDocumentById;

public class GetDocumentByIdQueryValidator : AbstractValidator<GetDocumentByIdQuery>
{
    public GetDocumentByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .NotNull()
            .WithMessage("Id is required.");
    }
}