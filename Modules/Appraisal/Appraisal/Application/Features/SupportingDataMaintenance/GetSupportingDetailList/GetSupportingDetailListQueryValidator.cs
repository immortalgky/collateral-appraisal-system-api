namespace Appraisal.Application.Features.SupportingDataMaintenance.GetSupportingDetailList;

public class GetSupportingDetailListQueryValidator
    : AbstractValidator<GetSupportingDetailListQuery>
{
    public GetSupportingDetailListQueryValidator()
    {
        RuleFor(x => x.SupportingId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
