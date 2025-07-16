namespace Assignment.Assignments.Features.UpdateAssignment;

public record UpdateAssignmentCommand(
    long Id,
    string ReqID,
    string AssignmentMethod,
    string ExternalCompanyID,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
) : ICommand<UpdateAssignmentResult>;