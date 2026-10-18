namespace Shared.Messaging.Services;

/// <summary>Why an outbox row's stored <c>EventType</c> could, or couldn't, be resolved.</summary>
public enum TypeResolution
{
    /// <summary>Resolved to a real <see cref="Type"/> inside <see cref="IntegrationEventNamespace.Allowed"/>.</summary>
    Resolved,

    /// <summary><see cref="Type.GetType(string, bool)"/> returned null or threw. Could be a genuine
    /// bad value, or simply a type this node's build doesn't know about yet (rolling deploy).</summary>
    Unresolvable,

    /// <summary>Resolved to a real <see cref="Type"/>, but outside <see cref="IntegrationEventNamespace.Allowed"/>.
    /// Deterministic — this can never become valid by waiting.</summary>
    Disallowed
}

/// <summary>
/// The single source of truth for which event types the outbox may publish — anything outside
/// <see cref="Allowed"/> is reported as unresolved rather than blindly <c>Type.GetType()</c>'d.
/// Used by <see cref="IntegrationEventDeliveryService{TDbContext}"/> and, where present, admin read
/// models.
/// </summary>
public static class IntegrationEventNamespace
{
    public const string Allowed = "Shared.Messaging.Events";

    /// <summary>
    /// <see cref="Type.GetType(string, bool)"/> (<c>throwOnError: false</c>) plus the allowed-namespace
    /// check, in one place. Resolution failure is deliberately caught broadly — ANY exception (not
    /// just the documented <see cref="FileLoadException"/>/<see cref="ArgumentException"/>/
    /// <see cref="TypeLoadException"/>, which can still surface even with <c>throwOnError: false</c>)
    /// is reported as <see cref="TypeResolution.Unresolvable"/> rather than allowed to escape: a
    /// malformed stored type name must never make a caller's batch loop stall on a row it can't get
    /// past.
    /// </summary>
    public static TypeResolution TryResolve(string eventType, out Type? type)
    {
        Type? resolved;
        try
        {
            resolved = Type.GetType(eventType, throwOnError: false);
        }
        catch (Exception)
        {
            type = null;
            return TypeResolution.Unresolvable;
        }

        if (resolved is null)
        {
            type = null;
            return TypeResolution.Unresolvable;
        }

        if (resolved.Namespace != Allowed)
        {
            type = null;
            return TypeResolution.Disallowed;
        }

        type = resolved;
        return TypeResolution.Resolved;
    }
}
