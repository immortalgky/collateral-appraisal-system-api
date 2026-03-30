namespace Auth.Application.Features.Permissions.GetPermissionById;

public record GetPermissionByIdQuery(Guid Id) : IQuery<GetPermissionByIdResult>;
