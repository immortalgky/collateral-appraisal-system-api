using System.Data;
using System.Text.RegularExpressions;
using FluentAssertions;
using Reporting.Application.Formatting;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;
using Reporting.Tests.Infrastructure;

namespace Reporting.Tests;

/// <summary>Code-review round 3 on the appraisal book (items numbered as in the review).</summary>
public class BookReviewRound3Tests
{
    private static DataTable Empty() { var t = new DataTable(); t.Columns.Add("Unused", typeof(int)); return t; }

    // ── 1-3. every photo query reads the document join unfiltered ───────────────────────────────────────────────────

    private static string ProviderSources()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Modules", "Reporting", "Reporting"))) dir = dir.Parent;
        dir.Should().NotBeNull();
        return string.Join("\n", Directory.EnumerateFiles(Path.Combine(dir!.FullName, "Modules", "Reporting", "Reporting", "Application"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
    }

    [Fact]
    public void No_photo_query_filters_the_document_in_the_join_so_none_falls_back_to_the_gallery_path_for_a_deleted_document()
    {
        var src = ProviderSources();

        Regex.IsMatch(src, @"JOIN document\.Documents\s+\w+\s+ON[^\r\n]*IsDeleted").Should().BeFalse();
        Regex.Matches(src, @"\(\w+\.Id IS NULL OR \(\w+\.IsActive = 1 AND \w+\.IsDeleted = 0\)\)").Count.Should().BeGreaterThanOrEqualTo(7);   // building, law, 2x comparison, 3x appendix (4 predicates)
    }

    [Fact]
    public async Task The_law_photo_query_drops_a_deleted_document_and_the_topic_photo_prefers_the_documents_mime()
    {
        var f = new FakeSqlConnectionFactory();
        var ds = new DataSet();
        ds.Tables.Add(Empty()); ds.Tables.Add(Empty()); ds.Tables.Add(Empty());
        f.SetMultiResult(ds);
        await LawSectionLoader.LoadAsync(f.GetOpenConnection(), Guid.NewGuid());
        f.Commands.Should().ContainSingle().Which.Should().Contain("AND (d.Id IS NULL OR (d.IsActive = 1 AND d.IsDeleted = 0))").And.Contain("COALESCE(d.MimeType, g.MimeType)");

        var a = new FakeSqlConnectionFactory();
        a.EnqueueResult(Empty()); a.EnqueueResult(Empty());
        await AppendixSectionLoader.LoadAsync(a.GetOpenConnection(), Guid.NewGuid());
        a.Commands.Should().Contain(c => c.Contains("COALESCE(d.MimeType, g.MimeType)    AS MimeType")).And.NotContain(c => c.Contains("COALESCE(g.MimeType, d.MimeType)"));
    }

    // ── 6. a land's unnumbered deed rows keep their owners ──────────────────────────────────────────────────────────

    [Fact]
    public void A_land_with_numbered_and_unnumbered_deed_rows_lists_the_unnumbered_owner_too()
    {
        var land = new LandSection
        {
            Titles =
            [
                new LandTitleRow { TitleTypeDisplay = "โฉนดที่ดิน", TitleNumber = "12345", OwnerName = "นายเอ" },
                new LandTitleRow { TitleTypeDisplay = "โฉนดที่ดิน", TitleNumber = " - ", OwnerName = "นายบี" }
            ]
        };
        var m = new AppraisalSummaryModel { BodyType = "standard", PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [land] }] };

        LetterOwnerRows.LandOwners(m).Should().Be("โฉนดที่ดิน 12345: นายเอ\nที่ดินแปลงที่ 1 (ไม่ระบุเลขที่): นายบี");
    }

    // ── 9. one room label ──────────────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("101", "ห้อง 101")]
    [InlineData(" 101 ", "ห้อง 101")]
    [InlineData("-", "ห้องที่ 3")]
    [InlineData("  ", "ห้องที่ 3")]
    [InlineData(null, "ห้องที่ 3")]
    public void The_room_label_reads_a_blank_or_dash_room_as_its_position(string? room, string expected) =>
        ThaiAddressFormatter.RoomLabel(room, 2).Should().Be(expected);

    [Fact]
    public void The_condo_book_rows_use_that_label_for_a_dash_room()
    {
        var condo = new CondoSection
        {
            Units = [new CondoUnitAreaRow { RoomNumber = "-", OwnerName = "นายเอ" }, new CondoUnitAreaRow { RoomNumber = "102", OwnerName = "นายบี" }]
        };

        condo.OwnersText.Should().Be("ห้องที่ 1: นายเอ\nห้อง 102: นายบี");
    }
}
