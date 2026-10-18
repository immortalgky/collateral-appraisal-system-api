using System.Text.Json;

namespace Integration.FailedMessages;

/// <summary>
/// Resolves the design D12 reference columns (RefType/RefId/RefNumber) from a message's JSON.
/// Pure/static so it works identically for a collected fault's <c>message</c> body and for an
/// outbox row's raw <c>Payload</c> string (the API pass reuses this for outbox rows — both are the
/// event object serialised at its top level, camelCase). Only top-level properties are checked,
/// matched case-insensitively; <c>correlationId</c> is never used (design D12: it means RequestId or
/// QuotationRequestId depending on event, not a reference on its own).
/// </summary>
public static class FailedMessageReferenceResolver
{
    // Precedence order per design D12: Appraisal → Request → Quotation → Meeting → Document.
    private static readonly (string IdKey, string Type, string NumberKey)[] Precedence =
    [
        ("appraisalId", "appraisal", "appraisalNumber"),
        ("requestId", "request", "requestNumber"),
        ("quotationRequestId", "quotation", "quotationNumber"),
        ("meetingId", "meeting", "meetingNumber"),
        ("documentId", "document", "documentNumber")
    ];

    public static (string? RefType, Guid? RefId, string? RefNumber) Resolve(string? messageJson)
    {
        if (string.IsNullOrWhiteSpace(messageJson))
            return (null, null, null);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(messageJson);
        }
        catch (JsonException)
        {
            return (null, null, null);
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return (null, null, null);

            var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in doc.RootElement.EnumerateObject())
                properties.TryAdd(property.Name, property.Value);

            foreach (var (idKey, type, numberKey) in Precedence)
            {
                if (!properties.TryGetValue(idKey, out var idElement) ||
                    idElement.ValueKind != JsonValueKind.String ||
                    !Guid.TryParse(idElement.GetString(), out var refId))
                    continue;

                string? refNumber = properties.TryGetValue(numberKey, out var numberElement) &&
                                     numberElement.ValueKind == JsonValueKind.String
                    ? numberElement.GetString()
                    : null;

                return (type, refId, refNumber);
            }

            return (null, null, null);
        }
    }
}
