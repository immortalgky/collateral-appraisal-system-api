using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

public class AppraisalAssignmentAssignerTests
{
    [Fact]
    public void ReturningToTheQueue_LeavesAPendingRowsAdminAlone()
    {
        var row = AppraisalAssignment.Create(Guid.NewGuid(), "Internal", assignedBy: "System");
        row.SetAssigner("P5229");

        row.ReturnToAssignmentQueue(); // already Pending: a no-op

        Assert.Equal("P5229", row.AssignedBy);
    }
}
