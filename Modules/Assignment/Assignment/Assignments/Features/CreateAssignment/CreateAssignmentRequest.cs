namespace Assignment.Assignments.Features.CreateAssignment;

public record CreateAssignmentRequest(
    string ReqID,
    string AssignmentMethod,
    string ExternalCompanyID,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
);