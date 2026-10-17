using System.Text.Json;
using Auth.Domain.Passkeys;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.WebUtilities;
using Shared.Exceptions;
using Shared.Time;

namespace Auth.Application.Services;

/// <summary>The <c>errorCode</c>s of the passkey endpoints' 400s, for the client to translate. The English <c>detail</c> stays as it is.</summary>
public static class PasskeyErrors
{
    public const string ReauthRequired = "PasskeyReauthRequired";
    public const string LimitReached = "PasskeyLimitReached";
    public const string StateExpired = "PasskeyStateExpired";
    public const string RegistrationFailed = "PasskeyRegistrationFailed";
    public const string AlreadyRegistered = "PasskeyAlreadyRegistered";
    public const string NameInvalid = "PasskeyNameInvalid";
}

/// <summary>A passkey assertion that checked out, waiting for the caller's own checks and <see cref="PasskeyService.TryMarkUsedAsync"/>.</summary>
public sealed record AssertionVerified(UserPasskey Passkey, uint SignCount, bool BackedUp, DateTime IssuedAt);

/// <summary>
/// WebAuthn registration and sign-in. There is no server-side store for challenges: each is sealed with Data Protection
/// into a short-lived state the browser carries back, so any node can finish what another began.
/// </summary>
public class PasskeyService(
    PasskeySettings settings,
    IFido2 fido2,
    PasskeyStateSealer sealer,
    AuthDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<PasskeyService> logger)
{
    private string Seal(string purpose, string optionsJson) =>
        sealer.Seal(purpose, optionsJson, dateTimeProvider.ApplicationNow);

    // ---- name ---------------------------------------------------------------------------------------------------------

    /// <summary>The trimmed name, or a 400 <c>PasskeyNameInvalid</c>: 1–100 characters, none of them a control character.</summary>
    public static string ValidName(string? name)
    {
        var trimmed = name?.Trim() ?? "";
        if (trimmed.Length is 0 or > UserPasskey.MaxNameLength || trimmed.Any(char.IsControl))
            throw new BadRequestException(
                $"Give the passkey a name of 1 to {UserPasskey.MaxNameLength} characters.", null, PasskeyErrors.NameInvalid);
        return trimmed;
    }

    // ---- registration -------------------------------------------------------------------------------------------------

    /// <summary>Options for <c>navigator.credentials.create</c> (as JSON the browser can parse) and the state to send back with the result.</summary>
    public async Task<(string OptionsJson, string State)> BeginRegistrationAsync(ApplicationUser user)
    {
        var existing = await dbContext.UserPasskeys.AsNoTracking().Where(p => p.UserId == user.Id)
            .Select(p => new { p.CredentialId, p.Transports }).ToListAsync();
        if (existing.Count >= UserPasskey.MaxPerUser) throw LimitReached();

        var options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = new Fido2User
            {
                Id = user.Id.ToByteArray(),
                Name = user.UserName!,
                DisplayName = $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } full ? full : user.UserName!
            },
            // The browser then refuses to register an authenticator that already holds a passkey for this account.
            ExcludeCredentials = existing.Select(p => new PublicKeyCredentialDescriptor(
                PublicKeyCredentialType.PublicKey, p.CredentialId, ParseTransports(p.Transports))).ToList(),
            // Discoverable (the sign-in page does not know who is signing in) and always user-verified: the passkey is the
            // second factor too, so a PIN or biometric must have been used.
            AuthenticatorSelection = new AuthenticatorSelection
            {
                ResidentKey = ResidentKeyRequirement.Required,
                UserVerification = UserVerificationRequirement.Required
            },
            AttestationPreference = AttestationConveyancePreference.None,
            PubKeyCredParams = [PubKeyCredParam.ES256, PubKeyCredParam.RS256]
        });

        var json = options.ToJson();
        return (json, Seal(PasskeyStateSealer.RegisterPurpose(user.Id), json));
    }

    public async Task<UserPasskey> FinishRegistrationAsync(
        ApplicationUser user, string? sealedState, string? name, JsonElement credential)
    {
        var passkeyName = ValidName(name);
        var state = sealer.Unseal(PasskeyStateSealer.RegisterPurpose(user.Id), sealedState)
                    ?? throw new BadRequestException(
                        "The registration took too long or did not start here. Try again.", null, PasskeyErrors.StateExpired);

        if (await dbContext.UserPasskeys.CountAsync(p => p.UserId == user.Id) >= UserPasskey.MaxPerUser)
            throw LimitReached();

        RegisteredPublicKeyCredential registered;
        try
        {
            if (credential.ValueKind != JsonValueKind.Object) throw new JsonException("No credential.");
            var response = credential.Deserialize<AuthenticatorAttestationRawResponse>()
                           ?? throw new JsonException("Empty credential.");
            registered = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = response,
                OriginalOptions = CredentialCreateOptions.FromJson(state.Options),
                // Uniqueness is answered below, with its own error code.
                IsCredentialIdUniqueToUserCallback = (_, _) => Task.FromResult(true)
            });
        }
        catch (Exception ex) when (IsNotFatal(ex))
        {
            // Anything the client's bytes can provoke: a failed check, bad JSON, malformed CBOR, an algorithm we do not support.
            logger.LogWarning("Passkey registration refused for {UserId}: {Reason}", user.Id, ex.Message);
            throw new BadRequestException(
                "The passkey could not be verified. Try again.", null, PasskeyErrors.RegistrationFailed);
        }

        if (await dbContext.UserPasskeys.AnyAsync(p => p.CredentialId == registered.Id))
            throw AlreadyRegistered();

        var passkey = new UserPasskey
        {
            Id = Guid.CreateVersion7(),
            UserId = user.Id,
            CredentialId = registered.Id,
            PublicKey = registered.PublicKey,
            Name = passkeyName,
            CreatedAt = dateTimeProvider.ApplicationNow,
            SignCount = registered.SignCount,
            Transports = registered.Transports is { Length: > 0 } t ? string.Join(',', t) : null,
            AaGuid = registered.AaGuid,
            IsBackupEligible = registered.IsBackupEligible,
            IsBackedUp = registered.IsBackedUp
        };
        // Added, not saved: the caller saves it together with the audit row, in one statement.
        dbContext.UserPasskeys.Add(passkey);
        return passkey;
    }

    // ---- sign-in ------------------------------------------------------------------------------------------------------

    /// <summary>Options for <c>navigator.credentials.get</c> and the state to post back. Discoverable: no allow-list.</summary>
    public (string OptionsJson, string State) BeginAssertion()
    {
        var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required
        });
        var json = options.ToJson();
        return (json, Seal(PasskeyStateSealer.AssertPurpose, json));
    }

    /// <summary>
    /// Checks the signature and everything WebAuthn requires of an assertion against the options in the state. The reason,
    /// when it fails, is the audit reason: <c>InvalidPasskey</c> (state, format or signature) or <c>UnknownPasskey</c>.
    /// </summary>
    public async Task<(AssertionVerified? Verified, string? Reason, string? UnknownCredentialId)> VerifyAssertionAsync(
        string? sealedState, string? credentialJson)
    {
        var state = sealer.Unseal(PasskeyStateSealer.AssertPurpose, sealedState);
        if (state is null) return (null, "InvalidPasskey", null);

        AuthenticatorAssertionRawResponse response;
        try
        {
            response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(credentialJson ?? "")
                       ?? throw new JsonException("Empty credential.");
        }
        catch (Exception ex) when (IsNotFatal(ex))
        {
            return (null, "InvalidPasskey", null);
        }

        var credentialId = response.RawId;
        if (credentialId is not { Length: > 0 }) return (null, "InvalidPasskey", null);
        var passkey = await dbContext.UserPasskeys.AsNoTracking().FirstOrDefaultAsync(p => p.CredentialId == credentialId);
        // The only failure that names the credential: the page uses it to tell the browser the passkey is gone.
        if (passkey is null) return (null, "UnknownPasskey", WebEncoders.Base64UrlEncode(credentialId));

        try
        {
            var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = response,
                OriginalOptions = AssertionOptions.FromJson(state.Options),
                StoredPublicKey = passkey.PublicKey,
                StoredSignatureCounter = passkey.SignCount,
                // The credential must belong to the user it says it is for, or one user's key could vouch for another.
                IsUserHandleOwnerOfCredentialIdCallback = (args, _) => Task.FromResult(
                    args.UserHandle is { Length: > 0 } handle && handle.AsSpan().SequenceEqual(passkey.UserId.ToByteArray()))
            });
            return (new AssertionVerified(passkey, result.SignCount, result.IsBackedUp, state.IssuedAt), null, null);
        }
        catch (Exception ex) when (IsNotFatal(ex))
        {
            logger.LogWarning("Passkey assertion refused for passkey {PasskeyId}: {Reason}", passkey.Id, ex.Message);
            return (null, "InvalidPasskey", null);
        }
    }

    /// <summary>
    /// Records the use and makes the assertion single-use, in one statement: it applies only if the passkey has not been
    /// used since the state was issued, so a replayed POST (or any state older than the last success) changes no row.
    /// </summary>
    public async Task<bool> TryMarkUsedAsync(AssertionVerified verified)
    {
        // ponytail: compares timestamps taken on whichever node served each request, so a replay inside the clock skew
        // between two nodes could pass. Nodes are NTP-synced and a replay still needs a valid signature over a fresh
        // challenge; a store (or a per-state nonce column) closes it if that ever matters.
        var now = dateTimeProvider.ApplicationNow;
        var rows = await dbContext.UserPasskeys
            .Where(p => p.Id == verified.Passkey.Id && (p.LastUsedAt == null || p.LastUsedAt < verified.IssuedAt))
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.LastUsedAt, now)
                .SetProperty(p => p.SignCount, verified.SignCount)
                .SetProperty(p => p.IsBackedUp, verified.BackedUp));
        return rows == 1;
    }

    // ---- helpers ------------------------------------------------------------------------------------------------------

    /// <summary>
    /// A malformed or hostile credential can make the parsers and the cryptography throw almost anything (CBOR, JSON, an unsupported
    /// algorithm). All of it is "this credential is no good", never a 500; only the exceptions that mean the process is in
    /// trouble, or that the caller went away, are left alone.
    /// </summary>
    private static bool IsNotFatal(Exception ex) => ex is not (OutOfMemoryException or OperationCanceledException or StackOverflowException);

    private static AuthenticatorTransport[]? ParseTransports(string? stored) =>
        string.IsNullOrEmpty(stored)
            ? null
            : stored.Split(',').Select(t => Enum.TryParse<AuthenticatorTransport>(t, out var parsed) ? parsed : (AuthenticatorTransport?)null)
                .Where(t => t.HasValue).Select(t => t!.Value).ToArray();

    internal static BadRequestException LimitReached() =>
        new($"A user can have at most {UserPasskey.MaxPerUser} passkeys. Remove one first.", null, PasskeyErrors.LimitReached);

    /// <summary>
    /// Whether a failed save is the unique index on <c>CredentialId</c> refusing a second registration of one credential (SQL
    /// Server 2601/2627). Any other save failure is not this and must stay an error.
    /// </summary>
    public static bool IsCredentialIdConflict(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 } sql
        && sql.Message.Contains("IX_UserPasskeys_CredentialId", StringComparison.Ordinal);

    internal static BadRequestException AlreadyRegistered() =>
        new("This passkey is already registered.", null, PasskeyErrors.AlreadyRegistered);
}
