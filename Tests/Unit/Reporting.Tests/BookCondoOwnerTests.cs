using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The condo owner follows the building rule: IsOwnerVerified == false reads "ไม่สามารถตรวจสอบได้" instead of the typed name; verified or unanswered keeps the name.</summary>
public class BookCondoOwnerTests
{
    private const string NotVerified = "ไม่สามารถตรวจสอบได้";

    [Theory]
    [InlineData("  นายตัวอย่าง ใจดี ", true, "นายตัวอย่าง ใจดี")]
    [InlineData("นายตัวอย่าง", null, "นายตัวอย่าง")]
    [InlineData("นายตัวอย่าง", false, NotVerified)]
    [InlineData(null, false, NotVerified)]
    [InlineData(null, true, "-")]
    [InlineData("   ", null, "-")]
    public void Condo_owner_text_is_the_building_rule(string? name, bool? verified, string expected) =>
        new CondoUnitAreaRow { OwnerName = name, IsOwnerVerified = verified }.OwnerText.Should().Be(expected);

    [Fact]
    public void The_condo_and_the_building_say_the_same_thing_for_an_unverified_owner() =>
        new CondoUnitAreaRow { OwnerName = "สมชาย", IsOwnerVerified = false }.OwnerText
            .Should().Be(new BuildingSection { OwnerName = "สมชาย", IsOwnerVerified = false }.OwnerText)
            .And.Be(AppraisalSummaryCommonLoader.OwnerDisplay("สมชาย", false));

    private static Task<string> RenderAsync(string template, AppraisalSummaryModel m) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync(template, m, CancellationToken.None);

    private static AppraisalSummaryModel Condo(bool? verified) => new()
    {
        BodyType = "condo",
        CondoSection = new CondoSection { OwnerName = "นายตัวอย่าง", IsOwnerVerified = verified, Units = [new CondoUnitAreaRow { RoomNumber = "101", OwnerName = "นายตัวอย่าง", IsOwnerVerified = verified }] }
    };

    [Theory]
    [InlineData(false, NotVerified)]
    [InlineData(true, "นายตัวอย่าง")]
    [InlineData(null, "นายตัวอย่าง")]
    public async Task The_2_1_room_table_owner_cell_follows_the_rule(bool? verified, string shown)
    {
        var html = await RenderAsync("{{ include 'partials/book-flow-condo' }}", Condo(verified));

        html.Should().Contain($"<td>{shown}</td>");
        if (verified == false) html.Should().NotContain("นายตัวอย่าง");
    }

    [Theory]
    [InlineData(false, NotVerified)]
    [InlineData(true, "นายตัวอย่าง")]
    public async Task The_mixed_book_owner_row_follows_the_rule_and_the_separate_verification_checkbox_row_is_gone(bool verified, string shown)
    {
        var m = new AppraisalSummaryModel { BodyType = "standard", CondoSection = new CondoSection { Units = [new CondoUnitAreaRow { RoomNumber = "101", OwnerName = "นายตัวอย่าง", IsOwnerVerified = verified }] } };

        var html = await RenderAsync("{{ include 'partials/book-funcs' }}{{ n3 = 0 }}{{ include 'partials/book-mixed-extras' }}", m);

        html.Should().Contain($"<span class=\"l\">ผู้ถือกรรมสิทธิ์</span><span class=\"c\">:</span><span class=\"v\">{shown}</span>")
            .And.NotContain("ตรวจสอบกรรมสิทธิ์").And.NotContain("ตรวจสอบแล้ว").And.NotContain("ไม่ได้ตรวจสอบ");
    }

    // ── the external letter / summary page: the rule is applied per room before the rooms are grouped ────────────────

    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static Task<string?> CondoOwnerAsync(params (string Room, string? Owner, bool? Verified)[] rooms) => CondoOwnerAsync("condo", rooms);

    private static async Task<string?> CondoOwnerAsync(string bodyType, params (string Room, string? Owner, bool? Verified)[] rooms)
    {
        var header = new DataTable();
        header.Columns.Add("AppraisalNumber", typeof(string)); header.Columns.Add("RequestId", typeof(Guid)); header.Columns.Add("CompletedAt", typeof(DateTime));
        header.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        var condo = new DataTable();
        condo.Columns.Add("CondoRoomNumber", typeof(string)); condo.Columns.Add("CondoOwner", typeof(string)); condo.Columns.Add("CondoOwnerVerified", typeof(bool));
        foreach (var (room, owner, verified) in rooms) condo.Rows.Add(room, (object?)owner ?? DBNull.Value, (object?)verified ?? DBNull.Value);
        var ds = new DataSet();
        for (var i = 0; i < 14; i++) ds.Tables.Add(i == 0 ? header : i == 8 ? condo : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var m = await ExternalBookBuilder.BuildAsync(f.GetOpenConnection(), Guid.NewGuid(), new AppraisalSummaryModel(), 70m, CancellationToken.None);
        m.BodyType = bodyType;   // the book provider sets it before it words the letter's owner rows
        return LetterOwnerRows.CondoOwners(m);
    }

    [Fact]
    public async Task An_unverified_room_reads_the_fixed_text_in_the_letter_row_and_is_paired_with_its_room_when_owners_differ()
    {
        (await CondoOwnerAsync(("101", "นายเอ", false))).Should().Be(NotVerified);
        (await CondoOwnerAsync(("101", "นายเอ", false), ("102", "นายบี", false))).Should().Be(NotVerified);   // the same text: one entry, like any other
        (await CondoOwnerAsync(("101", "นายเอ", false), ("102", "นายบี", true))).Should().Be($"ห้อง 101: {NotVerified}\nห้อง 102: นายบี");
        (await CondoOwnerAsync(("101", "นายเอ", null), ("102", "นายเอ", true))).Should().Be("นายเอ");
    }

    [Fact]
    public async Task Above_ten_rooms_the_unverified_ones_are_counted_like_any_other_owner_text()
    {
        var rooms = Enumerable.Range(1, 12).Select(i => ((100 + i).ToString(), (string?)"นายเอ", (bool?)(i > 7))).ToArray();   // rooms 1-7 unverified, 8-12 verified

        (await CondoOwnerAsync(rooms)).Should().Be($"{NotVerified} จำนวน 7 ห้อง\nนายเอ จำนวน 5 ห้อง (รายละเอียดตามข้อ 2.1)");
    }

    [Fact]
    public async Task The_external_summary_page_shows_the_condo_owner_row_with_the_fixed_text()
    {
        var m = new AppraisalSummaryModel { IsExternal = true, CondoOwner = await CondoOwnerAsync(("101", "นายเอ", false)) };

        var html = await RenderAsync("{{ include 'partials/book-ext-summary' }}", m);

        html.Should().Contain($"<span class=\"l\">ผู้ถือกรรมสิทธิ์ห้องชุด</span><span class=\"c\">:</span><span class=\"v\">{NotVerified}</span>");
    }
}
