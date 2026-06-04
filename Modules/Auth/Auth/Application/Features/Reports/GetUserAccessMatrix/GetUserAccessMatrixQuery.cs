namespace Auth.Application.Features.Reports.GetUserAccessMatrix;

public record GetUserAccessMatrixQuery(
    string? Scope,
    Guid? CompanyId,
    string? RoleName,
    Guid? GroupId,
    Guid? TeamId,
    bool? IsActive,
    string? Search,
    int PageNumber = 0,
    int PageSize = 20
) : IQuery<GetUserAccessMatrixResult>;
