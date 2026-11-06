using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// WQS weighted score must equal the screen: round2(round2(Weight x Intensity) x Score / 10).
/// Sale Adjustment Grid / Direct Comparison send Intensity = null and keep Weight x Score.
/// </summary>
public class PricingFactorScoreWeightedScoreTests
{
    private static PricingFactorScore Row(decimal weight, string? value, decimal? score, decimal? intensity)
    {
        var row = PricingFactorScore.Create(Guid.NewGuid(), Guid.NewGuid(), weight, 1);
        row.SetValues(value, score, intensity);
        return row;
    }

    [Fact]
    public void Wqs_weighted_score_applies_intensity_over_max_score_of_10()
    {
        Assert.Equal(8.00m, Row(2m, null, 8m, 5m).WeightedScore);
    }

    [Fact]
    public void Wqs_intensity_10_equals_legacy_weight_times_score()
    {
        Assert.Equal(2m * 8m, Row(2m, null, 8m, 10m).WeightedScore);
    }

    [Fact]
    public void Wqs_rounds_each_step_to_2_decimals_like_the_screen()
    {
        // round2(2.35 x 3) = 7.05 ; 7.05 x 7 / 10 = 4.935 -> 4.94 (half up)
        Assert.Equal(4.94m, Row(2.35m, null, 7m, 3m).WeightedScore);
    }

    [Fact]
    public void Wqs_weight_update_recomputes_with_stored_intensity()
    {
        var row = Row(2m, null, 8m, 50m);
        row.UpdateWeight(3m);
        Assert.Equal(120m, row.WeightedScore);
    }

    [Fact]
    public void Without_intensity_weighted_score_stays_weight_times_score_and_null_score_gives_null()
    {
        Assert.Equal(16m, Row(2m, "x", 8m, null).WeightedScore); // SaleGrid / DirectComparison
        Assert.Null(Row(2m, "x", null, 5m).WeightedScore);
    }
}
