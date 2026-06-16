using Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Tests for <see cref="PricingGroupValidator"/> — the pure pricing-analysis pre-flight rules.
/// </summary>
public class PricingGroupValidatorTests
{
    // Builds a fully-valid property of the given type (all relevant details present).
    private static PricingValidationProperty ValidProperty(int seq, string typeCode) =>
        new(
            SequenceNumber: seq,
            TypeCode: typeCode,
            HasBuildingDetail: true,
            HasRentalSchedule: true);

    private static PricingValidationStep Step(ValidateGroupForPricingResult r, string key) =>
        r.Steps.Single(s => s.Key == key);

    // ── Rule 1: group must contain at least one property ──────────────────────

    [Fact]
    public void EmptyGroup_FailsWithHasProperties()
    {
        var result = PricingGroupValidator.Evaluate([], surveyCount: 5);

        Assert.False(result.Valid);
        Assert.Equal(PricingValidationStatus.Failed, Step(result, "HasProperties").Status);
    }

    // ── Rule 2: maker survey ──────────────────────────────────────────────────

    [Fact]
    public void NoSurvey_FailsMakerSurvey()
    {
        var result = PricingGroupValidator.Evaluate(
            [ValidProperty(1, "L")], surveyCount: 0);

        Assert.False(result.Valid);
        Assert.Equal(PricingValidationStatus.Failed, Step(result, "MakerSurvey").Status);
    }

    [Fact]
    public void WithSurvey_PassesMakerSurvey()
    {
        var result = PricingGroupValidator.Evaluate(
            [ValidProperty(1, "L")], surveyCount: 1);

        Assert.Equal(PricingValidationStatus.Passed, Step(result, "MakerSurvey").Status);
    }

    // ── Rule 3: building detail ───────────────────────────────────────────────

    [Fact]
    public void BuildingWithoutBuildingDetail_FailsBuildingDetail()
    {
        var building = ValidProperty(1, "B") with { HasBuildingDetail = false };

        var result = PricingGroupValidator.Evaluate([building], surveyCount: 1);

        Assert.False(result.Valid);
        var step = Step(result, "BuildingDetail");
        Assert.Equal(PricingValidationStatus.Failed, step.Status);
        Assert.Single(step.Messages);
    }

    [Fact]
    public void NoBuildingProperty_SkipsBuildingDetail()
    {
        var result = PricingGroupValidator.Evaluate(
            [ValidProperty(1, "L")], surveyCount: 1);

        Assert.Equal(PricingValidationStatus.Skipped, Step(result, "BuildingDetail").Status);
    }

    // ── Rule 4: rental schedule (lease types) ─────────────────────────────────

    [Theory]
    [InlineData("LS")]
    [InlineData("LSL")]
    [InlineData("LSU")]
    [InlineData("LSB")]
    public void LeaseWithoutRentalSchedule_FailsRentalSchedule(string typeCode)
    {
        var lease = ValidProperty(1, typeCode) with { HasRentalSchedule = false };

        var result = PricingGroupValidator.Evaluate([lease], surveyCount: 1);

        Assert.False(result.Valid);
        Assert.Equal(PricingValidationStatus.Failed, Step(result, "RentalSchedule").Status);
    }

    [Fact]
    public void NonLeaseProperty_SkipsRentalSchedule()
    {
        var result = PricingGroupValidator.Evaluate(
            [ValidProperty(1, "U")], surveyCount: 1);

        Assert.Equal(PricingValidationStatus.Skipped, Step(result, "RentalSchedule").Status);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void FullyPopulatedGroup_IsValid()
    {
        var properties = new List<PricingValidationProperty>
        {
            ValidProperty(1, "B"),
            ValidProperty(2, "LS"),
            ValidProperty(3, "L"),
        };

        var result = PricingGroupValidator.Evaluate(properties, surveyCount: 2);

        Assert.True(result.Valid);
        Assert.DoesNotContain(result.Steps, s => s.Status == PricingValidationStatus.Failed);
    }
}
