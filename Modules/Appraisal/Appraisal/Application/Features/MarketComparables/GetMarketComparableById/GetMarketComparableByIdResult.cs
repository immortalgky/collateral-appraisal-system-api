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

    // Location
    public string Province { get; set; } = null!;
    public string? District { get; set; }
    public string? SubDistrict { get; set; }
    public string? Address { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Transaction
    public string? TransactionType { get; set; }
    public DateTime? TransactionDate { get; set; }
    public decimal? TransactionPrice { get; set; }
    public decimal? PricePerUnit { get; set; }
    public string? UnitType { get; set; }

    // Data Quality
    public string DataSource { get; set; } = null!;
    public string? DataConfidence { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public Guid? VerifiedBy { get; set; }

    // Status
    public string Status { get; set; } = null!;
    public DateTime? ExpiryDate { get; set; }

    // Survey
    public DateTime SurveyDate { get; set; }
    public Guid? SurveyedBy { get; set; }

    // Notes
    public string? Description { get; set; }
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
