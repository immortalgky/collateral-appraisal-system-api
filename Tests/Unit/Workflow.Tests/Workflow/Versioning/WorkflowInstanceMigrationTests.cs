using FluentAssertions;
using Workflow.Workflow.Models;
using Xunit;

namespace Workflow.Tests.Workflow.Versioning;

public class WorkflowInstanceMigrationTests
{
    [Fact]
    public void MigrateToVersion_RunningInstanceNoRemap_UpdatesVersionIdOnly()
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "wf", null, "tester");
        instance.SetCurrentActivity("a");
        var newVersionId = Guid.NewGuid();

        instance.MigrateToVersion(newVersionId, remappedCurrentActivityId: null);

        instance.WorkflowDefinitionVersionId.Should().Be(newVersionId);
        instance.CurrentActivityId.Should().Be("a");
    }

    [Fact]
    public void MigrateToVersion_WithRemap_RewritesCurrentActivityId()
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "wf", null, "tester");
        instance.SetCurrentActivity("a");
        var newVersionId = Guid.NewGuid();

        instance.MigrateToVersion(newVersionId, remappedCurrentActivityId: "a_new");

        instance.WorkflowDefinitionVersionId.Should().Be(newVersionId);
        instance.CurrentActivityId.Should().Be("a_new");
    }

    [Fact]
    public void MigrateToVersion_NotRunning_Throws()
    {
        var instance = WorkflowInstance.Create(Guid.NewGuid(), Guid.NewGuid(), "wf", null, "tester");
        instance.UpdateStatus(WorkflowStatus.Completed);

        var act = () => instance.MigrateToVersion(Guid.NewGuid(), null);

        act.Should().Throw<InvalidOperationException>();
    }
}
