namespace Document.Documents.Features.GetDocumentById;

public class GetDocumentByIdQueryValidator : AbstractValidator<GetDocumentByIdQuery>
{
    public GetDocumentByIdQueryValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithMessage("Id must be greater than 0.")
            .NotNull()
            .WithMessage("Id is required.");
    }
}