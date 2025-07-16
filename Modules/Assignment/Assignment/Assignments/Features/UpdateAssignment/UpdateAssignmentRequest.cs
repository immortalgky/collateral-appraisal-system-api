namespace Assignment.Assignments.Features.UpdateAssignment;

public record UpdateAssignmentRequest(
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