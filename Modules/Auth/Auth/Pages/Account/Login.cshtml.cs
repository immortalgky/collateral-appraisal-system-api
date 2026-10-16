using Auth.Application.Configurations;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Auth.Pages.Account;

[AllowAnonymous]
[AllowWhilePasswordChangeRequired]
public class Login(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ILdapAuthenticationService ldapService,
    IOptions<LdapConfiguration> ldapOptions,
    ILogger<Login> logger,
    IAuthAuditWriter auditWriter,
    LoginCompletion completion,
    BffUrls urls,
    IOptions<IdentityOptions> identityOptions)
    : IdpPageModel
{
    private readonly LdapConfiguration _ldapConfig = ldapOptions.Value;

    [BindProperty] public string Error { get; set; } = string.Empty;

    [BindProperty] public string Username { get; set; } = string.Empty;

    [BindProperty] public string Password { get; set; } = string.Empty;

    /// <summary>"Choose another way" target: the SPA's sign-in page (its interstitial restores the return URL itself).</summary>
    public string SpaLoginUrl => urls.SpaHome + "login";

    /// <summary>False when no SPA origin is configured (SpaHome is then "/"): the link would point at a 404.</summary>
    public bool HasSpaLogin => urls.SpaHome != "/";

    /// <summary>
    /// Sign-ins are always non-persistent, so the Identity cookie is a session cookie that dies with
    /// the browser. That is load-bearing: a persistent one would let /connect/authorize mint a fresh
    /// authorization code after a browser restart and sign the user straight back in without a
    /// password, defeating the session-scoped refresh cookie.
    /// <para>
    /// Deliberately a constant rather than a bound "remember me" property. Login.cshtml renders no
    /// such checkbox, but model binding takes its value from the posted form, not from the rendered
    /// view — a hand-crafted POST carrying RememberMe=true would have been honoured.
    /// </para>
    /// </summary>
    private const bool IsPersistentSignIn = false;

    public IActionResult OnGet(string? returnUrl = null)
    {
        // Not from the OIDC flow (a bookmark, a typed address, a stale tab): send the browser back to the SPA to
        // start over.
        if (!IsAuthorizeReturnUrl(returnUrl))
            return Redirect(urls.SpaHome);

        ReturnUrl = returnUrl;
        // "Continue as ...": the authorize request carries the username to show. Razor encodes it on output.
        Username = LoginHint.FromAuthorizeUrl(returnUrl, identityOptions.Value.User.AllowedUserNameCharacters) ?? "";
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        logger.LogInformation("Login attempt for user: {Username}", Username);

        if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
        {
            Error = T.RequiredError;
            return Page();
        }

        // The account must already exist locally — we never auto-provision from LDAP at login.
        // Resolve the user before issuing any session so unknown/inactive accounts never get a cookie.
        var user = await userManager.FindByNameAsync(Username);
        if (user is null)
        {
            logger.LogWarning("Login attempt for non-existent user: {Username}", Username);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, null, Username, new { reason = "UserNotFound" });
            Error = T.InvalidAttemptError;
            return Page();
        }

        // Deactivated, or a temporary-access account whose window has run out. The latter gets the generic message on
        // purpose: "that account exists but its window closed" tells an outsider the account is real and worth attacking,
        // and the admin who opened the window already knows when it ends. The audit log carries the true reason.
        if (completion.BlockedReason(user) is { } reason)
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason });
            Error = reason == "Inactive" ? T.DeactivatedError : T.InvalidAttemptError;
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
            Error = T.InvalidAttemptError;
            return Page();
        }

        // Apply the same DB-policy lockout as local accounts — the bind below does not go through
        // SignInManager, so without this LDAP accounts would have no app-level brute-force throttle.
        if (await userManager.IsLockedOutAsync(user))
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "LockedOut" });
            Error = T.LockedOutError;
            return Page();
        }

        var ldapResult = await ldapService.AuthenticateAsync(Username, Password);
        if (!ldapResult.Succeeded || ldapResult.UserInfo is null)
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("LDAP auth failed for {Username}: {Error}", Username, ldapResult.ErrorMessage);
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "InvalidCredentials" });
            Error = T.InvalidAttemptError;
            return Page();
        }

        await SyncLdapAttributesAsync(user, ldapResult.UserInfo);
        return await ContinueAfterFirstFactorAsync(user);
    }

    private async Task<IActionResult> LoginWithLocalPasswordAsync(ApplicationUser user)
    {
        var result = await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        if (result.Succeeded)
            return await ContinueAfterFirstFactorAsync(user);

        if (result.IsLockedOut)
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "LockedOut" });
            Error = T.LockedOutError;
            return Page();
        }

        await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, Username, new { reason = "InvalidCredentials" });
        Error = T.InvalidAttemptError;
        return Page();
    }

    // Where both sign-in sources meet: the first factor (password or LDAP bind) is proven, and one place decides
    // whether the user still has to set up or enter a second factor or can be signed in.
    private async Task<IActionResult> ContinueAfterFirstFactorAsync(ApplicationUser user)
    {
        var step = await completion.DecideAsync(user);
        if (step != SecondStep.None)
        {
            await completion.ParkAsync(user);
            return RedirectToPage(step == SecondStep.Enrol ? "TwoFactorSetup" : "TwoFactor", new { returnUrl = ReturnUrl });
        }

        await signInManager.SignInAsync(user, IsPersistentSignIn);
        await completion.CompleteAsync(user);
        return Redirect(GetSafeRedirectUrl(logger, user));
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
}
