namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplates;

public record GetMarketComparableTemplatesResult(List<MarketComparableTemplateDto> Templates);

public record MarketComparableTemplateDto(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    DateTime? CreatedOn,
    DateTime? UpdatedOn
);
