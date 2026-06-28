namespace Auth.Contracts.Users;

public record RequestorInfoDto(
    Guid UserId,
    string EmployeeId,
    string Name,
    string? Email,
    string? ContactNo,
    string? AoCode,
    string? CostCenterCode,
    string? CostCenterDescription,
    string? Department);
