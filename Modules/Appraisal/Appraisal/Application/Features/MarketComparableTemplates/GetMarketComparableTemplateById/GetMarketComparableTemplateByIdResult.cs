namespace Appraisal.Application.Features.MarketComparableTemplates.GetMarketComparableTemplateById;

public record GetMarketComparableTemplateByIdResult(MarketComparableTemplateDetailDto Template);

public record MarketComparableTemplateDetailDto(
    Guid Id,
    string TemplateCode,
    string TemplateName,
    string PropertyType,
    string? Description,
    bool IsActive,
    List<TemplateFactorDto> Factors,
    DateTime? CreatedOn,
    DateTime? UpdatedOn
);

public record TemplateFactorDto(
    Guid TemplateFactorId,
    Guid FactorId,
    string FactorCode,
    string FieldName,
    string DataType,
    int? FieldLength,
    int? FieldDecimal,
    string? ParameterGroup,
    int DisplaySequence,
    bool IsMandatory,
    bool IsActive,
    IReadOnlyList<FactorTranslationDto> Translations
);

public record FactorTranslationDto(string Language, string FactorName);
