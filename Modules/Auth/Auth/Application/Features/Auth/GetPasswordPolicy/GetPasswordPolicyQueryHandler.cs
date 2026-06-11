using Auth.Infrastructure.Configuration;

namespace Auth.Application.Features.Auth.GetPasswordPolicy;

public class GetPasswordPolicyQueryHandler(IPasswordPolicyProvider policyProvider)
    : IQueryHandler<GetPasswordPolicyQuery, GetPasswordPolicyResult>
{
    public async Task<GetPasswordPolicyResult> Handle(GetPasswordPolicyQuery query, CancellationToken cancellationToken)
    {
        var p = await policyProvider.GetAsync(cancellationToken);
        return new GetPasswordPolicyResult(
            p.RequiredLength,
            p.RequireDigit,
            p.RequireLowercase,
            p.RequireUppercase,
            p.RequireNonAlphanumeric,
            p.RequiredUniqueChars);
    }
}
