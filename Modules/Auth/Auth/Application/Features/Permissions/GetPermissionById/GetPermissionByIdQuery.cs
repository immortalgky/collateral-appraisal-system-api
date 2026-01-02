namespace Auth.Domain.Permissions.Features.GetPermissionById;

public record GetPermissionByIdQuery(Guid Id) : IQuery<GetPermissionByIdResult>;
