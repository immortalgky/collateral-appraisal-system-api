using Collateral.Contracts;
using FluentValidation;

namespace Appraisal.Application.Features.Appraisals.GetInProgressAppraisalsByCollateral;

public class GetInProgressAppraisalsByCollateralQueryValidator
    : AbstractValidator<GetInProgressAppraisalsByCollateralQuery>
{
    private static readonly string[] SupportedTypes =
        [CollateralTypes.Land, CollateralTypes.LandWithBuilding, CollateralTypes.Condo, CollateralTypes.Machine];

    public GetInProgressAppraisalsByCollateralQueryValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => SupportedTypes.Contains(t))
            .WithMessage($"type must be one of: {string.Join(", ", SupportedTypes)}.");
    }
}
