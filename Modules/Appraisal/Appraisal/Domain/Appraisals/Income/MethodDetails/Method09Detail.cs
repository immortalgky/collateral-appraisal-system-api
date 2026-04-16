using System.Text.Json.Serialization;

namespace Appraisal.Domain.Appraisals.Income.MethodDetails;

/// <summary>Method 09 — Position-Based Salary (headcount × monthly × 12).</summary>
public sealed record Method09Detail
{
    [JsonPropertyName("jobPositionDetails")]
    public JobPositionDetail[] JobPositionDetails { get; init; } = [];

    [JsonPropertyName("sumSalaryBahtPerPersonPerMonth")]
    public decimal SumSalaryBahtPerPersonPerMonth { get; init; }

    [JsonPropertyName("sumTotalSalaryPerYear")]
    public decimal SumTotalSalaryPerYear { get; init; }

    [JsonPropertyName("increaseRatePct")]
    public decimal IncreaseRatePct { get; init; }

    [JsonPropertyName("increaseRateYrs")]
    public int IncreaseRateYrs { get; init; }

    // Table (year-indexed arrays)
    [JsonPropertyName("increaseRate")]
    public decimal[] IncreaseRate { get; init; } = [];

    [JsonPropertyName("totalPositionBasedSalaryPerYear")]
    public decimal[] TotalPositionBasedSalaryPerYear { get; init; } = [];

    public sealed record JobPositionDetail
    {
        [JsonPropertyName("jobPosition")]
        public string JobPosition { get; init; } = string.Empty;

        [JsonPropertyName("jobPositionOther")]
        public string? JobPositionOther { get; init; }

        [JsonPropertyName("salaryBahtPerPersonPerMonth")]
        public decimal SalaryBahtPerPersonPerMonth { get; init; }

        [JsonPropertyName("numberOfEmployees")]
        public int NumberOfEmployees { get; init; }

        [JsonPropertyName("totalSalaryPerYear")]
        public decimal TotalSalaryPerYear { get; init; }
    }
}
