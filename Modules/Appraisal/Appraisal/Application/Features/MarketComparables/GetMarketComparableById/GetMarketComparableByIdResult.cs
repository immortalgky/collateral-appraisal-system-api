namespace Appraisal.Application.Features.MarketComparables.GetMarketComparableById;

/// <summary>
/// Result of getting a market comparable by ID
/// </summary>
public record GetMarketComparableByIdResult(MarketComparableDetailDto MarketComparable);

/// <summary>
/// Detailed DTO for a market comparable including factor data and images
/// </summary>
public record MarketComparableDetailDto
{
    public Guid Id { get; set; }
    public string ComparableNumber { get; set; } = null!;
    public string PropertyType { get; set; } = null!;
    public string SurveyName { get; set; } = null!;

    // Data Information
    public DateTime? InfoDateTime { get; set; }
    public string? SourceInfo { get; set; }

    // Notes
    public string? Notes { get; set; }

    // Template Reference
    public Guid? TemplateId { get; set; }

    // Audit
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }

    // Child collections
    public List<FactorDataDto> FactorData { get; set; } = [];
    public List<ImageDto> Images { get; set; } = [];
}

/// <summary>
/// DTO for factor data item
/// </summary>
public record FactorDataDto
{
    public Guid Id { get; set; }
    public Guid FactorId { get; set; }
    public string FactorCode { get; set; } = default!;
    public string FactorName { get; set; } = default!;
    public string FieldName { get; set; } = default!;
    public FactorDataType DataType { get; set; }
    public int? FieldLength { get; set; }
    public int? FieldDecimal { get; set; }
    public string? ParameterGroup { get; set; }
    public string? Value { get; set; }
    public string? OtherRemarks { get; set; }
}

/// <summary>
/// DTO for image item
/// </summary>
public record ImageDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public int DisplaySequence { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}
