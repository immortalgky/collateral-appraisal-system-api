using Appraisal.Contracts.Appraisals;
using Dapper;
using Microsoft.Extensions.Logging;
using Request.Contracts.RequestDocuments.Dto;
using Request.Contracts.Requests.Dtos;
using Shared.Data;

namespace Request.Application.Services;

/// <summary>
/// The documents a system-created reappraisal carries from its prior appraisal — the same rule the request
/// page applies when a maker picks one: the carry-forward query's files with defaultUse, stamped "PREV".
/// Request-level files go to the request; a title-level file goes to the copied title with the same key
/// (collateral type + that type's number), or is left out (staff pick it on the draft) unless exactly one
/// copied title has that key.
/// The prior request's checklist slots (required request types, every title type) are kept as empty rows for the types that got no file, so a system-created
/// draft still shows what is required (an auto-submitted block reappraisal never passes through the page).
/// </summary>
internal static class CarriedDocuments
{
    public const string PrevSource = "PREV";

    /// <summary>
    /// The prior appraisal's carry-forward files, or null — no files carried, the draft is still created —
    /// when there is no prior in this system (legacy 99A / unknown book) or the query refuses it
    /// (404 not found, 409 not Completed).
    /// </summary>
    public static async Task<CarryForwardDocumentsResult?> TryFetchAsync(
        ISender mediator,
        Guid? priorAppraisalId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        if (priorAppraisalId is not { } id)
        {
            logger.LogInformation("[CARRY-FORWARD] No prior appraisal in this system; carrying no documents");
            return null;
        }

        try
        {
            return await mediator.Send(new GetCarryForwardDocumentsQuery(id), cancellationToken);
        }
        catch (Exception ex) when (ex is NotFoundException or ConflictException)
        {
            logger.LogInformation(
                "[CARRY-FORWARD] Prior appraisal {AppraisalId} offers no documents ({Reason}); carrying none",
                id, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// A checklist slot on the prior request that the new one keeps: a required type at request level
    /// (CollateralType null), a document type per title, which names its title by key (collateral type + that
    /// type's number).
    /// </summary>
    public sealed record Placeholder(string? CollateralType, string? TitleNumber, string DocumentType);

    // Request level: the types of every row marked required, filled or empty — a required type must stay
    // on the checklist even when its file was not carried. Title level: every document type the prior title
    // had, filled or empty — a title document's IsRequired is not stored yet (TitleDocument.Create hard-codes
    // false), so the flag cannot tell the required ones apart. The summary codes are left out at title level
    // as in vw_CarryForwardDocuments: the only summary a new request gets is the carried, re-typed D036.
    private const string PlaceholderSql = """
        SELECT CAST(NULL AS nvarchar(10)) AS CollateralType, CAST(NULL AS nvarchar(100)) AS TitleNumber, rd.DocumentType
        FROM request.RequestDocuments rd
        WHERE rd.RequestId = @RequestId AND rd.IsRequired = 1
          AND LTRIM(RTRIM(rd.DocumentType)) <> ''
        UNION ALL
        SELECT DISTINCT t.CollateralType,
               CASE
                   WHEN t.CollateralType = '10' THEN COALESCE(NULLIF(t.LicensePlateNumber, ''), t.VehicleRegistrationNumber)
                   WHEN t.CollateralType = '11' THEN CASE WHEN t.RegistrationStatus = 1 THEN t.RegistrationNumber END
                   WHEN t.CollateralType = '12' THEN COALESCE(NULLIF(t.VesselRegistrationNumber, ''), t.HIN)
                   ELSE t.TitleNumber
                   END AS TitleNumber,
               td.DocumentType
        FROM request.RequestTitleDocuments td
        JOIN request.RequestTitles t ON t.Id = td.TitleId
        WHERE t.RequestId = @RequestId
          AND LTRIM(RTRIM(td.DocumentType)) <> ''
          AND td.DocumentType NOT IN ('D036', 'D042', 'D043')
        """;

    public static async Task<List<Placeholder>> LoadPlaceholdersAsync(
        ISqlConnectionFactory connectionFactory,
        Guid priorRequestId,
        CancellationToken cancellationToken) =>
        (await connectionFactory.GetOpenConnection().QueryAsync<Placeholder>(new CommandDefinition(
            PlaceholderSql, new { RequestId = priorRequestId }, cancellationToken: cancellationToken))).ToList();

    /// <summary>
    /// The request-level documents to create, and the copied titles with their carried documents attached.
    /// The titles' own documents are dropped first: nothing is copied from the prior request's rows any more
    /// except its checklist slots (<paramref name="placeholders"/>), as empty rows for types that got no carried file.
    /// </summary>
    public static (List<RequestDocumentDto> RequestDocuments, List<RequestTitleDto>? Titles) Build(
        CarryForwardDocumentsResult? carried,
        IReadOnlyList<RequestTitleDto>? titles,
        IReadOnlyList<Placeholder>? placeholders = null)
    {
        placeholders ??= [];
        var requestRequired = placeholders.Where(p => p.CollateralType is null).Select(p => p.DocumentType).ToHashSet();
        var requestDocuments = new List<RequestDocumentDto>();
        var copied = titles?.Select(t => t with { Documents = [] }).ToList();
        // One bucket per copied title, by position.
        var buckets = copied?.Select(_ => new List<RequestTitleDocumentDto>()).ToList();

        foreach (var doc in carried?.Documents.Where(d => d.DefaultUse) ?? [])
        {
            if (doc.Level == "Title")
            {
                var index = FindTitle(copied, doc.CollateralType, doc.TitleNumber);
                if (index >= 0)
                    buckets![index].Add(new RequestTitleDocumentDto
                    {
                        DocumentId = doc.DocumentId,
                        DocumentType = doc.DocumentType,
                        FileName = doc.FileName,
                        Prefix = doc.Prefix,
                        Set = doc.Set,
                        Notes = doc.Notes,
                        FilePath = doc.FilePath,
                        Source = PrevSource,
                        UploadedBy = doc.UploadedBy,
                        UploadedByName = doc.UploadedByName,
                        UploadedAt = doc.UploadedAt ?? default // CreateRequestService stamps the creation time on a default
                    });
            }
            else
            {
                requestDocuments.Add(new RequestDocumentDto(
                    null, Guid.Empty, doc.DocumentId, doc.DocumentType, doc.FileName, doc.Prefix, (short)doc.Set,
                    doc.Notes, doc.FilePath, PrevSource, requestRequired.Contains(doc.DocumentType),
                    doc.UploadedBy, doc.UploadedByName, doc.UploadedAt));
            }
        }

        // Empty rows stay for the types that got no file, once per type and section.
        foreach (var type in requestRequired.Where(t => requestDocuments.All(d => d.DocumentType != t)))
            requestDocuments.Add(new RequestDocumentDto(
                null, Guid.Empty, null, type, null, null, 1, null, null, "REQUEST", true, null, null, null));

        foreach (var group in placeholders.Where(p => p.CollateralType is not null)
                     .GroupBy(p => FindTitle(copied, p.CollateralType, p.TitleNumber))
                     .Where(g => g.Key >= 0))
            foreach (var type in group.Select(p => p.DocumentType).Distinct()
                         .Where(t => buckets![group.Key].All(d => d.DocumentType != t)))
                buckets![group.Key].Add(new RequestTitleDocumentDto { DocumentType = type, Set = 1, Source = "REQUEST" });

        return (requestDocuments,
            copied?.Select((t, i) => t with { Documents = buckets![i] }).ToList());
    }

    // The number the carry-forward view emits for a title — the request page's movableIdentity rule (FE
    // titleList.ts), written three times (view, PlaceholderSql, here): keep them identical.
    private static string? NumberOf(RequestTitleDto title) => title.CollateralType switch
    {
        "10" => Some(title.LicensePlateNumber) ?? title.VehicleRegistrationNumber,
        "11" => title.RegistrationStatus ? title.RegistrationNumber : null,
        "12" => Some(title.VesselRegistrationNumber) ?? title.HIN,
        _ => title.TitleNumber
    };

    // Whitespace-only counts as empty, like the SQL's NULLIF(x, '') (trailing spaces do not count there).
    private static string? Some(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    // Index of the copied title with this key — collateral type + that type's number, trimmed and
    // case-insensitive — only when EXACTLY ONE copied title has it. Never by the prior title's id: a copied
    // title is a new row. No key, no match or a duplicate key gives -1 and the file stays unplaced (staff pick it).
    private static int FindTitle(List<RequestTitleDto>? titles, string? collateralType, string? number)
    {
        if (titles is null || string.IsNullOrWhiteSpace(collateralType) || string.IsNullOrWhiteSpace(number)) return -1;

        var matches = titles
            .Select((t, i) => (t, i))
            .Where(x => string.Equals(x.t.CollateralType?.Trim(), collateralType.Trim(), StringComparison.OrdinalIgnoreCase)
                        && string.Equals(NumberOf(x.t)?.Trim(), number.Trim(), StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        return matches.Count == 1 ? matches[0].i : -1;
    }
}
