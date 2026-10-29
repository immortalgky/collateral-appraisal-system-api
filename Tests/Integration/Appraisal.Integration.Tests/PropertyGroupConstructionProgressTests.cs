using Appraisal.Application.Features.Appraisals.GetPropertyGroupById;
using Appraisal.Domain.Appraisals;
using Integration.Fixtures;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// The property card's construction progress (facts SQL of GetPropertyGroupById): in summary mode it is the round's own
/// figure, or null while not keyed — never a made-up 0%.
/// </summary>
[Collection("Integration")]
public class PropertyGroupConstructionProgressTests(IntegrationTestFixture fixture) : AffectedHandlersBase(fixture)
{
    private async Task<decimal?> ProgressAsync(bool full, decimal? summaryCurrent, decimal? workRowCurrent = null)
    {
        var (aid, groups, _) = await SeedGroupedAsync(a =>
        {
            var p = a.AddBuildingProperty();
            p.BuildingDetail!.Update(ownerName: "owner", isUnderConstruction: true);
            var ci = full
                ? ConstructionInspection.CreateFullDetail(Guid.Empty, 1_000_000m, null)
                : ConstructionInspection.CreateSummary(Guid.Empty, 1_000_000m, null, 10m, summaryCurrent, null);
            if (full)
            {
                ci.AddWorkDetail(Guid.NewGuid(), "item", 1, 100m, 0m, workRowCurrent ?? 0m);
                ci.ComputeAllValues();
            }
            p.SetConstructionInspection(ci);
            return [p];
        });

        var result = await SendAsync(new GetPropertyGroupByIdQuery(aid, groups[0]));

        return Assert.Single(result.Properties!).ConstructionProgressPct;
    }

    [Fact]
    public async Task Summary_round_not_yet_keyed_shows_no_percent_not_zero() =>
        Assert.Null(await ProgressAsync(full: false, summaryCurrent: null));

    [Fact]
    public async Task Summary_round_shows_its_own_figure() =>
        Assert.Equal(40m, await ProgressAsync(full: false, summaryCurrent: 40m));

    [Fact]
    public async Task Summary_round_keyed_at_zero_still_shows_zero() =>
        Assert.Equal(0m, await ProgressAsync(full: false, summaryCurrent: 0m));

    [Fact]
    public async Task Full_detail_round_is_the_sum_of_the_work_items() =>
        Assert.Equal(60m, await ProgressAsync(full: true, summaryCurrent: null, workRowCurrent: 60m));

    private async Task<bool> DevelopmentOnlyAsync(params bool[] depreciationIsBuilding)
    {
        var (aid, groups, _) = await SeedGroupedAsync(a =>
        {
            var p = a.AddBuildingProperty();
            p.BuildingDetail!.Update(ownerName: "owner");
            foreach (var isBuilding in depreciationIsBuilding)
                p.BuildingDetail!.AddDepreciationDetail("Period", isBuilding: isBuilding);
            return [p];
        });

        var result = await SendAsync(new GetPropertyGroupByIdQuery(aid, groups[0]));

        return Assert.Single(result.Properties!).IsDevelopmentOnly;
    }

    [Fact]
    public async Task Building_with_only_development_rows_is_development_only() =>
        Assert.True(await DevelopmentOnlyAsync(false, false));

    [Fact]
    public async Task Building_with_a_building_row_is_not_development_only() =>
        Assert.False(await DevelopmentOnlyAsync(false, true));

    [Fact]
    public async Task Building_with_no_depreciation_rows_is_not_development_only() =>
        Assert.False(await DevelopmentOnlyAsync());
}
