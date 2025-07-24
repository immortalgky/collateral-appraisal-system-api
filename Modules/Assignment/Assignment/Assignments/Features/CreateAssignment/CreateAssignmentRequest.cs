namespace Assignment.Assignments.Features.CreateAssignment;

public record CreateAssignmentRequest(
    long RequestId,
    string AssignmentMethod,
    string ExternalCompanyId,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
);