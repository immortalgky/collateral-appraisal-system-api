using Dapper;
using global::Integration.Application.Services;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.Webhooks;

/// <summary>
/// AppraisalLookupService is static Dapper over appraisal.Appraisals and request.Requests. Run it against the
/// real schema: SQL Server validates column names before executing, so a column that does not exist
/// (the ORDER BY once named CreatedOn, which these tables do not have) fails here, not in the cancel webhook.
///
/// Rows are inserted raw, not through the aggregate, so no domain event, outbox row or workflow is started;
/// every id and number is unique per run because the shared database is never cleaned between tests.
/// </summary>
[Collection("Integration")]
public class AppraisalLookupServiceTests(IntegrationTestFixture fixture)
{
    [Fact]
    public async Task GetKeysByRequestIdAsync_returns_null_for_an_unknown_request()
    {
        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var lookup = scope.ServiceProvider.GetRequiredService<IAppraisalLookupService>();

        Assert.Null(await lookup.GetKeysByRequestIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetKeysByRequestIdAsync_returns_the_oldest_live_appraisal_number_and_external_case_key()
    {
        var ct = TestContext.Current.CancellationToken;
        var requestId = Guid.CreateVersion7();
        var token = $"{Guid.NewGuid():N}"[..12];

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();
            await connection.ExecuteAsync(
                """
                INSERT INTO request.Requests
                    (Id, RequestNumber, Status, Requestor, RequestorName, Creator, CreatorName,
                     IsPma, IsDeleted, CreatedAt, ExternalCaseKey, ExternalSystem)
                VALUES
                    (@RequestId, @RequestNumber, 'Draft', 'P0000', 'Requestor', 'P0000', 'Requestor',
                     0, 0, SYSDATETIME(), @ExternalCaseKey, 'LOS');

                -- A soft-deleted appraisal created first must not win; of the two live ones the oldest does.
                INSERT INTO appraisal.Appraisals
                    (Id, RequestId, AppraisalNumber, AppraisalType, Priority, Status, IsPma, IsDeleted, CreatedAt, CreatedBy)
                VALUES
                    (@DeletedId, @RequestId, @DeletedNumber, 'New', 'Normal', 'Pending', 0, 1, '2026-01-01', 'U1'),
                    (@LiveId,    @RequestId, @LiveNumber,    'New', 'Normal', 'Pending', 0, 0, '2026-01-02', 'U1'),
                    (@NewerId,   @RequestId, @NewerNumber,   'New', 'Normal', 'Pending', 0, 0, '2026-01-03', 'U1');
                """,
                new
                {
                    RequestId = requestId,
                    RequestNumber = $"REQ-{token}",
                    ExternalCaseKey = $"CASE-{token}",
                    DeletedId = Guid.CreateVersion7(),
                    DeletedNumber = $"LKD-{token}",
                    LiveId = Guid.CreateVersion7(),
                    LiveNumber = $"LK-{token}",
                    NewerId = Guid.CreateVersion7(),
                    NewerNumber = $"LKN-{token}"
                });
        }

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var lookup = scope.ServiceProvider.GetRequiredService<IAppraisalLookupService>();

            var keys = await lookup.GetKeysByRequestIdAsync(requestId, ct);

            Assert.NotNull(keys);
            Assert.Equal($"LK-{token}", keys.AppraisalNumber);
            Assert.Equal($"CASE-{token}", keys.ExternalCaseKey);
            Assert.Equal("LOS", keys.ExternalSystem);
        }
    }
}
