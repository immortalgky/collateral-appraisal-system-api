using System.Data;
using FluentAssertions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The external letter's owner rows and อาคารสิ่งปลูกสร้าง row cover every deed / building / room, not the first.</summary>
public class BookLetterOwnerRowsTests
{
    private static LandSection Land(params (string Number, string? Owner)[] deeds) =>
        new() { Titles = deeds.Select(d => new LandTitleRow { TitleNumber = d.Number, TitleTypeDisplay = "โฉนดที่ดิน", OwnerName = d.Owner }).ToList() };

    private static BuildingSection Bld(int n, string? owner = "นายเอ", bool? verified = null, string? type = "บ้านเดี่ยว", decimal? floors = 3m, string? model = "A1",
        bool? appraisable = null, bool dev = false) =>
        new() { BuildingNumber = n, OwnerName = owner, IsOwnerVerified = verified, BuildingTypeText = type, NumberOfFloors = floors, ModelName = model, IsAppraisable = appraisable, IsDevelopmentOnly = dev };

    private static AppraisalSummaryModel Model(IEnumerable<LandSection>? lands = null, IEnumerable<BuildingSection>? buildings = null, string body = "standard") =>
        new() { BodyType = body, PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = (lands ?? []).ToList(), Buildings = (buildings ?? []).ToList() }] };

    // ── land ────────────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_same_owner_on_every_deed_prints_the_name_alone()
    {
        var m = Model([Land(("1", "นายเอ"), ("2", " นายเอ "), ("3", "นายเอ"))]);

        LetterOwnerRows.LandOwners(m).Should().Be("นายเอ");
    }

    [Fact]
    public void Different_land_owners_up_to_ten_deeds_are_paired_with_their_deed()
    {
        var m = Model([Land(("12345", "นายเอ"), ("12346", "นายบี")), Land(("12347", "นายเอ"))]);

        LetterOwnerRows.LandOwners(m).Should().Be("โฉนดที่ดิน 12345: นายเอ\nโฉนดที่ดิน 12346: นายบี\nโฉนดที่ดิน 12347: นายเอ");
    }

    [Theory]
    [InlineData("standard", "2.1")]
    [InlineData("construction", "2")]
    public void Different_land_owners_above_ten_deeds_are_counted_per_owner_with_the_section(string body, string section)
    {
        var deeds = Enumerable.Range(1, 11).Select(i => (i.ToString(), (string?)(i <= 8 ? "นายเอ" : "นายบี"))).ToArray();

        LetterOwnerRows.LandOwners(Model([Land(deeds)], body: body)).Should().Be($"นายเอ จำนวน 8 ฉบับ\nนายบี จำนวน 3 ฉบับ (รายละเอียดตามข้อ {section})");
    }

    [Fact]
    public void Exactly_ten_deeds_with_two_owners_are_still_paired()
    {
        var deeds = Enumerable.Range(1, 10).Select(i => (i.ToString(), (string?)(i == 1 ? "นายเอ" : "นายบี"))).ToArray();

        LetterOwnerRows.LandOwners(Model([Land(deeds)])).Should().StartWith("โฉนดที่ดิน 1: นายเอ\nโฉนดที่ดิน 2: นายบี").And.NotContain("รายละเอียดตามข้อ");
    }

    [Fact]
    public void A_deed_with_no_owner_is_left_out_a_deed_with_no_number_is_listed_as_such_and_no_owner_at_all_prints_nothing()
    {
        LetterOwnerRows.LandOwners(Model([Land(("1", "นายเอ"), ("2", null), ("3", "-"), ("", "นายบี"))])).Should().Be("โฉนดที่ดิน 1: นายเอ\nที่ดินแปลงที่ 1 (ไม่ระบุเลขที่): นายบี");   // r3: its owner is not dropped, and the label says why it has no number
        LetterOwnerRows.LandOwners(Model([Land(("1", null))])).Should().BeNull();
        LetterOwnerRows.LandOwners(Model()).Should().BeNull();
    }

    // ── buildings ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Building_owners_pair_with_the_books_building_number_and_skip_what_the_book_does_not_count()
    {
        var m = Model(buildings: [Bld(1, "นายเอ"), Bld(2, "นายบี"), Bld(0, "นายซี", appraisable: false), Bld(0, "นายดี", dev: true)]);

        LetterOwnerRows.BuildingOwners(m).Should().Be("หลังที่ 1: นายเอ\nหลังที่ 2: นายบี");
    }

    [Fact]
    public void An_unverified_building_owner_keeps_the_letters_wording()
    {
        var one = Model(buildings: [Bld(1, "นายเอ", verified: false)]);
        var two = Model(buildings: [Bld(1, "นายเอ", verified: false), Bld(2, "นายบี", verified: true)]);

        LetterOwnerRows.BuildingOwners(one).Should().Be("ไม่สามารถตรวจสอบได้");
        LetterOwnerRows.BuildingOwners(two).Should().Be("หลังที่ 1: ไม่สามารถตรวจสอบได้\nหลังที่ 2: นายบี");
    }

    [Fact]
    public void Different_building_owners_above_ten_are_counted_per_owner_and_point_at_the_building_tables()
    {
        var m = Model([Land(("1", "x"))], Enumerable.Range(1, 12).Select(i => Bld(i, i <= 5 ? "นายเอ" : "นายบี")));

        LetterOwnerRows.BuildingOwners(m).Should().Be("นายเอ จำนวน 5 หลัง\nนายบี จำนวน 7 หลัง (รายละเอียดตามข้อ 3.2)");   // 3.1 is the land, 3.2 the buildings
    }

    [Fact]
    public void The_building_table_sections_follow_the_standard_books_numbering_group_by_group()
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = "standard",
            PropertyGroups =
            [
                new PropertyGroupDetail { GroupNumber = 1, Lands = [Land(("1", "a"))], Buildings = [Bld(1)] },        // 3.1 land, 3.2 buildings
                new PropertyGroupDetail { GroupNumber = 2, Lands = [Land(("2", "a"))], Buildings = [] },               // 3.3 land
                new PropertyGroupDetail { GroupNumber = 3, Lands = [], Buildings = [Bld(2)] }                          // 3.4 buildings
            ]
        };

        LetterOwnerRows.BuildingSections(m).Should().Be("3.2, 3.4");
        LetterOwnerRows.BuildingSections(new AppraisalSummaryModel { BodyType = "construction" }).Should().Be("3");
    }

    [Fact]
    public void Buildings_are_grouped_by_type_floors_and_model_in_first_seen_order()
    {
        var m = Model(buildings: [Bld(1), Bld(2, type: "โรงจอดรถ", floors: 1m, model: null), Bld(3)]);

        LetterOwnerRows.BuildingDetails(m).Should().Be("บ้านเดี่ยว 3 ชั้น แบบ A1 จำนวน 2 หลัง\nโรงจอดรถ 1 ชั้น จำนวน 1 หลัง");
    }

    [Fact]
    public void A_single_building_keeps_the_old_text_without_a_count_and_dashes_are_left_out()
    {
        LetterOwnerRows.BuildingDetails(Model(buildings: [Bld(1)])).Should().Be("บ้านเดี่ยว 3 ชั้น แบบ A1");
        LetterOwnerRows.BuildingDetails(Model(buildings: [Bld(1, model: "-")])).Should().Be("บ้านเดี่ยว 3 ชั้น");
        LetterOwnerRows.BuildingDetails(Model(buildings: [Bld(1, type: "-", floors: null, model: " ")])).Should().BeNull();
        LetterOwnerRows.BuildingDetails(Model(buildings: [Bld(0, appraisable: false), Bld(0, dev: true)])).Should().BeNull();
        LetterOwnerRows.BuildingDetails(Model()).Should().BeNull();
    }

    [Fact]
    public void Several_buildings_without_any_type_floors_or_model_still_say_how_many()
    {
        LetterOwnerRows.BuildingDetails(Model(buildings: [Bld(1, type: null, floors: null, model: null), Bld(2, type: null, floors: null, model: null)])).Should().Be("สิ่งปลูกสร้าง จำนวน 2 หลัง");
    }

    // ── condo (through the builder's own query) ─────────────────────────────────────────────────────────────────────

    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static Task<string?> CondoOwnerAsync(params (string? Room, string? Owner)[] rooms) => CondoOwnerAsync("condo", rooms);

    private static async Task<string?> CondoOwnerAsync(string bodyType, params (string? Room, string? Owner)[] rooms)
    {
        var header = new DataTable();
        header.Columns.Add("AppraisalNumber", typeof(string)); header.Columns.Add("RequestId", typeof(Guid)); header.Columns.Add("CompletedAt", typeof(DateTime));
        header.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        var condo = new DataTable();
        condo.Columns.Add("CondoRoomNumber", typeof(string)); condo.Columns.Add("CondoOwner", typeof(string));
        foreach (var (room, owner) in rooms) condo.Rows.Add((object?)room ?? DBNull.Value, (object?)owner ?? DBNull.Value);
        var ds = new DataSet();
        for (var i = 0; i < 14; i++) ds.Tables.Add(i == 0 ? header : i == 8 ? condo : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var m = await ExternalBookBuilder.BuildAsync(f.GetOpenConnection(), Guid.NewGuid(), new AppraisalSummaryModel(), 70m, CancellationToken.None);
        m.BodyType = bodyType;   // the book provider sets it before it words the letter's owner rows
        return LetterOwnerRows.CondoOwners(m);
    }

    [Fact]
    public async Task Condo_owners_are_one_name_when_alike_and_paired_with_the_room_when_they_differ()
    {
        (await CondoOwnerAsync(("101", "นายเอ"), ("102", "นายเอ"))).Should().Be("นายเอ");
        (await CondoOwnerAsync(("101", "นายเอ"), ("102", "นายบี"))).Should().Be("ห้อง 101: นายเอ\nห้อง 102: นายบี");
        (await CondoOwnerAsync(("101", "นายเอ"), (null, "นายบี"))).Should().Be("ห้อง 101: นายเอ\nห้องที่ 2: นายบี");
    }

    [Fact]
    public async Task Condo_owners_above_ten_rooms_are_counted_per_owner_with_section_2_1()
    {
        var rooms = Enumerable.Range(1, 11).Select(i => ((string?)(100 + i).ToString(), (string?)(i <= 6 ? "นายเอ" : "นายบี"))).ToArray();

        (await CondoOwnerAsync(rooms)).Should().Be("นายเอ จำนวน 6 ห้อง\nนายบี จำนวน 5 ห้อง (รายละเอียดตามข้อ 2.1)");
    }
}
