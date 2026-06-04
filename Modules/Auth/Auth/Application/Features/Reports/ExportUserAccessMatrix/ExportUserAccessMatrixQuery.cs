namespace Auth.Application.Features.Reports.ExportUserAccessMatrix;

public record ExportUserAccessMatrixQuery(
    string? Scope,
    Guid? CompanyId,
    string? RoleName,
    Guid? GroupId,
    Guid? TeamId,
    bool? IsActive,
    string? Search
) : IQuery<ExportUserAccessMatrixResult>;
