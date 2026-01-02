namespace Auth.Domain.Roles.Features.GetRoles;

public record GetRoleQuery(PaginationRequest PaginationRequest) : IQuery<GetRoleResult>;
