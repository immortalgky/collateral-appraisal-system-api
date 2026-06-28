using Reporting.Application.Services;

namespace Reporting.Infrastructure;

/// <summary>
/// Resolves a report identifier to the entity Guid via a single parameterized Dapper lookup:
/// Meeting-category reports key off <c>workflow.Meetings.MeetingNo</c>; Appointment-category
/// reports key off the <b>RequestId</b> and accept a typed Request No. OR Appraisal No.; all other
/// categories off <c>appraisal.Appraisals.AppraisalNumber</c>. An identifier already in Guid form —
/// a direct id, or a value already resolved upstream — is returned as-is, which also keeps composite
/// child-report recursion a no-op.
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
        var isAppointment = string.Equals(category, "Appointment", StringComparison.OrdinalIgnoreCase);

        // Appointment letter is keyed by RequestId; the user may type a Request No. or an
        // Appraisal No. (request match wins, else fall back to the appraisal's RequestId).
        var sql = isMeeting
            ? "SELECT TOP 1 Id FROM workflow.Meetings WHERE MeetingNo = @Number"
            : isAppointment
                ? """
                  SELECT TOP 1 Id FROM (
                      SELECT r.Id, 1 AS pri FROM request.Requests r
                          WHERE r.RequestNumber = @Number AND r.IsDeleted = 0
                      UNION ALL
                      SELECT a.RequestId AS Id, 2 AS pri FROM appraisal.Appraisals a
                          WHERE a.AppraisalNumber = @Number AND a.IsDeleted = 0
                  ) x ORDER BY pri
                  """
                : "SELECT TOP 1 Id FROM appraisal.Appraisals WHERE AppraisalNumber = @Number AND IsDeleted = 0";

        var parameters = new DynamicParameters();
        parameters.Add("Number", entityId);

        using var connection = connectionFactory.CreateNewConnection();
        var id = await connection.QueryFirstOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        var kind = isMeeting ? "Meeting" : isAppointment ? "Request" : "Appraisal";
        if (id is null)
            throw new NotFoundException(kind, entityId);

        logger.LogInformation("Resolved {Kind} number {Number} → {EntityId}", kind, entityId, id.Value);

        return id.Value.ToString();
    }
}
