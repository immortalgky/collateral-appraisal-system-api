namespace Assignment.Assignments.ValueObjects;

public class AssignmentDetail : ValueObject
{
    public long ReqID { get; private set; } = default!;
    public string AssignmentMethod { get; private set; } = default!;
    public string ExternalCompanyId { get; private set; } = default!;
    public string ExternalCompanyAssignType { get; private set; } = default!;
    public string ExtApprStaff { get; private set; } = default!;
    public string ExtApprStaffAssignmentType { get; private set; } = default!;
    public string IntApprStaff { get; private set; } = default!;
    public string IntApprStaffAssignmentType { get; private set; } = default!;
    public string Remark { get; private set; } = default!;

    private AssignmentDetail()
    {
        // For EF Core
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    private AssignmentDetail(
        long reqID,
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
        ReqID = reqID;
        AssignmentMethod = assignmentMethod;
        ExternalCompanyId  = externalCompanyId;
        ExternalCompanyAssignType = externalCompanyAssignType;
        ExtApprStaff = extApprStaff;
        ExtApprStaffAssignmentType = extApprStaffAssignmentType;
        IntApprStaff = intApprStaff;
        IntApprStaffAssignmentType = intApprStaffAssignmentType;
        Remark = remark;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("SonarQube", "S107:Methods should not have too many parameters")]
    public static AssignmentDetail Create(
        long reqID,
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
        ArgumentNullException.ThrowIfNull(assignmentMethod);
       
        return new AssignmentDetail(
            reqID,
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