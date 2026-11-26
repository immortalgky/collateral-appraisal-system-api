using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Integration.Auth.Integration.Tests.Bff;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;

namespace Integration.AppraisalResults;

/// <summary>
/// How the three LOS result endpoints treat a suspended appraisal (Completed + IsSuspended = 1):
/// v2 by number → 400 APPRAISAL_SUSPENDED, v2 by case key → left out, v1 legacy → ResultCode 0.
/// Rows are inserted raw (no workflow or events) and the flag is set directly: the suspension screen is
/// not under test. Every key is unique per test because the shared database is never cleaned.
/// </summary>
[Collection("Integration")]
public class SuspendedAppraisalResultTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Tag() => Guid.NewGuid().ToString("N")[..10];

    // ── Clients ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>Real client_credentials token with the "integration" scope, as LOS calls these routes.</summary>
    private async Task<HttpClient> LosClientAsync()
    {
        var client = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        var token = await BffTestSupport.ClientCredentialsTokenAsync(client, "integration");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    // ── Seeding ─────────────────────────────────────────────────────────────────────────────────────

    private async Task<T> WithConnectionAsync<T>(Func<System.Data.IDbConnection, Task<T>> action)
    {
        using var scope = fixture.BffWebApplicationFactory.Services.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();
        return await action(connection);
    }

    private async Task<Guid> SeedRequestAsync(string caseKey) =>
        await WithConnectionAsync(async c =>
        {
            var requestId = Guid.CreateVersion7();
            await c.ExecuteAsync(
                """
                INSERT INTO request.Requests
                    (Id, RequestNumber, Status, Requestor, RequestorName, Creator, CreatorName,
                     IsPma, IsDeleted, CreatedAt, ExternalCaseKey, ExternalSystem)
                VALUES
                    (@requestId, @requestNumber, 'Completed', 'P0000', 'Requestor', 'P0000', 'Requestor',
                     0, 0, SYSDATETIME(), @caseKey, 'LOS');
                """,
                new { requestId, requestNumber = $"RQ-{Tag()}", caseKey });
            return requestId;
        });

    private async Task<string> SeedAppraisalAsync(
        Guid requestId, string status = "Completed", bool suspended = false, bool deleted = false) =>
        await WithConnectionAsync(async c =>
        {
            var number = $"SR-{Tag()}";
            await c.ExecuteAsync(
                """
                INSERT INTO appraisal.Appraisals
                    (Id, RequestId, AppraisalNumber, AppraisalType, Priority, Status, IsPma, IsDeleted,
                     IsSuspended, CompletedAt, CreatedAt, CreatedBy)
                VALUES
                    (@id, @requestId, @number, 'New', 'Normal', @status, 0, @deleted,
                     @suspended, @completedAt, SYSDATETIME(), 'U1');
                """,
                new
                {
                    id = Guid.CreateVersion7(), requestId, number, status, deleted, suspended,
                    completedAt = status == "Completed" ? DateTime.Now : (DateTime?)null
                });
            return number;
        });

    /// <summary>A condo block appraisal with one unit: room 301, floor 3.</summary>
    private async Task<string> SeedCondoBlockAsync(bool suspended)
    {
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"), suspended: suspended);
        await WithConnectionAsync(async c =>
        {
            var projectId = Guid.CreateVersion7();
            var unitId = Guid.CreateVersion7();
            await c.ExecuteAsync(
                """
                INSERT INTO appraisal.Projects (Id, AppraisalId, ProjectType)
                SELECT @projectId, Id, 'U' FROM appraisal.Appraisals WHERE AppraisalNumber = @number;
                INSERT INTO appraisal.ProjectUnits
                    (Id, ProjectId, UploadBatchId, SequenceNumber, CondoRegistrationNumber, RoomNumber, Floor)
                VALUES (@unitId, @projectId, NEWID(), 1, '301', '301', 3);
                INSERT INTO appraisal.ProjectUnitPrices (Id, ProjectUnitId, TotalAppraisalValueRounded)
                VALUES (NEWID(), @unitId, 3000000);
                """,
                new { projectId, unitId, number });
            return 0;
        });
        return number;
    }

    private Task SetSuspendedAsync(string number, bool suspended) =>
        WithConnectionAsync(c => c.ExecuteAsync(
            "UPDATE appraisal.Appraisals SET IsSuspended = @suspended WHERE AppraisalNumber = @number",
            new { suspended, number }));

    private Task<int> TicketCountAsync(string number) =>
        WithConnectionAsync(c => c.ExecuteScalarAsync<int>(
            """
            SELECT COUNT(*) FROM appraisal.UnitTickets t
            JOIN appraisal.Appraisals a ON a.Id = t.AppraisalId
            WHERE a.AppraisalNumber = @number
            """,
            new { number }));

    // ── Calls ───────────────────────────────────────────────────────────────────────────────────────

    private static async Task<(HttpStatusCode Status, JsonElement Body)> GetJsonAsync(HttpClient client, string url)
    {
        using var response = await client.GetAsync(url, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        var body = text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone();
        return (response.StatusCode, body);
    }

    private static Task<(HttpStatusCode Status, JsonElement Body)> ByNumberAsync(
        HttpClient client, string number, string query = "") =>
        GetJsonAsync(client, $"/api/v2/appraisals/{number}/result{query}");

    private static async Task<List<string>> ByCaseKeyAsync(HttpClient client, string caseKey)
    {
        var (status, body) = await GetJsonAsync(client, $"/api/v2/appraisals/result?externalCaseKey={caseKey}");
        status.Should().Be(HttpStatusCode.OK);
        return body.EnumerateArray().Select(e => e.GetProperty("appraisalNumber").GetString()!).ToList();
    }

    /// <summary>v1 legacy: condo block unit selected by Filter1 = room, Filter2 = floor.</summary>
    private static async Task<int> LegacyResultCodeAsync(HttpClient client, string number)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/appraisals/result",
            new { ApplicationNo = number, AssetTypeId = 0, Filter1 = "301", Filter2 = "3" }, Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, "the legacy contract is always 200");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return json.RootElement.GetProperty("ResultCode").GetInt32();
    }

    // ── v2 by number ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ByNumber_active_appraisal_returns_200_without_an_isSuspended_field()
    {
        using var client = await LosClientAsync();
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"));

        var (status, body) = await ByNumberAsync(client, number);

        status.Should().Be(HttpStatusCode.OK);
        body.GetProperty("appraisalNumber").GetString().Should().Be(number);
        body.TryGetProperty("isSuspended", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ByNumber_suspended_appraisal_returns_400_with_APPRAISAL_SUSPENDED()
    {
        using var client = await LosClientAsync();
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"), suspended: true);

        var (status, body) = await ByNumberAsync(client, number);

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("detail").GetString().Should().Be("Appraisal is suspended.");
        body.GetProperty("errorCode").GetString().Should().Be("APPRAISAL_SUSPENDED");
    }

    [Fact]
    public async Task ByNumber_follows_the_latest_suspension_state()
    {
        using var client = await LosClientAsync();
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"), suspended: true);

        await SetSuspendedAsync(number, false);
        (await ByNumberAsync(client, number)).Status.Should().Be(HttpStatusCode.OK);

        await SetSuspendedAsync(number, true);
        (await ByNumberAsync(client, number)).Status.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ByNumber_active_block_issues_a_unit_ticket()
    {
        // Control for the next test: proves the ticket count really moves on this path.
        using var client = await LosClientAsync();
        var number = await SeedCondoBlockAsync(suspended: false);

        var (status, _) = await ByNumberAsync(client, number, "?roomNumber=301&floorNumber=3");

        status.Should().Be(HttpStatusCode.OK);
        (await TicketCountAsync(number)).Should().Be(1);
    }

    [Fact]
    public async Task ByNumber_suspended_block_is_rejected_before_any_unit_ticket_is_issued()
    {
        using var client = await LosClientAsync();
        var number = await SeedCondoBlockAsync(suspended: true);

        var (status, body) = await ByNumberAsync(client, number, "?roomNumber=301&floorNumber=3");

        status.Should().Be(HttpStatusCode.BadRequest);
        body.GetProperty("errorCode").GetString().Should().Be("APPRAISAL_SUSPENDED");
        (await TicketCountAsync(number)).Should().Be(0);
    }

    [Fact]
    public async Task ByNumber_in_progress_appraisal_is_still_returned()
    {
        using var client = await LosClientAsync();
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"), status: "InProgress");

        (await ByNumberAsync(client, number)).Status.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ByNumber_unknown_or_deleted_appraisal_returns_404()
    {
        using var client = await LosClientAsync();
        var deleted = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"), deleted: true);

        (await ByNumberAsync(client, $"NONE-{Tag()}")).Status.Should().Be(HttpStatusCode.NotFound);
        (await ByNumberAsync(client, deleted)).Status.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ByNumber_without_a_token_is_rejected()
    {
        using var client = fixture.BffWebApplicationFactory.CreateBrowser(handleCookies: false);
        var number = await SeedAppraisalAsync(await SeedRequestAsync($"CK-{Tag()}"));

        var (status, _) = await ByNumberAsync(client, number);

        status.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden);
    }

    // ── v2 by case key ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ByCaseKey_returns_only_the_active_completed_appraisal()
    {
        using var client = await LosClientAsync();
        var caseKey = $"CK-{Tag()}";
        var active = await SeedAppraisalAsync(await SeedRequestAsync(caseKey));
        await SeedAppraisalAsync(await SeedRequestAsync(caseKey), suspended: true);
        await SeedAppraisalAsync(await SeedRequestAsync(caseKey), status: "InProgress");

        (await ByCaseKeyAsync(client, caseKey)).Should().Equal(active);
    }

    [Fact]
    public async Task ByCaseKey_with_only_a_suspended_appraisal_returns_an_empty_list_until_reactivated()
    {
        using var client = await LosClientAsync();
        var caseKey = $"CK-{Tag()}";
        var number = await SeedAppraisalAsync(await SeedRequestAsync(caseKey), suspended: true);

        (await ByCaseKeyAsync(client, caseKey)).Should().BeEmpty();

        await SetSuspendedAsync(number, false);
        (await ByCaseKeyAsync(client, caseKey)).Should().Equal(number);
    }

    [Fact]
    public async Task ByCaseKey_unknown_key_returns_an_empty_list()
    {
        using var client = await LosClientAsync();

        (await ByCaseKeyAsync(client, $"NONE-{Tag()}")).Should().BeEmpty();
    }

    [Fact]
    public async Task ByCaseKey_without_a_key_returns_400()
    {
        using var client = await LosClientAsync();

        var (status, _) = await GetJsonAsync(client, "/api/v2/appraisals/result?externalCaseKey=");

        status.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── v1 legacy ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Legacy_active_appraisal_returns_result_code_1()
    {
        using var client = await LosClientAsync();
        var number = await SeedCondoBlockAsync(suspended: false);

        (await LegacyResultCodeAsync(client, number)).Should().Be(1);
    }

    [Fact]
    public async Task Legacy_suspended_appraisal_returns_result_code_0_until_reactivated()
    {
        using var client = await LosClientAsync();
        var number = await SeedCondoBlockAsync(suspended: true);

        (await LegacyResultCodeAsync(client, number)).Should().Be(0);

        await SetSuspendedAsync(number, false);
        (await LegacyResultCodeAsync(client, number)).Should().Be(1);
    }

    [Fact]
    public async Task Legacy_unknown_appraisal_returns_result_code_0()
    {
        using var client = await LosClientAsync();

        (await LegacyResultCodeAsync(client, $"NONE-{Tag()}")).Should().Be(0);
    }
}
