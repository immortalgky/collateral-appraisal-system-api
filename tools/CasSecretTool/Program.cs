using System.Security.Cryptography.X509Certificates;
using Shared.Security;

// CasSecretTool — encrypts / verifies configuration secrets for appsettings.Production.json.
//
// Runs on the app server, where the secrets certificate lives in LocalMachine\My with its
// private key. Uses the SAME Shared.Security.SecretProtector the application uses to decrypt at
// startup, so an encrypted value can never be one the app cannot read.
//
// Usage:
//   CasSecretTool                                  interactive (recommended)
//   CasSecretTool protect --thumbprint <T>         scriptable; prompts for the value (no echo)
//   CasSecretTool verify  --thumbprint <T> --value ENC:v1:...

return RunSafely(args);

static int RunSafely(string[] args)
{
    try
    {
        return args.Length == 0 ? RunInteractive() : RunWithArgs(args);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Error: {ex.Message}");
        return 1;
    }
}

static int RunInteractive()
{
    Console.WriteLine("CAS Secret Tool");
    Console.WriteLine("===============");

    var cert = ChooseCertificate();
    if (cert is null) return 1;

    Console.Write("(e)ncrypt or (v)erify: ");
    var mode = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

    if (mode.StartsWith('e'))
    {
        var value = ReadSecret("Value: ");
        if (string.IsNullOrEmpty(value)) { Console.Error.WriteLine("No value entered."); return 1; }
        var encrypted = SecretProtector.Protect(value, cert);
        Console.WriteLine();
        Console.WriteLine(encrypted);
        TryCopyToClipboard(encrypted);
        return 0;
    }

    if (mode.StartsWith('v'))
    {
        Console.Write("Paste ENC:v1: value: ");
        var enc = (Console.ReadLine() ?? "").Trim();
        return Verify(enc, cert);
    }

    Console.Error.WriteLine("Unknown choice.");
    return 1;
}

static int RunWithArgs(string[] args)
{
    var command = args[0].ToLowerInvariant();
    var thumbprint = GetOption(args, "--thumbprint")
        ?? throw new ArgumentException("--thumbprint is required.");
    var cert = CertificateProvider.LoadFromStoreByThumbprint(thumbprint, requirePrivateKey: true);

    switch (command)
    {
        case "protect":
            var value = ReadSecret("Value: ");
            if (string.IsNullOrEmpty(value)) { Console.Error.WriteLine("No value entered."); return 1; }
            Console.WriteLine(SecretProtector.Protect(value, cert));
            return 0;

        case "verify":
            var enc = GetOption(args, "--value")
                ?? throw new ArgumentException("--value is required for verify.");
            return Verify(enc, cert);

        default:
            Console.Error.WriteLine($"Unknown command '{command}'. Use protect or verify.");
            return 1;
    }
}

static int Verify(string enc, X509Certificate2 cert)
{
    if (!SecretProtector.IsProtected(enc))
    {
        Console.Error.WriteLine($"Not an {SecretProtector.Prefix} value.");
        return 1;
    }

    var plaintext = SecretProtector.Unprotect(enc, cert);
    Console.WriteLine($"OK — decrypts successfully to: {Mask(plaintext)}");
    return 0;
}

// --- certificate selection ---------------------------------------------------

static X509Certificate2? ChooseCertificate()
{
    var certs = EnumeratePrivateKeyCertificates();
    if (certs.Count == 0)
    {
        Console.Error.WriteLine("No certificates with a private key found in LocalMachine\\My or CurrentUser\\My.");
        return null;
    }

    Console.WriteLine("Certificates with a private key:");
    for (var i = 0; i < certs.Count; i++)
    {
        var c = certs[i];
        Console.WriteLine($"  [{i + 1}] {c.Subject}  ({c.Thumbprint})  [{c.Location}]");
    }

    Console.Write("Select cert: ");
    if (!int.TryParse(Console.ReadLine(), out var choice) || choice < 1 || choice > certs.Count)
    {
        Console.Error.WriteLine("Invalid selection.");
        return null;
    }

    // Reuse the same loader the app uses, so selection and runtime load are identical.
    return CertificateProvider.LoadFromStoreByThumbprint(certs[choice - 1].Thumbprint, requirePrivateKey: true);
}

static List<CertInfo> EnumeratePrivateKeyCertificates()
{
    var result = new List<CertInfo>();
    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var location in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
    {
        try
        {
            using var store = new X509Store(StoreName.My, location);
            store.Open(OpenFlags.ReadOnly);
            foreach (var cert in store.Certificates)
            {
                if (cert.HasPrivateKey && seen.Add(cert.Thumbprint))
                    result.Add(new CertInfo(cert.Subject, cert.Thumbprint, location.ToString()));
            }
        }
        catch
        {
            // Store unavailable on this platform/location — skip it.
        }
    }

    return result;
}

// --- helpers -----------------------------------------------------------------

static string ReadSecret(string prompt)
{
    // Prompt on stderr so that stdout carries only the ENC:v1: result (safe to capture in a pipe).
    Console.Error.Write(prompt);

    // When stdin is redirected (scripting / CI) ReadKey is unavailable — fall back to a plain
    // read. Masking is only possible on an interactive terminal anyway.
    if (Console.IsInputRedirected)
        return (Console.ReadLine() ?? "").TrimEnd('\r', '\n');

    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace)
        {
            if (chars.Count > 0) chars.RemoveAt(chars.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string(chars.ToArray());
}

static string Mask(string value)
{
    if (value.Length <= 3) return new string('*', value.Length);
    return value[..3] + new string('*', Math.Min(value.Length - 3, 8));
}

static string? GetOption(string[] args, string name)
{
    var idx = Array.FindIndex(args, a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : null;
}

static void TryCopyToClipboard(string text)
{
    // Best-effort only; ignore if the platform tool is missing.
    string? tool = OperatingSystem.IsWindows() ? "clip"
        : OperatingSystem.IsMacOS() ? "pbcopy"
        : null;
    if (tool is null) return;

    try
    {
        var psi = new System.Diagnostics.ProcessStartInfo(tool) { RedirectStandardInput = true, UseShellExecute = false };
        using var proc = System.Diagnostics.Process.Start(psi);
        if (proc is null) return;
        proc.StandardInput.Write(text);
        proc.StandardInput.Close();
        proc.WaitForExit();
        Console.WriteLine("(copied to clipboard)");
    }
    catch
    {
        // No clipboard available — the value is already printed above.
    }
}

internal readonly record struct CertInfo(string Subject, string Thumbprint, string Location);
