using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Auth.Application.Features.Auth.GetPasswordPolicy;

public class GetPasswordPolicyQueryHandler(IOptions<IdentityOptions> identityOptions)
    : IQueryHandler<GetPasswordPolicyQuery, GetPasswordPolicyResult>
{
    public Task<GetPasswordPolicyResult> Handle(GetPasswordPolicyQuery query, CancellationToken cancellationToken)
    {
        var pw = identityOptions.Value.Password;
        var result = new GetPasswordPolicyResult(
            pw.RequiredLength,
            pw.RequireDigit,
            pw.RequireLowercase,
            pw.RequireUppercase,
            pw.RequireNonAlphanumeric,
            pw.RequiredUniqueChars);
        return Task.FromResult(result);
    }
}
