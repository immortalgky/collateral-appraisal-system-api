using Appraisal.Domain.Projects;
using Appraisal.Infrastructure;
using Dapper;
using Integration.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Integration.Appraisal.Integration.Tests;

/// <summary>
/// Pins what SQL Server / SqlClient do with a third decimal on the decimal(18,2) price columns, because the
/// calculated TotalAppraisalValue is not rounded to 2 dp in the domain (LB: price + land adjustment + location,
/// Condo: area-scaled). Run against the isolated Testcontainers database.
/// </summary>
[Collection("Integration")]
public class UnitPriceDecimalStorageTests(IntegrationTestFixture fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    // .125 and .145 are the rows that matter: truncation gives .12/.14 and half-even gives .12/.14, half-away gives .13/.15.
    [InlineData("1369326.125", "1369326.13")]
    [InlineData("1369326.145", "1369326.15")]
    [InlineData("1369326.135", "1369326.14")]
    [InlineData("1369326.124", "1369326.12")]
    public async Task A_third_decimal_is_rounded_half_away_from_zero_on_save(string written, string expectedStored)
    {
        Guid unitId;
        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", DateTime.Now);
            appraisal.SetAppraisalNumber($"DEC-{Guid.NewGuid():N}"[..18]);
            db.Appraisals.Add(appraisal);
            await db.SaveChangesAsync(Ct);

            var project = Project.Create(appraisal.Id, ProjectType.Condo, projectName: "Decimal Test");
            var unit = ProjectUnit.CreateCondo(project.Id, Guid.Empty, 1, 1, "A", "9/1", "101", null, 30m, 1m);
            project.ImportUnits("units.xlsx", null, [unit]);
            db.Projects.Add(project);

            var price = ProjectUnitPrice.Create(unit.Id);
            price.UpdateCondoCalculatedValues(null, 1m, 0m, decimal.Parse(written), decimal.Parse(written), 1m, 1m);
            db.ProjectUnitPrices.Add(price);
            await db.SaveChangesAsync(Ct);
            unitId = unit.Id;
        }

        using (var scope = fixture.IntegrationTestWebApplicationFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppraisalDbContext>();
            var viaEf = await db.ProjectUnitPrices.AsNoTracking()
                .Where(p => p.ProjectUnitId == unitId).Select(p => p.TotalAppraisalValue).SingleAsync(Ct);
            Assert.Equal(decimal.Parse(expectedStored), viaEf);
        }

        await using var conn = new SqlConnection(fixture.ConnectionString);
        var raw = await conn.ExecuteScalarAsync<decimal>(new CommandDefinition(
            "SELECT TotalAppraisalValue FROM appraisal.ProjectUnitPrices WHERE ProjectUnitId = @unitId",
            new { unitId }, cancellationToken: Ct));
        Assert.Equal(decimal.Parse(expectedStored), raw);
    }
}
