using System.Security.Claims;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Auth.Infrastructure.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Shared.Exceptions;

namespace Auth.Pages.Account;

/// <summary>
/// Forced enrolment, when the policy requires two-factor and the user has none: scan, confirm with a code, save the
/// recovery codes, then continue. Reached only with the cookie Login parks the user in after the first factor.
/// </summary>
[AllowAnonymous]
[AllowWhilePasswordChangeRequired]
public class TwoFactorSetup(
    SignInManager<ApplicationUser> signInManager,
    TwoFactorService twoFactor,
    LoginCompletion completion,
    IAuthAuditWriter auditWriter,
    BffUrls urls,
    ILogger<TwoFactorSetup> logger)
    : IdpPageModel
{
    // Never rendered back into the page.
    // Null when the field is posted empty.
    [BindProperty] public string? Code { get; set; }

    public string Error { get; private set; } = string.Empty;

    public string SharedKey { get; private set; } = string.Empty;

    public string QrSvg { get; private set; } = string.Empty;

    /// <summary>Set once, on the response to the right code: the page then shows these instead of the QR code.</summary>
    public IReadOnlyList<string>? RecoveryCodes { get; private set; }

    /// <summary>Where "continue" goes: the pending /connect/authorize request.</summary>
    public string ContinueUrl { get; private set; } = "/";

    public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
    {
        if (!IsAuthorizeReturnUrl(returnUrl))
            return Redirect(urls.SpaHome);

        ReturnUrl = returnUrl;
        var user = await completion.GetParkedUserAsync();
        if (user is null) return BackToLogin();
        if (await RefuseIfBlockedAsync(user, completion, auditWriter) is { } blocked) return blocked;
        if (await WrongPageAsync(user) is { } elsewhere) return elsewhere;

        await LoadSetupAsync(user);
        return Page();
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostAsync()
    {
        var user = await completion.GetParkedUserAsync();
        if (user is null) return BackToLogin();
        if (await RefuseIfBlockedAsync(user, completion, auditWriter) is { } blocked) return blocked;
        if (await WrongPageAsync(user) is { } elsewhere) return elsewhere;

        // An account an admin (or the sign-in lockout) has locked stays locked. Wrong enrolment codes are deliberately NOT
        // counted toward that lockout: a typo during first-time setup must not lock anyone out.
        if (await signInManager.UserManager.IsLockedOutAsync(user))
        {
            await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName,
                new { reason = "LockedOut" });
            Error = T.LockedOutError;
            await LoadSetupAsync(user);
            return Page();
        }

        try
        {
            RecoveryCodes = await twoFactor.EnableAsync(user, Code);
        }
        catch (BadRequestException ex)
        {
            var wrongCode = ex.Code == TwoFactorService.InvalidCodeErrorCode;
            if (wrongCode)
                await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName,
                    new { reason = "InvalidTwoFactorCode" });
            Error = wrongCode ? T2.InvalidCodeError : T2.SetupFailedError;
            await LoadSetupAsync(user);
            return Page();
        }

        await auditWriter.RecordAuthEventAsync(AuditAction.TwoFactorEnabled, user.Id, user.UserName,
            new { via = "signIn" });

        // The code just proved the app holds the key, which is the second factor, so the user signs in now. The page
        // that shows the recovery codes is therefore a plain "continue" link, with nothing to replay.
        await ClearParkedAsync();
        await signInManager.SignInWithClaimsAsync(user, isPersistent: false, [new Claim("amr", "mfa")]);
        await completion.RememberDeviceAsync(user);
        await completion.CompleteAsync(user, "enrolled");

        ContinueUrl = GetSafeRedirectUrl(logger, user);
        Response.Headers.CacheControl = "no-store";
        return Page();
    }

    // This page is for a user who must enrol. One who already has two-factor needs the code page, and one the policy does
    // not require (or no longer requires) has no business here: the sign-in starts over and Login decides afresh.
    private async Task<IActionResult?> WrongPageAsync(ApplicationUser user)
    {
        if (user.TwoFactorEnabled) return RedirectToPage("TwoFactor", new { returnUrl = ReturnUrl });
        if (await completion.MustEnrolAsync(user)) return null;

        await ClearParkedAsync();
        return BackToLogin();
    }

    // The key is not shown, and no QR code either, unless it was saved.
    private async Task LoadSetupAsync(ApplicationUser user)
    {
        try
        {
            var stampBefore = await signInManager.UserManager.GetSecurityStampAsync(user);
            var setup = await twoFactor.BeginSetupAsync(user);
            SharedKey = setup.SharedKey;
            QrSvg = setup.QrSvg;

            // Creating the key rotates the security stamp the parked cookie carries, so park again under the new one.
            // Only then: parking on every reload would renew the five minutes each time, and the parked user would
            // never expire.
            if (await signInManager.UserManager.GetSecurityStampAsync(user) != stampBefore)
                await completion.ParkAsync(user);
        }
        catch (BadRequestException)
        {
            Error = T2.SetupFailedError;
            SharedKey = QrSvg = string.Empty;
        }
    }
}
