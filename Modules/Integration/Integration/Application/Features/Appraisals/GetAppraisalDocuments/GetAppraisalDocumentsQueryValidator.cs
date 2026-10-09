using FluentValidation;

namespace Integration.Application.Features.Appraisals.GetAppraisalDocuments;

public class GetAppraisalDocumentsQueryValidator : AbstractValidator<GetAppraisalDocumentsQuery>
{
    public GetAppraisalDocumentsQueryValidator()
    {
        RuleFor(x => x.AppraisalNumber).NotEmpty().MaximumLength(50);
    }
}
