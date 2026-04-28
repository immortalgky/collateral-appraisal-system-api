using Appraisal.Application.Features.Quotations.Shared;
using Appraisal.Domain.Quotations;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.SetSharedDocuments;

/// <summary>
/// Sets the list of documents that will be shared with invited external companies.
/// Full-replace semantics — the entire set is overwritten.
/// Admin-only, Draft-only.
///
/// v7: Document IDs must be drawn from `/requests/{requestId}/documents` — i.e., each
/// posted (AppraisalId, DocumentId, Level) tuple is validated against
/// request.RequestDocuments (RequestLevel) or request.RequestTitleDocuments (TitleLevel)
/// for the appraisal's RequestId.
/// </summary>
public class SetSharedDocumentsCommandHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory connectionFactory)
    : ICommandHandler<SetSharedDocumentsCommand, Unit>
{
    public async Task<Unit> Handle(SetSharedDocumentsCommand command, CancellationToken cancellationToken)
    {
        QuotationAccessPolicy.EnsureAdmin(currentUser);

        var quotation = await quotationRepository.GetByIdWithSharedDocumentsAsync(
            command.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation '{command.QuotationRequestId}' not found.");

        // Validate that every posted document belongs to its appraisal's request per the
        // /requests/{requestId}/documents contract (v7).
        await ValidateAgainstRequestDocumentsAsync(
            command.QuotationRequestId,
            command.Documents,
            cancellationToken);

        var selections = command.Documents
            .Select(d => (d.AppraisalId, d.DocumentId, d.Level));

        quotation.SetSharedDocuments(selections, currentUser.UserId?.ToString() ?? "system");

        quotationRepository.Update(quotation);

        return Unit.Value;
    }

    private async Task ValidateAgainstRequestDocumentsAsync(
        Guid quotationRequestId,
        IReadOnlyList<SharedDocumentEntry> documents,
        CancellationToken cancellationToken)
    {
        if (documents.Count == 0)
            return;

        // Enforce known Level values up front — the Dapper query only looks at two tables.
        var invalidLevel = documents.FirstOrDefault(d =>
            d.Level != QuotationSharedDocument.RequestLevel &&
            d.Level != QuotationSharedDocument.TitleLevel);

        if (invalidLevel is not null)
            throw new BadRequestException(
                $"Invalid shared-document level '{invalidLevel.Level}' for document '{invalidLevel.DocumentId}'. " +
                $"Expected '{QuotationSharedDocument.RequestLevel}' or '{QuotationSharedDocument.TitleLevel}'.");

        var appraisalIds = documents.Select(d => d.AppraisalId).Distinct().ToArray();

        var connection = connectionFactory.GetOpenConnection();

        // Resolve each appraisal to its RequestId so we can scope the validation query.
        var appraisalRequests = (await connection.QueryAsync<(Guid AppraisalId, Guid RequestId)>(
                """
                SELECT Id AS AppraisalId, RequestId
                FROM appraisal.Appraisals
                WHERE Id IN @AppraisalIds
                """,
                new { AppraisalIds = appraisalIds }))
            .ToDictionary(r => r.AppraisalId, r => r.RequestId);

        var unknownAppraisal = appraisalIds.FirstOrDefault(id => !appraisalRequests.ContainsKey(id));
        if (unknownAppraisal != Guid.Empty)
            throw new BadRequestException(
                $"Appraisal '{unknownAppraisal}' not found; cannot validate shared documents.");

        // Fetch every DocumentId the request module exposes for the resolved requests,
        // tagged with the source Level. Two separate queries UNIONed keeps the plan simple.
        var requestIds = appraisalRequests.Values.Distinct().ToArray();

        var validPairs = (await connection.QueryAsync<(Guid RequestId, Guid DocumentId, string Level)>(
                """
                SELECT rd.RequestId, rd.DocumentId AS DocumentId, 'RequestLevel' AS Level
                FROM [request].[RequestDocuments] rd
                WHERE rd.RequestId IN @RequestIds AND rd.DocumentId IS NOT NULL
                UNION ALL
                SELECT t.RequestId, td.DocumentId AS DocumentId, 'TitleLevel' AS Level
                FROM [request].[RequestTitles] t
                INNER JOIN [request].[RequestTitleDocuments] td ON td.TitleId = t.Id
                WHERE t.RequestId IN @RequestIds AND td.DocumentId IS NOT NULL
                """,
                new { RequestIds = requestIds }))
            .ToHashSet();

        foreach (var entry in documents)
        {
            var requestId = appraisalRequests[entry.AppraisalId];
            if (!validPairs.Contains((requestId, entry.DocumentId, entry.Level)))
            {
                throw new BadRequestException(
                    $"Document '{entry.DocumentId}' (level '{entry.Level}') is not available in " +
                    $"request '{requestId}' for appraisal '{entry.AppraisalId}'. " +
                    "It may have been removed, renamed, or belongs to a different request.");
            }
        }
    }
}
