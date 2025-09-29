namespace Auth.Permissions.Features.GetPermissions;

public record GetPermissionQuery(PaginationRequest PaginationRequest) : IQuery<GetPermissionResult>;
