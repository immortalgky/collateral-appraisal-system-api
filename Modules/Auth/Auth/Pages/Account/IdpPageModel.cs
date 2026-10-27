using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using Auth.Application.Services;
using Auth.Domain.Auditing;
using Microsoft.AspNetCore.Authentication;

namespace Auth.Pages.Account;

/// <summary>What the sign-in page and the two-factor pages share: language, cache-buster, the return URL and its safety rules.</summary>
public abstract class IdpPageModel : PageModel
{
    [BindProperty] public string ReturnUrl { get; set; } = "/";

    /// <summary>Cache-buster for /Assets/index.css and /Assets/flags (the fonts are loaded from the CSS without it). <c>asp-append-version</c> cannot be used:
    /// /Assets is served by its own file provider, not the web root it looks in. A short hash of those files' content,
    /// computed once, lazily, on first IdP page use, so it changes exactly when they do.</summary>
    public static readonly string AssetVersion = HashAssets();

    private static string HashAssets()
    {
        try
        {
            // A missing folder just hashes nothing.
            var root = Path.Combine(AppContext.BaseDirectory, "Assets");
            var files = new[] { ("", "index.css"), ("flags", "*") }
                .Select(d => (Dir: Path.Combine(root, d.Item1), Pattern: d.Item2))
                .Where(d => Directory.Exists(d.Dir))
                .SelectMany(d => Directory.GetFiles(d.Dir, d.Pattern))
                .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
                .Order(StringComparer.Ordinal);

            // Relative names and ordinal order, so every node and culture gets the same hash for the same files.
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            foreach (var file in files)
            {
                hash.AppendData(System.Text.Encoding.UTF8.GetBytes(file));
                hash.AppendData(System.IO.File.ReadAllBytes(Path.Combine(root, file)));
            }

            return Convert.ToHexStringLower(hash.GetHashAndReset())[..8];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"IdP asset hash failed, falling back to the build id: {ex.Message}");
            // An exception must never escape a static initializer: an unreadable file must not take down every IdP page.
            // A last resort: the build id does not change for a CSS or flag-only change (deterministic builds), so
            // browsers may keep the old files.
            return typeof(IdpPageModel).Assembly.ManifestModule.ModuleVersionId.ToString("N")[..8];
        }
    }

    private string? _lang;

    public string Lang => _lang ??= LoginText.Resolve(
        Request.Cookies[LoginText.CookieName], Request.Headers.AcceptLanguage.ToString());

    public LoginText T => LoginText.For(Lang);

    public TwoFactorText T2 => TwoFactorText.For(Lang);

    public PasskeyText TP => PasskeyText.For(Lang);

    /// <summary>
    /// The only legitimate way to these pages is the OIDC flow, which arrives with the pending /connect/authorize
    /// request as its return URL. A bookmark, a typed address or a stale tab has none; signing in from it would mint a
    /// session nobody asked for.
    /// </summary>
    protected bool IsAuthorizeReturnUrl([NotNullWhen(true)] string? returnUrl) =>
        returnUrl is not null && Url.IsLocalUrl(returnUrl)
                              && returnUrl.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase);

    protected string GetSafeRedirectUrl(ILogger logger, ApplicationUser? user = null)
    {
        var redirectUrl = string.IsNullOrEmpty(ReturnUrl) ? "/" : ReturnUrl;

        // Local URLs only: an absolute one would let a crafted link bounce a freshly signed-in user to
        // any site. The legitimate target is always /connect/authorize on this host.
        if (!Url.IsLocalUrl(redirectUrl))
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

    protected IActionResult BackToLogin() => RedirectToPage("Login", new { returnUrl = ReturnUrl });

    /// <summary>Drops the cookie that parks a user between the first and the second sign-in step.</summary>
    protected Task ClearParkedAsync() => HttpContext.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);

    /// <summary>
    /// The gate of the pages after the first factor: the account can be deactivated or its access window can close
    /// between the password and the code. Returns where to send the browser, or null when the user may go on.
    /// </summary>
    protected async Task<IActionResult?> RefuseIfBlockedAsync(
        ApplicationUser user, LoginCompletion completion, IAuthAuditWriter auditWriter)
    {
        if (completion.BlockedReason(user) is not { } reason) return null;

        await auditWriter.RecordAuthEventAsync(AuditAction.LoginFailed, user.Id, user.UserName, new { reason });
        await ClearParkedAsync();
        return BackToLogin();
    }
}
