using FluentAssertions;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>The title pieces of a รายการทรัพย์สิน land sentence: no aerial-photo map or แผ่นที่ any more, and no survey page for a น.ส.3.</summary>
public class TitleIdentityPartsTests
{
    private static AppraisalSummaryLandBuildingDataProvider.GroupTitleRow Title(string type) => new()
    {
        TitleType = type, TitleNumber = "12346", BookNumber = "1", PageNumber = "1",
        LandParcelNumber = "1", SurveyNumber = "1", Rawang = "5137 IV"
    };

    [Fact]
    public void Ns3k_prints_neither_an_aerial_map_nor_a_sheet_nor_the_stale_survey_page()
    {
        var parts = AppraisalSummaryLandBuildingDataProvider.TitleIdentityParts(Title("NS3K"));

        parts.Should().Equal("น.ส.3 ก. เลขที่ 12346", "เล่ม 1", "หน้า 1", "เลขที่ดิน 1", "ระวาง 5137 IV");
        parts.Should().NotContain(p => p.Contains("ระวางรูปถ่ายทางอากาศ") || p.Contains("แผ่นที่") || p.StartsWith("หมายเลข"));
    }

    [Fact]
    public void Deed_still_prints_the_survey_page_and_its_rawang()
    {
        AppraisalSummaryLandBuildingDataProvider.TitleIdentityParts(Title("DEED")).Should().Contain("หน้าสำรวจ 1").And.Contain("ระวาง 5137 IV");
    }
}
