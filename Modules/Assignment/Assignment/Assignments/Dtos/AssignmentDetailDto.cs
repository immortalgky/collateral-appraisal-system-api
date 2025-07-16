namespace Assignment.Assignments.Dtos;

public record AssignmentDetailDto(
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
);