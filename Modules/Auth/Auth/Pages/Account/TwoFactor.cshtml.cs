using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

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
    TwoFactorService twoFactor,
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

        // A good authenticator code is "Accepted" here too: the one outcome type serves both second factors.
        var outcome = Recovery ? await SignInWithRecoveryCodeAsync(user) : await SignInWithAuthenticatorCodeAsync();
        switch (outcome)
        {
            case SecondFactorOutcome.Accepted:
                if (Recovery)
                    await auditWriter.RecordAuthEventAsync(AuditAction.RecoveryCodeUsed, user.Id, user.UserName,
                        new { left = await userManager.CountRecoveryCodesAsync(user) });
                else
                    await completion.RememberDeviceAsync(user);

                await completion.CompleteAsync(user, Recovery ? "recovery" : "totp");
                return Redirect(GetSafeRedirectUrl(logger, user));
            case SecondFactorOutcome.SaveFailed:
                await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName, new { reason = "SaveFailed" });
                Error = T2.CodeSaveFailedError;
                return Page();
            default:
                // Deliberate: when Identity refused to save a wrong authenticator code's failed-attempt count, this audit
                // row's save flushes it, which is what keeps wrong codes counted (and the lockout working) on such
                // accounts. Discarding it here would allow unlimited guesses.
                var lockedOut = outcome == SecondFactorOutcome.LockedOut;
                await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName,
                    new { reason = lockedOut ? "LockedOut" : "InvalidTwoFactorCode" });
                Error = lockedOut ? T.LockedOutError : T2.InvalidCodeError;
                return Page();
        }
    }

    // Wrong codes count toward the same lockout as wrong passwords; Identity does that itself here.
    private async Task<SecondFactorOutcome> SignInWithAuthenticatorCodeAsync()
    {
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(
            TwoFactorService.NormalizeCode(Code), isPersistent: false, rememberClient: false);
        return result.Succeeded ? SecondFactorOutcome.Accepted
            : result.IsLockedOut ? SecondFactorOutcome.LockedOut
            : SecondFactorOutcome.Invalid;
    }

    // The redeem and the lockout accounting are TwoFactorService's (shared with the profile's new-device endpoint); what
    // is left here is the lockout refusal and what Identity's own recovery sign-in does after a good code: end the
    // parked-user cookie and sign in.
    private async Task<SecondFactorOutcome> SignInWithRecoveryCodeAsync(ApplicationUser user)
    {
        if (await userManager.IsLockedOutAsync(user)) return SecondFactorOutcome.LockedOut;

        var outcome = await twoFactor.TryRedeemRecoveryCodeAsync(user, Code);
        if (outcome != SecondFactorOutcome.Accepted) return outcome;

        await ClearParkedAsync();
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);
        return outcome;
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
