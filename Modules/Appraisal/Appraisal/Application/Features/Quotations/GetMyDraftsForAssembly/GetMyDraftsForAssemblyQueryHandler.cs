using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.GetMyDraftsForAssembly;

/// <summary>
/// Returns the calling admin's Draft quotations for the entry-modal picker.
/// Uses Dapper for the main query and a second query for appraisal number previews.
/// </summary>
public class GetMyDraftsForAssemblyQueryHandler(
    ISqlConnectionFactory connectionFactory,
    ICurrentUserService currentUser)
    : IQueryHandler<GetMyDraftsForAssemblyQuery, GetMyDraftsForAssemblyResult>
{
    public async Task<GetMyDraftsForAssemblyResult> Handle(
        GetMyDraftsForAssemblyQuery query,
        CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var adminUsername = currentUser.Username
            ?? throw new UnauthorizedAccessException("Cannot resolve current user username from token");

        using var connection = connectionFactory.GetOpenConnection();

        // ── Main query: get Draft quotations owned by this admin ──────────────
        var sql = """
            SELECT
                q.Id,
                q.QuotationNumber,
                q.RequestDate,
                q.DueDate,
                q.BankingSegment,
                q.TotalAppraisals,
                q.TotalCompaniesInvited
            FROM appraisal.QuotationRequests q
            WHERE q.Status = 'Draft'
              AND q.RequestedBy = @AdminUsername
            """;

        var parameters = new DynamicParameters();
        parameters.Add("AdminUsername", adminUsername);

        if (!string.IsNullOrWhiteSpace(query.BankingSegment))
        {
            sql += " AND q.BankingSegment = @BankingSegment";
            parameters.Add("BankingSegment", query.BankingSegment);
        }

        sql += " ORDER BY q.RequestDate DESC";

        var rows = (await connection.QueryAsync<DraftRow>(sql, parameters)).ToList();

        if (rows.Count == 0)
            return new GetMyDraftsForAssemblyResult([]);

        var quotationIds = rows.Select(r => r.Id).ToArray();

        // ── Preview query: top 5 appraisal numbers per quotation ─────────────
        // Uses QuotationRequestItems (display items added on AddAppraisal)
        var previewSql = """
            SELECT qi.QuotationRequestId, qi.AppraisalNumber
            FROM appraisal.QuotationRequestItems qi
            WHERE qi.QuotationRequestId IN @QuotationIds
            ORDER BY qi.ItemNumber
            """;

        var previewRows = (await connection.QueryAsync<AppraisalPreviewRow>(
            previewSql,
            new { QuotationIds = quotationIds })).ToList();

        var previewLookup = previewRows
            .GroupBy(p => p.QuotationRequestId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(p => p.AppraisalNumber).Take(5).ToList());

        var drafts = rows.Select(r => new QuotationDraftSummaryDto(
            Id: r.Id,
            QuotationNumber: r.QuotationNumber,
            RequestDate: r.RequestDate,
            DueDate: r.DueDate,
            BankingSegment: r.BankingSegment,
            TotalAppraisals: r.TotalAppraisals,
            TotalCompaniesInvited: r.TotalCompaniesInvited,
            AppraisalNumberPreview: previewLookup.TryGetValue(r.Id, out var preview)
                ? preview.AsReadOnly()
                : (IReadOnlyList<string>)[]
        )).ToList();

        return new GetMyDraftsForAssemblyResult(drafts.AsReadOnly());
    }

    private record DraftRow(
        Guid Id,
        string? QuotationNumber,
        DateTime RequestDate,
        DateTime DueDate,
        string? BankingSegment,
        int TotalAppraisals,
        int TotalCompaniesInvited);

    private record AppraisalPreviewRow(Guid QuotationRequestId, string AppraisalNumber);
}
