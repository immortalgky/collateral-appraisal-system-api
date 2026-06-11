namespace Notification.Infrastructure.Email.Templates;

/// <summary>
/// Brand assets shared by the email template (which references the logo via
/// <c>&lt;img src="cid:..."&gt;</c>) and the SMTP sender (which attaches it as an inline
/// linked resource). The LH Bank logo is embedded in the assembly, so it renders without an
/// external fetch, survives image-blocking, and is identical on every app server.
/// </summary>
internal static class EmailBranding
{
    /// <summary>Content-ID the template's logo <c>&lt;img&gt;</c> points at and the inline resource is keyed by.</summary>
    public const string LogoContentId = "lhbank-logo";

    public const string LogoContentType = "image/png";

    public const string LogoFileName = "logo-lhbank-email.png";

    private static readonly Lazy<byte[]> LogoBytesLazy = new(LoadLogo);

    /// <summary>The embedded LH Bank logo PNG bytes (loaded once, cached).</summary>
    public static byte[] LogoBytes => LogoBytesLazy.Value;

    private static byte[] LoadLogo()
    {
        var assembly = typeof(EmailBranding).Assembly;

        // Match by suffix so we are not brittle to the assembly's root-namespace prefix.
        var resourceName = Array.Find(
            assembly.GetManifestResourceNames(),
            n => n.EndsWith(LogoFileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Embedded email logo '{LogoFileName}' not found. Ensure it is included as an " +
                "<EmbeddedResource> in Notification.csproj.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        return ms.ToArray();
    }
}
