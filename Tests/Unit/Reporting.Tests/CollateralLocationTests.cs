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

    private static AppraisalSummaryCommonLoader.CollateralLocationRow Unit(string room, string floor, string building = "อาคารชุดโนเบิล", string? tower = null) => new()
    {
        Kind = "U", RoomNumber = room, FloorNumber = floor, CondoName = building, BuildingNumber = tower,
        SubDistrict = "บางพลัด", District = "บางพลัด", Province = "กรุงเทพมหานคร"
    };

    private static AppraisalSummaryCommonLoader.CollateralLocationRow House(string? number) => new()
    {
        Kind = "L", HouseNumber = number, Village = "หมู่บ้านสุขสันต์",
        SubDistrict = "หนองบอน", District = "ประเวศ", Province = "กรุงเทพมหานคร"
    };

    private const string CondoTail = "อาคารชุดโนเบิล ตำบล/แขวง บางพลัด อำเภอ/เขต บางพลัด จังหวัดกรุงเทพมหานคร";
    private const string LandTail = "หมู่บ้านสุขสันต์ ตำบล/แขวง หนองบอน อำเภอ/เขต ประเวศ จังหวัดกรุงเทพมหานคร";

    [Fact]
    public void Condo_units_on_the_same_floor_list_rooms_once_with_one_tail()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("312/429", "31"), Unit("312/430", "31")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 312/429, 312/430 ชั้นที่ 31 {CondoTail}");
        result.Condo.SubDistrict.Should().Be("บางพลัด");
    }

    [Fact]
    public void Condo_units_on_different_floors_each_carry_their_floor()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("312/429", "31"), Unit("315/12", "35")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 312/429 ชั้นที่ 31, 315/12 ชั้นที่ 35 {CondoTail}");
    }

    [Fact]
    public void Condo_floor_not_stated_is_left_off_a_unit()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("312/429", "31"), Unit("315/12", "0")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 312/429 ชั้นที่ 31, 315/12 {CondoTail}");
    }

    [Fact]
    public void Condo_units_in_different_buildings_print_each_full_address()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("312/429", "31"), Unit("9", "2", building: "อาคารชุดเอ")]);

        result.Condo!.Address.Should().Be(
            $"ห้องชุดเลขที่ 312/429 ชั้นที่ 31 {CondoTail}, " +
            "ห้องชุดเลขที่ 9 ชั้นที่ 2 อาคารชุดเอ ตำบล/แขวง บางพลัด อำเภอ/เขต บางพลัด จังหวัดกรุงเทพมหานคร");
    }

    [Fact]
    public void Land_units_list_their_house_numbers_once_with_one_tail()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([House("99/1"), House("99/2")]);

        result.Land!.Address.Should().Be($"เลขที่ 99/1, 99/2 {LandTail}");
        result.Land.SubDistrict.Should().Be("หนองบอน");
    }

    [Fact]
    public void Land_blank_house_numbers_are_skipped_and_duplicates_collapse()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [House("99/1"), House(null), House("-"), House("99/1")]);

        result.Land!.Address.Should().Be($"เลขที่ 99/1 {LandTail}");
    }

    [Fact]
    public void Land_units_all_without_a_number_print_just_the_tail()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([House(null), House("")]);

        result.Land!.Address.Should().Be(LandTail);
    }

    [Fact]
    public void Land_plots_group_by_tail_so_one_odd_plot_does_not_repeat_the_rest()
    {
        var other = new AppraisalSummaryCommonLoader.CollateralLocationRow
        {
            Kind = "L", HouseNumber = "99/3", Village = "หมู่บ้านอื่น",
            SubDistrict = "หนองบอน", District = "ประเวศ", Province = "กรุงเทพมหานคร"
        };

        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([House("99/1"), House("99/2"), other]);

        result.Land!.Address.Should().Be(
            $"เลขที่ 99/1, 99/2 {LandTail}, " +
            "เลขที่ 99/3 หมู่บ้านอื่น ตำบล/แขวง หนองบอน อำเภอ/เขต ประเวศ จังหวัดกรุงเทพมหานคร");
    }

    [Fact]
    public void Identical_bare_plots_collapse_next_to_a_different_one()
    {
        var other = new AppraisalSummaryCommonLoader.CollateralLocationRow
        {
            Kind = "L", HouseNumber = "5", Village = "หมู่บ้านอื่น", Province = "กรุงเทพมหานคร"
        };

        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations([House(null), House(null), other]);

        result.Land!.Address.Should().Be(
            $"{LandTail}, เลขที่ 5 หมู่บ้านอื่น จังหวัดกรุงเทพมหานคร");
    }

    [Fact]
    public void Condo_units_without_room_numbers_list_their_floors_after_the_room_label()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("-", "1"), Unit("-", "13"), Unit("-", "0")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ ชั้นที่ 1, ชั้นที่ 13 {CondoTail}");
    }

    [Fact]
    public void Condo_unit_without_a_room_number_still_contributes_its_floor_next_to_a_named_one()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("312/429", "31"), Unit("-", "13")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 312/429 ชั้นที่ 31, ชั้นที่ 13 {CondoTail}");
    }

    [Fact]
    public void Same_room_in_different_towers_prints_twice_but_the_same_tower_dedupes()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("101", "1", tower: "A"), Unit("101", "1", tower: "B"), Unit("101", "1", tower: "A")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 101, 101 ชั้นที่ 1 {CondoTail}");
    }

    [Fact]
    public void One_land_expanded_into_one_row_per_house_lists_every_house_number()
    {
        // The SQL yields a row per house for a land property: own 99/1, B 99/2, B with no number.
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [House("99/1"), House("99/2"), House(null)]);

        result.Land!.Address.Should().Be($"เลขที่ 99/1, 99/2 {LandTail}");
    }

    [Fact]
    public void Roomless_units_in_different_towers_print_their_floor_once()
    {
        var result = AppraisalSummaryCommonLoader.ComposeCollateralLocations(
            [Unit("101", "1", tower: "A"), Unit("-", "13", tower: "A"), Unit("-", "13", tower: "B")]);

        result.Condo!.Address.Should().Be($"ห้องชุดเลขที่ 101 ชั้นที่ 1, ชั้นที่ 13 {CondoTail}");
    }
}
