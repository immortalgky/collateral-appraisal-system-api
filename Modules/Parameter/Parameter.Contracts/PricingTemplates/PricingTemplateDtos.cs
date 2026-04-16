namespace Parameter.Contracts.PricingTemplates;

public record GetPricingTemplateResult(PricingTemplateDto Template);

public record PricingTemplateDto(
    Guid Id,
    string Code,
    string Name,
    string TemplateType,
    string? Description,
    int TotalNumberOfYears,
    int TotalNumberOfDayInYear,
    decimal CapitalizeRate,
    decimal DiscountedRate,
    bool IsActive,
    int DisplaySeq,
    List<PricingTemplateSectionDto> Sections);

public record PricingTemplateSectionDto(
    Guid Id,
    string SectionType,
    string SectionName,
    string Identifier,
    int DisplaySeq,
    List<PricingTemplateCategoryDto> Categories);

public record PricingTemplateCategoryDto(
    Guid Id,
    string CategoryType,
    string CategoryName,
    string Identifier,
    int DisplaySeq,
    List<PricingTemplateAssumptionDto> Assumptions);

public record PricingTemplateAssumptionDto(
    Guid Id,
    string AssumptionType,
    string AssumptionName,
    string Identifier,
    int DisplaySeq,
    string MethodTypeCode,
    string MethodDetailJson);
