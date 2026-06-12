using Reporting.Application.Services;

namespace Reporting.Infrastructure;

/// <summary>
/// Resolves a report identifier to the entity Guid via a single parameterized Dapper lookup:
/// Meeting-category reports key off <c>workflow.Meetings.MeetingNo</c>; all others off
/// <c>appraisal.Appraisals.AppraisalNumber</c> (both columns carry a unique filtered index).
/// An identifier already in Guid form — a direct id, or a value already resolved upstream — is
/// returned as-is, which also keeps composite child-report recursion a no-op.
/// </summary>
internal sealed class ReportEntityResolver(
    ISqlConnectionFactory connectionFactory,
    ILogger<ReportEntityResolver> logger) : IReportEntityResolver
{
    public async Task<string> ResolveAsync(
        string entityId,
        string category,
        CancellationToken cancellationToken)
    {
        if (Guid.TryParse(entityId, out _))
            return entityId;

        var isMeeting = string.Equals(category, "Meeting", StringComparison.OrdinalIgnoreCase);

        var sql = isMeeting
            ? "SELECT TOP 1 Id FROM workflow.Meetings WHERE MeetingNo = @Number"
            : "SELECT TOP 1 Id FROM appraisal.Appraisals WHERE AppraisalNumber = @Number AND IsDeleted = 0";

        var parameters = new DynamicParameters();
        parameters.Add("Number", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var id = await connection.QueryFirstOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        if (id is null)
            throw new NotFoundException(isMeeting ? "Meeting" : "Appraisal", entityId);

        logger.LogInformation(
            "Resolved {Kind} number {Number} → {EntityId}",
            isMeeting ? "meeting" : "appraisal", entityId, id.Value);

        return id.Value.ToString();
    }
}
