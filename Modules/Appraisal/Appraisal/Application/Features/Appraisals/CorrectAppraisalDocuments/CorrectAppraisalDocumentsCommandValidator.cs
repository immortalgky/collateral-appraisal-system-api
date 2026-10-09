namespace Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;

public class CorrectAppraisalDocumentsCommandValidator : AbstractValidator<CorrectAppraisalDocumentsCommand>
{
    public CorrectAppraisalDocumentsCommandValidator()
    {
        RuleFor(x => x.AppraisalId).NotEmpty();

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("A reason is required for every document correction.")
            .MaximumLength(4000);

        // A call that neither attaches nor deletes would write an audit row for nothing.
        RuleFor(x => x)
            .Must(x => x.RemoveId is not null || x.Add is not null)
            .WithMessage("Specify a document to add, a document to remove, or both.");

        RuleFor(x => x.RemoveId).NotEqual(Guid.Empty).When(x => x.RemoveId is not null);

        When(x => x.Add is not null, () =>
        {
            RuleFor(x => x.Add!.DocumentTypeCode).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Add!.DocumentId).NotEmpty();
        });
    }
}
