using Appraisal.Application.Features.Quotations.Shared;
using Dapper;
using Shared.Data;
using Shared.Identity;

namespace Appraisal.Application.Features.Quotations.Documents;

public class ListQuotationDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/quotations/{id:guid}/documents", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new ListQuotationDocumentsQuery(id), ct);
                return Results.Ok(result);
            })
            .WithName("ListQuotationDocuments")
            .WithTags("Quotation")
            .RequireAuthorization()
            .Produces<List<QuotationDocumentDto>>();
    }
}

public record ListQuotationDocumentsQuery(Guid QuotationRequestId) : IQuery<List<QuotationDocumentDto>>;

public record QuotationDocumentDto(
    Guid Id,
    Guid DocumentId,
    string FileName,
    string DocumentType,
    string Source,
    string? CreatedBy,
    DateTime? CreatedAt,
    long? FileSizeBytes,
    string? MimeType);

public class ListQuotationDocumentsQueryHandler(
    IQuotationRepository quotationRepository,
    ICurrentUserService currentUser,
    ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<ListQuotationDocumentsQuery, List<QuotationDocumentDto>>
{
    public async Task<List<QuotationDocumentDto>> Handle(
        ListQuotationDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        var quotation = await quotationRepository.GetForViewAccessCheckAsync(query.QuotationRequestId, cancellationToken)
                        ?? throw new NotFoundException($"Quotation request '{query.QuotationRequestId}' not found.");

        QuotationAccessPolicy.EnsureCanViewQuotation(quotation, currentUser);

        const string sql = """
                           SELECT
                               qd.Id,
                               qd.DocumentId,
                               qd.FileName,
                               qd.DocumentType,
                               qd.Source,
                               qd.CreatedBy,
                               qd.CreatedAt,
                               d.FileSizeBytes,
                               d.MimeType
                           FROM appraisal.QuotationDocuments qd
                           LEFT JOIN document.Documents d ON d.Id = qd.DocumentId AND d.IsDeleted = 0
                           WHERE qd.QuotationRequestId = @QuotationRequestId
                           ORDER BY qd.CreatedAt
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("QuotationRequestId", query.QuotationRequestId);

        var rows = await connection.QueryAsync<QuotationDocumentDto>(sql, parameters);
        return rows.ToList();
    }
}
