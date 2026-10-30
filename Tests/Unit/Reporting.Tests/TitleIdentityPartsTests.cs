using FluentAssertions;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>The title pieces of a รายการทรัพย์สิน land sentence: น.ส.3 ก. prints its aerial map, not a survey page.</summary>
public class TitleIdentityPartsTests
{
    private static AppraisalSummaryLandBuildingDataProvider.GroupTitleRow Title(string type) => new()
    {
        TitleType = type, TitleNumber = "12346", BookNumber = "1", PageNumber = "1",
        LandParcelNumber = "1", SurveyNumber = "1",
        MapSheetNumber = "9", AerialMapName = "กรุงเทพมหานครฯ", AerialMapNumber = "9"
    };

    [Fact]
    public void Ns3k_prints_aerial_map_and_sheet_but_not_the_stale_survey_page()
    {
        AppraisalSummaryLandBuildingDataProvider.TitleIdentityParts(Title("NS3K")).Should().Equal(
            "น.ส.3 ก. เลขที่ 12346", "เล่ม 1", "หน้า 1", "เลขที่ดิน 1",
            "ระวางรูปถ่ายทางอากาศชื่อ กรุงเทพมหานครฯ", "หมายเลข 9", "แผ่นที่ 9");
    }

    [Fact]
    public void Deed_still_prints_the_survey_page()
    {
        AppraisalSummaryLandBuildingDataProvider.TitleIdentityParts(Title("DEED")).Should().Contain("หน้าสำรวจ 1");
    }

    [Fact]
    public void A_blank_aerial_piece_is_skipped_and_the_others_still_print()
    {
        var parts = AppraisalSummaryLandBuildingDataProvider.TitleIdentityParts(
            new() { TitleType = "NS3K", AerialMapName = " ", AerialMapNumber = "9" });

        parts.Should().Equal("หมายเลข 9");
    }
}
