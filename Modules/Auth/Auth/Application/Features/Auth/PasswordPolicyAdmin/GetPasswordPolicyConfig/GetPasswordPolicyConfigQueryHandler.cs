using Auth.Application.Features.Auth.PasswordPolicyAdmin;
using Auth.Infrastructure.Configuration;

namespace Auth.Application.Features.Auth.PasswordPolicyAdmin.GetPasswordPolicyConfig;

public class GetPasswordPolicyConfigQueryHandler(IPasswordPolicyProvider policyProvider)
    : IQueryHandler<GetPasswordPolicyConfigQuery, PasswordPolicyConfigDto>
{
    public async Task<PasswordPolicyConfigDto> Handle(
        GetPasswordPolicyConfigQuery query,
        CancellationToken cancellationToken)
    {
        var p = await policyProvider.GetAsync(cancellationToken);
        return new PasswordPolicyConfigDto(
            p.RequiredLength,
            p.RequireDigit,
            p.RequireLowercase,
            p.RequireUppercase,
            p.RequireNonAlphanumeric,
            p.RequiredUniqueChars,
            p.ExpiryDays,
            p.HistoryCount,
            p.Blocklist,
            p.LockoutEnabled,
            p.MaxFailedAccessAttempts,
            p.LockoutMinutes);
    }
}
