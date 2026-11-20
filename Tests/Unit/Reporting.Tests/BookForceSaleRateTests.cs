using System.Data;
using FluentAssertions;
using Reporting.Application.Providers;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The % the book prints follows the rate the value was calculated with: override, else the block project's, else the system default; 0 or below and above 100 become 70.</summary>
public class BookForceSaleRateTests
{
    [Theory]
    [InlineData(65.0, 55.0, 70.0, 65.0)]      // the override wins
    [InlineData(null, 55.0, 70.0, 55.0)]    // else the block project's %
    [InlineData(null, null, 60.0, 60.0)]  // else the system default
    [InlineData(null, null, 70.0, 70.0)]  // default unset reaches the loaders as 70
    [InlineData(null, 0.0, 60.0, 70.0)]     // a bad project % falls back to 70 (not the default, not "0%")
    [InlineData(null, 150.0, 60.0, 70.0)]
    [InlineData(0.0, 55.0, 60.0, 70.0)]       // the guard covers the override too, as ForceSaleRateResolver.ResolveAsync does
    [InlineData(null, null, 100.0, 100.0)]
    public void Resolve_follows_the_resolver_chain_and_range_guard(double? over, double? project, double def, double expected) =>
        BookForceSaleRate.Resolve((decimal?)over, (decimal?)project, (decimal)def).Should().Be((decimal)expected);

    // The loaders only need RS12 / RS05 to carry the valuation columns; every other result set can be empty.
    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static DataTable Valuation(decimal? over, decimal? project)
    {
        var t = new DataTable();
        foreach (var c in new[] { "AppraisedValue", "ForcedSaleValue", "ForceSaleRate", "InsuranceValue", "ProjectForceSalePercentage" }) t.Columns.Add(c, typeof(decimal));
        t.Rows.Add(DBNull.Value, DBNull.Value, (object?)over ?? DBNull.Value, DBNull.Value, (object?)project ?? DBNull.Value);
        return t;
    }

    private static DataTable Header()
    {
        var t = new DataTable();
        t.Columns.Add("AppraisalNumber", typeof(string)); t.Columns.Add("RequestId", typeof(Guid)); t.Columns.Add("CompletedAt", typeof(DateTime));
        t.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        return t;
    }

    private static FakeSqlConnectionFactory Factory(int valuationIndex, DataTable valuation)
    {
        var ds = new DataSet();
        for (var i = 0; i < 16; i++) ds.Tables.Add(i == 0 ? Header() : i == valuationIndex ? valuation : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);
        return f;
    }

    [Theory]
    [InlineData(null, 55.0, 60.0, 55.0)]    // no override: the project %, not the default
    [InlineData(null, null, 60.0, 60.0)]  // no project: the default
    [InlineData(null, null, 70.0, 70.0)]
    [InlineData(65.0, 55.0, 60.0, 65.0)]
    [InlineData(null, 0.0, 60.0, 70.0)]
    [InlineData(null, 150.0, 60.0, 70.0)]
    public async Task The_summary_forms_loader_resolves_the_rate_the_same_way(double? over, double? project, double def, double expected)
    {
        var data = await AppraisalSummaryCommonLoader.LoadAsync(Factory(4, Valuation((decimal?)over, (decimal?)project)).GetOpenConnection(), Guid.NewGuid(), (decimal)def);

        data.ForceSaleRate.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null, 55.0, 60.0, 55.0)]
    [InlineData(null, null, 60.0, 60.0)]
    [InlineData(null, null, 70.0, 70.0)]
    [InlineData(65.0, 55.0, 60.0, 65.0)]
    [InlineData(null, 0.0, 60.0, 70.0)]
    [InlineData(null, 150.0, 60.0, 70.0)]
    public async Task The_external_book_resolves_the_rate_the_same_way(double? over, double? project, double def, double expected)
    {
        var model = await ExternalBookBuilder.BuildAsync(Factory(10, Valuation((decimal?)over, (decimal?)project)).GetOpenConnection(), Guid.NewGuid(),
            new Reporting.Application.Models.AppraisalSummaryModel(), (decimal)def, CancellationToken.None);

        model.ForceSaleRate.Should().Be((decimal)expected);
    }
}
