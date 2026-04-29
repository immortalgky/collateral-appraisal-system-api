using Dapper;
using Microsoft.Extensions.Logging;
using Shared.Data;

namespace Integration.Application.Services;

public class AppraisalLookupService(
    ISqlConnectionFactory connectionFactory,
    ILogger<AppraisalLookupService> logger) : IAppraisalLookupService
{
    private const string Sql = """
        SELECT
            a.AppraisalNumber AS AppraisalNumber,
            r.ExternalCaseKey AS ExternalCaseKey
        FROM appraisal.Appraisals a
        JOIN request.Requests r ON r.Id = a.RequestId
        WHERE a.Id = @AppraisalId
        """;

    public async Task<AppraisalKeys?> GetKeysAsync(Guid appraisalId, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", appraisalId);

        var command = new CommandDefinition(Sql, parameters, cancellationToken: ct);
        var row = await connection.QuerySingleOrDefaultAsync<AppraisalKeysRow>(command);

        if (row is null)
        {
            logger.LogWarning("AppraisalLookupService: no appraisal found for AppraisalId {AppraisalId}", appraisalId);
            return null;
        }

        return new AppraisalKeys(row.AppraisalNumber, row.ExternalCaseKey);
    }

    private sealed record AppraisalKeysRow(string? AppraisalNumber, string? ExternalCaseKey);
}
