using Auth.Application.Configurations;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Shared.Time;

namespace Auth.Pages.Account;

[AllowAnonymous]
public class Login(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILdapAuthenticationService ldapService,
    IOptions<LdapConfiguration> ldapOptions,
    ILogger<Login> logger,
    IDateTimeProvider dateTimeProvider,
    IPasswordPolicyProvider passwordPolicyProvider,
    IAuthAuditWriter auditWriter,
    AuthDbContext dbContext)
    : PageModel
{
    private readonly LdapConfiguration _ldapConfig = ldapOptions.Value;

    [BindProperty] public string ReturnUrl { get; set; } = "/";

    [BindProperty] public string Error { get; set; } = string.Empty;

    [BindProperty] public string Username { get; set; } = string.Empty;

    [BindProperty] public string Password { get; set; } = string.Empty;

    [BindProperty] public bool RememberMe { get; set; }

    public void OnGet(string returnUrl = null)
    {
        ReturnUrl = returnUrl ?? "/";
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        logger.LogInformation("Login attempt for user: {Username}", Username);

        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            Error = "Username and password are required.";
            return Page();
        }

        // The account must already exist locally — we never auto-provision from LDAP at login.
        // Resolve the user before issuing any session so unknown/inactive accounts never get a cookie.
        var user = await userManager.FindByNameAsync(Username);
        if (user is null)
        {
            logger.LogWarning("Login attempt for non-existent user: {Username}", Username);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, null, Username, new { reason = "UserNotFound" });
            Error = "Invalid login attempt.";
            return Page();
        }

        if (!user.IsActive)
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "Inactive" });
            Error = "This account is deactivated. Contact your administrator.";
            return Page();
        }

        // Authentication source is per-user: LDAP users authenticate against AD with their AD password;
        // everyone else uses their local password.
        return AuthSources.IsLdap(user.AuthSource)
            ? await LoginWithLdapAsync(user)
            : await LoginWithLocalPasswordAsync(user);
    }

    private async Task<IActionResult> LoginWithLdapAsync(ApplicationUser user)
    {
        if (!_ldapConfig.Enabled)
        {
            logger.LogWarning("LDAP user {Username} attempted login but LDAP is disabled", Username);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "LdapDisabled" });
            Error = "Invalid login attempt.";
            return Page();
        }

        // Apply the same DB-policy lockout as local accounts — the bind below does not go through
        // SignInManager, so without this LDAP accounts would have no app-level brute-force throttle.
        if (await userManager.IsLockedOutAsync(user))
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "LockedOut" });
            Error = "Account locked out.";
            return Page();
        }

        var ldapResult = await ldapService.AuthenticateAsync(Username, Password);
        if (!ldapResult.Succeeded || ldapResult.UserInfo is null)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("LDAP auth failed for {Username}: {Error}", Username, ldapResult.ErrorMessage);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "InvalidCredentials" });
            Error = "Invalid login attempt.";
            return Page();
        }

        await userManager.ResetAccessFailedCountAsync(user);
        await SyncLdapAttributesAsync(user, ldapResult.UserInfo);
        await StampLastLoginAsync(user);
        await signInManager.SignInAsync(user, RememberMe);
        logger.LogInformation("User {Username} logged in via LDAP", Username);
        await auditWriter.RecordAuthEventAsync(AuditAction.LoggedIn, user.Id, Username, new { source = "Ldap" });
        return Redirect(GetSafeRedirectUrl(user));
    }

    private async Task<IActionResult> LoginWithLocalPasswordAsync(ApplicationUser user)
    {
        var result = await signInManager.PasswordSignInAsync(Username, Password, RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await EnforcePasswordExpiryAsync(user);
            await StampLastLoginAsync(user);
            logger.LogInformation("User {Username} logged in successfully", Username);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoggedIn, user.Id, Username, new { source = "Local" });
            return Redirect(GetSafeRedirectUrl(user));
        }

        if (result.IsLockedOut)
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "LockedOut" });
            Error = "Account locked out.";
            return Page();
        }

        await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "InvalidCredentials" });
        Error = "Invalid login attempt.";
        return Page();
    }

    // If the local password has exceeded the configured max age, flag it so the SPA routes the user
    // to the change-password screen (reuses the MustChangePassword mechanism). The flag is set
    // in-memory only; StampLastLoginAsync persists it alongside LastLoginAt. Legacy accounts with no
    // PasswordChangedAt are skipped so enabling expiry doesn't lock everyone out at once.
    private async Task EnforcePasswordExpiryAsync(ApplicationUser user)
    {
        if (AuthSources.IsLdap(user.AuthSource) || user.MustChangePassword || user.PasswordChangedAt is null)
            return;

        var policy = await passwordPolicyProvider.GetAsync();
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

    // Refresh an existing LDAP user's profile fields from AD on login. Never creates a user —
    // accounts must be provisioned ahead of time via the admin user-creation screen.
    private async Task SyncLdapAttributesAsync(ApplicationUser user, LdapUserInfo info)
    {
        var changed = false;

        if (info.Email is not null && user.Email != info.Email) { user.Email = info.Email; changed = true; }
        if (info.FirstName is not null && user.FirstName != info.FirstName) { user.FirstName = info.FirstName; changed = true; }
        if (info.LastName is not null && user.LastName != info.LastName) { user.LastName = info.LastName; changed = true; }
        if (info.Department is not null && user.Department != info.Department) { user.Department = info.Department; changed = true; }
        if (info.Position is not null && user.Position != info.Position) { user.Position = info.Position; changed = true; }

        if (changed)
        {
            // AD email is authoritative, so keep this on UpdateAsync (it validates uniqueness). But a
            // duplicate email is a FAILED result, not an exception — inspect it and log instead of
            // silently dropping the sync. Login still proceeds; the next login retries the sync.
            var result = await userManager.UpdateAsync(user);
            if (result.Succeeded)
                logger.LogInformation("Synced AD attributes for user: {Username}", user.UserName);
            else
                logger.LogWarning("Failed to sync AD attributes for {Username}: {Errors}",
                    user.UserName, string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }

    private string GetSafeRedirectUrl(ApplicationUser? user = null)
    {
        var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;

        if (!Url.IsLocalUrl(redirectUrl) && !redirectUrl.StartsWith("https://"))
        {
            logger.LogWarning("Invalid return URL: {ReturnUrl}, redirecting to home", redirectUrl);
            redirectUrl = "/";
        }

        // If the user must change their password, append a flag so the SPA can intercept
        // and redirect to the change-password screen before allowing normal navigation.
        // Only append to LOCAL urls — never leak account state to an external domain.
        if (user?.MustChangePassword == true && Url.IsLocalUrl(redirectUrl))
        {
            var separator = redirectUrl.Contains('?') ? "&" : "?";
            redirectUrl += $"{separator}mustChangePassword=true";
        }

        return redirectUrl;
    }
}
