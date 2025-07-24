namespace Assignment.Assignments.Features.UpdateAssignment;

public record UpdateAssignmentCommand(
    long Id,
    long RequestId,
    string AssignmentMethod,
    string ExternalCompanyId,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
) : ICommand<UpdateAssignmentResult>;