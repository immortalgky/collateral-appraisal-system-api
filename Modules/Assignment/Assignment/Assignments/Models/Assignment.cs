namespace Assignment.Assignments.Models;

public class Assignment : Aggregate<long>
{
    public long RequestId { get; private set; } = default!;
    public string AssignmentMethod { get; private set; } = default!;
    public string ExternalCompanyId { get; private set; } = default!;
    public string ExternalCompanyAssignType { get; private set; } = default!;
    public string ExtApprStaff { get; private set; } = default!;
    public string ExtApprStaffAssignmentType { get; private set; } = default!;
    public string IntApprStaff { get; private set; } = default!;
    public string IntApprStaffAssignmentType { get; private set; } = default!;
    public string Remark { get; private set; } = default!;

    private Assignment()
    {
        // For EF Core
    }

    private Assignment(
            long requestId,
            string assignmentMethod,
            string externalCompanyId,
            string externalCompanyAssignType, 
            string extApprStaff,
            string extApprStaffAssignmentType,
            string intApprStaff,
            string intApprStaffAssignmentType,
            string remark)
    {
        RequestId = requestId;
        AssignmentMethod = assignmentMethod;
        ExternalCompanyId = externalCompanyId;
        ExternalCompanyAssignType = externalCompanyAssignType; 
        ExtApprStaff = extApprStaff;
        ExtApprStaffAssignmentType = extApprStaffAssignmentType;
        IntApprStaff = intApprStaff;
        IntApprStaffAssignmentType = intApprStaffAssignmentType;
        Remark = remark;

        AddDomainEvent(new AssignmentCreatedEvent(this));
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Assignment Create(
            long requestId,
            string assignmentMethod,
            string externalCompanyId,
            string externalCompanyAssignType, 
            string extApprStaff,
            string extApprStaffAssignmentType,
            string intApprStaff,
            string intApprStaffAssignmentType,
            string remark
    )
    {
        return new Assignment(requestId,
             assignmentMethod,
             externalCompanyId,
             externalCompanyAssignType, 
             extApprStaff,
             extApprStaffAssignmentType,
             intApprStaff,
             intApprStaffAssignmentType,
             remark);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public void UpdateDetail(
        long requestId,
            string assignmentMethod,
            string externalCompanyId,
            string externalCompanyAssignType,
            string extApprStaff,
            string extApprStaffAssignmentType,
            string intApprStaff,
            string intApprStaffAssignmentType,
            string remark
    )
    {
        RequestId = requestId;
        AssignmentMethod = assignmentMethod;
        ExternalCompanyId = externalCompanyId;
        ExternalCompanyAssignType = externalCompanyAssignType;
        ExtApprStaff = extApprStaff;
        ExtApprStaffAssignmentType = extApprStaffAssignmentType;
        IntApprStaff = intApprStaff;
        IntApprStaffAssignmentType = intApprStaffAssignmentType;
        Remark = remark;
        
    }
     [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static Assignment UpdateDetailObject(
        long requestId,
            string assignmentMethod,
            string externalCompanyId,
            string externalCompanyAssignType,
            string extApprStaff,
            string extApprStaffAssignmentType,
            string intApprStaff,
            string intApprStaffAssignmentType,
            string remark
    )
    {
        return new Assignment(
             requestId,
             assignmentMethod,
             externalCompanyId,
             externalCompanyAssignType, 
             extApprStaff,
             extApprStaffAssignmentType,
             intApprStaff,
             intApprStaffAssignmentType,
             remark);
    }
}