namespace Auth.Permissions.Features.GetPermissionById;

public record GetPermissionByIdQuery(Guid Id) : IQuery<GetPermissionByIdResult>;
