namespace Auth.Application.Features.Roles.GetRoles;

public record GetRoleQuery(string? Search, string? Scope, int PageNumber = 1, int PageSize = 20) : IQuery<GetRoleResult>;
