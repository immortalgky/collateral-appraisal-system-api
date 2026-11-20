using FluentAssertions;
using Reporting.Application.Models.Sections;
using Reporting.Application.Providers;

namespace Reporting.Tests;

/// <summary>ผู้ถือกรรมสิทธิ์อาคาร: "ไม่สามารถตรวจสอบได้" (IsOwnerVerified == false) prints a fixed phrase; verified or unanswered keeps the stored name.</summary>
public class OwnerDisplayTests
{
    [Fact]
    public void Not_verifiable_prints_the_cannot_verify_phrase_even_when_a_name_is_stored()
    {
        AppraisalSummaryCommonLoader.OwnerDisplay("สมชาย", false).Should().Be("ไม่สามารถตรวจสอบได้");
        AppraisalSummaryCommonLoader.OwnerDisplay(null, false).Should().Be("ไม่สามารถตรวจสอบได้");
    }

    [Fact]
    public void Verified_prints_the_name()
        => AppraisalSummaryCommonLoader.OwnerDisplay("สมชาย", true).Should().Be("สมชาย");

    [Fact]
    public void Unanswered_keeps_todays_behaviour()
    {
        AppraisalSummaryCommonLoader.OwnerDisplay("สมชาย", null).Should().Be("สมชาย");
        AppraisalSummaryCommonLoader.OwnerDisplay(null, null).Should().BeNull();
    }

    [Fact]
    public void The_book_the_letter_and_the_summary_say_the_same_thing_for_an_unverified_owner() =>
        new BuildingSection { OwnerName = "สมชาย", IsOwnerVerified = false }.OwnerText
            .Should().Be(AppraisalSummaryCommonLoader.OwnerDisplay("สมชาย", false)).And.Be("ไม่สามารถตรวจสอบได้");
}
