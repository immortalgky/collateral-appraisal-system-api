using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 02 — Room Income By Seasonal Rates (per room type split by season).</summary>
public sealed record Method02Detail
{
    [JsonPropertyName("seasonCount")]
    public int SeasonCount { get; init; }

    [JsonPropertyName("seasonDetails")]
    public SeasonDetail[] SeasonDetails { get; init; } = [];

    [JsonPropertyName("roomDetails")]
    public RoomIncomeRow[] RoomDetails { get; init; } = [];

    [JsonPropertyName("avgRoomRate")]
    public decimal AvgRoomRate { get; init; }

    [JsonPropertyName("totalSaleableArea")]
    public decimal TotalSaleableArea { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    [JsonPropertyName("occupancyRateFirstYearPct")]
    public decimal OccupancyRateFirstYearPct { get; init; }

    [JsonPropertyName("occupancyRatePct")]
    public decimal OccupancyRatePct { get; init; }

    [JsonPropertyName("occupancyRateYrs")]
    public int OccupancyRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("saleableArea")]
    public decimal[] SaleableArea { get; init; } = [];

    [JsonPropertyName("occupancyRate")]
    public decimal[] OccupancyRate { get; init; } = [];

    [JsonPropertyName("totalSaleableAreaDeductByOccRate")]
    public decimal[] TotalSaleableAreaDeductByOccRate { get; init; } = [];

    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("avgDailyRate")]
    public decimal[] AvgDailyRate { get; init; } = [];

    [JsonPropertyName("roomIncome")]
    public decimal[] RoomIncome { get; init; } = [];

    [JsonPropertyName("startIn")]
    public int StartIn { get; init; } = 1;

    public sealed record SeasonDetail
    {
        [JsonPropertyName("seasonName")]
        public string SeasonName { get; init; } = string.Empty;

        [JsonPropertyName("numberOfMonths")]
        public int NumberOfMonths { get; init; }

        [JsonPropertyName("description")]
        public string Description { get; init; } = string.Empty;

        [JsonPropertyName("avgTotalRoomIncomePerDay")]
        public decimal AvgTotalRoomIncomePerDay { get; init; }

        [JsonPropertyName("avgTotalRoomIncomePerSeason")]
        public decimal AvgTotalRoomIncomePerSeason { get; init; }
    }

    public sealed record SeasonRateInput
    {
        [JsonPropertyName("seasonId")]
        public string SeasonId { get; init; } = string.Empty;

        [JsonPropertyName("roomIncome")]
        public decimal? RoomIncome { get; init; }

        [JsonPropertyName("saleableArea")]
        public decimal? SaleableArea { get; init; }
    }

    public sealed record RoomIncomeRow
    {
        [JsonPropertyName("id")]
        public string Id { get; init; } = string.Empty;

        [JsonPropertyName("roomType")]
        public string RoomType { get; init; } = string.Empty;

        [JsonPropertyName("roomTypeOther")]
        public string? RoomTypeOther { get; init; }

        [JsonPropertyName("seasons")]
        public SeasonRateInput[] Seasons { get; init; } = [];
    }
}
