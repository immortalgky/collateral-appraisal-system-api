using Appraisal.Domain.Appraisals;

namespace Appraisal.Tests.Domain;

/// <summary>
/// <see cref="LandAppraisalDetail.AddTitle"/> puts a title that arrives without a position after the
/// others, so no path can make an unnumbered title "the first title" that LOS / AS400 / reports pick.
/// </summary>
public class LandTitleOrderDomainTests
{
    private static LandAppraisalDetail NewLand()
    {
        var appraisal = Appraisal.Domain.Appraisals.Appraisal.Create(
            requestId: Guid.NewGuid(), appraisalType: "New", priority: "Normal", now: new DateTime(2026, 1, 1));
        return appraisal.AddLandProperty().LandDetail!;
    }

    [Fact]
    public void Unnumbered_titles_are_numbered_in_the_order_they_are_added()
    {
        var land = NewLand();
        foreach (var number in new[] { "C", "A", "B" })
            land.AddTitle(LandTitle.Create(land.Id, number, "DEED"));

        Assert.Equal(["C", "A", "B"], land.Titles.Select(t => t.TitleNumber));
        Assert.Equal([1, 2, 3], land.Titles.Select(t => t.SequenceNumber));
    }

    [Fact]
    public void An_unnumbered_title_goes_after_titles_that_predate_the_column()
    {
        var land = NewLand();
        var legacy = LandTitle.Create(land.Id, "OLD", "DEED");
        land.AddTitle(legacy);
        legacy.SetSequenceNumber(0); // as loaded from a row written before SequenceNumber existed

        land.AddTitle(LandTitle.Create(land.Id, "NEW", "DEED"));

        Assert.Equal(["OLD", "NEW"], land.Titles.Select(t => t.TitleNumber));
    }

    [Fact]
    public void Titles_that_predate_the_column_come_out_in_SQL_Server_uniqueidentifier_order()
    {
        // The order SQL Server's ORDER BY gives these ids (checked against a live instance), which is what
        // every SQL reader's `ORDER BY SequenceNumber, Id` returns — not Guid.CompareTo's order.
        string[] sqlOrder =
        [
            "01000000-0000-0000-0000-000000000000",
            "00000000-0000-0000-0000-000000000001",
            "c2be433e-f36b-1410-8973-006f4f934fe1",
            "7c5f433e-f36b-1410-8975-006f4f934fe1",
            "88a8433e-f36b-1410-8976-006f4f934fe1",
            "019fb3f1-440e-7c82-aad5-1ed216f88053",
        ];
        var land = NewLand();
        foreach (var id in sqlOrder.Reverse())
        {
            var title = LandTitle.Create(land.Id, id, "DEED");
            title.Id = Guid.Parse(id);
            land.AddTitle(title);
            title.SetSequenceNumber(0); // as loaded from a row written before SequenceNumber existed
        }

        Assert.Equal(sqlOrder, land.Titles.Select(t => t.Id.ToString()));
    }
}
