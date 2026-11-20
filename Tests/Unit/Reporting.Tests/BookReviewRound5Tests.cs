using FluentAssertions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>Code-review round 5 on the appraisal book (items numbered as in the review).</summary>
public class BookReviewRound5Tests
{
    // 12 units: 7 owned by นายเอ, 5 by นายบี; 7 mortgaged to ธนาคาร ก, 5 to ธนาคาร ข
    private static CondoSection Twelve() => new()
    {
        Units = Enumerable.Range(1, 12).Select(i => new CondoUnitAreaRow
        {
            RoomNumber = $"{100 + i}", OwnerName = i <= 7 ? "นายเอ" : "นายบี",
            ObligationMortgage = i <= 7 ? "จำนองกับธนาคาร ก" : "จำนองกับธนาคาร ข", ObligationOthers = "ไม่มี"
        }).ToList()
    };

    // ── A. the book lists every room, never grouped ────────────────────────────────────────────────────────────────

    [Fact]
    public void The_book_texts_list_one_line_per_room_however_many_there_are_and_point_to_no_section()
    {
        var c = Twelve();

        c.OwnersText!.Split('\n').Should().HaveCount(12).And.StartWith("ห้อง 101: นายเอ");
        c.ObligationMortgageText!.Split('\n').Should().HaveCount(12);
        c.ObligationOthersText.Should().Be("ไม่มี");
        new[] { c.OwnersText, c.ObligationMortgageText }.Should().OnlyContain(t => !t!.Contains("รายละเอียดตามข้อ") && !t.Contains("จำนวน"));
    }

    // ── B. the condo summary groups above 10, with no pointer ───────────────────────────────────────────────────────

    [Fact]
    public void The_summary_texts_group_above_ten_rooms_and_point_to_no_section()
    {
        var c = Twelve();

        c.Owners(grouped: true).Should().Be("นายเอ จำนวน 7 ห้อง\nนายบี จำนวน 5 ห้อง");
        c.Mortgage(grouped: true).Should().Be("จำนองกับธนาคาร ก จำนวน 7 ห้อง\nจำนองกับธนาคาร ข จำนวน 5 ห้อง");
        c.Others(grouped: true).Should().Be("ไม่มี");
    }

    // ── C. the letter's pointer only where 2.1 lists the rooms ──────────────────────────────────────────────────────

    private static AppraisalSummaryModel LetterModel(string bodyType, bool land)
    {
        var m = new AppraisalSummaryModel
        {
            BodyType = bodyType,
            CondoObligationItems = Enumerable.Range(1, 12).Select(i => new ExternalObligationItem
            {
                Label = $"ห้อง {100 + i}", Owner = i <= 7 ? "นายเอ" : "นายบี", Mortgage = i <= 7 ? "จำนองกับธนาคาร ก" : "จำนองกับธนาคาร ข", Others = "ไม่มี"
            }).ToList()
        };
        if (land) m.PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = [new LandSection { ObligationMortgage = "ไม่มี", ObligationOthers = "ไม่มี", Titles = [new LandTitleRow { TitleTypeDisplay = "โฉนดที่ดิน", TitleNumber = "1" }] }] }];
        return m;
    }

    [Fact]
    public void The_letter_points_to_2_1_in_a_condo_book_and_to_nothing_in_a_land_and_condo_book()
    {
        LetterOwnerRows.CondoOwners(LetterModel("condo", false)).Should().Be("นายเอ จำนวน 7 ห้อง\nนายบี จำนวน 5 ห้อง (รายละเอียดตามข้อ 2.1)");
        LetterOwnerRows.Obligations(LetterModel("condo", false)).Mortgage.Should().Be("จำนองกับธนาคาร ก จำนวน 7 ห้อง\nจำนองกับธนาคาร ข จำนวน 5 ห้อง (รายละเอียดตามข้อ 2.1)");

        LetterOwnerRows.CondoOwners(LetterModel("standard", true)).Should().Be("นายเอ จำนวน 7 ห้อง\nนายบี จำนวน 5 ห้อง");
        LetterOwnerRows.Obligations(LetterModel("standard", true)).Mortgage.Should().NotContain("รายละเอียดตามข้อ").And.Contain("จำนวน");
    }

    [Fact]
    public void The_lands_obligation_row_keeps_its_land_pointer_when_there_are_no_rooms()
    {
        var lands = Enumerable.Range(1, 12).Select(i => new LandSection { ObligationMortgage = i <= 7 ? "จำนองกับธนาคาร ก" : "จำนองกับธนาคาร ข", ObligationOthers = "ไม่มี", Titles = [new LandTitleRow { TitleTypeDisplay = "โฉนดที่ดิน", TitleNumber = $"{i}" }] }).ToList();
        var m = new AppraisalSummaryModel { BodyType = "standard", PropertyGroups = [new PropertyGroupDetail { GroupNumber = 1, Lands = lands }] };

        LetterOwnerRows.Obligations(m).Mortgage.Should().EndWith("(รายละเอียดตามข้อ 2.1)");
    }

    [Fact]
    public void Summarise_with_a_null_section_prints_no_pointer_and_with_group_false_never_groups()
    {
        var items = Enumerable.Range(1, 12).Select(i => ($"ห้อง {i}", (string?)(i <= 7 ? "นายเอ" : "นายบี"), 1)).ToList();

        LetterOwnerRows.Summarise(items, "ห้อง", "2.1").Should().Be("นายเอ จำนวน 7 ห้อง\nนายบี จำนวน 5 ห้อง (รายละเอียดตามข้อ 2.1)");
        LetterOwnerRows.Summarise(items, "ห้อง", null).Should().NotContain("ข้อ");
        LetterOwnerRows.Summarise(items, "ห้อง", null, null, group: false)!.Split('\n').Should().HaveCount(12);
    }

    // ── 5. the unverified rule has one source ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_unverified_owner_reads_the_same_everywhere()
    {
        new CondoUnitAreaRow { OwnerName = "นายเอ", IsOwnerVerified = false }.OwnerText.Should().Be(AppraisalSummaryCommonLoader.OwnerNotVerifiedText);
        new CondoUnitAreaRow { OwnerName = " นายเอ ", IsOwnerVerified = true }.OwnerText.Should().Be("นายเอ");
        new CondoUnitAreaRow { OwnerName = "-" }.OwnerText.Should().Be("-");
    }
}
