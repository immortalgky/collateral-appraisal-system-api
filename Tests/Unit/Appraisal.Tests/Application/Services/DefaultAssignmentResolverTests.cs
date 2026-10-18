using Appraisal.Application.Features.Assignments.GetAssignmentDefaults;
using Appraisal.Application.Services;

namespace Appraisal.Tests.Application.Services;

public class DefaultAssignmentResolverTests
{
    private static AssignmentDefaultsSnapshot Snapshot(string appraisalType, AssignmentDefaultsEntry entry) =>
        new(new Dictionary<string, AssignmentDefaultsEntry> { [appraisalType] = entry });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void Unknown_or_missing_type_gets_the_baseline(string? appraisalType)
    {
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("New", new AssignmentDefaultsEntry(true, "Internal", "Manual", "Manual")), appraisalType);

        Assert.Equal(AssignmentDefaultValues.Baseline, resolved);
    }

    [Fact]
    public void Fields_the_entry_leaves_out_fall_back_to_the_baseline_one_by_one()
    {
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("New", new AssignmentDefaultsEntry(true, null, "RoundRobin", null)), "New");

        Assert.Equal("External", resolved.AssignmentType);
        Assert.Equal("RoundRobin", resolved.AssignmentMethod);
        Assert.Equal("RoundRobin", resolved.InternalFollowupAssignmentMethod);
    }

    [Fact]
    public void Lookup_ignores_casing()
    {
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("ReAppraisal", new AssignmentDefaultsEntry(true, "External", "Manual", "Manual")), "reappraisal");

        Assert.Equal("Manual", resolved.AssignmentMethod);
    }

    [Fact]
    public void Quotation_is_downgraded_to_manual_when_the_type_is_not_external()
    {
        // Type set alone to Internal: the method falls back to the Quotation baseline, then gets downgraded.
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("New", new AssignmentDefaultsEntry(true, "Internal", null, null)), "New");

        Assert.Equal("Internal", resolved.AssignmentType);
        Assert.Equal("Manual", resolved.AssignmentMethod);
    }

    [Fact]
    public void Quotation_is_kept_for_external()
    {
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("New", new AssignmentDefaultsEntry(true, "External", "Quotation", null)), "New");

        Assert.Equal("Quotation", resolved.AssignmentMethod);
    }

    [Fact]
    public void Followup_method_is_still_returned_for_internal()
    {
        var resolved = DefaultAssignmentResolver.Resolve(
            Snapshot("New", new AssignmentDefaultsEntry(true, "Internal", "Manual", "Manual")), "New");

        Assert.Equal("Manual", resolved.InternalFollowupAssignmentMethod);
    }
}
