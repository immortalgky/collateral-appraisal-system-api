namespace Appraisal.Application.Features.SupportingDataMaintenance.CreateSupportingDetail;

public class CreateSupportingDetailCommandValidator
    : AbstractValidator<CreateSupportingDetailCommand>
{
    public CreateSupportingDetailCommandValidator()
    {
        RuleFor(x => x.Detail).NotNull();
        RuleFor(x => x.Detail.CollateralType).NotEmpty();
        RuleFor(x => x.Detail.BuildingType).NotEmpty();
        RuleFor(x => x.Detail.InformationDate).NotEmpty();
    }
}