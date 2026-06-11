using Dapper;
using Shared.Data;

namespace Workflow.Meetings.Features.Documents;

public class ListMeetingDocumentsEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("/meetings/{id:guid}/documents", async (
                Guid id,
                ISender sender,
                CancellationToken ct) =>
            {
                var result = await sender.Send(new ListMeetingDocumentsQuery(id), ct);
                return Results.Ok(result);
            })
            .WithName("ListMeetingDocuments")
            .WithTags("Meetings")
            .RequireAuthorization("MeetingAdmin")
            .Produces<List<MeetingDocumentDto>>();
    }
}

public record ListMeetingDocumentsQuery(Guid MeetingId) : IQuery<List<MeetingDocumentDto>>;

public record MeetingDocumentDto(
    Guid Id,
    Guid DocumentId,
    string FileName,
    string DocumentType,
    string Source,
    string? CreatedBy,
    DateTime? CreatedAt,
    long? FileSizeBytes,
    string? MimeType);

public class ListMeetingDocumentsQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
    : IQueryHandler<ListMeetingDocumentsQuery, List<MeetingDocumentDto>>
{
    public async Task<List<MeetingDocumentDto>> Handle(
        ListMeetingDocumentsQuery query,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               md.Id,
                               md.DocumentId,
                               md.FileName,
                               md.DocumentType,
                               md.Source,
                               md.CreatedBy,
                               md.CreatedAt,
                               d.FileSizeBytes,
                               d.MimeType
                           FROM workflow.MeetingDocuments md
                           LEFT JOIN document.Documents d ON d.Id = md.DocumentId AND d.IsDeleted = 0
                           WHERE md.MeetingId = @MeetingId
                           ORDER BY md.CreatedAt
                           """;

        var connection = sqlConnectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("MeetingId", query.MeetingId);

        var rows = await connection.QueryAsync<MeetingDocumentDto>(sql, parameters);
        return rows.ToList();
    }
}
