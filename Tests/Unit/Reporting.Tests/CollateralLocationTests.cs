using FluentAssertions;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>
/// ที่ตั้งทรัพย์สิน is composed from the property (<see cref="AppraisalSummaryCommonLoader.CollateralLocationSql"/>):
/// condo rows use the condo format, land rows the land-building format with no ม. segment, and each
/// summary form reads the first property of its own family.
/// </summary>
public class CollateralLocationTests
{
    private static AppraisalSummaryCommonLoader.CollateralLocationRow Land() => new()
    {
        Kind = "L", HouseNumber = "3/48", Village = "หมู่บ้านสุขสันต์",
        Soi = "-", Street = "ศรีนครินทร์",
        SubDistrict = "หนองบอน", District = "ประเวศ", Province = "กรุงเทพมหานคร"
    };

    private static AppraisalSummaryCommonLoader.CollateralLocationRow Condo() => new()
    {
        Kind = "U", RoomNumber = "176/120", FloorNumber = "7",
        CondoName = "อาคารชุดเออร์บาโน ราชวิถี", Soi = "ราชวิถี 12", Street = "ราชวิถี",
        SubDistrict = "บางพลัด", District = "บางพลัด", Province = "กรุงเทพมหานคร"
    };

    [Fact]
    public void Condo_uses_the_condo_format()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([Condo()]);

        result.Condo!.Address.Should().Be(
            "ห้องชุดเลขที่ 176/120 ชั้นที่ 7 อาคารชุดเออร์บาโน ราชวิถี ซอย ราชวิถี 12 ถนน ราชวิถี " +
            "ตำบล/แขวง บางพลัด อำเภอ/เขต บางพลัด จังหวัดกรุงเทพมหานคร");
        result.Condo.SubDistrict.Should().Be("บางพลัด");
        result.Land.Should().BeNull();
    }

    [Fact]
    public void Land_uses_the_land_building_format_and_drops_placeholders()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([Land()]);

        result.Land!.Address.Should().Be(
            "เลขที่ 3/48 หมู่บ้านสุขสันต์ ถนน ศรีนครินทร์ " +
            "ตำบล/แขวง หนองบอน อำเภอ/เขต ประเวศ จังหวัดกรุงเทพมหานคร");
    }

    [Fact]
    public void Each_family_keeps_its_own_anchor_and_land_wins_the_shared_slot()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([Condo(), Land()]);

        result.LandOrCondo.Should().Be(result.Land);
        result.Land!.SubDistrict.Should().Be("หนองบอน");
        result.Condo!.SubDistrict.Should().Be("บางพลัด");
    }

    [Fact]
    public void An_empty_anchor_never_hides_a_usable_one()
    {
        var empty = new AppraisalSummaryCommonLoader.CollateralLocationRow { Kind = "L" };

        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([empty, Condo()]);

        result.Land.Should().BeNull();
        result.LandOrCondo.Should().Be(result.Condo);
    }

    [Fact]
    public void Condo_drops_an_unstated_floor_and_dash_placeholders()
    {
        var row = new AppraisalSummaryCommonLoader.CollateralLocationRow
        {
            Kind = "U", RoomNumber = "206", FloorNumber = "0", CondoName = "–", Soi = "—", Street = "ราชวิถี"
        };

        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([row]);

        result.Condo!.Address.Should().Be("ห้องชุดเลขที่ 206 ถนน ราชวิถี");
    }

    [Theory]
    [InlineData(null), InlineData(""), InlineData(" "), InlineData("-"), InlineData("--"), InlineData(" – "),
     InlineData("—"), InlineData("−"), InlineData("- -"), InlineData("- - -"), InlineData("-  –  -")]
    public void A_blank_or_dash_placeholder_is_no_value(string? value) =>
        Reporting.Application.Formatting.ThaiAddressFormatter.Stated(value).Should().BeNull();

    [Fact]
    public void A_real_value_is_kept_trimmed() =>
        Reporting.Application.Formatting.ThaiAddressFormatter.Stated(" 3/48 ").Should().Be("3/48");

    [Theory]
    [InlineData("0"), InlineData("00"), InlineData("–0"), InlineData("0 -"), InlineData("--"), InlineData(" ")]
    public void An_unstated_floor_is_not_stated(string floor) =>
        Reporting.Application.Formatting.ThaiAddressFormatter.IsStated(floor).Should().BeFalse();

    [Theory]
    [InlineData("7"), InlineData("G"), InlineData("12A")]
    public void A_real_floor_is_stated(string floor) =>
        Reporting.Application.Formatting.ThaiAddressFormatter.IsStated(floor).Should().BeTrue();

    [Fact]
    public void Missing_dopa_leaves_the_locality_blank()
    {
        var row = new AppraisalSummaryCommonLoader.CollateralLocationRow
        {
            Kind = "L", HouseNumber = "3/48", Street = "ศรีนครินทร์"
        };

        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([row]);

        result.Land.Should().Be(new CollateralLocation("เลขที่ 3/48 ถนน ศรีนครินทร์", null));
    }
}
