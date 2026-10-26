using Appraisal.Application.Features.DecisionSummary.SaveDecisionSummary;

namespace Appraisal.Tests.Application.Features;

/// <summary>Block Book Verification: the typed appraised total scales the units' forced sale by their own ratio.</summary>
public class BlockForcedSaleReviewTests
{
    [Fact]
    public void TypedTotalEqualToTheUnitTotal_GivesExactlyTheUnitSum()
    {
        // Not a multiple of 1,000 on purpose: an imported figure is kept as stored.
        Assert.Equal(7_000_000.55m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(10_000_000m, 10_000_000m, 7_000_000.55m, 70m));
    }

    [Fact]
    public void NineMillionTypedOnATenMillionBlockWithSevenMillionForcedSale_GivesSixPointThreeMillion()
    {
        Assert.Equal(6_300_000m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(9_000_000m, 10_000_000m, 7_000_000m, 70m));
    }

    [Fact]
    public void TheScaledFigureIsRoundedToTheNearestThousand()
    {
        // 9,999,999 x 0.7 = 6,999,999.3 -> 7,000,000
        Assert.Equal(7_000_000m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(9_999_999m, 10_000_000m, 7_000_000m, 70m));
    }

    [Fact]
    public void ARepeatingRatio_DoesNotPushAnExactHalfBelowTheHalf()
    {
        // 6,750,000 x 4,100,000 / 6,000,000 = 4,612,500 exactly -> 4,613,000 (a ratio-first 0.68333... gives 4,612,000).
        Assert.Equal(4_613_000m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(6_750_000m, 6_000_000m, 4_100_000m, 70m));
    }

    [Fact]
    public void WithoutAUnitTotal_TheEffectivePercentageStandsIn()
    {
        Assert.Equal(4_500_000m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(9_000_000m, 0m, 0m, 50m));
        Assert.Equal(0m,
            SaveDecisionSummaryCommandHandler.BlockForcedSaleReview(9_000_000m, 0m, 0m, 0m));   // 0% is real
    }
}
