using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Infrastructure.Rendering;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>The letter's ภาระผูกพัน / ภาระผูกพันอื่นๆ rows cover every deed (or every condo room), and each row collapses on its own.</summary>
public class BookLetterObligationRowsTests
{
    private static LandSection Land(string number, string? mortgage, string? others = "ไม่มี") =>
        new() { ObligationMortgage = mortgage, ObligationOthers = others, Titles = [new LandTitleRow { TitleNumber = number, TitleTypeDisplay = "โฉนดที่ดิน" }] };

    private static AppraisalSummaryModel Model(IEnumerable<LandSection> lands, string body = "standard") =>
        new() { BodyType = body, PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = lands.ToList() }] };

    [Fact]
    public void The_same_text_on_every_deed_prints_once()
    {
        var (m, o) = LetterOwnerRows.Obligations(Model([Land("1", "ไม่มี"), Land("2", "ไม่มี"), Land("3", "ไม่มี")]));

        m.Should().Be("ไม่มี");
        o.Should().Be("ไม่มี");
    }

    [Fact]
    public void Three_lands_with_different_mortgages_are_paired_with_their_deed_and_the_others_row_collapses_on_its_own()
    {
        var (m, o) = LetterOwnerRows.Obligations(Model([Land("12345", "จำนองกับธนาคาร ก"), Land("12346", "ไม่มี"), Land("12347", "จำนองกับธนาคาร ข")]));

        m.Should().Be("โฉนดที่ดิน 12345: จำนองกับธนาคาร ก\nโฉนดที่ดิน 12347: จำนองกับธนาคาร ข");   // the deed with nothing recorded is not listed
        o.Should().Be("ไม่มี");
    }

    [Fact]
    public void The_others_row_can_differ_while_the_mortgage_row_is_one_text()
    {
        var (m, o) = LetterOwnerRows.Obligations(Model([Land("1", "ไม่มี", "สิทธิเก็บกิน"), Land("2", "ไม่มี", "ไม่มี")]));

        m.Should().Be("ไม่มี");
        o.Should().Be("โฉนดที่ดิน 1: สิทธิเก็บกิน");
    }

    [Theory]
    [InlineData("standard", "2.1")]
    [InlineData("construction", "2")]
    public void More_than_ten_deeds_with_different_texts_are_counted_per_text_with_the_section(string body, string section)
    {
        // 13 deeds: 7 mortgaged to ก, 4 to ข, 2 with nothing recorded. Only the 11 with an entry are counted.
        var lands = Enumerable.Range(1, 13).Select(i => Land(i.ToString(), i <= 7 ? "จำนองกับธนาคาร ก" : i <= 11 ? "จำนองกับธนาคาร ข" : "ไม่มี"));

        var (m, _) = LetterOwnerRows.Obligations(Model(lands, body));

        m.Should().Be($"จำนองกับธนาคาร ก จำนวน 7 ฉบับ\nจำนองกับธนาคาร ข จำนวน 4 ฉบับ (รายละเอียดตามข้อ {section})");
    }

    [Fact]
    public void A_deed_with_an_entry_is_named_even_when_the_others_have_none_and_all_none_reads_ไม่มี()
    {
        var (m, o) = LetterOwnerRows.Obligations(Model([Land("1", "จำนองกับธนาคาร ก"), Land("2", "ไม่มี"), Land("3", "ไม่มี")]));

        m.Should().Be("โฉนดที่ดิน 1: จำนองกับธนาคาร ก");   // not a bare "จำนองกับธนาคาร ก", which would read as if it applied to every deed
        o.Should().Be("ไม่มี");
        LetterOwnerRows.Obligations(Model([Land("1", "ไม่มี"), Land("2", "ไม่มี")])).Should().Be(("ไม่มี", "ไม่มี"));
    }

    [Fact]
    public void A_land_with_no_numbered_deed_is_listed_as_its_own_plot_and_no_land_means_nothing()
    {
        var unnumbered = new LandSection { ObligationMortgage = "จำนองกับธนาคาร ก", ObligationOthers = "ไม่มี" };

        // its mortgage is never dropped when another land has numbered deeds, and never replaced by another land's text
        LetterOwnerRows.Obligations(Model([unnumbered, Land("12345", "ไม่มี")])).Should().Be(("ที่ดินแปลงที่ 1: จำนองกับธนาคาร ก", "ไม่มี"));
        LetterOwnerRows.Obligations(Model([unnumbered])).Should().Be(("จำนองกับธนาคาร ก", "ไม่มี"));   // one land, alike: the text alone
        LetterOwnerRows.Obligations(Model([unnumbered, Land("", "ไม่มี")])).Should().Be(("ที่ดินแปลงที่ 1: จำนองกับธนาคาร ก", "ไม่มี"));
        LetterOwnerRows.Obligations(Model([])).Should().Be((null, null));
    }

    [Fact]
    public void A_land_and_a_condo_keep_both_the_deeds_and_the_rooms_obligations()
    {
        var m = Model([Land("12345", "จำนองกับธนาคาร ก")]);
        m.CondoObligationItems = [new ExternalObligationItem { Label = "ห้อง 101", Mortgage = "จำนองกับธนาคาร ข", Others = "สิทธิเก็บกิน" }, new ExternalObligationItem { Label = "ห้อง 102", Mortgage = "ไม่มี", Others = "ไม่มี" }];

        var (mortgage, others) = LetterOwnerRows.Obligations(m);

        mortgage.Should().Be("โฉนดที่ดิน 12345: จำนองกับธนาคาร ก\nห้อง 101: จำนองกับธนาคาร ข");   // the room with none is not listed
        others.Should().Be("ห้อง 101: สิทธิเก็บกิน");
    }

    [Fact]
    public void Above_ten_items_across_lands_and_rooms_are_counted_per_text_as_รายการ()
    {
        var lands = Enumerable.Range(1, 8).Select(i => Land(i.ToString(), "จำนองกับธนาคาร ก")).ToList();
        var m = Model(lands);
        m.CondoObligationItems = Enumerable.Range(1, 4).Select(i => new ExternalObligationItem { Label = $"ห้อง {i}", Mortgage = "จำนองกับธนาคาร ข", Others = "ไม่มี" }).ToList();

        LetterOwnerRows.Obligations(m).Mortgage.Should().Be("จำนองกับธนาคาร ก จำนวน 8 รายการ\nจำนองกับธนาคาร ข จำนวน 4 รายการ");   // 2.1 lists only the land deeds here: no pointer
    }

    // ── condo-only appraisal, through the builder's own query ───────────────────────────────────────────────────────

    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    private static async Task<AppraisalSummaryModel> CondoOnlyAsync(params (string Room, string? Type, string? Mortgage, string? Details)[] rooms)
    {
        var header = new DataTable();
        header.Columns.Add("AppraisalNumber", typeof(string)); header.Columns.Add("RequestId", typeof(Guid)); header.Columns.Add("CompletedAt", typeof(DateTime));
        header.Rows.Add("A-1", Guid.NewGuid(), DBNull.Value);
        var condo = new DataTable();
        foreach (var c in new[] { "CondoRoomNumber", "CondoObligationType", "CondoMortgageDetails", "CondoObligationDetails" }) condo.Columns.Add(c, typeof(string));
        foreach (var r in rooms) condo.Rows.Add(r.Room, (object?)r.Type ?? DBNull.Value, (object?)r.Mortgage ?? DBNull.Value, (object?)r.Details ?? DBNull.Value);
        var names = new DataTable();
        names.Columns.Add("Code", typeof(string)); names.Columns.Add("Description", typeof(string));
        names.Rows.Add("03", "ติดสัญญาเช่า");
        var ds = new DataSet();
        for (var i = 0; i < 14; i++) ds.Tables.Add(i == 0 ? header : i == 8 ? condo : i == 12 ? names : Empty());
        var f = new FakeSqlConnectionFactory();
        f.SetMultiResult(ds);

        var m = await ExternalBookBuilder.BuildAsync(f.GetOpenConnection(), Guid.NewGuid(), new AppraisalSummaryModel(), 70m, CancellationToken.None);
        m.BodyType = "condo";   // the book provider sets it before it words the rows
        (m.ObligationMortgage, m.ObligationOthers) = LetterOwnerRows.Obligations(m);   // the provider combines the rooms (and any lands) the same way
        return m;
    }

    [Fact]
    public async Task A_condo_only_appraisal_reads_the_obligations_per_room()
    {
        var m = await CondoOnlyAsync(("101", "[\"02\"]", "จำนองกับธนาคาร ก", null), ("102", "[\"03\"]", null, null), ("103", "[\"02\"]", "จำนองกับธนาคาร ก", null));

        m.ObligationMortgage.Should().Be("ห้อง 101: จำนองกับธนาคาร ก\nห้อง 103: จำนองกับธนาคาร ก");
        m.ObligationOthers.Should().Be("ห้อง 102: ติดสัญญาเช่า");
    }

    [Fact]
    public async Task A_condo_only_appraisal_with_the_same_obligation_on_every_room_prints_it_once()
    {
        var m = await CondoOnlyAsync(("101", "[\"02\"]", "จำนองกับธนาคาร ก", null), ("102", "[\"02\"]", "จำนองกับธนาคาร ก", null));

        m.ObligationMortgage.Should().Be("จำนองกับธนาคาร ก");
        m.ObligationOthers.Should().Be("ไม่มี");
    }

    [Fact]
    public async Task More_than_ten_condo_rooms_with_different_texts_are_counted_per_text_with_section_2_1()
    {
        // 13 rooms: 7 mortgaged to ก, 4 to ข, 2 with none: only the 11 with an entry are counted.
        var rooms = Enumerable.Range(1, 13).Select(i => ((100 + i).ToString(), (string?)(i <= 11 ? "[\"02\"]" : null), (string?)(i <= 7 ? "จำนองกับธนาคาร ก" : "จำนองกับธนาคาร ข"), (string?)null)).ToArray();

        var m = await CondoOnlyAsync(rooms);

        m.ObligationMortgage.Should().Be("จำนองกับธนาคาร ก จำนวน 7 ห้อง\nจำนองกับธนาคาร ข จำนวน 4 ห้อง (รายละเอียดตามข้อ 2.1)");
    }

    [Fact]
    public async Task The_letter_labels_the_rows_ภาระผูกพันการจำนอง_and_ภาระผูกพันอื่นๆ()
    {
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-letter-external' }}",
            new AppraisalSummaryModel { IsExternal = true, ObligationMortgage = "ไม่มี", ObligationOthers = "ไม่มี" }, CancellationToken.None);

        html.Should().Contain("scope=\"row\">ภาระผูกพันการจำนอง</th>").And.Contain("scope=\"row\">ภาระผูกพันอื่นๆ</th>");
    }

    [Fact]
    public async Task The_summary_page_labels_the_rows_the_same_way()
    {
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/book-ext-summary' }}",
            new AppraisalSummaryModel { IsExternal = true, ObligationMortgage = "ไม่มี", ObligationOthers = "ไม่มี" }, CancellationToken.None);

        html.Should().Contain("<span class=\"l\">ภาระผูกพันการจำนอง</span>").And.Contain("<span class=\"l\">ภาระผูกพันอื่นๆ</span>");
    }
}
