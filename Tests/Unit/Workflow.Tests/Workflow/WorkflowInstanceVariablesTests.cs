using FluentAssertions;
using Workflow.Workflow.Models;

namespace Workflow.Tests.Workflow;

/// <summary>
/// Guards PART 5 / Fix B: <see cref="WorkflowInstance.UpdateVariables"/> must replace the dictionary
/// REFERENCE. The <c>Variables</c> property has a JSON value converter but no value comparer, so EF
/// detects changes by reference only — an in-place mutation would never be persisted by SaveChanges
/// (the bug that dropped the appointmentDate write so appointment-anchored SLAs went null).
/// </summary>
public class WorkflowInstanceVariablesTests
{
    private static WorkflowInstance NewInstance(Dictionary<string, object>? vars = null) =>
        WorkflowInstance.Create(
            Guid.NewGuid(), "test-workflow", correlationId: Guid.NewGuid().ToString(),
            startedBy: "tester", initialVariables: vars);

    [Fact]
    public void UpdateVariables_ReplacesReference_AndPreservesExistingKeys()
    {
        var appointment = new DateTime(2026, 7, 3, 17, 30, 0);
        var instance = NewInstance(new Dictionary<string, object> { ["bankingSegment"] = "Retail" });
        var before = instance.Variables;

        instance.UpdateVariables(new Dictionary<string, object> { ["appointmentDate"] = appointment });

        instance.Variables.Should().NotBeSameAs(before,
            "a new reference is required for EF to detect the change (Variables has no value comparer)");
        instance.Variables["appointmentDate"].Should().Be(appointment);
        instance.Variables["bankingSegment"].Should().Be("Retail", "existing keys must be preserved");
    }
}
