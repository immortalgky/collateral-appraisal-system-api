namespace Auth.Domain.Auth.Features.Me;

public record MeQuery(Guid UserId) : IQuery<MeResult>;
