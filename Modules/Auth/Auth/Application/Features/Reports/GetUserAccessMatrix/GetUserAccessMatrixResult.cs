namespace Auth.Application.Features.Reports.GetUserAccessMatrix;

public record UserAccessRow(
    Guid UserId,
    string UserName,
    string FullName,
    string? Email,
    string? CompanyName,
    string Scope,
    bool IsActive,
    string Roles,
    string Groups,
    string Teams);

public record GetUserAccessMatrixResult(
    IReadOnlyList<UserAccessRow> Items,
    int TotalCount,
    int PageNumber,
    int PageSize);
