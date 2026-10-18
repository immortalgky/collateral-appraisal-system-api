using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Dapper;
using Reporting.Contracts;

namespace Appraisal.Application.Features.Appraisals.GetCarryForwardDocuments;

/// <summary>
/// Reads every file of the prior request and of the prior appraisal itself from
/// appraisal.vw_CarryForwardDocuments. Files are shared by DocumentId, so rows whose link has no
/// file yet (empty checklist placeholders) and files already soft-deleted are left out.
/// </summary>
public class GetCarryForwardDocumentsQueryHandler(ISqlConnectionFactory connectionFactory)
    : IRequestHandler<GetCarryForwardDocumentsQuery, CarryForwardDocumentsResult>
{
    internal const string SummaryDocumentType = "D036";

    private const string HeaderSql = """
        SELECT a.Id AS AppraisalId, a.AppraisalNumber, a.Status, a.AppraisalType,
               CAST(CASE WHEN EXISTS (SELECT 1 FROM appraisal.Projects pr WHERE pr.AppraisalId = a.Id)
                         THEN 1 ELSE 0 END AS bit) AS ProjectExists
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE a.Id = @AppraisalId AND a.IsDeleted = 0 AND r.IsDeleted = 0
        """;

    // The view (appraisal.vw_CarryForwardDocuments) holds the cross-schema joins and the soft-delete
    // filters and returns every file but D036. Map decides each row's defaultUse; nothing else is filtered by code here.
    // Ordering uses GroupOrder / Seq / UploadedAt / RowId rather than Guid v7 order.
    private const string DocumentsSql = """
        SELECT Level, DocumentId, DocumentType, PriorTitleId, CollateralType, TitleNumber, FileName, FilePath, Prefix, [Set],
               Notes, UploadedBy, UploadedByName, UploadedAt, CarryForwardByDefault
        FROM appraisal.vw_CarryForwardDocuments
        WHERE AppraisalId = @AppraisalId
        ORDER BY GroupOrder, Seq, UploadedAt, RowId
        """;

    public async Task<CarryForwardDocumentsResult> Handle(
        GetCarryForwardDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        // The current summary's code depends on the appraisal's type, so the header is read first.
        var header = await connection.QueryFirstOrDefaultAsync<HeaderRow>(new CommandDefinition(
            HeaderSql, new { query.AppraisalId }, cancellationToken: cancellationToken));

        if (header is null)
            throw new AppraisalNotFoundException(query.AppraisalId);

        if (!query.AnyStatus && header.Status != AppraisalStatus.Completed.Code)
            throw new ConflictException(
                $"Appraisal '{header.AppraisalNumber ?? query.AppraisalId.ToString()}' has status '{header.Status}'. " +
                "Only Completed appraisals can be referenced.");

        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(
            DocumentsSql,
            new { query.AppraisalId },
            cancellationToken: cancellationToken));

        return new CarryForwardDocumentsResult(
            header.AppraisalId, header.AppraisalNumber,
            Map(rows, SummaryCodeFor(header.ProjectExists, header.AppraisalType)));
    }

    /// <summary>
    /// The code AppraisalSummaryAutoAttachJob files the summary under, via the same classifier: only a
    /// Construction body is D042; Block (which wins over Progressive) and Standard are D043.
    /// </summary>
    internal static string SummaryCodeFor(bool projectExists, string? appraisalType) =>
        AppraisalBodyTypeClassifier.Classify(projectExists, appraisalType) == AppraisalBodyType.Construction
            ? "D042"
            : "D043";

    // Request / title rows store the code as given, so match trimmed, any case. D036 never arrives (the view
    // leaves it out), so only the two originals are summary codes here.
    private static bool IsSummaryCode(string code) =>
        code.Trim().ToUpperInvariant() is "D042" or "D043";

    /// <summary>
    /// Every file the view returns (it never returns D036: that is the summary of the generation before the
    /// prior, and only one generation is carried). The prior appraisal's own summary (an 'Appraisal' row with
    /// <paramref name="summaryCode"/>) is re-typed D036 and always used. Any other D042 / D043 (on the request
    /// or title, or the other summary code on the appraisal) keeps its code and is never used by default: it
    /// is a stale summary that would pile up a reappraisal chain. Everything else
    /// follows parameter.DocumentTypes.CarryForwardByDefault, which is off unless an admin ticked the type (an unknown type
    /// is off too). Appraisal-level rows go out as level "Request".
    /// </summary>
    internal static List<CarryForwardDocumentDto> Map(IEnumerable<DocumentRow> rows, string summaryCode) =>
        rows.Where(r => r.DocumentId is not null)
            .Select(r =>
            {
                var current = r.Level == "Appraisal" && r.DocumentType == summaryCode;
                return new CarryForwardDocumentDto(
                    r.DocumentId!.Value,
                    current ? SummaryDocumentType : r.DocumentType,
                    r.DocumentType,
                    r.Level == "Appraisal" ? "Request" : r.Level,
                    r.PriorTitleId,
                    r.CollateralType,
                    r.TitleNumber,
                    r.FileName,
                    r.FilePath,
                    r.Prefix,
                    r.Set ?? 1,
                    r.Notes,
                    r.UploadedBy,
                    r.UploadedByName,
                    r.UploadedAt,
                    current || (!IsSummaryCode(r.DocumentType) && (r.CarryForwardByDefault ?? false)));
            })
            .ToList();

    private sealed class HeaderRow
    {
        public Guid AppraisalId { get; set; }
        public string? AppraisalNumber { get; set; }
        public string Status { get; set; } = "";
        public string? AppraisalType { get; set; }
        public bool ProjectExists { get; set; }
    }

    internal sealed class DocumentRow
    {
        public string Level { get; set; } = "";
        public Guid? DocumentId { get; set; }
        public string DocumentType { get; set; } = "";
        public Guid? PriorTitleId { get; set; }
        public string? CollateralType { get; set; }
        public string? TitleNumber { get; set; }
        public string? FileName { get; set; }
        public string? FilePath { get; set; }
        public string? Prefix { get; set; }
        public int? Set { get; set; }
        public string? Notes { get; set; }
        public string? UploadedBy { get; set; }
        public string? UploadedByName { get; set; }
        public DateTime? UploadedAt { get; set; }
        public bool? CarryForwardByDefault { get; set; }
    }
}
