using Appraisal.Application.Features.Shared;
using Appraisal.Contracts.Appraisals;
using Appraisal.Domain.Appraisals;
using Dapper;
using Reporting.Contracts;
using Shared.Identity;

namespace Appraisal.Application.Features.Appraisals.GetCarryForwardDocuments;

/// <summary>
/// Reads the prior request's files and the prior appraisal's summary report from
/// appraisal.vw_CarryForwardDocuments. Files are shared by DocumentId, so rows whose link has no
/// file yet (empty checklist placeholders) and files already soft-deleted are left out.
/// </summary>
public class GetCarryForwardDocumentsQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
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
    // filters. It exposes both summary codes; only the one matching the appraisal's type is kept.
    // Ordering uses GroupOrder / Seq / UploadedAt / RowId rather than Guid v7 order.
    private const string DocumentsSql = """
        SELECT Level, DocumentId, DocumentType, PriorTitleId, CollateralType, TitleNumber, FileName, FilePath, Prefix, [Set],
               Notes, UploadedBy, UploadedByName, UploadedAt, CarryForwardByDefault
        FROM appraisal.vw_CarryForwardDocuments
        WHERE AppraisalId = @AppraisalId
          AND (Level <> 'Summary' OR DocumentType = @SummaryCode)
        ORDER BY GroupOrder, Seq, UploadedAt, RowId
        """;

    // With EnforceCallerScope, external (company) callers see only appraisals assigned to their company, as the
    // sibling appraisal reads do (GetPreviousAppraisalChain); anything else is "not found", so existence is
    // never confirmed.
    private const string CompanyScopeSql = """
        SELECT CAST(CASE WHEN EXISTS (
                   SELECT 1 FROM appraisal.vw_AppraisalList al
                   WHERE al.Id = @AppraisalId
                     AND TRY_CAST(al.AssigneeCompanyId AS uniqueidentifier) = @CompanyId)
               THEN 1 ELSE 0 END AS bit)
        """;

    public async Task<CarryForwardDocumentsResult> Handle(
        GetCarryForwardDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();

        // Only when the caller asks (the FE endpoint): Integration and background callers have no company user.
        if (query.EnforceCallerScope
            && AppraisalAccessScope.GetEnforcedCompanyId(currentUser) is { } companyId
            && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                CompanyScopeSql, new { query.AppraisalId, CompanyId = companyId },
                cancellationToken: cancellationToken)))
            throw new AppraisalNotFoundException(query.AppraisalId);

        // The summary code depends on the appraisal's type, so the header is read first.
        var header = await connection.QueryFirstOrDefaultAsync<HeaderRow>(new CommandDefinition(
            HeaderSql, new { query.AppraisalId }, cancellationToken: cancellationToken));

        if (header is null)
            throw new AppraisalNotFoundException(query.AppraisalId);

        if (header.Status != AppraisalStatus.Completed.Code)
            throw new ConflictException(
                $"Appraisal '{header.AppraisalNumber ?? query.AppraisalId.ToString()}' has status '{header.Status}'. " +
                "Only Completed appraisals can be referenced.");

        var rows = await connection.QueryAsync<DocumentRow>(new CommandDefinition(
            DocumentsSql,
            new { query.AppraisalId, SummaryCode = SummaryCodeFor(header.ProjectExists, header.AppraisalType) },
            cancellationToken: cancellationToken));

        return new CarryForwardDocumentsResult(header.AppraisalId, header.AppraisalNumber, Map(rows));
    }

    /// <summary>
    /// The code AppraisalSummaryAutoAttachJob files the summary under, via the same classifier: only a
    /// Construction body is D042; Block (which wins over Progressive) and Standard are D043.
    /// </summary>
    internal static string SummaryCodeFor(bool projectExists, string? appraisalType) =>
        AppraisalBodyTypeClassifier.Classify(projectExists, appraisalType) == AppraisalBodyType.Construction
            ? "D042"
            : "D043";

    internal static List<CarryForwardDocumentDto> Map(IEnumerable<DocumentRow> rows) =>
        rows.Where(r => r.DocumentId is not null)
            .Select(r =>
            {
                var summary = r.Level == "Summary";
                return new CarryForwardDocumentDto(
                    r.DocumentId!.Value,
                    summary ? SummaryDocumentType : r.DocumentType,
                    r.DocumentType,
                    summary ? "Request" : r.Level,
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
                    summary || (r.CarryForwardByDefault ?? true));
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
