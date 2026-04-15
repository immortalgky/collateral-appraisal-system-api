using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Features.GetPricingTemplates;

public class GetPricingTemplatesQueryHandler(
    ParameterDbContext context
) : IQueryHandler<GetPricingTemplatesQuery, GetPricingTemplatesResult>
{
    public async Task<GetPricingTemplatesResult> Handle(
        GetPricingTemplatesQuery query,
        CancellationToken cancellationToken)
    {
        var dbQuery = context.PricingTemplates.AsQueryable();

        if (query.ActiveOnly)
            dbQuery = dbQuery.Where(t => t.IsActive);

        var templates = await dbQuery
            .OrderBy(t => t.DisplaySeq)
            .Select(t => new PricingTemplateListDto(
                t.Id,
                t.Code,
                t.Name,
                t.TemplateType,
                t.Description,
                t.IsActive,
                t.DisplaySeq))
            .ToListAsync(cancellationToken);

        return new GetPricingTemplatesResult(templates);
    }
}
