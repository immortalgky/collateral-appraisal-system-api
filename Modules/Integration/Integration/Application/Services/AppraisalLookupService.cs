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

    private const string SqlPriorAppraisalByNumber = """
        SELECT TOP 1 a.Id, a.Status
        FROM appraisal.Appraisals a
        WHERE a.AppraisalNumber = @AppraisalNumber AND a.IsDeleted = 0
        """;

    public async Task<PriorAppraisalRef?> ResolvePriorAppraisalByNumberAsync(
        string appraisalNumber, CancellationToken ct = default)
    {
        var connection = connectionFactory.GetOpenConnection();

        var parameters = new DynamicParameters();
        parameters.Add("AppraisalNumber", appraisalNumber);

        var command = new CommandDefinition(SqlPriorAppraisalByNumber, parameters, cancellationToken: ct);
        var row = await connection.QueryFirstOrDefaultAsync<PriorAppraisalRefRow>(command);

        if (row is null)
        {
            logger.LogWarning(
                "AppraisalLookupService: no appraisal found for AppraisalNumber {AppraisalNumber}", appraisalNumber);
            return null;
        }

        return new PriorAppraisalRef(row.Id, row.Status);
    }

    private sealed record PriorAppraisalRefRow(Guid Id, string? Status);

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
