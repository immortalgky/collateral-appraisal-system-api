using Auth.Domain.Configuration;
using Auth.Infrastructure.Configuration;

namespace Auth.Application.Features.Auth.PasswordPolicyAdmin.UpdatePasswordPolicyConfig;

public class UpdatePasswordPolicyConfigCommandHandler(
    AuthDbContext dbContext,
    IPasswordPolicyProvider policyProvider)
    : ICommandHandler<UpdatePasswordPolicyConfigCommand>
{
    public async Task<Unit> Handle(
        UpdatePasswordPolicyConfigCommand command,
        CancellationToken cancellationToken)
    {
        var policy = await dbContext.PasswordPolicy.FirstOrDefaultAsync(cancellationToken);
        if (policy is null)
        {
            policy = PasswordPolicy.CreateDefault();
            dbContext.PasswordPolicy.Add(policy);
        }

        policy.Update(
            command.RequiredLength,
            command.RequireDigit,
            command.RequireLowercase,
            command.RequireUppercase,
            command.RequireNonAlphanumeric,
            command.RequiredUniqueChars,
            command.ExpiryDays,
            command.HistoryCount,
            command.Blocklist,
            command.LockoutEnabled,
            command.MaxFailedAccessAttempts,
            command.LockoutMinutes);

        await dbContext.SaveChangesAsync(cancellationToken);

        // Drop the cached policy so the next read (validator / policy endpoint) is fresh.
        policyProvider.Invalidate();

        return Unit.Value;
    }
}
