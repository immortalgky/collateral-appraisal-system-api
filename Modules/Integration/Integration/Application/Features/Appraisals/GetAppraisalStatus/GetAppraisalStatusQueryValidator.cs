using FluentValidation;

namespace Integration.Application.Features.Appraisals.GetAppraisalStatus;

public class GetAppraisalStatusQueryValidator : AbstractValidator<GetAppraisalStatusQuery>
{
    public GetAppraisalStatusQueryValidator()
    {
        RuleFor(x => x.AppraisalNumber).NotEmpty().MaximumLength(50);
    }
}
