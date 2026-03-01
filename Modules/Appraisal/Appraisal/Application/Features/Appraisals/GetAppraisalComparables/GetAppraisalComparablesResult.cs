namespace Appraisal.Application.Features.Appraisals.GetAppraisalComparables;

public record GetAppraisalComparablesResult(List<AppraisalComparableDto> Comparables);

public record AppraisalComparableDto
{
    public Guid Id { get; set; }
    public Guid AppraisalId { get; set; }
    public Guid MarketComparableId { get; set; }
    public int SequenceNumber { get; set; }
    public decimal Weight { get; set; }
    public decimal OriginalPricePerUnit { get; set; }
    public decimal AdjustedPricePerUnit { get; set; }
    public decimal TotalAdjustmentPct { get; set; }
    public decimal WeightedValue { get; set; }
    public string? SelectionReason { get; set; }
    public string? Notes { get; set; }

    // MarketComparable info (from JOIN)
    public string ComparableNumber { get; set; } = default!;
    public string ComparablePropertyType { get; set; } = default!;
    public string ComparableSurveyName { get; set; } = default!;
    public DateTime? ComparableInfoDateTime { get; set; }
    public string? ComparableSourceInfo { get; set; }
    public decimal? ComparableOfferPrice { get; set; }
    public decimal? ComparableOfferPriceAdjustmentPercent { get; set; }
    public decimal? ComparableOfferPriceAdjustmentAmount { get; set; }
    public decimal? ComparableSalePrice { get; set; }
    public DateTime? ComparableSaleDate { get; set; }

    // Nested collection (loaded separately)
    public List<ComparableAdjustmentDto> Adjustments { get; set; } = [];
}

public record ComparableAdjustmentDto
{
    public Guid Id { get; set; }
    public Guid AppraisalComparableId { get; set; }
    public string AdjustmentCategory { get; set; } = default!;
    public string AdjustmentType { get; set; } = default!;
    public decimal AdjustmentPercent { get; set; }
    public string AdjustmentDirection { get; set; } = default!;
    public string? SubjectValue { get; set; }
    public string? ComparableValue { get; set; }
    public string? Justification { get; set; }
}
