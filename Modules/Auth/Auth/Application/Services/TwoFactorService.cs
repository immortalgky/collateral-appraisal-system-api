using Auth.Infrastructure.Configuration;
using Microsoft.AspNetCore.Identity;
using QRCoder;
using Shared.Exceptions;

namespace Auth.Application.Services;

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
        if (user.TwoFactorEnabled) throw new BadRequestException("Two-factor authentication is already on.", null, AlreadyEnabledErrorCode);

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            Check(await userManager.ResetAuthenticatorKeyAsync(user));
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
        if (user.TwoFactorEnabled) throw new BadRequestException("Two-factor authentication is already on.", null, AlreadyEnabledErrorCode);
        if (!await VerifyAsync(user, code)) throw InvalidCode();

        // Codes first: if switching on then fails, nothing is left on without its recovery codes.
        var codes = await NewRecoveryCodesAsync(user);
        Check(await userManager.SetTwoFactorEnabledAsync(user, true));
        return codes;
    }

    /// <summary>Verifies the code for a user who must already have two-factor on, then issues a fresh set of recovery codes.</summary>
    public async Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(ApplicationUser user, string? code)
    {
        if (!user.TwoFactorEnabled) throw new BadRequestException("Two-factor authentication is not on.", null, NotEnabledErrorCode);
        await VerifyGuardedAsync(user, code);

        return await NewRecoveryCodesAsync(user);
    }

    /// <summary>Verifies the code, then turns two-factor off. Refused when the policy requires it for this user.</summary>
    public async Task DisableAsync(ApplicationUser user, string? code)
    {
        if (!user.TwoFactorEnabled) throw new BadRequestException("Two-factor authentication is not on.", null, NotEnabledErrorCode);
        if ((await policyProvider.GetAsync()).RequiresTwoFactor(user.CompanyId))
            throw new BadRequestException(
                "Two-factor authentication is required for your account and cannot be turned off.", null, RequiredByPolicyErrorCode);
        await VerifyGuardedAsync(user, code);

        await ResetAsync(user);
    }

    /// <summary>
    /// Turns two-factor off and removes the authenticator key and the recovery codes: the account has neither afterwards,
    /// and the next set-up creates a new key. The security stamp is rotated explicitly, which also ends every remembered
    /// device, parked sign-in and IdP session cookie.
    /// </summary>
    public async Task ResetAsync(ApplicationUser user)
    {
        Check(await userManager.SetTwoFactorEnabledAsync(user, false));
        if (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 0) is null) throw SaveFailed();
        Check(await userManager.RemoveAuthenticationTokenAsync(user, AuthenticatorTokenProvider, AuthenticatorKeyToken));
        Check(await userManager.UpdateSecurityStampAsync(user));
    }

    // Identity's own names for where the key lives (auth.AspNetUserTokens); it has no public constants for them.
    private const string AuthenticatorTokenProvider = "[AspNetUserStore]";
    private const string AuthenticatorKeyToken = "AuthenticatorKey";

    /// <summary>
    /// For the endpoints that guard an already-enabled account (disable, new recovery codes): wrong codes feed the same
    /// lockout as wrong passwords, and a locked-out account is refused outright. Always a 400, never a 401.
    /// </summary>
    private async Task VerifyGuardedAsync(ApplicationUser user, string? code)
    {
        if (await userManager.IsLockedOutAsync(user))
            throw new BadRequestException(
                "The account is locked out. Try again later or ask your administrator.", null, LockedOutErrorCode);

        if (!await VerifyAsync(user, code))
        {
            await userManager.AccessFailedAsync(user);
            throw InvalidCode();
        }

        await userManager.ResetAccessFailedCountAsync(user);
    }

    private async Task<IReadOnlyList<string>> NewRecoveryCodesAsync(ApplicationUser user) =>
        (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodeCount))?.ToList()
        ?? throw SaveFailed();

    private static BadRequestException InvalidCode() =>
        new("The code is not valid. Check your authenticator app and try again.", null, InvalidCodeErrorCode);

    // Identity refuses an update for reasons that have nothing to do with two-factor (a duplicate e-mail on an old
    // account, say). That is a 400 the caller can show, not a 500, and the details stay in the log.
    private static BadRequestException SaveFailed() =>
        new("Two-factor settings could not be saved. Please contact your administrator.", null, SaveFailedErrorCode);

    private void Check(IdentityResult result)
    {
        if (result.Succeeded) return;

        logger.LogWarning("Two-factor change refused by Identity: {Errors}",
            string.Join("; ", result.Errors.Select(e => e.Code)));
        throw SaveFailed();
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
