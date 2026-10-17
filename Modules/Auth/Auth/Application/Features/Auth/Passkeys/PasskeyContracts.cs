using System.Linq.Expressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using Auth.Application.Services;
using Auth.Domain.Passkeys;

namespace Auth.Application.Features.Auth.Passkeys;

/// <summary>
/// What the profile and the admin user panel list. <c>Synced</c> = the authenticator says the credential is backed up to a
/// cloud account. <c>LastUsedAt</c> is always present, null until first used (the API otherwise leaves nulls out).
/// </summary>
public record PasskeyDto(
    Guid Id, string Name, bool Synced, DateTime CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? LastUsedAt)
{
    /// <summary>The one mapping, for queries (translated to SQL) and for an entity in hand alike.</summary>
    public static readonly Expression<Func<UserPasskey, PasskeyDto>> Projection =
        p => new PasskeyDto(p.Id, p.Name, p.IsBackedUp, p.CreatedAt, p.LastUsedAt);

    private static readonly Func<UserPasskey, PasskeyDto> Compiled = Projection.Compile();

    public static PasskeyDto From(UserPasskey passkey) => Compiled(passkey);
}

public record GetMyPasskeysQuery(Guid UserId) : IQuery<IReadOnlyList<PasskeyDto>>;

/// <param name="SessionId">The caller's session, to tell how recently they signed in. Null when the caller has none (a Bearer token).</param>
public record BeginPasskeyRegistrationCommand(Guid UserId, Guid? SessionId) : ICommand<PasskeyRegistrationOptionsResult>;

/// <param name="Options">Valid input to <c>PublicKeyCredential.parseCreationOptionsFromJSON</c>.</param>
/// <param name="State">Sealed with the options; the browser sends it back with the result.</param>
public record PasskeyRegistrationOptionsResult(JsonElement Options, string State);

public record RegisterPasskeyCommand(Guid UserId, string? State, string? Name, JsonElement Credential) : ICommand<PasskeyDto>;

public record RenamePasskeyCommand(Guid UserId, Guid PasskeyId, string? Name) : ICommand;

public record DeletePasskeyCommand(Guid UserId, Guid PasskeyId) : ICommand;

/// <summary>Body of <c>POST /auth/me/passkeys/register</c>: <c>credential</c> is the browser's <c>credential.toJSON()</c>.</summary>
public record RegisterPasskeyRequest(string? State, string? Name, JsonElement Credential);

public record RenamePasskeyRequest(string? Name);
