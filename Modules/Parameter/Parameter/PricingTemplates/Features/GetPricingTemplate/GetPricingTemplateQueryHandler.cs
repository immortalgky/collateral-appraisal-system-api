using Parameter.Contracts.PricingTemplates;
using Parameter.PricingTemplates.Models;

namespace Parameter.PricingTemplates.Features.GetPricingTemplate;

public class GetPricingTemplateQueryHandler(
    ParameterDbContext context
) : IRequestHandler<GetPricingTemplateQuery, GetPricingTemplateResult>
{
    public async Task<GetPricingTemplateResult> Handle(
        GetPricingTemplateQuery query,
        CancellationToken cancellationToken)
    {
        var template = await context.PricingTemplates
            .Include(t => t.Sections.OrderBy(s => s.DisplaySeq))
            .ThenInclude(s => s.Categories.OrderBy(c => c.DisplaySeq))
            .ThenInclude(c => c.Assumptions.OrderBy(a => a.DisplaySeq))
            .FirstOrDefaultAsync(t => t.Code == query.Code, cancellationToken);

        if (template is null)
            throw new NotFoundException("PricingTemplate", query.Code);

        var dto = new PricingTemplateDto(
            template.Id,
            template.Code,
            template.Name,
            template.TemplateType,
            template.Description,
            template.TotalNumberOfYears,
            template.TotalNumberOfDayInYear,
            template.CapitalizeRate,
            template.DiscountedRate,
            template.IsActive,
            template.DisplaySeq,
            template.Sections.Select(s => new PricingTemplateSectionDto(
                s.Id,
                s.SectionType,
                s.SectionName,
                s.Identifier,
                s.DisplaySeq,
                s.Categories.Select(c => new PricingTemplateCategoryDto(
                    c.Id,
                    c.CategoryType,
                    c.CategoryName,
                    c.Identifier,
                    c.DisplaySeq,
                    c.Assumptions.Select(a => new PricingTemplateAssumptionDto(
                        a.Id,
                        a.AssumptionType,
                        a.AssumptionName,
                        a.Identifier,
                        a.DisplaySeq,
                        a.MethodTypeCode,
                        a.MethodDetailJson
                    )).ToList()
                )).ToList()
            )).ToList()
        );

        return new GetPricingTemplateResult(dto);
    }
}
