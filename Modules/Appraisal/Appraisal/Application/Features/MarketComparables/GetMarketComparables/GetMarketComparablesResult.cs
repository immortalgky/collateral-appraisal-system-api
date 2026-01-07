using Shared.Pagination;

namespace Appraisal.Application.Features.MarketComparables.GetMarketComparables;

/// <summary>
/// Result of getting all Market Comparables
/// </summary>
public record GetMarketComparablesResult(PaginatedResult<MarketComparableDto> Result);

/// <summary>
/// DTO for Market Comparable list item
/// </summary>
public record MarketComparableDto
{
    public Guid Id { get; set; }
    public string ComparableNumber { get; set; } = null!;
    public string PropertyType { get; set; } = null!;
    public string Province { get; set; } = null!;
    public string? District { get; set; }
    public string? SubDistrict { get; set; }
    public string? Address { get; set; }
    public string? TransactionType { get; set; }
    public DateTime? TransactionDate { get; set; }
    public decimal? TransactionPrice { get; set; }
    public decimal? PricePerUnit { get; set; }
    public string? UnitType { get; set; }
    public string DataSource { get; set; } = null!;
    public string? DataConfidence { get; set; }
    public bool IsVerified { get; set; }
    public string Status { get; set; } = null!;
    public DateTime SurveyDate { get; set; }
    public DateTime? CreatedOn { get; set; }
}