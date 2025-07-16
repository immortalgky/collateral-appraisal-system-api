using Assignment.Assignments.ValueObjects;

namespace Assignment.Assignments.Models;

public class Assignment : Aggregate<long>
{
    public AssignmentDetail Detail { get; private set; } = default!;

    private Assignment()
    {
        // For EF Core
    }

    private Assignment(AssignmentDetail detail)
    {
        Detail = detail;

        AddDomainEvent(new AssignmentCreatedEvent(this));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Assignment Create(
            string ReqID,
            string AssignmentMethod,
            string ExternalCompanyID,
            string ExternalCompanyAssignType,
            string ExtApprStaff,
            string ExtApprStaffAssignmentType,
            string IntApprStaff,
            string IntApprStaffAssignmentType,
            string Remark
    )
    {
        var detail = AssignmentDetail.Create(
            ReqID,
            AssignmentMethod,
            ExternalCompanyID,
            ExternalCompanyAssignType,
            ExtApprStaff,
            ExtApprStaffAssignmentType,
            IntApprStaff,
            IntApprStaffAssignmentType,
            Remark
        );

        return new Assignment(detail);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void UpdateDetail(
        string ReqID,
            string AssignmentMethod,
            string ExternalCompanyID,
            string ExternalCompanyAssignType,
            string ExtApprStaff,
            string ExtApprStaffAssignmentType,
            string IntApprStaff,
            string IntApprStaffAssignmentType,
            string Remark
    )
    {
        var newDetail =  AssignmentDetail.Create(
            ReqID,
            AssignmentMethod,
            ExternalCompanyID,
            ExternalCompanyAssignType,
            ExtApprStaff,
            ExtApprStaffAssignmentType,
            IntApprStaff,
            IntApprStaffAssignmentType,
            Remark
        );

        if (!Detail.Equals(newDetail))
        {
            Detail = newDetail;
        }
    }

}