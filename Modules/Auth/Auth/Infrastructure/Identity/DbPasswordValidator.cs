using Auth.Infrastructure.Configuration;

namespace Auth.Infrastructure.Identity;

/// <summary>
/// Enforces the DB-maintained password policy at validation time (length, character classes,
/// unique characters, blocklist, and reuse-of-last-N history). Because it reads the policy from
/// <see cref="IPasswordPolicyProvider"/> on each call, admin edits apply without an app restart.
/// The built-in Identity complexity options are relaxed in AuthModule so this is the single source
/// of truth.
/// </summary>
public class DbPasswordValidator(
    IPasswordPolicyProvider policyProvider,
    AuthDbContext dbContext,
    IPasswordHasher<ApplicationUser> passwordHasher)
    : IPasswordValidator<ApplicationUser>
{
    public async Task<IdentityResult> ValidateAsync(
        UserManager<ApplicationUser> manager,
        ApplicationUser user,
        string? password)
    {
        var errors = new List<IdentityError>();

        if (string.IsNullOrEmpty(password))
        {
            errors.Add(Err("PasswordRequired", "Password is required."));
            return IdentityResult.Failed(errors.ToArray());
        }

        var policy = await policyProvider.GetAsync();

        if (password.Length < policy.RequiredLength)
            errors.Add(Err("PasswordTooShort", $"Passwords must be at least {policy.RequiredLength} characters."));

        // ASCII char-classes (a-z / A-Z / 0-9) to match the labels shown to users and the
        // frontend checklist regex exactly — avoids "all green but server rejects" on non-ASCII input.
        if (policy.RequireDigit && !password.Any(char.IsAsciiDigit))
            errors.Add(Err("PasswordRequiresDigit", "Passwords must have at least one digit ('0'-'9')."));

        if (policy.RequireLowercase && !password.Any(char.IsAsciiLetterLower))
            errors.Add(Err("PasswordRequiresLower", "Passwords must have at least one lowercase letter ('a'-'z')."));

        if (policy.RequireUppercase && !password.Any(char.IsAsciiLetterUpper))
            errors.Add(Err("PasswordRequiresUpper", "Passwords must have at least one uppercase letter ('A'-'Z')."));

        if (policy.RequireNonAlphanumeric && password.All(char.IsAsciiLetterOrDigit))
            errors.Add(Err("PasswordRequiresNonAlphanumeric", "Passwords must have at least one non-alphanumeric character."));

        if (policy.RequiredUniqueChars >= 1 && password.Distinct().Count() < policy.RequiredUniqueChars)
            errors.Add(Err("PasswordRequiresUniqueChars", $"Passwords must use at least {policy.RequiredUniqueChars} different characters."));

        if (!string.IsNullOrWhiteSpace(policy.Blocklist) && IsBlocklisted(policy.Blocklist, password))
            errors.Add(Err("PasswordBlocklisted", "This password is too common or not allowed. Please choose another."));

        if (policy.HistoryCount > 0 && user.Id != default && await IsReusedAsync(user, password, policy.HistoryCount))
            errors.Add(Err("PasswordReused", $"You cannot reuse any of your last {policy.HistoryCount} passwords."));

        return errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray());
    }

    private static bool IsBlocklisted(string blocklist, string password) =>
        blocklist
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(entry => string.Equals(entry, password, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> IsReusedAsync(ApplicationUser user, string password, int historyCount)
    {
        // Always block reuse of the CURRENT password, even when no history rows exist yet
        // (seeded/admin-created accounts are created via UserManager.CreateAsync without the
        // history recorder, so their PasswordHistory table is empty on the first change).
        if (!string.IsNullOrEmpty(user.PasswordHash)
            && passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed)
            return true;

        var recentHashes = await dbContext.PasswordHistory
            .AsNoTracking()
            .Where(h => h.UserId == user.Id)
            .OrderByDescending(h => h.CreatedAt)
            .Take(historyCount)
            .Select(h => h.PasswordHash)
            .ToListAsync();

        return recentHashes.Any(hash =>
            passwordHasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed);
    }

    private static IdentityError Err(string code, string description) => new() { Code = code, Description = description };
}
