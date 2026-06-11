using Auth.Application.Services;

namespace Auth.Application.Features.Users.LookupLdapUser;

public class LookupLdapUserQueryHandler(ILdapAuthenticationService ldapService)
    : IQueryHandler<LookupLdapUserQuery, LookupLdapUserResult>
{
    public async Task<LookupLdapUserResult> Handle(LookupLdapUserQuery query, CancellationToken cancellationToken)
    {
        var info = await ldapService.GetUserInfoAsync(query.Username);

        if (info is null)
            return new LookupLdapUserResult(false, query.Username, null, null, null, null, null);

        return new LookupLdapUserResult(
            true,
            info.Username,
            info.Email,
            info.FirstName,
            info.LastName,
            info.Department,
            info.Position);
    }
}
