using FluentValidation;

namespace Integration.Application.Features.AppraisalResults.GetAppraisalResult;

public class GetAppraisalResultByNumberQueryValidator : AbstractValidator<GetAppraisalResultByNumberQuery>
{
    public GetAppraisalResultByNumberQueryValidator()
    {
        RuleFor(x => x.AppraisalNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.PlotNumber).MaximumLength(100);
        RuleFor(x => x.RoomNumber).MaximumLength(50);
        RuleFor(x => x.FloorNumber).MaximumLength(20);
    }
}

public class GetAppraisalResultsByCaseKeyQueryValidator : AbstractValidator<GetAppraisalResultsByCaseKeyQuery>
{
    public GetAppraisalResultsByCaseKeyQueryValidator()
    {
        RuleFor(x => x.ExternalCaseKey).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PlotNumber).MaximumLength(100);
        RuleFor(x => x.RoomNumber).MaximumLength(50);
        RuleFor(x => x.FloorNumber).MaximumLength(20);
    }
}
