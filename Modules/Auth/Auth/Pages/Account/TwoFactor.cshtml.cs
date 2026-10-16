using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using IdentitySignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace Auth.Pages.Account;

/// <summary>
/// Second step of a sign-in: the authenticator code, or a recovery code when <see cref="Recovery"/> is set. Reached
/// only with the cookie Login parks the user in once the password (or LDAP bind) was right.
/// </summary>
[AllowAnonymous]
[AllowWhilePasswordChangeRequired]
public class TwoFactor(
    SignInManager<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    LoginCompletion completion,
    IAuthAuditWriter auditWriter,
    BffUrls urls,
    ILogger<TwoFactor> logger)
    : IdpPageModel
{
    [BindProperty(SupportsGet = true)] public bool Recovery { get; set; }

    // Never rendered back into the page.
    // Null when the field is posted empty.
    [BindProperty] public string? Code { get; set; }

    public string Error { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (!IsAuthorizeReturnUrl(returnUrl))
            return Redirect(urls.SpaHome);

        ReturnUrl = returnUrl;
        // Nobody parked here (the cookie lives five minutes): start the sign-in over.
        var user = await completion.GetParkedUserAsync();
        if (user is null) return BackToLogin();
        return await RefuseIfBlockedAsync(user, completion, auditWriter) ?? await ToEnrolmentIfNoKeyAsync(user) ?? Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await completion.GetParkedUserAsync();
        if (user is null) return BackToLogin();
        if (await RefuseIfBlockedAsync(user, completion, auditWriter) is { } blocked) return blocked;
        if (await ToEnrolmentIfNoKeyAsync(user) is { } enrol) return enrol;

        var result = Recovery ? await SignInWithRecoveryCodeAsync(user) : await SignInWithAuthenticatorCodeAsync();
        if (result.Succeeded)
        {
            if (Recovery)
                await auditWriter.RecordAuthEventAsync(AuditAction.RecoveryCodeUsed, user.Id, user.UserName,
                    new { left = await userManager.CountRecoveryCodesAsync(user) });
            else
                await completion.RememberDeviceAsync(user);

            await completion.CompleteAsync(user, Recovery ? "recovery" : "totp");
            return Redirect(GetSafeRedirectUrl(logger, user));
        }

        await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName,
            new { reason = result.IsLockedOut ? "LockedOut" : "InvalidTwoFactorCode" });
        Error = result.IsLockedOut ? T.LockedOutError : T2.InvalidCodeError;
        return Page();
    }

    // Wrong codes count toward the same lockout as wrong passwords; Identity does that itself here.
    private Task<IdentitySignInResult> SignInWithAuthenticatorCodeAsync() =>
        signInManager.TwoFactorAuthenticatorSignInAsync(
            TwoFactorService.NormalizeCode(Code), isPersistent: false, rememberClient: false);

    // Identity's recovery sign-in neither checks nor feeds the lockout, so both are done here.
    private async Task<IdentitySignInResult> SignInWithRecoveryCodeAsync(ApplicationUser user)
    {
        if (await userManager.IsLockedOutAsync(user))
            return IdentitySignInResult.LockedOut;

        // Identity throws on an empty code instead of refusing it.
        var typed = TwoFactorService.NormalizeRecoveryCode(Code);
        var result = typed.Length == 0
            ? IdentitySignInResult.Failed
            : await signInManager.TwoFactorRecoveryCodeSignInAsync(typed);
        if (result.Succeeded) return result;

        // This attempt may be the one that locks the account: say so now, as the next attempt would.
        await userManager.AccessFailedAsync(user);
        return await userManager.IsLockedOutAsync(user) ? IdentitySignInResult.LockedOut : result;
    }

    // A user with no two-factor has no key to check a code against. If the policy makes them enrol, that is the page for
    // them; if not (an admin reset their two-factor while they were parked here), the sign-in starts over and Login
    // now lets the password through.
    private async Task<IActionResult?> ToEnrolmentIfNoKeyAsync(ApplicationUser user)
    {
        if (user.TwoFactorEnabled) return null;
        if (await completion.MustEnrolAsync(user))
            return RedirectToPage("TwoFactorSetup", new { returnUrl = ReturnUrl });

        await ClearParkedAsync();
        return BackToLogin();
    }
}
