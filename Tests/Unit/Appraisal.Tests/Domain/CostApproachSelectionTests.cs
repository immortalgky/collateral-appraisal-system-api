using Appraisal.Domain.Appraisals;
using Shared.Exceptions;

namespace Appraisal.Tests.Domain;

/// <summary>
/// A Cost approach sums its selected methods, so a building counted by two selected methods
/// inflates the appraised value that reaches the book and LOS. These pin the selection rules
/// that keep the building counted exactly once.
/// </summary>
public class CostApproachSelectionTests
{
    private static (PricingAnalysisApproach Cost, PricingAnalysisMethod Wqs, PricingAnalysisMethod Sag) CostWithTwoLandMethods()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var cost = pa.AddApproach("Cost");
        return (cost, cost.AddMethod("WQS"), cost.AddMethod("SaleGrid"));
    }

    [Fact]
    public void Linking_an_unselected_method_keeps_the_selected_BuildingCost()
    {
        var (cost, wqs, sag) = CostWithTwoLandMethods();
        var bc = cost.LinkOrCreateBuildingCostMethod(sag.Id, 3_000_000m);
        cost.UnlinkBuildingCostMethod(sag.Id);          // SAG unselected → BC stays as it was
        cost.SelectMethod(wqs.Id);
        cost.SelectMethod(bc.Id);                        // Land (WQS) + Building (BC)

        cost.LinkOrCreateBuildingCostMethod(sag.Id, 3_000_000m);   // alternative, not selected

        Assert.True(bc.IsSelected);
        Assert.False(sag.IsSelected);
    }

    [Fact]
    public void Unlinking_an_unselected_method_does_not_reselect_BuildingCost_under_a_linked_selection()
    {
        var (cost, wqs, sag) = CostWithTwoLandMethods();
        cost.SelectMethod(wqs.Id);
        var bc = cost.LinkOrCreateBuildingCostMethod(wqs.Id, 3_000_000m);   // WQS selected → BC out
        cost.LinkOrCreateBuildingCostMethod(sag.Id, 3_000_000m);

        cost.UnlinkBuildingCostMethod(sag.Id);

        Assert.False(bc.IsSelected);
        Assert.True(wqs.IsSelected);
    }

    [Fact]
    public void Unlinking_the_selected_method_brings_BuildingCost_back()
    {
        var (cost, wqs, _) = CostWithTwoLandMethods();
        cost.SelectMethod(wqs.Id);
        var bc = cost.LinkOrCreateBuildingCostMethod(wqs.Id, 3_000_000m);

        cost.UnlinkBuildingCostMethod(wqs.Id);

        Assert.True(bc.IsSelected);
        Assert.Equal("Land", wqs.Role);
    }

    [Fact]
    public void Selecting_a_linked_method_deselects_its_BuildingCost()
    {
        var (cost, wqs, sag) = CostWithTwoLandMethods();
        cost.SelectMethod(sag.Id);
        var bc = cost.LinkOrCreateBuildingCostMethod(wqs.Id, 3_000_000m);   // WQS unselected → BC kept
        cost.SelectMethod(bc.Id);

        cost.SelectMethod(wqs.Id);   // LandAndBuilding replaces SAG (Land) and BC (linked)

        Assert.True(wqs.IsSelected);
        Assert.False(sag.IsSelected);
        Assert.False(bc.IsSelected);
    }

    [Fact]
    public void Selecting_BuildingCost_next_to_a_linked_selection_is_rejected()
    {
        var (cost, wqs, _) = CostWithTwoLandMethods();
        cost.SelectMethod(wqs.Id);
        var bc = cost.LinkOrCreateBuildingCostMethod(wqs.Id, 3_000_000m);

        Assert.Throws<DomainException>(() => cost.SelectMethod(bc.Id));
    }

    [Fact]
    public void Removing_one_selected_Cost_component_keeps_the_rest_and_rederives_the_value()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var cost = pa.AddApproach("Cost");
        var land = cost.AddMethod("WQS");
        var machinery = cost.AddMethod("MachineryCost");
        land.SetValue(2_000_000m, null, "PerUnit");
        machinery.SetValue(500_000m, null, "PerUnit");
        pa.SelectMethod(land.Id);
        pa.SelectMethod(machinery.Id);
        pa.SelectApproach(cost.Id);

        var removed = pa.TryRemoveSelectedCostComponent(cost.Id, machinery.Id);

        Assert.True(removed);
        Assert.True(cost.IsSelected);
        Assert.Equal(2_000_000m, cost.ApproachValue);
        Assert.Equal(2_000_000m, pa.FinalAppraisedValue);
    }

    [Fact]
    public void Reverting_an_unlinked_LandAndBuilding_method_brings_BuildingCost_back()
    {
        var (cost, wqs, _) = CostWithTwoLandMethods();
        var bc = cost.AddMethod("BuildingCost");
        cost.SetMethodRole(wqs.Id, "LandAndBuilding");   // data-fix shape: tagged, never linked
        cost.SelectMethod(wqs.Id);

        cost.RevertToLand(wqs.Id);

        Assert.Equal("Land", wqs.Role);
        Assert.True(wqs.IsSelected);
        Assert.True(bc.IsSelected);
    }

    [Fact]
    public void Resetting_one_selected_Cost_component_keeps_the_rest()
    {
        var pa = PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());
        var cost = pa.AddApproach("Cost");
        var land = cost.AddMethod("WQS");
        var bc = cost.AddMethod("BuildingCost");
        land.SetValue(2_000_000m, null, "PerUnit");
        bc.SetValue(3_000_000m, null, "PerUnit");
        pa.SelectMethod(land.Id);
        pa.SelectMethod(bc.Id);
        pa.SelectApproach(cost.Id);

        land.Reset();
        var kept = pa.TryRederiveCostAfterComponentReset(cost.Id);

        Assert.True(kept);
        Assert.True(cost.IsSelected);
        Assert.Equal(3_000_000m, pa.FinalAppraisedValue);
    }
}
