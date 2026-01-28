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

        if (!string.IsNullOrWhiteSpace(query.PropertyType))
        {
            templates = await repository.GetByPropertyTypeAsync(
                query.PropertyType,
                query.IsActive ?? true,
                cancellationToken);
        }
        else
        {
            templates = await repository.GetAllAsync(
                query.IsActive ?? true,
                cancellationToken);
        }

        var dtos = templates.Select(t => new MarketComparableTemplateDto(
            t.Id,
            t.TemplateCode,
            t.TemplateName,
            t.PropertyType,
            t.Description,
            t.IsActive,
            t.CreatedOn,
            t.UpdatedOn
        )).ToList();

        return new GetMarketComparableTemplatesResult(dtos);
    }
}
