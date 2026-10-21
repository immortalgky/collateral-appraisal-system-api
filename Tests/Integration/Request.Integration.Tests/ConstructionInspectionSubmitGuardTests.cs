using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Integration.Helpers;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Request.Integration.Tests;

/// <summary>
/// The submit guard's refusal as the client sees it: HTTP 400 ProblemDetails whose <c>errorCode</c> extension carries
/// the block reason, for a 06/11 request whose prior appraisal's construction is already 100% complete.
/// </summary>
public class ConstructionInspectionSubmitGuardTests(IntegrationTestFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Guid> SeedCompletedFinishedBuildingAsync()
    {
        using var scope = Fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();

        // The reference view joins the appraisal's own (completed) request.
        var priorRequestId = Guid.NewGuid();
        await using (var conn = new SqlConnection(Fixture.ConnectionString))
            await conn.ExecuteAsync(new CommandDefinition("""
                INSERT INTO request.Requests (Id, Status, Requestor, RequestorName, Creator, CreatorName, CreatedAt, IsPma, IsDeleted, Purpose)
                VALUES (@priorRequestId, N'Completed', N'u', N'u', N'u', N'u', SYSDATETIME(), 0, 0, N'06');
                INSERT INTO request.RequestDetails (RequestId, HasAppraisalBook) VALUES (@priorRequestId, 0);
                """, new { priorRequestId }, cancellationToken: Ct));

        var a = AppraisalAggregate.Create(priorRequestId, AppraisalTypes.Progressive, "Normal", DateTime.Now, purpose: "06");
        a.SetAppraisalNumber($"AP-{Guid.NewGuid():N}"[..14]);
        a.SetInspectionNumber(3);
        typeof(AppraisalAggregate).GetProperty("Status")!.SetValue(a, AppraisalStatus.Completed);
        typeof(AppraisalAggregate).GetProperty("CompletedAt")!.SetValue(a, new DateTime(2026, 10, 21, 9, 30, 0));

        var prop = a.AddLandProperty();
        prop.LandDetail!.Update(address: Address.Create("100101", "1001", "10"), landOffice: "0100");
        prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, $"T-{Guid.NewGuid():N}"[..12], "Chanote"));
        var inspection = ConstructionInspection.CreateSummary(Guid.Empty, 1_000_000m, null, 90m, 100m, null);
        prop.SetConstructionInspection(inspection);
        typeof(ConstructionInspection).GetProperty("AppraisalPropertyId")!.SetValue(inspection, prop.Id);

        db.Appraisals.Add(a);
        await db.SaveChangesAsync(Ct);
        return a.Id;
    }

    private async Task<Guid> CreateRequestAsync(string purpose, Guid prevAppraisalId)
    {
        var json = JsonNode.Parse(await JsonHelper.JsonFileToJson("Request.Integration.Tests", "CreateRequest_ValidRequest.json"))!;
        json["purpose"] = purpose;
        json["detail"]!["prevAppraisalId"] = prevAppraisalId;

        var response = await _client.PostAsync(
            "/requests", new StringContent(json.ToJsonString(), Encoding.UTF8, "application/json"), Ct);
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        return (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("id").GetGuid();
    }

    [Theory]
    [InlineData("06")]
    [InlineData("11")]
    public async Task Submitting_an_inspection_request_on_a_finished_building_is_refused_with_an_errorCode(string purpose)
    {
        var prior = await SeedCompletedFinishedBuildingAsync();
        var requestId = await CreateRequestAsync(purpose, prior);

        var response = await _client.PostAsJsonAsync($"/requests/{requestId}/submit", new { id = requestId }, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);

        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{(int)response.StatusCode}: {body}");
        var problem = JsonDocument.Parse(body).RootElement;
        Assert.True(problem.TryGetProperty("errorCode", out var errorCode), body);
        Assert.Equal("ConstructionComplete", errorCode.GetString());
        Assert.Contains("is already 100% complete", problem.GetProperty("detail").GetString());
        Assert.Contains("inspection #3, 21/10/2026", problem.GetProperty("detail").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
    }
}
