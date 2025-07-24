namespace Assignment.Assignments.Features.CreateAssignment;

public record CreateAssignmentCommand(
    long RequestId,
    string AssignmentMethod,
    string ExternalCompanyId,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
) : ICommand<CreateAssignmentResult>;