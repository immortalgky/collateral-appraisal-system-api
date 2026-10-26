using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Shared.Exceptions;

namespace Auth.Application.Services;

/// <summary>How a typed second-factor code fared: what <see cref="TwoFactorService.TryRedeemRecoveryCodeAsync"/> and the IdP code page make of it.</summary>
public enum SecondFactorOutcome
{
    /// <summary>The code was good (a recovery code is now spent; an authenticator code is not used up).</summary>
    Accepted,

    /// <summary>The code was not accepted. Counted toward the lockout where the caller counts it.</summary>
    Invalid,

    /// <summary>The account is locked out, already or by this very attempt.</summary>
    LockedOut,

    /// <summary>
    /// Identity could not save the spent recovery code, or could not save a wrong code's failed-attempt count (deliberately
    /// the same answer as for a right code, so such an account cannot be probed). Not counted toward the lockout.
    /// </summary>
    SaveFailed
}

/// <summary>Everything the user needs to add the account to an authenticator app.</summary>
public sealed record TwoFactorSetupInfo(string SharedKey, string AuthenticatorUri, string QrSvg);

/// <summary>
/// Authenticator-app (TOTP) enrolment, shared by the profile endpoints and the IdP enrolment page. The key and the
/// recovery codes live in Identity's own store (<c>auth.AspNetUserTokens</c>). <c>TwoFactorEnabled</c> is only ever set
/// after a code from the app has been verified, so a half-finished enrolment never locks the account.
/// </summary>
public class TwoFactorService(
    UserManager<ApplicationUser> userManager,
    IPasswordPolicyProvider policyProvider,
    AuthDbContext dbContext,
    ILogger<TwoFactorService> logger)
{
    public const string Issuer = "Collateral Appraisal System";

    /// <summary>The <c>errorCode</c> of each 400, for the client to translate. The English <c>detail</c> stays as it is.</summary>
    public const string InvalidCodeErrorCode = "InvalidTwoFactorCode";
    public const string LockedOutErrorCode = "TwoFactorLockedOut";
    public const string AlreadyEnabledErrorCode = "TwoFactorAlreadyEnabled";
    public const string NotEnabledErrorCode = "TwoFactorNotEnabled";
    public const string RequiredByPolicyErrorCode = "TwoFactorRequiredByPolicy";
    public const string SaveFailedErrorCode = "TwoFactorSaveFailed";
    public const int RecoveryCodeCount = 10;

    /// <summary>What users type is often copied as "123 456" or "123-456".</summary>
    public static string NormalizeCode(string? code) =>
        string.Concat((code ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-'));

    /// <summary>
    /// A recovery code as typed: any case, with or without the hyphen Identity puts in the middle ("ABCDE-12345"). Only
    /// letters and digits are kept; ten of them are put back into that shape. Anything else is returned as is and simply
    /// will not match.
    /// </summary>
    public static string NormalizeRecoveryCode(string? code)
    {
        var kept = string.Concat((code ?? "").Where(char.IsAsciiLetterOrDigit)).ToUpperInvariant();
        return kept.Length == 10 ? $"{kept[..5]}-{kept[5..]}" : kept;
    }

    /// <summary>
    /// The authenticator key, created on first use. An unverified key that already exists is kept, so a refresh or a
    /// second click neither invalidates the QR code the user already scanned nor rotates the security stamp.
    /// </summary>
    public async Task<TwoFactorSetupInfo> BeginSetupAsync(ApplicationUser user)
    {
        if (user.TwoFactorEnabled) throw AlreadyEnabled();

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            Check(user, await userManager.ResetAuthenticatorKeyAsync(user));
            key = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var uri = BuildUri(user.UserName!, key!);
        return new TwoFactorSetupInfo(FormatKey(key!), uri, RenderQr(uri));
    }

    public Task<bool> VerifyAsync(ApplicationUser user, string? code) =>
        userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, NormalizeCode(code));

    /// <summary>Turns two-factor on once <paramref name="code"/> proves the app holds the key. Returns the recovery codes, shown once.</summary>
    public async Task<IReadOnlyList<string>> EnableAsync(ApplicationUser user, string? code)
    {
        if (user.TwoFactorEnabled) throw AlreadyEnabled();
        if (!await VerifyAsync(user, code)) throw InvalidCode();

        // Codes first: if switching on then fails, nothing is left on without its recovery codes.
        var codes = await NewRecoveryCodesAsync(user);
        Check(user, await userManager.SetTwoFactorEnabledAsync(user, true));
        return codes;
    }

    /// <summary>Verifies the code for a user who must already have two-factor on, then issues a fresh set of recovery codes.</summary>
    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(ApplicationUser user, string? code)
    {
        if (!user.TwoFactorEnabled) throw NotEnabled();
        await VerifyGuardedAsync(user, code);

        return await NewRecoveryCodesAsync(user);
    }

    /// <summary>
    /// Checks that two-factor may be turned off and that the code is right; the caller then does <see cref="ResetAsync"/>
    /// in a transaction of its own (this part must stay outside it: a wrong code's count has to survive the 400).
    /// Refused when the policy requires two-factor for this user.
    /// </summary>
    public async Task VerifyForDisableAsync(ApplicationUser user, string? code)
    {
        if (!user.TwoFactorEnabled) throw NotEnabled();
        if ((await policyProvider.GetAsync()).RequiresTwoFactor(user.CompanyId))
            throw new BadRequestException(
                "Two-factor authentication is required for your account and cannot be turned off.", null, RequiredByPolicyErrorCode);
        await VerifyGuardedAsync(user, code);
    }

    /// <summary>
    /// For the user who lost their phone but still holds a recovery code (or has the phone): proves the second factor with
    /// either, without the policy refusal <see cref="VerifyForDisableAsync"/> has, since the point is to set it up again on a new
    /// device. Returns true when a recovery code (now spent) was the proof, so the caller can record that before anything
    /// else can fail. The caller then clears the failed-attempt count and resets the account.
    /// </summary>
    public async Task<bool> VerifyForNewDeviceAsync(ApplicationUser user, string? code)
    {
        if (!user.TwoFactorEnabled) throw NotEnabled();
        if (await userManager.IsLockedOutAsync(user)) throw LockedOut();

        var usedRecoveryCode = !await VerifyAsync(user, code);
        if (usedRecoveryCode)
            switch (await TryRedeemRecoveryCodeAsync(user, code))
            {
                case SecondFactorOutcome.Accepted: break;
                case SecondFactorOutcome.LockedOut: throw LockedOut();
                case SecondFactorOutcome.SaveFailed: throw SaveFailed(); // already logged and discarded by the helper behind it
                default: throw InvalidCode();
            }

        return usedRecoveryCode;
    }

    /// <summary>
    /// Spends a typed recovery code. Shared by the IdP code page and <see cref="VerifyForNewDeviceAsync"/> so the two cannot
    /// drift. The caller has already refused a locked-out account. Identity's own recovery sign-in does not feed the
    /// lockout, so it is done here: a wrong code counts like a wrong password, and the attempt that locks the account says
    /// so now, as the next one would. An empty code is never handed to Identity, which throws on it.
    /// </summary>
    public async Task<SecondFactorOutcome> TryRedeemRecoveryCodeAsync(ApplicationUser user, string? code)
    {
        var typed = NormalizeRecoveryCode(code);
        if (typed.Length > 0)
        {
            var result = await userManager.RedeemTwoFactorRecoveryCodeAsync(user, typed);
            if (result.Succeeded) return SecondFactorOutcome.Accepted;

            // "Not one of this account's codes" is Identity's own RecoveryCodeRedemptionFailed; any other error is the save.
            if (result.Errors.Any(e => e.Code != nameof(IdentityErrorDescriber.RecoveryCodeRedemptionFailed)))
            {
                SaveRefused(user, "Recovery code", result); // logged and discarded; the outcome says so
                return SecondFactorOutcome.SaveFailed;
            }
        }

        if (await CountFailureAsync(user) is not null) return SecondFactorOutcome.SaveFailed;
        return await userManager.IsLockedOutAsync(user) ? SecondFactorOutcome.LockedOut : SecondFactorOutcome.Invalid;
    }

    /// <summary>
    /// Turns two-factor off and removes the authenticator key and the recovery codes: the account has neither afterwards,
    /// and the next set-up creates a new key. The security stamp is rotated explicitly, which also ends every remembered
    /// device, parked sign-in and IdP session cookie.
    /// </summary>
    public async Task ResetAsync(ApplicationUser user)
    {
        Check(user, await userManager.SetTwoFactorEnabledAsync(user, false));
        if (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0) is null) throw SaveRefused(user, "Recovery codes");
        Check(user, await userManager.RemoveAuthenticationTokenAsync(user, AuthenticatorTokenProvider, AuthenticatorKeyToken));
        Check(user, await userManager.UpdateSecurityStampAsync(user));
    }

    // Identity's own names for where the key lives (auth.AspNetUserTokens); it has no public constants for them.
    private const string AuthenticatorTokenProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyToken = "AuthenticatorKey";

    /// <summary>
    /// For the endpoints that guard an already-enabled account (disable, new recovery codes): wrong codes feed the same
    /// lockout as wrong passwords, and a locked-out account is refused outright, the attempt that locks it included.
    /// Always a 400, never a 401.
    /// </summary>
    private async Task VerifyGuardedAsync(ApplicationUser user, string? code)
    {
        if (await userManager.IsLockedOutAsync(user)) throw LockedOut();

        if (!await VerifyAsync(user, code))
        {
            if (await CountFailureAsync(user) is { } refused) throw refused;
            throw await userManager.IsLockedOutAsync(user) ? LockedOut() : InvalidCode();
        }

        await ClearFailedAttemptsAsync(user);
    }

    /// <summary>A right code ends the run of wrong ones. Answers "save failed" when Identity refuses, like every other write.</summary>
    public async Task ClearFailedAttemptsAsync(ApplicationUser user)
    {
        var result = await userManager.ResetAccessFailedCountAsync(user);
        if (!result.Succeeded) throw SaveRefused(user, "Failed-attempt reset", result);
    }

    /// <summary>
    /// Counts a wrong code toward the lockout. Null when counted; otherwise the "save failed" 400 for when Identity refused
    /// to save the count (an account its validators reject). The caller answers that rather than "invalid code", so for
    /// such an account a right and a wrong code look the same and cannot be told apart by guessing.
    /// </summary>
    private async Task<BadRequestException?> CountFailureAsync(ApplicationUser user)
    {
        var result = await userManager.AccessFailedAsync(user);
        return result.Succeeded ? null : SaveRefused(user, "Failed two-factor attempt", result);
    }

    /// <summary>
    /// The one place an Identity refusal is handled: logs it, puts back what Identity left pending (so nothing that saves
    /// next flushes it) and returns the "save failed" 400 for the caller to throw, or to turn into an outcome.
    /// </summary>
    private BadRequestException SaveRefused(ApplicationUser user, string what, IdentityResult? result = null)
    {
        logger.LogWarning("{What} refused by Identity: {Errors}", what,
            string.Join("; ", result?.Errors.Select(e => e.Code) ?? []));
        DiscardPendingChangesOf(user);
        return SaveFailed();
    }

    /// <summary>
    /// Identity leaves a change it refused pending in the request's context, and whatever saves next would flush it.
    /// Reverts every pending change on the user entity and on the user's token rows (all of them, not just the refused
    /// one: nothing of this user is meant to be saved after a refusal) and leaves any other pending work of the scope alone.
    /// </summary>
    private void DiscardPendingChangesOf(ApplicationUser user)
    {
        var touched = dbContext.ChangeTracker.Entries()
            .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached
                        && (e.Entity == user || e.Entity is IdentityUserToken<Guid> token && token.UserId == user.Id))
            .ToList();

        foreach (var entry in touched)
        {
            if (entry.State == EntityState.Added)
            {
                entry.State = EntityState.Detached;
                continue;
            }

            entry.CurrentValues.SetValues(entry.OriginalValues);
            entry.State = EntityState.Unchanged;
        }
    }

    private async Task<IReadOnlyList<string>> NewRecoveryCodesAsync(ApplicationUser user) =>
        (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList()
        ?? throw SaveRefused(user, "Recovery codes");

    private static BadRequestException AlreadyEnabled() =>
        new("Two-factor authentication is already on.", null, AlreadyEnabledErrorCode);

    private static BadRequestException NotEnabled() =>
        new("Two-factor authentication is not on.", null, NotEnabledErrorCode);

    private static BadRequestException LockedOut() =>
        new("The account is locked out. Try again later or ask your administrator.", null, LockedOutErrorCode);

    private static BadRequestException InvalidCode() =>
        new("The code is not valid. Check your authenticator app and try again.", null, InvalidCodeErrorCode);

    // Identity refuses an update for reasons that have nothing to do with two-factor (a duplicate e-mail on an old
    // account, say). That is a 400 the caller can show, not a 500, and the details stay in the log.
    // Built by SaveRefused, which logs the refusal and discards its pending change. The one other use is
    // VerifyForNewDeviceAsync, which rethrows an outcome whose refusal SaveRefused has already handled.
    private static BadRequestException SaveFailed() =>
        new("Two-factor settings could not be saved. Please contact your administrator.", null, SaveFailedErrorCode);

    private void Check(ApplicationUser user, IdentityResult result)
    {
        if (!result.Succeeded) throw SaveRefused(user, "Two-factor change", result);
    }

    /// <summary>The otpauth URI authenticator apps read from the QR code.</summary>
    internal static string BuildUri(string username, string key)
    {
        var issuer = Uri.EscapeDataString(Issuer);
        return $"otpauth://totp/{issuer}:{Uri.EscapeDataString(username)}?secret={key}&issuer={issuer}&digits=6";
    }

    /// <summary>The key to type by hand: lower case, in groups of four.</summary>
    internal static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));

    private static string RenderQr(string uri)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        // The ViewBox sizing leaves the width to the page's CSS.
        return new SvgQRCode(data).GetGraphic(
            4, "#000000", "#ffffff", drawQuietZones: true, SvgQRCode.SizingMode.ViewBoxAttribute);
    }
}
