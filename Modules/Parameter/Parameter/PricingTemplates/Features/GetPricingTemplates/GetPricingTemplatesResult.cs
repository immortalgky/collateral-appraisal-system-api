namespace Parameter.PricingTemplates.Features.GetPricingTemplates;

public record GetPricingTemplatesResult(List<PricingTemplateListDto> Templates);

public record PricingTemplateListDto(
    Guid Id,
    string Code,
    string Name,
    string TemplateType,
    string? Description,
    bool IsActive,
    int DisplaySeq);
