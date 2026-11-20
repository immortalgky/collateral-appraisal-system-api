using FluentAssertions;
using Reporting.Application.Providers;
using Reporting.Application.Providers.Sections;

namespace Reporting.Tests;

/// <summary>
/// The two obligation lines of the book and the summaries: ภาระผูกพันการจำนอง (code 02) and ภาระผูกพันอื่นๆ
/// (codes 03/04/05/99). Stored as the multi-select ObligationType, a JSON array of Obligation parameter codes.
/// </summary>
public class ObligationTextTests
{
    private static readonly Dictionary<string, string?> Names = new()
    {
        ["01"] = "ไม่มีภาระผูกพัน",
        ["02"] = "จำนองเป็นประกัน",
        ["03"] = "ติดสัญญาเช่า",
        ["04"] = "สิทธิเก็บกิน",
        ["05"] = "สิทธิเหนือพื้นดิน",
        ["06"] = "ภาระจำยอม",
        ["99"] = "อื่นๆ",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("[\"01\"]")]
    public void No_obligation_prints_none_on_both_lines(string? type)
    {
        ObligationText.Mortgage(type, "ignored").Should().Be("ไม่มี");
        ObligationText.Others(type, "ignored", Names).Should().Be("ไม่มี");
    }

    [Fact]
    public void Mortgage_only_prints_the_mortgage_text_and_none_for_the_others()
    {
        ObligationText.Mortgage("[\"02\"]", "จำนองกับธนาคาร ก").Should().Be("จำนองกับธนาคาร ก");
        ObligationText.Others("[\"02\"]", null, Names).Should().Be("ไม่มี");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Mortgage_without_a_typed_text_prints_the_option_name(string? details)
        => ObligationText.Mortgage("[\"02\"]", details).Should().Be("จำนองเป็นประกัน");

    [Fact]
    public void Others_only_prints_the_remark_alone_and_none_for_the_mortgage()
    {
        ObligationText.Others("[\"99\",\"03\"]", "ผูกพัน 30 ปี", Names).Should().Be("ผูกพัน 30 ปี");
        ObligationText.Mortgage("[\"99\",\"03\"]", "must not show").Should().Be("ไม่มี");
    }

    [Fact]
    public void Others_without_a_remark_falls_back_to_the_names_in_parameter_order()
    {
        ObligationText.Others("[\"04\",\"05\"]", " ", Names).Should().Be("สิทธิเก็บกิน, สิทธิเหนือพื้นดิน");
        ObligationText.Others("[\"99\",\"03\"]", null, Names).Should().Be("ติดสัญญาเช่า, อื่นๆ"); // 99 last, whatever order it was ticked in
    }

    [Fact]
    public void Both_lines_fill_when_the_mortgage_and_another_obligation_are_picked()
    {
        const string type = "[\"02\",\"03\"]";
        ObligationText.Mortgage(type, "จำนองกับธนาคาร ก").Should().Be("จำนองกับธนาคาร ก");
        ObligationText.Others(type, "สัญญาเช่า 30 ปี", Names).Should().Be("สัญญาเช่า 30 ปี");
    }

    [Fact]
    public void The_new_servitude_code_prints_with_the_other_obligations_in_parameter_order()
    {
        ObligationText.Others("[\"06\"]", null, Names).Should().Be("ภาระจำยอม");
        ObligationText.Others("[\"99\",\"06\",\"03\"]", null, Names).Should().Be("ติดสัญญาเช่า, ภาระจำยอม, อื่นๆ");
        ObligationText.Others("[\"06\"]", "ทางเข้าบ้านเพื่อนบ้าน", Names).Should().Be("ทางเข้าบ้านเพื่อนบ้าน"); // the typed remark still wins
    }

    [Fact]
    public void A_code_added_to_the_group_later_prints_too_but_01_and_02_never_do()
    {
        ObligationText.Others("[\"07\"]", null, Names).Should().Be("07"); // unknown to the map: prints as itself
        ObligationText.Others("[\"07\"]", null, new Dictionary<string, string?> { ["07"] = "สิทธิอื่นที่เพิ่มใหม่" }).Should().Be("สิทธิอื่นที่เพิ่มใหม่");
        ObligationText.Others("[\"01\",\"02\"]", null, Names).Should().Be("ไม่มี");
        ObligationText.Others("[\"02\",\"06\"]", null, Names).Should().Be("ภาระจำยอม");
    }

    [Fact]
    public void A_code_without_a_name_prints_as_itself()
        => ObligationText.Others("[\"05\"]", null, new Dictionary<string, string?>()).Should().Be("05");

    [Fact]
    public void A_bare_legacy_code_and_unreadable_json_never_throw()
    {
        ObligationText.Mortgage("02", null).Should().Be("จำนองเป็นประกัน");
        ObligationText.Others("03", null, Names).Should().Be("ติดสัญญาเช่า");
        ObligationText.Mortgage("[not json", null).Should().Be("ไม่มี");
        ObligationText.Others("0", "text", Names).Should().Be("ไม่มี"); // legacy bit-era value: not a code
    }

    [Theory]
    [InlineData(null, "-")]
    [InlineData(true, "จดทะเบียน")]
    [InlineData(false, "ไม่จดทะเบียน")]
    public void Lease_registration_prints_a_dash_until_answered(bool? value, string expected)
        => LeaseAgreementLoader.YesNo(value, "จดทะเบียน", "ไม่จดทะเบียน").Should().Be(expected);
}
