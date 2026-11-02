using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

public class AppraisalAssignmentAssignerTests
{
    private static AppraisalAssignment Assigned()
    {
        var row = AppraisalAssignment.Create(Guid.NewGuid(), "External", Guid.NewGuid().ToString(), "Manual", assignedBy: "system");
        row.Assign("External", assigneeCompanyId: Guid.NewGuid().ToString(), assignmentMethod: "Manual", assignedBy: "system");
        return row;
    }

    [Fact]
    public void ReturnToAssignmentQueue_MovesAnAssignedRowBackToPending()
    {
        var row = Assigned();

        row.ReturnToAssignmentQueue();

        Assert.Equal(AssignmentStatus.Pending, row.AssignmentStatus);
    }

    [Fact]
    public void ReturnToAssignmentQueue_MovesAnInProgressRowBackToPending()
    {
        var row = Assigned();
        row.StartWork();

        row.ReturnToAssignmentQueue();

        Assert.Equal(AssignmentStatus.Pending, row.AssignmentStatus);
    }

    [Fact]
    public void ReturnToAssignmentQueue_LeavesAPendingRowAndItsAdminAlone()
    {
        var row = AppraisalAssignment.Create(Guid.NewGuid(), "Internal", assignedBy: "System");
        row.SetAssigner("P5229");

        row.ReturnToAssignmentQueue();

        Assert.Equal(AssignmentStatus.Pending, row.AssignmentStatus);
        Assert.Equal("P5229", row.AssignedBy);
    }
}
