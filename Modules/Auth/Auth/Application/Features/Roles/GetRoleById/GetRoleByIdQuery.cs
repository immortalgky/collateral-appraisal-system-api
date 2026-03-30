namespace Auth.Application.Features.Roles.GetRoleById;

public record GetRoleByIdQuery(Guid Id) : IQuery<GetRoleByIdResult>;
