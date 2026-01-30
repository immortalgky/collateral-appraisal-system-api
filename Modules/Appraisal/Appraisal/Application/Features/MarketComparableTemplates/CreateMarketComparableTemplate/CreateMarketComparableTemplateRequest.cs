namespace Appraisal.Application.Features.MarketComparableTemplates.CreateMarketComparableTemplate;

public record CreateMarketComparableTemplateRequest(
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description
);
