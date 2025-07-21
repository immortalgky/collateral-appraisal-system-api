namespace Assignment.Assignments.Dtos;

public record AssignmentDetailDto(
    long Id,
    long RequestID,
    string AssignmentMethod,
    string ExternalCompanyID,
    string ExternalCompanyAssignType,
    string ExtApprStaff,
    string ExtApprStaffAssignmentType,
    string IntApprStaff,
    string IntApprStaffAssignmentType,
    string Remark
);