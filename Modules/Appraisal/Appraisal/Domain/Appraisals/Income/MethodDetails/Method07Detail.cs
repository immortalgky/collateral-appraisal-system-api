using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 07 — Room Cost Based On Expense Per Room Per Day.</summary>
public sealed record Method07Detail
{
    [JsonPropertyName("roomDetails")]
    public RoomDetail[] RoomDetails { get; init; } = [];

    [JsonPropertyName("sumSaleableArea")]
    public decimal SumSaleableArea { get; init; }

    [JsonPropertyName("sumTotalRoomExpensePerDay")]
    public decimal SumTotalRoomExpensePerDay { get; init; }

    [JsonPropertyName("sumTotalRoomExpensePerYear")]
    public decimal SumTotalRoomExpensePerYear { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("saleableArea")]
    public decimal[] SaleableArea { get; init; } = [];

    [JsonPropertyName("roomRateIncrease")]
    public decimal[] RoomRateIncrease { get; init; } = [];

    [JsonPropertyName("roomExpense")]
    public decimal[] RoomExpense { get; init; } = [];

    public sealed record RoomDetail
    {
        [JsonPropertyName("roomType")]
        public string? RoomType { get; init; }

        [JsonPropertyName("roomTypeOther")]
        public string? RoomTypeOther { get; init; }

        [JsonPropertyName("roomExpensePerDay")]
        public decimal RoomExpensePerDay { get; init; }

        [JsonPropertyName("saleableArea")]
        public decimal SaleableArea { get; init; }

        [JsonPropertyName("totalRoomExpensePerDay")]
        public decimal TotalRoomExpensePerDay { get; init; }

        [JsonPropertyName("totalRoomExpensePerYear")]
        public decimal TotalRoomExpensePerYear { get; init; }
    }
    [JsonPropertyName("startIn")]
    public int StartIn { get; init; }
}
