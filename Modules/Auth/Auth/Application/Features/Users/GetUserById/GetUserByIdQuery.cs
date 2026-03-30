namespace Auth.Application.Features.Users.GetUserById;

public record GetUserByIdQuery(Guid Id) : IQuery<GetUserByIdResult>;
