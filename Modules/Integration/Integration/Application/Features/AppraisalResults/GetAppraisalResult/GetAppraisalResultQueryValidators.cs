using FluentValidation;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByNumberQueryValidator : AbstractValidator<GetAppraisalResultByNumberQuery>
{
    public GetAppraisalResultByNumberQueryValidator()
    {
        RuleFor(x => x.AppraisalNumber).NotEmpty().MaximumLength(50);
    }
}

public class GetAppraisalResultsByCaseKeyQueryValidator : AbstractValidator<GetAppraisalResultsByCaseKeyQuery>
{
    public GetAppraisalResultsByCaseKeyQueryValidator()
    {
        RuleFor(x => x.ExternalCaseKey).NotEmpty().MaximumLength(100);
    }
}
