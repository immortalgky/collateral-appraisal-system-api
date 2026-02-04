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
    public string SurveyName { get; set; } = null!;
    public DateTime? InfoDateTime { get; set; }
    public string? SourceInfo { get; set; }
    public string? Notes { get; set; }
    public DateTime? CreatedOn { get; set; }
}