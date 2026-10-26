using Appraisal.Application.Features.Appraisals.GetAppraisalBrief;

namespace Appraisal.Tests.Application.Features.GetAppraisalBrief;

public class GetAppraisalBriefShapingTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json")]
    [InlineData("{\"a\":1}")]
    [InlineData("[1,2]")]
    [InlineData("null")]
    [InlineData("[]")]
    public void Unreadable_or_empty_code_lists_become_an_empty_list_and_never_throw(string? json)
    {
        Assert.Empty(GetAppraisalBriefQueryHandler.ParseCodes(json));
    }

    [Fact]
    public void Code_lists_keep_their_order_and_drop_blank_entries()
    {
        var codes = GetAppraisalBriefQueryHandler.ParseCodes("[\"01\",null,\" \",\"05\"]");

        Assert.Equal(["01", "05"], codes);
    }

    [Fact]
    public void Towers_hold_real_towers_only_and_towerless_units_are_left_for_UnassignedUnits()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var towers = new List<GetAppraisalBriefQueryHandler.TowerRow>
        {
            new(a, "A", null, null),
            new(b, "B", 12, 99)
        };
        var floors = new List<GetAppraisalBriefQueryHandler.TowerFloorRow>
        {
            new(a, 1, 10, 2), new(a, 2, 10, 0),
            new(b, 1, 5, 1),
            new(null, null, 7, 0), new(null, 3, 3, 1)
        };
        const int projectUnitCount = 35; // what COUNT(*) over ProjectUnits gives for the rows above

        var result = GetAppraisalBriefQueryHandler.BuildTowers(towers, floors);

        Assert.Equal(2, result.Count);
        Assert.All(result, t => Assert.NotNull(t.TowerName));
        Assert.Equal(20, result[0].NumberOfUnits);   // fell back to the uploaded units
        Assert.Equal(12, result[1].NumberOfFloors);  // recorded height passes through
        Assert.Equal(99, result[1].NumberOfUnits);   // the tower's own figure wins
        var unassigned = GetAppraisalBriefQueryHandler.SumUnassigned(towers, floors);
        Assert.Equal((10, 1), unassigned);
        Assert.Equal(projectUnitCount, result.Sum(t => t.Floors.Sum(f => f.Units)) + unassigned.Units);
    }

    [Fact]
    public void Units_on_a_deleted_tower_count_as_unassigned_so_the_totals_still_add_up()
    {
        var live = Guid.NewGuid();
        var deleted = Guid.NewGuid();   // a ProjectTowerId that no tower row carries any more
        var towers = new List<GetAppraisalBriefQueryHandler.TowerRow> { new(live, "A", null, null) };
        var floors = new List<GetAppraisalBriefQueryHandler.TowerFloorRow>
        {
            new(live, 1, 10, 2),
            new(deleted, 1, 4, 1),
            new(null, null, 3, 0)
        };

        var unassigned = GetAppraisalBriefQueryHandler.SumUnassigned(towers, floors);
        var inTowers = GetAppraisalBriefQueryHandler.BuildTowers(towers, floors).Sum(t => t.Floors.Sum(f => f.Units));

        Assert.Equal((7, 1), unassigned);                 // the deleted tower's 4 + the towerless 3
        Assert.Equal(17, inTowers + unassigned.Units);    // every uploaded unit is somewhere
    }

    [Fact]
    public void A_stored_zero_tower_count_does_not_block_the_uploaded_units_fallback()
    {
        var a = Guid.NewGuid();

        var tower = Assert.Single(GetAppraisalBriefQueryHandler.BuildTowers(
            [new(a, "A", 0, 0)], [new(a, 1, 6, 0), new(a, 2, 4, 0)]));

        Assert.Null(tower.NumberOfFloors);   // 0 = not recorded
        Assert.Equal(10, tower.NumberOfUnits);
    }

    [Fact]
    public void A_project_with_no_towers_reports_none()
    {
        var floors = new List<GetAppraisalBriefQueryHandler.TowerFloorRow> { new(null, null, 40, 5) };

        Assert.Empty(GetAppraisalBriefQueryHandler.BuildTowers([], floors));
    }

    private static readonly Dictionary<string, string?> Labels = new() { ["01"] = "บ้านเดี่ยว", ["99"] = "อื่นๆ" };

    [Fact]
    public void A_house_model_row_parses_its_json_lists_and_never_returns_null_lists()
    {
        var id = Guid.NewGuid();

        var model = new GetAppraisalBriefQueryHandler.HouseModelRow(
            id, "Monaco", "01", null, 2m, 50m, 120m, "[\"01\",\"02\"]", "garbage", 3, 1, null, null, null, null).ToModel(Labels);

        Assert.Equal(id, model.Id);
        Assert.Equal(["01", "02"], model.RoofType);
        Assert.Empty(model.FenceType);
        Assert.Equal((3, 1), (model.Units, model.SoldUnits));
    }

    [Fact]
    public void A_project_land_row_parses_land_use_and_keeps_the_legal_flags()
    {
        var land = new GetAppraisalBriefQueryHandler.ProjectLandRow(
            null, 20m, 8m, "01", "[\"01\"]", false, null, true, null, false, "สวน", "ลูกรัง").ToLand();

        Assert.Equal(["01"], land.LandUseType);
        Assert.Null(land.LandShapeType);
        Assert.True(land.IsInExpropriationLine);
        Assert.Equal(20m, land.RoadFrontage);
        Assert.Equal(("สวน", "ลูกรัง"), (land.LandUseTypeOther, land.RoadSurfaceTypeOther));
    }

    [Fact]
    public void An_unrecorded_tower_height_stays_null_even_when_units_sit_on_high_floors()
    {
        var a = Guid.NewGuid();

        var tower = Assert.Single(GetAppraisalBriefQueryHandler.BuildTowers(
            [new(a, "A", null, null)], [new(a, 1205, 1, 0), new(a, 3, 4, 0)]));

        Assert.Null(tower.NumberOfFloors);   // a typo'd floor must not become the tower's height
        Assert.Equal(5, tower.NumberOfUnits);
    }

    // ── appraisal summary raw fields ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(-1.0, null)]   // a negative count is a data error, not a fact
    [InlineData(3.0, 3.0)]
    [InlineData(2.5, 2.5)]
    public void A_count_or_area_is_only_sent_when_positive(double? input, double? expected)
    {
        var result = GetAppraisalBriefQueryHandler.PositiveOrNull<decimal>(input is null ? null : (decimal)input);

        Assert.Equal(expected is null ? null : (decimal)expected, result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0, null)]
    [InlineData(-2, null)]
    [InlineData(7, 7)]
    public void The_same_rule_holds_for_whole_numbers_such_as_age_and_house_count(int? input, int? expected)
    {
        Assert.Equal(expected, GetAppraisalBriefQueryHandler.PositiveOrNull<int>(input));
    }

    [Theory]
    // code 99 (other): the appraiser's remark IS the answer, not the label "อื่นๆ"
    [InlineData("99", "ถังน้ำ", "อื่นๆ", "ถังน้ำ")]
    // code 99 with no remark falls back to the label
    [InlineData("99", null, "อื่นๆ", "อื่นๆ")]
    [InlineData("99", "", "อื่นๆ", "อื่นๆ")]
    // SQL Server compares padded, so NULLIF('  ', '') is NULL in the report: a spaces-only remark is empty
    // and the label wins (verified on the dev database)
    [InlineData("99", " ", "อื่นๆ", "อื่นๆ")]
    [InlineData("99", "   ", "อื่นๆ", "อื่นๆ")]
    // padding is spaces only: a tab is a character, so the remark is picked and then reads as blank
    [InlineData("99", "\t", "อื่นๆ", null)]
    // ordinary code: the label
    [InlineData("01", null, "บ้านเดี่ยว", "บ้านเดี่ยว")]
    // a remark on a non-99 code never overrides the label
    [InlineData("01", "ignored", "บ้านเดี่ยว", "บ้านเดี่ยว")]
    // no master row: remark, then the raw code
    [InlineData("77", "remark", null, "remark")]
    [InlineData("77", null, null, "77")]
    // nothing at all
    [InlineData(null, null, null, null)]
    [InlineData("", "", null, null)]
    public void A_code_is_shown_the_way_the_summary_report_shows_it(
        string? code, string? other, string? label, string? expected)
    {
        Assert.Equal(expected, GetAppraisalBriefQueryHandler.CodeOrOtherDisplay(code, other, label));
    }

    [Fact]
    public void A_building_names_itself_by_its_type_and_only_then_by_its_own_name()
    {
        // The type wins, including the code-99 remark.
        Assert.Equal("ถังน้ำ",
            GetAppraisalBriefQueryHandler.BuildingDisplayName("99", "ถังน้ำ", "อื่นๆ", "พัฒนาไปอี้ก"));
        // No type at all: the property's own name.
        Assert.Equal("พัฒนาไปอี้ก",
            GetAppraisalBriefQueryHandler.BuildingDisplayName(null, null, null, " พัฒนาไปอี้ก "));
        // Nothing recorded.
        Assert.Null(GetAppraisalBriefQueryHandler.BuildingDisplayName("", null, null, " "));
        // A spaces-only remark on code 99 is empty to SQL Server, so the report prints the label.
        Assert.Equal("อื่นๆ",
            GetAppraisalBriefQueryHandler.BuildingDisplayName("99", "  ", "อื่นๆ", "พัฒนาไปอี้ก"));
        // A remark that is whitespace but not spaces (a tab) is picked and reads as blank: the property's name.
        Assert.Equal("พัฒนาไปอี้ก",
            GetAppraisalBriefQueryHandler.BuildingDisplayName("99", "\t", "อื่นๆ", "พัฒนาไปอี้ก"));
        // An unknown code with a blank remark and no label: the report's COALESCE yields the blank
        // remark, which falls back to the property's name.
        Assert.Equal("พัฒนาไปอี้ก",
            GetAppraisalBriefQueryHandler.BuildingDisplayName("77", "  ", null, "พัฒนาไปอี้ก"));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(100.0, null)]   // finished: reads as an ordinary building
    [InlineData(112.5, null)] // a sum nothing bounds can exceed 100
    [InlineData(0.0, 0.0)]        // not started is a statement, not an omission
    [InlineData(-3.0, 0.0)]
    [InlineData(47.07, 47.07)]
    public void Construction_progress_is_only_sent_while_work_is_under_way(double? input, double? expected)
    {
        var result = GetAppraisalBriefQueryHandler.ProgressPercent(input is null ? null : (decimal)input);

        Assert.Equal(expected is null ? null : (decimal)expected, result);
    }

    private static List<BriefMachine> Build(
        IEnumerable<GetAppraisalBriefQueryHandler.MachineRow> rows, bool priceVisible = true) =>
        GetAppraisalBriefQueryHandler.BuildMachines(rows, priceVisible);

    private static GetAppraisalBriefQueryHandler.MachineRow Machine(
        string name, bool registered, string? installation = null, int? group = 1,
        string? brand = null, bool certified = true, string? conditionUse = null) =>
        new(Guid.NewGuid(), group, name, brand, null, null, null, null, null, conditionUse,
            certified, registered, installation);

    [Fact]
    public void Machines_come_out_per_group_then_registered_then_not_registered_then_not_installed()
    {
        // Arrives in the order the SQL returns: group, then the appraiser's sequence.
        var rows = new[]
        {
            Machine("g1-unreg", registered: false),
            Machine("g1-notinst", registered: false, installation: "2"),
            Machine("g1-reg-a", registered: true),
            Machine("g1-reg-b", registered: true),
            Machine("g2-notinst", registered: true, installation: "2"),   // registered, but still under procurement
            Machine("g2-reg", registered: true, group: 2),
            Machine("loose-reg", registered: true, group: null),
            Machine("loose-unreg", registered: false, group: null)
        };
        // g2-notinst above is group 1 by default; give it group 2 explicitly so the test reads as written.
        rows[4] = rows[4] with { GroupNumber = 2 };

        var result = Build(rows);

        Assert.Equal(
            ["g1-reg-a", "g1-reg-b", "g1-unreg", "g1-notinst", "g2-reg", "g2-notinst", "loose-reg", "loose-unreg"],
            result.Select(m => m.MachineName));
        Assert.Equal(
            ["registered", "registered", "notRegistered", "notInstalled", "registered", "notInstalled",
                "registered", "notRegistered"],
            result.Select(m => m.Section));
    }

    [Fact]
    public void A_machine_still_under_procurement_is_not_installed_whatever_its_registration()
    {
        var registered = Machine("a", registered: true, installation: "2");
        var unregistered = Machine("b", registered: false, installation: "2");

        Assert.Equal("notInstalled", registered.Section);
        Assert.Equal("notInstalled", unregistered.Section);
    }

    [Fact]
    public void A_machine_the_report_cannot_describe_is_dropped_but_a_remark_alone_is_enough_to_keep_it()
    {
        var blank = Machine(null!, registered: true, certified: true);
        var notCertified = Machine(null!, registered: true, certified: false);
        var notFound = Machine(null!, registered: true, certified: true, conditionUse: "03");
        var named = Machine("Crane", registered: true);

        var result = Build([blank, notCertified, notFound, named]);

        Assert.Equal(3, result.Count);
        Assert.DoesNotContain(result, m => m.AssetId == blank.AssetId);
    }

    [Fact]
    public void IsPriceCertified_is_withheld_with_the_figures_and_does_not_leak_through_the_list()
    {
        var onlyUncertified = Machine(null!, registered: true, certified: false);
        var named = Machine("Crane", registered: true, certified: false);

        var released = Build([onlyUncertified, named]);
        var locked = Build([onlyUncertified, named], priceVisible: false);

        Assert.All(released, m => Assert.False(m.IsPriceCertified));
        // Locked: no flag at all, and the machine whose ONLY content was "not certified" is not listed,
        // so its presence cannot reveal the flag either.
        var only = Assert.Single(locked);
        Assert.Equal("Crane", only.MachineName);
        Assert.Null(only.IsPriceCertified);
    }

    [Fact]
    public void Ungrouped_machines_are_kept_and_come_after_every_group()
    {
        var result = Build([Machine("loose", registered: true, group: null), Machine("g3", registered: false, group: 3)]);

        Assert.Equal(["g3", "loose"], result.Select(m => m.MachineName));
    }

    [Fact]
    public void A_dash_only_adjacent_area_is_absent_and_a_side_with_nothing_is_dropped()
    {
        var result = GetAppraisalBriefQueryHandler.BuildBoundaries(
        [
            ("north", "-", 0m),            // placeholder + the 0.00 default: nothing
            ("south", " – ", 12m),         // placeholder text, real distance: keeps the distance only
            ("east", "ถนน", null),
            ("west", null, 3.5m)
        ]);

        Assert.Equal(["south", "east", "west"], result.Select(b => b.Direction));
        Assert.Null(result[0].AdjacentArea);
        Assert.Equal(12m, result[0].Distance);
        Assert.Equal("ถนน", result[1].AdjacentArea);
    }

    [Theory]
    [InlineData("DEED", "โฉนดที่ดิน", "โฉนดที่ดินเลขที่")]
    [InlineData("deed", "โฉนดที่ดิน", "โฉนดที่ดินเลขที่")]
    [InlineData("NS3", "น.ส.3", "น.ส.3 เลขที่")]
    [InlineData("NS3K", "น.ส.3 ก.", "น.ส.3 ก. เลขที่")]
    [InlineData("POSR", "ตราจอง", "ตราจองเลขที่")]
    // OTHER, junk and blank all read as the generic document, never as a title deed
    [InlineData("OTHER", "เอกสารสิทธิ์", "เอกสารสิทธิ์เลขที่")]
    [InlineData("-", "เอกสารสิทธิ์", "เอกสารสิทธิ์เลขที่")]
    [InlineData(null, "เอกสารสิทธิ์", "เอกสารสิทธิ์เลขที่")]
    public void A_title_is_named_by_its_own_document_type(string? code, string noun, string prefix)
    {
        Assert.Equal((noun, prefix), GetAppraisalBriefQueryHandler.TitleLabel(code));
    }

    [Fact]
    public void Utility_codes_become_labels_and_an_unmapped_code_stays_as_itself()
    {
        var labels = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["01"] = "ไฟฟ้า", ["02"] = null, ["03"] = " "
        };

        Assert.Equal(["ไฟฟ้า", "02", "03", "99"],
            GetAppraisalBriefQueryHandler.ToLabels(
                GetAppraisalBriefQueryHandler.ParseCodes("[\"01\",\"02\",\"03\",\"99\"]"), labels));
        Assert.Empty(GetAppraisalBriefQueryHandler.ToLabels(GetAppraisalBriefQueryHandler.ParseCodes("garbage"), labels));
    }

    [Fact]
    public void A_house_model_omits_zero_floors_houses_and_area_and_names_its_building_type()
    {
        var zeros = new GetAppraisalBriefQueryHandler.HouseModelRow(
            Guid.NewGuid(), "A", "99", "ทาวน์โฮม", 0m, 50m, 0m, null, null, 0, 0, 0, "คอนกรีต", "ไม้", 0m)
            .ToModel(Labels);
        var real = new GetAppraisalBriefQueryHandler.HouseModelRow(
            Guid.NewGuid(), "B", "01", null, 2m, 50m, 120.5m, null, null, 4, 1, 3, null, null, 95.5m)
            .ToModel(Labels);

        Assert.Null(zeros.NumberOfFloors);
        Assert.Null(zeros.StandardUsableArea);
        Assert.Null(zeros.NumberOfHouse);
        Assert.Null(zeros.UsableAreaMin);                   // 0 = not entered
        Assert.Equal("ทาวน์โฮม", zeros.BuildingTypeDisplay);   // code 99: the remark
        Assert.Equal(("คอนกรีต", "ไม้"), (zeros.RoofTypeOther, zeros.FenceTypeOther));
        Assert.Equal((2m, 120.5m, 3), (real.NumberOfFloors, real.StandardUsableArea, real.NumberOfHouse));
        Assert.Equal(95.5m, real.UsableAreaMin);
        Assert.Equal("บ้านเดี่ยว", real.BuildingTypeDisplay);
    }

    [Fact]
    public void A_project_location_uses_the_master_that_resolves_the_most_codes_and_dopa_on_a_tie()
    {
        var none = ((string?)null, (string?)null, (string?)null);

        // Sub-district blank, district and province valid: a name instead of nothing.
        Assert.Equal((null, "ประเวศ", "กรุงเทพมหานคร"),
            GetAppraisalBriefQueryHandler.PickLocationNames((null, "ประเวศ", "กรุงเทพมหานคร"), none));
        // Both masters know the sub-district: DOPA wins, and the Title names are not mixed in.
        Assert.Equal(("หนองบอน", "ประเวศ", "กรุงเทพมหานคร"),
            GetAppraisalBriefQueryHandler.PickLocationNames(
                ("หนองบอน", "ประเวศ", "กรุงเทพมหานคร"), ("หนองบอน", "พระโขนง", "กรุงเทพ")));
        // A legacy project: only the Title masters know it.
        Assert.Equal(("หนองบอน", "พระโขนง", "กรุงเทพ"),
            GetAppraisalBriefQueryHandler.PickLocationNames(none, ("หนองบอน", "พระโขนง", "กรุงเทพ")));
        // The master that resolves MORE of the codes wins; a tie goes to DOPA.
        Assert.Equal((null, "ประเวศ", null),
            GetAppraisalBriefQueryHandler.PickLocationNames((null, "ประเวศ", null), ("หนองบอน", null, null)));
        // A legacy project whose district code only the Title masters know gets all three Title names,
        // even though DOPA happens to know its province.
        Assert.Equal(("หนองบอน", "พระโขนง", "กรุงเทพ"),
            GetAppraisalBriefQueryHandler.PickLocationNames(
                (null, null, "กรุงเทพมหานคร"), ("หนองบอน", "พระโขนง", "กรุงเทพ")));
        // District only, known to both: DOPA first.
        Assert.Equal((null, "ประเวศ", null),
            GetAppraisalBriefQueryHandler.PickLocationNames((null, "ประเวศ", null), (null, "พระโขนง", null)));
        // Nothing resolves anywhere: all null, never a code.
        Assert.Equal(none, GetAppraisalBriefQueryHandler.PickLocationNames(none, none));
        // Province only, Title.
        Assert.Equal((null, null, "กรุงเทพ"),
            GetAppraisalBriefQueryHandler.PickLocationNames(none, (null, null, "กรุงเทพ")));
    }

    [Theory]
    [InlineData(true, 40.0, 40.0)]     // far from electricity: the distance is real
    [InlineData(true, 0.0, null)]      // far, but no distance entered
    [InlineData(false, 40.0, null)]    // not far: a leftover distance is stale
    [InlineData(null, 40.0, null)]
    [InlineData(true, null, null)]
    public void An_electricity_distance_is_only_sent_for_a_parcel_marked_far(bool? far, double? distance, double? expected)
    {
        var result = GetAppraisalBriefQueryHandler.DistanceIfFar(far, distance is null ? null : (decimal)distance);

        Assert.Equal(expected is null ? null : (decimal)expected, result);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("\t\n", null)]       // whitespace that is not padding survives the SQL compare, then trims away
    [InlineData(" ถังน้ำ ", "ถังน้ำ")]
    public void A_remark_that_trims_to_nothing_is_null_not_an_empty_string(string? input, string? expected)
    {
        Assert.Equal(expected, GetAppraisalBriefQueryHandler.TrimmedOrNull(input));
    }

    [Theory]
    [InlineData("  ", null)]            // spaces only: empty to SQL Server
    [InlineData("\t", null)]           // trims to nothing
    [InlineData(" ทาวน์โฮม ", "ทาวน์โฮม")]
    public void A_house_models_other_text_is_cleaned_like_an_assets(string other, string? expected)
    {
        var model = new GetAppraisalBriefQueryHandler.HouseModelRow(
            Guid.NewGuid(), "A", "99", other, 2m, 50m, 100m, null, null, 0, 0, 1, null, null, null).ToModel(Labels);

        Assert.Equal(expected, model.BuildingTypeOther);
    }

    [Fact]
    public void A_machines_serial_number_is_sent_as_SerialNumber()
    {
        var row = new GetAppraisalBriefQueryHandler.MachineRow(
            Guid.NewGuid(), 1, "Crane", null, null, null, "SN-1", null, null, null, true, true, null);

        Assert.Equal("SN-1", row.ToMachine("registered", priceVisible: true).SerialNumber);
    }
}
