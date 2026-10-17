using System.Security.Claims;
using Auth.Application.Configurations;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Configuration;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>What stands between a verified first factor (password or LDAP bind) and a signed-in user.</summary>
public enum SecondStep
{
    /// <summary>Nothing: sign the user in.</summary>
    None,

    /// <summary>The policy requires two-factor and the user has none: set it up first.</summary>
    Enrol,

    /// <summary>The user has two-factor on and this device is not remembered: ask for a code.</summary>
    Code
}

/// <summary>
/// The steps around signing in that are the same whichever way the first factor was proven, so the password and LDAP
/// paths, the code page and the enrolment page cannot drift apart.
/// </summary>
public class LoginCompletion(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IPasswordPolicyProvider policyProvider,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IOptions<IdentityOptions> identityOptions,
    ILdapAuthenticationService ldapService,
    IOptions<LdapConfiguration> ldapOptions,
    ILogger<LoginCompletion> logger)
{
    public static SecondStep Decide(bool requiredByPolicy, bool enabled, bool deviceRemembered) =>
        enabled
            ? deviceRemembered ? SecondStep.None : SecondStep.Code
            : requiredByPolicy ? SecondStep.Enrol : SecondStep.None;

    public async Task<SecondStep> DecideAsync(ApplicationUser user)
    {
        var policy = await policyProvider.GetAsync();
        // Asked only when it can matter: a remembered device means nothing without two-factor. The cookie's own validation
        // (AuthModule) already enforces the policy's hours and the security stamp.
        var remembered = user.TwoFactorEnabled && await signInManager.IsTwoFactorClientRememberedAsync(user);
        return Decide(policy.RequiresTwoFactor(user.CompanyId), user.TwoFactorEnabled, remembered);
    }

    /// <summary>The user has no two-factor yet and the policy says they must set it up before signing in.</summary>
    public async Task<bool> MustEnrolAsync(ApplicationUser user) =>
        !user.TwoFactorEnabled && (await policyProvider.GetAsync()).RequiresTwoFactor(user.CompanyId);

    /// <summary>
    /// Parks the user, who has passed the first factor, in Identity's two-factor cookie for the code or enrolment page.
    /// LDAP never goes through <c>SignInManager</c>, so the cookie is written here for both sources; the name claim is
    /// the user id, which is what <c>SignInManager.GetTwoFactorAuthenticationUserAsync</c> reads back. The security stamp
    /// rides along, so a password reset or a new access window in the five minutes between the two steps voids it
    /// (see <see cref="GetParkedUserAsync"/>).
    /// </summary>
    public async Task ParkAsync(ApplicationUser user) =>
        await signInManager.Context.SignInAsync(IdentityConstants.TwoFactorUserIdScheme,
            new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Name, user.Id.ToString()),
                    new Claim(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType,
                        await userManager.GetSecurityStampAsync(user))
                ],
                IdentityConstants.TwoFactorUserIdScheme)));

    /// <summary>
    /// The user parked by <see cref="ParkAsync"/>, or null when there is none, it has expired, or the account's security
    /// stamp has changed since (the cookie is then dropped). Every two-factor page starts here instead of at Identity's
    /// own <c>GetTwoFactorAuthenticationUserAsync</c>, which does not look at the stamp.
    /// </summary>
    public async Task<ApplicationUser?> GetParkedUserAsync()
    {
        var context = signInManager.Context;
        var result = await context.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);
        if (!result.Succeeded) return null;

        var user = await userManager.FindByIdAsync(result.Principal.FindFirstValue(ClaimTypes.Name) ?? "");
        var stamp = result.Principal.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        if (user is not null && stamp is not null && stamp == await userManager.GetSecurityStampAsync(user))
            return user;

        await context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
        return null;
    }

    /// <summary>
    /// Why the account may not sign in right now, or null. Login checks this before the first factor; the pages after
    /// it check again, because the account can be deactivated or its access window can close in between.
    /// </summary>
    public string? BlockedReason(ApplicationUser user) =>
        !user.IsActive ? "Inactive" : !user.IsUsable(dateTimeProvider.ApplicationNow) ? "Expired" : null;

    /// <summary>
    /// What stands in the way of a sign-in that never sent a password to be checked (a passkey), beyond
    /// <see cref="BlockedReason"/>: the password path gets these from its own checks. Null when none does. An LDAP account is
    /// checked against the directory itself on every sign-in, because nothing else here would ever learn that AD disabled
    /// it, and the directory being unreachable is a refusal, not a pass.
    /// </summary>
    public async Task<string?> PasswordlessBlockedReasonAsync(ApplicationUser user)
    {
        var ldap = AuthSources.IsLdap(user.AuthSource);
        if (ldap && !ldapOptions.Value.Enabled) return "LdapDisabled";
        if (await userManager.IsLockedOutAsync(user)) return "LockedOut";
        if (!ldap) return null;

        // The service bind: GetUserInfoAsync returns null for "not found" and for every error alike.
        return AdAccountStatus.RefusalReason(await ldapService.GetUserInfoAsync(user.UserName!), dateTimeProvider.UtcNow);
    }

    /// <summary>
    /// Lets this device skip the code page for the policy's number of hours. Identity's own helper cannot be used: it
    /// writes a cookie that lives for a fixed 14 days. The stamp claim ties the cookie to the credentials, so a reset,
    /// a disable or a password change ends it.
    /// </summary>
    public async Task RememberDeviceAsync(ApplicationUser user)
    {
        var hours = (await policyProvider.GetAsync()).TwoFactorRememberHours;
        if (hours <= 0) return;

        var identity = new ClaimsIdentity(IdentityConstants.TwoFactorRememberMeScheme);
        identity.AddClaim(new Claim(ClaimTypes.Name, user.Id.ToString()));
        identity.AddClaim(new Claim(
            identityOptions.Value.ClaimsIdentity.SecurityStampClaimType, await userManager.GetSecurityStampAsync(user)));
        await signInManager.Context.SignInAsync(IdentityConstants.TwoFactorRememberMeScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = new DateTimeOffset(dateTimeProvider.UtcNow.AddHours(hours), TimeSpan.Zero)
            });
    }

    /// <summary>
    /// The bookkeeping of a completed sign-in, run once the Identity cookie is issued: failed-attempt count cleared,
    /// password expiry, last-login stamp, audit entry. Every successful sign-in ends here and nothing else does, so the
    /// count is cleared only when no code is still to be entered: with one pending, someone who knows the password
    /// could otherwise guess codes forever by signing in again between attempts. <paramref name="secondFactor"/> names how the second factor was met, when there was one.
    /// </summary>
    public async Task CompleteAsync(ApplicationUser user, string? secondFactor = null)
    {
        await userManager.ResetAccessFailedCountAsync(user);
        await EnforcePasswordExpiryAsync(user);
        await StampLastLoginAsync(user);

        var source = AuthSources.IsLdap(user.AuthSource) ? "Ldap" : "Local";
        logger.LogInformation("User {Username} logged in via {Source}", user.UserName, source);
        await auditWriter.RecordAuthEventAsync(AuditAction.LoggedIn, user.Id, user.UserName,
            secondFactor is null ? new { source } : new { source, secondFactor });

        RememberLoginMethod(secondFactor == "passkey" ? LoginMethod.Passkey : LoginMethod.Password);
    }

    /// <summary>
    /// Tells the SPA's sign-in page which method this browser used last, so it can put that one first. Same attributes as
    /// the language cookie (the SPA reads it from its own origin, so not HttpOnly); it is a preference, never a credential.
    /// </summary>
    private void RememberLoginMethod(string method) =>
        signInManager.Context.Response.Cookies.Append(LoginMethod.CookieName, method, new CookieOptions
        {
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = true,
            HttpOnly = false,
            MaxAge = TimeSpan.FromDays(365)
        });

    // If the local password has exceeded the configured max age, flag it so the SPA routes the user
    // to the change-password screen (reuses the MustChangePassword mechanism). The flag is set
    // in-memory only; StampLastLoginAsync persists it alongside LastLoginAt. Legacy accounts with no
    // PasswordChangedAt are skipped so enabling expiry doesn't lock everyone out at once.
    private async Task EnforcePasswordExpiryAsync(ApplicationUser user)
    {
        if (AuthSources.IsLdap(user.AuthSource) || user.MustChangePassword || user.PasswordChangedAt is null)
            return;

        var policy = await policyProvider.GetAsync();
        if (policy.ExpiryDays <= 0) return;

        if (user.PasswordChangedAt.Value.AddDays(policy.ExpiryDays) < dateTimeProvider.ApplicationNow)
        {
            user.MustChangePassword = true;
            logger.LogInformation("Password expired for {Username}; requiring change on login", user.UserName);
        }
    }

    private async Task StampLastLoginAsync(ApplicationUser user)
    {
        var now = dateTimeProvider.ApplicationNow;
        user.LastLoginAt = now;
        try
        {
            // Write the bookkeeping columns directly rather than via UserManager.UpdateAsync: those
            // are our own fields, and UpdateAsync runs the Identity UserValidator (which, with
            // RequireUniqueEmail enabled, returns a FAILED result for any pre-existing duplicate-email
            // account — silently dropping LastLoginAt / the expiry flag). ExecuteUpdateAsync sets only
            // these columns and skips that validation.
            await dbContext.Users
                .Where(u => u.Id == user.Id)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(u => u.LastLoginAt, now)
                    .SetProperty(u => u.MustChangePassword, user.MustChangePassword));
        }
        catch (Exception ex)
        {
            // A transient store failure must not invalidate an otherwise-valid login
            logger.LogWarning(ex, "Failed to stamp LastLoginAt for user {Username} — login proceeds", user.UserName);
        }
    }
}
