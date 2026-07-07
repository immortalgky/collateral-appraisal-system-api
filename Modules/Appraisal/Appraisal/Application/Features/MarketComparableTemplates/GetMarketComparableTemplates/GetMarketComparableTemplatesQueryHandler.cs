using Appraisal.Domain.MarketComparables;
using Shared.CQRS;

namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplates;

public class GetMarketComparableTemplatesQueryHandler(
    IMarketComparableTemplateRepository repository
) : IQueryHandler<GetMarketComparableTemplatesQuery, GetMarketComparableTemplatesResult>
{
    public async Task<GetMarketComparableTemplatesResult> Handle(
        GetMarketComparableTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        IEnumerable<MarketComparableTemplate> templates;

        // Default to returning all statuses (active + inactive) so the admin list can
        // show and reactivate inactive templates; the UI filters by status client-side.
        if (!string.IsNullOrWhiteSpace(query.PropertyType))
            templates = await repository.GetByPropertyTypeAsync(
                query.PropertyType,
                query.IsActive ?? false,
                cancellationToken);
        else
            templates = await repository.GetAllAsync(
                query.IsActive ?? false,
                cancellationToken);

        var dtos = templates.OrderBy(t => t.CreatedAt).Select(t => new MarketComparableTemplateDto(
            t.Id,
            t.TemplateCode,
            t.TemplateName,
            t.PropertyType,
            t.Description,
            t.IsActive,
            t.CreatedAt,
            t.UpdatedAt,
            t.TemplateFactors.Count
        )).ToList();

        return new GetMarketComparableTemplatesResult(dtos);
    }
}