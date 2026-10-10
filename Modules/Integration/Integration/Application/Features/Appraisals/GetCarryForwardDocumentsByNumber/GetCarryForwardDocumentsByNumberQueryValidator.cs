using FluentValidation;

namespace Integration.Application.Features.Appraisals.GetCarryForwardDocumentsByNumber;

public class GetCarryForwardDocumentsByNumberQueryValidator : AbstractValidator<GetCarryForwardDocumentsByNumberQuery>
{
    public GetCarryForwardDocumentsByNumberQueryValidator()
    {
        RuleFor(x => x.AppraisalNumber).NotEmpty().MaximumLength(50);
    }
}
