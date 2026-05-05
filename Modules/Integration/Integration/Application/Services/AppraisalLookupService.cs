using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.Application.Services;

public class AppraisalLookupService(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalLookupService> logger) : IAppraisalLookupService
{
    private const string SqlByAppraisalId = """
        SELECT
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalCaseKey AS ExternalCaseKey,
            r.ExternalSystem AS ExternalSystem
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE a.Id = @AppraisalId
        """;

    private const string SqlByRequestId = """
        SELECT TOP 1
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalCaseKey AS ExternalCaseKey,
            r.ExternalSystem AS ExternalSystem
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE r.Id = @RequestId
        ORDER BY a.CreatedOn
        """;

    public async Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);

        var command = new CommandDefinition(SqlByAppraisalId, parameters, cancellationToken: ct);
        var row = await connection.QuerySingleOrDefaultAsync<AppraisalKeysRow>(command);

        if (row is null)
        {
            logger.LogWarning("AppraisalLookupService: no appraisal found for AppraisalId {AppraisalId}", appraisalId);
            return null;
        }

        return new AppraisalKeys(row.AppraisalNumber, row.ExternalCaseKey, row.ExternalSystem);
    }

    public async Task<AppraisalKeys?> GetKeysByRequestIdAsync(Guid requestId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("RequestId", requestId);

        var command = new CommandDefinition(SqlByRequestId, parameters, cancellationToken: ct);
        var row = await connection.QueryFirstOrDefaultAsync<AppraisalKeysRow>(command);

        if (row is null)
        {
            logger.LogWarning("AppraisalLookupService: no appraisal found for RequestId {RequestId}", requestId);
            return null;
        }

        return new AppraisalKeys(row.AppraisalNumber, row.ExternalCaseKey, row.ExternalSystem);
    }

    private sealed record AppraisalKeysRow(string? AppraisalNumber, string? ExternalCaseKey, string? ExternalSystem);
}
