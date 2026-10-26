using System.Net;
using System.Text.Json;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Shared.Data;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// End-to-end cover for <c>GET /appraisals/in-progress-by-collateral</c>: an appraisal still in the
/// workflow is reported for the collateral key it carries; closed ones and near-miss keys are not.
/// Keys are unique per test because the integration database is shared and never cleaned.
/// </summary>
[Collection("Integration")]
public class InProgressByCollateralEndpointTests(IntegrationTestFixture fixture)
{
    private readonly HttpClient _client = fixture.IntegrationTestWebApplicationFactory.CreateClient();

    private IServiceScope CreateScope()
        => fixture.IntegrationTestWebApplicationFactory.Services.CreateScope();

    private static string Tag() => Guid.NewGuid().ToString("N")[..12];

    /// <summary>Saves a bare appraisal, lets <paramref name="addProperty"/> shape it, then forces the status.</summary>
    private async Task<(Guid Id, string Number)> SeedAsync(
        string status, Action<AppraisalAggregate> addProperty)
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
        var number = $"IP-{Guid.NewGuid():N}"[..18];
        appraisal.SetAppraisalNumber(number);
        addProperty(appraisal);
        db.Appraisals.Add(appraisal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // The status machine is not under test; set the column directly.
        var connection = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>().GetOpenConnection();
        await connection.ExecuteAsync(
            "UPDATE appraisal.Appraisals SET Status = @status WHERE Id = @id",
            new { status, id = appraisal.Id });
        return (appraisal.Id, number);
    }

    private static Action<AppraisalAggregate> Land(string province, string district, string subDistrict, string titleNo) =>
        a =>
        {
            var prop = a.AddLandProperty();
            prop.LandDetail!.Update(address: Address.Create(subDistrict, district, province), landOffice: "LO");
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, titleNo, "Chanote"));
        };

    private static Action<AppraisalAggregate> Condo(
        string reg, string building, string floor, string room, string province, string district, string subDistrict) =>
        a =>
        {
            var prop = a.AddCondoProperty();
            prop.CondoDetail!.Update(
                condoRegistrationNumber: reg, buildingNumber: building, floorNumber: floor, roomNumber: room,
                titleNumber: "T-1", titleType: "Chanote", ownerName: "Owner",
                address: Address.Create(subDistrict, district, province), landOffice: "LO");
        };

    private static Action<AppraisalAggregate> LeaseLand(string province, string district, string subDistrict, string titleNo) =>
        a =>
        {
            var prop = a.AddLeaseAgreementLandProperty();
            prop.LandDetail!.Update(address: Address.Create(subDistrict, district, province), landOffice: "LO");
            prop.LandDetail.AddTitle(LandTitle.Create(prop.LandDetail.Id, titleNo, "Chanote"));
        };

    private static Action<AppraisalAggregate> LeaseCondo(
        string reg, string building, string floor, string room, string province, string district, string subDistrict) =>
        a =>
        {
            var prop = a.AddLeaseAgreementCondoProperty();
            prop.CondoDetail!.Update(
                condoRegistrationNumber: reg, buildingNumber: building, floorNumber: floor, roomNumber: room,
                titleNumber: "T-1", titleType: "Chanote", ownerName: "Owner",
                address: Address.Create(subDistrict, district, province), landOffice: "LO");
        };

    private static Action<AppraisalAggregate> Machine(string registrationNo) =>
        a => a.AddMachineryProperty().MachineryDetail!.Update(registrationNumber: registrationNo, registrationStatus: true);

    private async Task<List<JsonElement>> GetAsync(string url)
    {
        var response = await _client.GetAsync(url, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, $"GET {url} -> {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.EnumerateArray().ToList();
    }

    private static string LandUrl(string type, string province, string district, string subDistrict, string titleNo) =>
        $"/appraisals/in-progress-by-collateral?type={type}&province={province}&district={district}"
        + $"&subDistrict={subDistrict}&titleNumber={titleNo}";

    [Theory]
    [InlineData("L")]
    [InlineData("LB")]
    public async Task An_in_progress_land_appraisal_is_returned_for_either_land_type(string type)
    {
        var title = $"IPL-{Tag()}";
        var (id, number) = await SeedAsync("InProgress", Land("10", "1001", "100101", title));

        var rows = await GetAsync(LandUrl(type, "10", "1001", "100101", title));

        var row = Assert.Single(rows);
        Assert.Equal(id.ToString(), row.GetProperty("appraisalId").GetString());
        Assert.Equal(number, row.GetProperty("appraisalNumber").GetString());
        Assert.Equal("InProgress", row.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Only_open_appraisals_are_returned_and_closed_ones_are_not()
    {
        var title = $"IPC-{Tag()}";
        var (open, _) = await SeedAsync("Pending", Land("10", "1001", "100101", title));
        await SeedAsync("Completed", Land("10", "1001", "100101", title));
        await SeedAsync("Cancelled", Land("10", "1001", "100101", title));

        var rows = await GetAsync(LandUrl("L", "10", "1001", "100101", title));

        var row = Assert.Single(rows);
        Assert.Equal(open.ToString(), row.GetProperty("appraisalId").GetString());
    }

    [Fact]
    public async Task A_different_sub_district_does_not_match()
    {
        var title = $"IPS-{Tag()}";
        await SeedAsync("InProgress", Land("10", "1001", "100101", title));

        Assert.Empty(await GetAsync(LandUrl("L", "10", "1001", "100102", title)));
    }

    [Fact]
    public async Task An_incomplete_key_returns_an_empty_list()
    {
        var title = $"IPI-{Tag()}";
        await SeedAsync("InProgress", Land("10", "1001", "100101", title));

        Assert.Empty(await GetAsync($"/appraisals/in-progress-by-collateral?type=L&province=10&district=1001&titleNumber={title}"));
    }

    [Fact]
    public async Task An_unsupported_type_is_a_bad_request()
    {
        var response = await _client.GetAsync(
            "/appraisals/in-progress-by-collateral?type=LSL", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_condo_matches_on_the_full_unit_key_and_not_on_a_different_room()
    {
        var reg = $"IPU-{Tag()}";
        var (id, _) = await SeedAsync("Assigned", Condo(reg, "A", "3", "301", "10", "1001", "100101"));

        var url = $"/appraisals/in-progress-by-collateral?type=U&condoRegistrationNumber={reg}&building=A&floor=3"
                  + "&unit={0}&province=10&district=1001&subDistrict=100101";

        var hit = Assert.Single(await GetAsync(string.Format(url, "301")));
        Assert.Equal(id.ToString(), hit.GetProperty("appraisalId").GetString());
        Assert.Empty(await GetAsync(string.Format(url, "302")));
    }

    [Fact]
    public async Task An_open_lease_agreement_on_the_same_title_does_not_match()
    {
        // A lease property carries its own land detail but keys a leasehold master, not the land one.
        var title = $"IPLS-{Tag()}";
        var (land, _) = await SeedAsync("InProgress", Land("10", "1001", "100101", title));
        await SeedAsync("InProgress", LeaseLand("10", "1001", "100101", title));

        var row = Assert.Single(await GetAsync(LandUrl("L", "10", "1001", "100101", title)));
        Assert.Equal(land.ToString(), row.GetProperty("appraisalId").GetString());
    }

    [Fact]
    public async Task An_open_lease_agreement_on_the_same_condo_unit_does_not_match()
    {
        var reg = $"IPLU-{Tag()}";
        await SeedAsync("InProgress", LeaseCondo(reg, "A", "3", "301", "10", "1001", "100101"));

        Assert.Empty(await GetAsync(
            $"/appraisals/in-progress-by-collateral?type=U&condoRegistrationNumber={reg}&building=A&floor=3"
            + "&unit=301&province=10&district=1001&subDistrict=100101"));
    }

    [Fact]
    public async Task A_machine_matches_on_its_registration_number_only_while_open()
    {
        var reg = $"IPM-{Tag()}";
        var (open, _) = await SeedAsync("UnderReview", Machine(reg));
        await SeedAsync("Completed", Machine(reg));

        var rows = await GetAsync($"/appraisals/in-progress-by-collateral?type=MAC&machineRegistrationNo={reg}");

        var row = Assert.Single(rows);
        Assert.Equal(open.ToString(), row.GetProperty("appraisalId").GetString());
    }
}
