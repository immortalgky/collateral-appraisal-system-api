using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 06 — Rental Income Per Square Meter (rent/sqm × area × 12 × occupancy).</summary>
public sealed record Method06Detail
{
    [JsonPropertyName("areaDetail")]
    public AreaDetail AreaDetails { get; init; } = new();

    [JsonPropertyName("sumRentalPrice")]
    public decimal SumRentalPrice { get; init; }

    [JsonPropertyName("sumSaleableArea")]
    public decimal SumSaleableArea { get; init; }

    [JsonPropertyName("sumTotalRentalIncomePerMonth")]
    public decimal SumTotalRentalIncomePerMonth { get; init; }

    [JsonPropertyName("sumTotalRentalIncomePerYear")]
    public decimal SumTotalRentalIncomePerYear { get; init; }

    [JsonPropertyName("totalSaleableArea")]
    public decimal TotalSaleableArea { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    [JsonPropertyName("avgRentalRatePerMonth")]
    public decimal AvgRentalRatePerMonth { get; init; }

    [JsonPropertyName("occupancyRateFirstYearPct")]
    public decimal OccupancyRateFirstYearPct { get; init; }

    [JsonPropertyName("occupancyRatePct")]
    public decimal OccupancyRatePct { get; init; }

    [JsonPropertyName("occupancyRateYrs")]
    public int OccupancyRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("occupancyRate")]
    public decimal[] OccupancyRate { get; init; } = [];

    [JsonPropertyName("totalSaleableAreaDeductByOccRate")]
    public decimal[] TotalSaleableAreaDeductByOccRate { get; init; } = [];

    [JsonPropertyName("rentalRateIncrease")]
    public decimal[] RentalRateIncrease { get; init; } = [];

    [JsonPropertyName("avgRentalRate")]
    public decimal[] AvgRentalRate { get; init; } = [];

    [JsonPropertyName("totalRentalIncome")]
    public decimal[] TotalRentalIncome { get; init; } = [];

    public sealed record AreaDetail
    {
        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("rentalPrice")]
        public decimal RentalPrice { get; init; }

        [JsonPropertyName("saleableArea")]
        public decimal SaleableArea { get; init; }

        [JsonPropertyName("totalRentalIncomePerMonth")]
        public decimal TotalRentalIncomePerMonth { get; init; }

        [JsonPropertyName("totalRentalIncomePerYear")]
        public decimal TotalRentalIncomePerYear { get; init; }
    }
}
