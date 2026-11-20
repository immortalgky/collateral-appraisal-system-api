using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Code-review round 4 on the appraisal book (items numbered as in the review).</summary>
public class BookReviewRound4Tests
{
    // ── 1. the condo summary prints its multi-line values one line each ─────────────────────────────────────────────

    [Fact]
    public async Task The_condo_summary_owner_and_obligation_values_keep_their_line_breaks()
    {
        var m = new AppraisalSummaryModel { LandOwner = "ห้อง 101: A\nห้อง 102: B", ObligationMortgage = "ห้อง 101: ธนาคาร ก", ObligationOthers = "ห้อง 102: อื่นๆ" };

        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance).RenderRawAsync("{{ include 'partials/summary-condo-body' }}", m, CancellationToken.None);

        foreach (var value in new[] { "ห้อง 101: A\nห้อง 102: B", "ห้อง 101: ธนาคาร ก", "ห้อง 102: อื่นๆ" })
            html.Should().Contain($"<span class=\"v\" style=\"white-space:pre-line\">{value}</span>");
    }

    // ── 3. an ownerless unit is left out of the owner text, as in the letter ────────────────────────────────────────

    private static CondoSection Units(params (string Room, string? Owner, bool? Verified)[] u) =>
        new() { Units = u.Select(x => new CondoUnitAreaRow { RoomNumber = x.Room, OwnerName = x.Owner, IsOwnerVerified = x.Verified }).ToList() };

    [Fact]
    public void Owner_text_leaves_out_units_with_no_owner_like_the_letter()
    {
        Units(("101", "นายเอ", true), ("102", null, null)).OwnersText.Should().Be("นายเอ");                            // the one owner there is
        Units(("101", "นายเอ", true), ("102", "-", null), ("103", "นายบี", true)).OwnersText.Should().Be("ห้อง 101: นายเอ\nห้อง 103: นายบี");
        Units(("101", "นายเอ", false), ("102", "นายบี", true)).OwnersText.Should().Be("ห้อง 101: ไม่สามารถตรวจสอบได้\nห้อง 102: นายบี");
        Units(("101", null, null), ("102", " ", null)).OwnersText.Should().BeNull();   // the template prints the dash

    }
}
