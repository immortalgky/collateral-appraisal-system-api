using System.Text.Json;
using Appraisal.Infrastructure;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using Dapper;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// <c>GET /appraisals?search=</c> says why each row is in the list.
///
/// The all-fields search reaches columns the table never shows — a follow-up inspection carries the
/// appraisal it continues as its previous appraisal number, so searching an appraisal number also
/// returns the inspection with no visible reason. <c>matchedOn</c> is that reason.
/// </summary>
[Collection("Integration")]
public class AppraisalListMatchedOnTests(IntegrationTestFixture fixture)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed record Match(string Field, string Value);

    private sealed record Row(Guid Id, string? AppraisalNumber, List<Match>? MatchedOn);

    private sealed record Page(List<Row> Items);

    private sealed record Response(Page Result);

    /// <summary>Seeds an appraisal and its request; <paramref name="prevAppraisalNumber"/> lands on RequestDetails.</summary>
    private async Task<(Guid AppraisalId, string AppraisalNumber)> SeedAsync(
        string appraisalNumber, string? prevAppraisalNumber = null)
    {
        var requestId = Guid.CreateVersion7();

        using var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var appraisal = AppraisalAggregate.Create(
            requestId: requestId, appraisalType: "New", priority: "Normal", now: DateTime.Now);
        appraisal.SetAppraisalNumber(appraisalNumber);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync();

        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();
        await connection.ExecuteAsync(
            """
            INSERT INTO request.Requests
                (Id, RequestNumber, Status, Requestor, RequestorName, Creator, CreatorName,
                 IsPma, IsDeleted, CreatedAt)
            VALUES
                (@Id, @RequestNumber, 'Draft', 'P0000', 'Requestor', 'P0000', 'Requestor',
                 0, 0, SYSDATETIME());

            INSERT INTO request.RequestDetails (RequestId, HasAppraisalBook, PrevAppraisalNumber)
            VALUES (@Id, 0, @PrevAppraisalNumber);
            """,
            new { Id = requestId, RequestNumber = $"REQ-{appraisalNumber}", PrevAppraisalNumber = prevAppraisalNumber });

        return (appraisal.Id, appraisalNumber);
    }

    private async Task<List<Row>> ListAsync(string query)
    {
        using var client = fixture.IntegrationTestWebApplicationFactory.CreateClient();
        var response = await client.GetAsync($"/appraisals?pageSize=50&{query}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"GET /appraisals?{query} -> {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<Response>(body, Json)!.Result.Items;
    }

    [Fact]
    public async Task A_search_says_which_field_each_row_matched_on()
    {
        // Unique per run: the integration database is shared and never cleaned.
        var token = $"MO{Guid.NewGuid():N}"[..12];
        var (originalId, originalNumber) = await SeedAsync($"A{token}");
        var (followUpId, _) = await SeedAsync($"B{token}", prevAppraisalNumber: originalNumber);

        var rows = await ListAsync($"search={Uri.EscapeDataString(originalNumber)}");

        var original = rows.Single(r => r.Id == originalId);
        Assert.Equal([new Match("appraisalNumber", originalNumber)], original.MatchedOn);
        var followUp = rows.Single(r => r.Id == followUpId);
        Assert.Equal([new Match("prevAppraisalNumber", originalNumber)], followUp.MatchedOn);
    }

    [Fact]
    public async Task Without_a_search_no_row_carries_matches()
    {
        var token = $"MO{Guid.NewGuid():N}"[..12];
        var (id, number) = await SeedAsync($"C{token}");

        var rows = await ListAsync($"appraisalNumber={Uri.EscapeDataString(number)}");

        Assert.Null(rows.Single(r => r.Id == id).MatchedOn);
    }
}
