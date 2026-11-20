using System.Data;
using FluentAssertions;
using Reporting.Application.Models;
using Reporting.Application.Providers;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The external letter's ค่าพิกัด GPS row reads "N 13.000000 E 100.000000" (ThaiAddressFormatter.FormatGps), land first, else the condo.</summary>
public class BookExternalGpsTests
{
    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static async Task<string?> GpsAsync(decimal? landLat, decimal? landLon, decimal? condoLat = null, decimal? condoLon = null)
    {
        var header = new DataTable();
        header.Columns.Add("AppraisalNumber", typeof(string)); header.Columns.Add("RequestId", typeof(Guid)); header.Columns.Add("CompletedAt", typeof(DateTime));
        header.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        var land = new DataTable();
        land.Columns.Add("LandDetailId", typeof(Guid)); land.Columns.Add("Latitude", typeof(decimal)); land.Columns.Add("Longitude", typeof(decimal));
        if (landLat is not null || landLon is not null) land.Rows.Add(Guid.NewGuid(), (object?)landLat ?? DBNull.Value, (object?)landLon ?? DBNull.Value);
        var condo = new DataTable();
        condo.Columns.Add("CondoRoomNumber", typeof(string)); condo.Columns.Add("CondoLatitude", typeof(decimal)); condo.Columns.Add("CondoLongitude", typeof(decimal));
        if (condoLat is not null || condoLon is not null) condo.Rows.Add("101", (object?)condoLat ?? DBNull.Value, (object?)condoLon ?? DBNull.Value);
        var ds = new DataSet();
        for (var i = 0; i < 14; i++) ds.Tables.Add(i == 0 ? header : i == 6 ? land : i == 8 ? condo : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        return (await ExternalBookBuilder.BuildAsync(f.GetOpenConnection(), Guid.NewGuid(), new AppraisalSummaryModel(), 70m, CancellationToken.None)).Gps;
    }

    [Fact]
    public async Task The_land_coordinates_read_N_lat_E_lon()
    {
        (await GpsAsync(13m, 100m)).Should().Be("N 13.000000 E 100.000000");
        (await GpsAsync(13.7563456m, 100.5018m)).Should().Be("N 13.756346 E 100.501800");
    }

    [Fact]
    public async Task A_condo_only_appraisal_falls_back_to_the_condo_and_a_land_with_both_coordinates_wins()
    {
        (await GpsAsync(null, null, 7.8m, 98.4m)).Should().Be("N 7.800000 E 98.400000");
        (await GpsAsync(13m, 100m, 7.8m, 98.4m)).Should().Be("N 13.000000 E 100.000000");
        (await GpsAsync(13m, null, 7.8m, 98.4m)).Should().Be("N 7.800000 E 98.400000");   // a land with only one coordinate has none
    }

    [Fact]
    public async Task South_and_west_are_labelled_from_the_sign_and_a_book_with_no_coordinates_has_no_row()
    {
        (await GpsAsync(-6.2m, -106.8m)).Should().Be("S 6.200000 W 106.800000");
        (await GpsAsync(null, null)).Should().BeNull();
    }
}
