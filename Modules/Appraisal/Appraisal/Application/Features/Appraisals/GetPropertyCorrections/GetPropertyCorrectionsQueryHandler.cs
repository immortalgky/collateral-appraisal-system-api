using System.Text.Json;
using Appraisal.Infrastructure;

namespace Appraisal.Application.Features.Appraisals.GetPropertyCorrections;

public class GetPropertyCorrectionsQueryHandler(
    AppraisalDbContext dbContext,
    ILogger<GetPropertyCorrectionsQueryHandler> logger
) : IQueryHandler<GetPropertyCorrectionsQuery, GetPropertyCorrectionsResult>
{
    public async Task<GetPropertyCorrectionsResult> Handle(
        GetPropertyCorrectionsQuery query,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.AppraisalPropertyCorrectionLogs
            .AsNoTracking()
            .Where(c => c.AppraisalId == query.AppraisalId)
            .Where(c => query.PropertyId == null || c.AppraisalPropertyId == query.PropertyId)
            .OrderByDescending(c => c.ChangedAt)
            .ToListAsync(cancellationToken);

        var entries = rows
            .Select(r => new PropertyCorrectionEntry(
                r.Id,
                r.AppraisalPropertyId,
                r.PropertyType,
                r.Reason,
                r.ChangedBy,
                r.ChangedAt,
                ParseChanges(r.ChangedFields, r.Id, logger)))
            .ToList();

        return new GetPropertyCorrectionsResult(entries);
    }

    /// <summary>
    /// Expands the stored diff — <c>{ "Land.OwnerName": { "from": "A", "to": "B" } }</c> — into a
    /// flat list. A malformed row must not take down the whole history panel, so parse failures
    /// are logged and the row is returned with no changes rather than throwing.
    /// </summary>
    private static IReadOnlyList<PropertyCorrectionChange> ParseChanges(
        string changedFields, Guid logId, ILogger logger)
    {
        try
        {
            using var document = JsonDocument.Parse(changedFields);

            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return [];

            return document.RootElement.EnumerateObject()
                .Select(property => new PropertyCorrectionChange(
                    property.Name,
                    ReadValue(property.Value, "from"),
                    ReadValue(property.Value, "to")))
                .ToList();
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex,
                "Correction log {LogId} has unparseable ChangedFields; returning it with no changes",
                logId);
            return [];
        }
    }

    private static string? ReadValue(JsonElement changeElement, string propertyName)
    {
        if (changeElement.ValueKind != JsonValueKind.Object) return null;
        if (!changeElement.TryGetProperty(propertyName, out var value)) return null;

        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            _ => value.ToString(),
        };
    }
}
