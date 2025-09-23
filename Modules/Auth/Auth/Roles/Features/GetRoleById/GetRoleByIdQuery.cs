namespace Auth.Roles.Features.GetRoleById;

public record GetRoleByIdQuery(Guid Id) : IQuery<GetRoleByIdResult>;
