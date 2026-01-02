namespace Auth.Domain.Roles.Features.GetRoleById;

public record GetRoleByIdQuery(Guid Id) : IQuery<GetRoleByIdResult>;
