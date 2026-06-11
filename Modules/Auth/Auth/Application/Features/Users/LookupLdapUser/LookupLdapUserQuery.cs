namespace Auth.Application.Features.Users.LookupLdapUser;

public record LookupLdapUserQuery(string Username) : IQuery<LookupLdapUserResult>;
