namespace Auth.Domain.Permissions.Features.GetPermissions;

public record GetPermissionQuery(PaginationRequest PaginationRequest) : IQuery<GetPermissionResult>;
