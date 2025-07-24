namespace Assignment.Assignments.Dtos;

public record AssignmentDetailDto(
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
);