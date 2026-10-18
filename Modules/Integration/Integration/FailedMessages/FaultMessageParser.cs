using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Integration.Domain.FailedMessages;
using Integration.Infrastructure.Configurations;

namespace Integration.FailedMessages;

/// <summary>
/// One collected message's fault info, resolved from AMQP headers and/or the MassTransit envelope body
/// (design D10 step 2). Pure/static — no DI, unit-testable without a broker.
/// </summary>
/// <param name="MessagePayloadJson">
/// The inner message JSON (envelope's <c>message</c> object, or the whole body when it isn't
/// enveloped) — feeds <see cref="FailedMessageReferenceResolver"/>. Null when the body isn't valid JSON.
/// </param>
public record ParsedFault(
    Guid? MessageId,
    Guid? ConversationId,
    string MessageType,
    string? ConsumerType,
    string ExceptionType,
    string ExceptionMessage,
    string? StackTrace,
    int RetryCount,
    DateTime? FaultedAtUtc,
    string? MessagePayloadJson,
    // Fallback for FaultedAt on a Skipped message (no MT-Fault-Timestamp header at all) — the
    // MassTransit envelope's own "sentTime", read here since ParseEnvelope already parses this JSON.
    DateTime? EnvelopeSentTimeUtc);

/// <summary>
/// Parses a MassTransit <c>_error</c>/<c>_skipped</c> message (AMQP headers + envelope body) into
/// <see cref="ParsedFault"/>, and coerces AMQP header values to strings for storage/display.
///
/// Verified against a real MassTransit 8.4.1 + RabbitMQ.Client 7.1.2 round-trip (publish → consumer
/// throws → inspect the resulting <c>_error</c>/<c>_skipped</c> message): the app never configures a raw
/// JSON serializer, so every message on the wire is the default envelope
/// (<c>Content-Type: application/vnd.masstransit+json</c>) with <c>messageId</c>/<c>conversationId</c>/
/// <c>messageType[]</c>/<c>message</c> in the BODY — the AMQP <c>BasicProperties</c> equivalents
/// (MessageId/CorrelationId/Timestamp) are left at their defaults by MassTransit's own publisher, so
/// they are only used as a last-resort, trivial fallback here. Confirmed header names/values:
/// <c>MT-Fault-ExceptionType</c>, <c>MT-Fault-Message</c>, <c>MT-Fault-StackTrace</c>,
/// <c>MT-Fault-MessageType</c> (already dotted, e.g. "Probe.TestFail"), <c>MT-Fault-ConsumerType</c>,
/// <c>MT-Fault-RetryCount</c> (present only when &gt; 0), <c>MT-Fault-Timestamp</c> (ISO-8601 UTC,
/// e.g. "2026-09-26T18:02:03.5080760Z"), and <c>MT-Reason</c> (e.g. "fault" on an error, "dead-letter"
/// on a skip — MassTransit does NOT literally write "No consumer"; that string is this app's own
/// fallback for the rare case the header is missing entirely). A skipped message carries no
/// <c>MT-Fault-*</c> header at all.
/// </summary>
public static class FaultMessageParser
{
    public const string SkippedExceptionType = "Skipped";
    private const string DefaultSkippedReason = "No consumer";

    public const string ErrorSuffix = "_error";
    public const string SkippedSuffix = "_skipped";

    /// <summary>"_error"/"_skipped" → <see cref="FailedMessageKind"/>; null if neither suffix matches.</summary>
    public static string? DeriveKind(string queueName) => queueName switch
    {
        _ when queueName.EndsWith(ErrorSuffix, StringComparison.Ordinal) => FailedMessageKind.Error,
        _ when queueName.EndsWith(SkippedSuffix, StringComparison.Ordinal) => FailedMessageKind.Skipped,
        _ => null
    };

    /// <summary>The fault queue name with its trailing _error/_skipped suffix removed.</summary>
    public static string StripFaultSuffix(string sourceQueue) => sourceQueue switch
    {
        _ when sourceQueue.EndsWith(ErrorSuffix, StringComparison.Ordinal) => sourceQueue[..^ErrorSuffix.Length],
        _ when sourceQueue.EndsWith(SkippedSuffix, StringComparison.Ordinal) => sourceQueue[..^SkippedSuffix.Length],
        _ => sourceQueue
    };

    /// <summary>
    /// A discovered fault queue name (e.g. "appraisal-sync_error") → the base queue name to store as
    /// <c>FailedMessage.SourceQueue</c> ("appraisal-sync") plus its <see cref="FailedMessageKind"/>.
    /// SourceQueue is always the base name — design D2/§1/§3 and the FE both key off it directly (with
    /// `Kind` telling Error vs Skipped apart); it is also the exact routing key Retry republishes to.
    /// Null Kind means <paramref name="queueName"/> isn't a fault queue at all.
    /// </summary>
    public static (string SourceQueue, string? Kind) FromFaultQueue(string queueName)
    {
        var kind = DeriveKind(queueName);
        return kind is null ? (queueName, null) : (StripFaultSuffix(queueName), kind);
    }

    // Same constants FailedMessageConfiguration uses for the column HasMaxLength calls — one
    // source of truth so parsing here can never drift from what the database will actually accept.
    private const int MessageTypeMaxLength = FailedMessageConfiguration.MessageTypeMaxLength;
    private const int ConsumerTypeMaxLength = FailedMessageConfiguration.ConsumerTypeMaxLength;
    private const int ExceptionTypeMaxLength = FailedMessageConfiguration.ExceptionTypeMaxLength;
    private const int ExceptionMessageMaxLength = FailedMessageConfiguration.ExceptionMessageMaxLength;

    /// <summary>Coerces every AMQP header value to a string, per the API contract's header rules.</summary>
    public static Dictionary<string, string> CoerceHeaders(IDictionary<string, object?>? headers)
    {
        var result = new Dictionary<string, string>();
        if (headers is null)
            return result;

        foreach (var (key, value) in headers)
            result[key] = CoerceHeaderValue(value);

        return result;
    }

    private static string CoerceHeaderValue(object? value)
    {
        switch (value)
        {
            case null:
                return string.Empty;
            case string s:
                return s;
            case byte[] bytes:
                return Encoding.UTF8.GetString(bytes);
            case RabbitMQ.Client.AmqpTimestamp timestamp:
                return timestamp.UnixTime.ToString(CultureInfo.InvariantCulture);
            case IDictionary<string, object?> nested:
                return JsonSerializer.Serialize(CoerceHeaders(nested));
            case System.Collections.IEnumerable list and not string:
                var items = new List<string>();
                foreach (var item in list)
                    items.Add(CoerceHeaderValue(item));
                return JsonSerializer.Serialize(items);
            default:
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }
    }

    /// <summary>
    /// Parses fault/skip info from already-coerced headers and the raw body. <paramref name="amqpMessageId"/>
    /// is the trivial fallback used only when the body doesn't parse as a MassTransit envelope.
    /// </summary>
    public static ParsedFault Parse(
        IReadOnlyDictionary<string, string> headers,
        ReadOnlyMemory<byte> body,
        Guid? amqpMessageId = null)
    {
        var (messagePayloadJson, envelopeMessageId, envelopeConversationId, envelopeMessageType, envelopeSentTimeUtc) =
            ParseEnvelope(body);

        var hasFault = headers.ContainsKey("MT-Fault-ExceptionType");

        string exceptionType;
        string exceptionMessage;
        string? consumerType = null;
        string? stackTrace = null;
        var retryCount = 0;
        DateTime? faultedAtUtc = null;

        if (hasFault)
        {
            // FailedMessage.Create requires non-blank ExceptionType/ExceptionMessage/MessageType —
            // broker data must never be trusted to satisfy that, so every value here falls back to a
            // non-empty placeholder instead of throwing (an empty MT-Fault-Message/MT-Reason header is
            // realistic: some exceptions have no Message at all).
            exceptionType = Truncate(NonEmpty(headers["MT-Fault-ExceptionType"], "System.Exception"),
                ExceptionTypeMaxLength)!;
            exceptionMessage = Truncate(
                NonEmpty(headers.GetValueOrDefault("MT-Fault-Message"), exceptionType), ExceptionMessageMaxLength)!;
            stackTrace = NullIfEmpty(headers.GetValueOrDefault("MT-Fault-StackTrace"));
            consumerType = NullIfEmpty(headers.GetValueOrDefault("MT-Fault-ConsumerType"));
            if (consumerType is not null)
                consumerType = Truncate(consumerType, ConsumerTypeMaxLength);

            if (headers.TryGetValue("MT-Fault-RetryCount", out var rc) && int.TryParse(rc, out var parsedRc))
                retryCount = parsedRc;

            if (TryParseFaultTimestamp(headers, out var parsedTs))
                faultedAtUtc = parsedTs;
        }
        else
        {
            exceptionType = SkippedExceptionType;
            exceptionMessage = Truncate(
                NonEmpty(headers.GetValueOrDefault("MT-Reason"), DefaultSkippedReason), ExceptionMessageMaxLength)!;
        }

        var messageType = Truncate(
            NonEmpty(NullIfEmpty(headers.GetValueOrDefault("MT-Fault-MessageType")) ?? envelopeMessageType, "Unknown"),
            MessageTypeMaxLength)!;

        return new ParsedFault(
            envelopeMessageId ?? amqpMessageId,
            envelopeConversationId,
            messageType,
            consumerType,
            exceptionType,
            exceptionMessage,
            stackTrace,
            retryCount,
            faultedAtUtc,
            messagePayloadJson,
            envelopeSentTimeUtc);
    }

    /// <summary>
    /// The one place <c>MT-Fault-Timestamp</c> is parsed: true only for an MT-Fault-* message whose timestamp
    /// header actually parses — i.e. exactly when it becomes the row's FaultedAt in <see cref="Parse"/>. The
    /// collector's "Error collision = redelivery" shortcut asks this same question, so a header that merely
    /// EXISTS but does not parse (FaultedAt then falls back to a retry-preserved sentTime) never counts.
    /// </summary>
    public static bool TryParseFaultTimestamp(IReadOnlyDictionary<string, string> headers, out DateTime faultedAtUtc)
    {
        faultedAtUtc = default;
        return headers.ContainsKey("MT-Fault-ExceptionType")
               && headers.TryGetValue("MT-Fault-Timestamp", out var ts)
               && DateTime.TryParse(ts, CultureInfo.InvariantCulture,
                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out faultedAtUtc);
    }

    /// <summary>
    /// Reads the MassTransit envelope (messageId/conversationId/messageType[]/sentTime/message) out of
    /// the raw body. Falls back to treating the whole body as the message payload when it doesn't look
    /// like an envelope (e.g. no "message" property) — a trivial fallback for the raw-JSON case the app
    /// doesn't actually use (see class remarks).
    /// </summary>
    private static (string? PayloadJson, Guid? MessageId, Guid? ConversationId, string? MessageType, DateTime? SentTimeUtc)
        ParseEnvelope(ReadOnlyMemory<byte> body)
    {
        if (body.IsEmpty)
            return (null, null, null, null, null);

        string bodyText;
        try
        {
            bodyText = Encoding.UTF8.GetString(body.Span);
        }
        catch (DecoderFallbackException)
        {
            return (null, null, null, null, null);
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(bodyText);
        }
        catch (JsonException)
        {
            return (bodyText, null, null, null, null);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryGetPropertyIgnoreCase(root, "message", out var messageEl))
                return (bodyText, null, null, null, null); // not enveloped — treat the whole body as the payload

            // A malformed/hand-crafted message could have a non-string messageId/conversationId/
            // messageType[0] (e.g. a number) — GetString() throws InvalidOperationException on
            // anything but String/Null, so every ValueKind is checked first.
            Guid? messageId = TryGetPropertyIgnoreCase(root, "messageId", out var midEl) &&
                               midEl.ValueKind == JsonValueKind.String &&
                               Guid.TryParse(midEl.GetString(), out var mid)
                ? mid
                : null;
            Guid? conversationId = TryGetPropertyIgnoreCase(root, "conversationId", out var cidEl) &&
                                    cidEl.ValueKind == JsonValueKind.String &&
                                    Guid.TryParse(cidEl.GetString(), out var cid)
                ? cid
                : null;

            string? messageType = null;
            if (TryGetPropertyIgnoreCase(root, "messageType", out var mtEl) &&
                mtEl.ValueKind == JsonValueKind.Array && mtEl.GetArrayLength() > 0 &&
                mtEl[0].ValueKind == JsonValueKind.String)
                messageType = FormatMessageTypeUrn(mtEl[0].GetString());

            DateTime? sentTimeUtc = TryGetPropertyIgnoreCase(root, "sentTime", out var stEl) &&
                                     stEl.ValueKind == JsonValueKind.String &&
                                     DateTime.TryParse(stEl.GetString(), CultureInfo.InvariantCulture,
                                         DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var st)
                ? st
                : null;

            return (messageEl.GetRawText(), messageId, conversationId, messageType, sentTimeUtc);
        }
    }

    /// <summary>
    /// Cheap best-effort read of the MassTransit envelope's "correlationId" for the retry republish.
    /// Not stored on <see cref="ParsedFault"/>/<see cref="FailedMessage"/> today —
    /// Retry re-parses the raw body it already holds rather than adding a column just for this.
    /// </summary>
    public static Guid? TryParseCorrelationId(byte[] body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object &&
                TryGetPropertyIgnoreCase(root, "correlationId", out var cidEl) &&
                cidEl.ValueKind == JsonValueKind.String &&
                Guid.TryParse(cidEl.GetString(), out var cid))
                return cid;
        }
        catch (JsonException)
        {
            // Not a JSON envelope — no correlationId to copy, retry publish proceeds without one.
        }

        return null;
    }

    /// <summary>"urn:message:Namespace.With.Dots:TypeName" → "Namespace.With.Dots.TypeName".</summary>
    private static string? FormatMessageTypeUrn(string? urn)
    {
        if (string.IsNullOrEmpty(urn))
            return null;

        const string prefix = "urn:message:";
        var remainder = urn.StartsWith(prefix, StringComparison.Ordinal) ? urn[prefix.Length..] : urn;
        var lastColon = remainder.LastIndexOf(':');
        return lastColon < 0 ? remainder : remainder[..lastColon] + "." + remainder[(lastColon + 1)..];
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement obj, string name, out JsonElement value)
    {
        foreach (var property in obj.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }

        value = default;
        return false;
    }

    /// <summary>Public so callers outside this class (FailedMessageCollectorService) can truncate
    /// broker-controlled strings to the same column lengths before an INSERT.</summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Truncate(string? value, int maxLength) =>
        value is null || value.Length <= maxLength ? value : value[..maxLength];

    // IsNullOrWhiteSpace, not IsNullOrEmpty: FailedMessage.Create rejects whitespace-only values too.
    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;
}
