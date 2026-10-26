using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authorization;

namespace Auth.Pages.Account;

/// <summary>
/// Sign in with a passkey. The browser runs <c>navigator.credentials.get</c> on load (discoverable: nobody has said who they
/// are yet) and posts the result here. A passkey is always user-verified (a PIN or biometric), so it is the second factor
/// too: this page never asks for a code, never enrols, and never sets the remember-device cookie.
/// </summary>
[AllowAnonymous]
[AllowWhilePasswordChangeRequired]
public class Passkey(
    PasskeyService passkeys,
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    LoginCompletion completion,
    IAuthAuditWriter auditWriter,
    BffUrls urls,
    ILogger<Passkey> logger)
    : IdpPageModel
{
    // What the browser posts back: the sealed challenge it was given, and the assertion (credential.toJSON()).
    [BindProperty] public string? State { get; set; }
    [BindProperty] public string? Credential { get; set; }

    public string Error { get; private set; } = string.Empty;

    /// <summary>
    /// Set only when the browser offered a credential this server does not have (a passkey deleted in the profile or revoked by an
    /// admin): the page tells the browser to forget it. Never set for any other failure.
    /// </summary>
    public string? UnknownCredentialId { get; private set; }

    /// <summary>The options for <c>navigator.credentials.get</c>, as JSON. Rendered into an attribute, so HTML-encoded by Razor.</summary>
    public string OptionsJson { get; private set; } = string.Empty;

    /// <summary>"Use a password instead": the password page, which must stay on it (method=password).</summary>
    public string PasswordUrl => Url.Page("Login", new { returnUrl = ReturnUrl, method = LoginMethod.Password })!;

    public IActionResult OnGet(string? returnUrl = null)
    {
        // Same guard as the other sign-in pages: only the OIDC flow gets here.
        if (!IsAuthorizeReturnUrl(returnUrl))
            return Redirect(urls.SpaHome);

        ReturnUrl = returnUrl;
        NewChallenge();
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        // 1. The signature and everything WebAuthn requires of it.
        var (verified, reason, unknownCredentialId) = await passkeys.VerifyAssertionAsync(State, Credential);
        if (verified is null) return await FailAsync(reason!, unknownCredentialId: unknownCredentialId);

        // 2. Whose it is. The user-handle check already happened inside the verification.
        var passkey = verified.Passkey;
        var user = await userManager.FindByIdAsync(passkey.UserId.ToString());
        if (user is null) return await FailAsync("UnknownPasskey");

        // 3. The account itself: deactivated, access window closed, LDAP switched off or disabled in AD, locked out.
        if (completion.BlockedReason(user) is { } blocked) return await FailAsync(blocked, user);
        if (await completion.PasswordlessBlockedReasonAsync(user) is { } passwordless)
            return await FailAsync(passwordless, user);

        // 4. Use it up. Fails for a replay of this very POST, or of any challenge issued before the last successful use.
        if (!await passkeys.TryMarkUsedAsync(verified)) return await FailAsync("InvalidPasskey", user);

        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);
        await completion.CompleteAsync(user, "passkey");
        return Redirect(GetSafeRedirectUrl(logger, user));
    }

    // One generic message whatever went wrong: the page must not tell a stranger whether a passkey, an account or a
    // directory entry exists. The audit log has the real reason.
    private async Task<IActionResult> FailAsync(string reason, ApplicationUser? user = null, string? unknownCredentialId = null)
    {
        UnknownCredentialId = unknownCredentialId;
        await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user?.Id, user?.UserName, new { reason, method = "passkey" });
        Error = TP.FailedError;
        NewChallenge();
        return Page();
    }

    private void NewChallenge()
    {
        var (options, state) = passkeys.BeginAssertion();
        OptionsJson = options;
        State = state;
    }
}
