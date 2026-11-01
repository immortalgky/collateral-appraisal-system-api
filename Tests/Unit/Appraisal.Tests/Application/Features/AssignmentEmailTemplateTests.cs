using Appraisal.Application.Features.Assignments.AssignmentEmails;

namespace Appraisal.Tests.Application.Features;

/// <summary>The admin's remark keeps its line breaks and indentation in the template body.</summary>
public class AssignmentEmailTemplateTests
{
    private static string Body(string? remark) => AssignmentEmailTemplate.BodyHtml(
        new AssignmentEmailTemplate.Values("Acme", "cust", "69042630", null, null, null, null, "resp", remark));

    [Fact]
    public void A_multi_line_remark_is_joined_with_line_breaks_in_any_newline_style()
    {
        Assert.Contains("หมายเหตุ: one<br>two<br>three<br><br>จึงเรียน", Body("one\r\ntwo\rthree"));
    }

    [Fact]
    public void Leading_spaces_and_tabs_become_visible_indentation()
    {
        Assert.Contains("หมายเหตุ: testtest<br>&nbsp;&nbsp;test<br>&nbsp;&nbsp;&nbsp;&nbsp;tab<br>test<br><br>จึงเรียน",
            Body("testtest\n  test\n\ttab\ntest"));
    }

    [Fact]
    public void The_first_lines_own_indentation_is_kept_but_blank_lines_at_the_ends_and_trailing_spaces_go()
    {
        Assert.Contains("หมายเหตุ: &nbsp;&nbsp;a<br><br>b<br><br>จึงเรียน", Body("\n \n  a  \n\nb\n\n"));
    }

    [Fact]
    public void Unicode_and_legacy_line_breaks_are_line_breaks_and_other_control_characters_are_dropped()
    {
        Assert.Contains("หมายเหตุ: a<br>b<br>c<br>d<br>e<br>fg<br><br>จึงเรียน", Body("a\u2028b\u2029c\u0085d\ve\ff\u0000\u001bg"));
    }

    [Fact]
    public void Invisible_characters_that_hide_text_are_dropped()
    {
        Assert.Contains("หมายเหตุ: abcdefgh<br><br>จึงเรียน",
            Body("a\u200Bb\u202Ec\u2066d\uFEFFe" + char.ConvertFromUtf32(0xE0041) + "f\u3164g\u00ADh\u115F"));
    }

    [Fact]
    public void Runs_of_inner_spaces_and_inner_tabs_keep_their_width_but_a_single_space_stays_a_space()
    {
        Assert.Contains("หมายเหตุ: a b&nbsp;&nbsp;&nbsp; c&nbsp;&nbsp;&nbsp;&nbsp;d<br><br>จึงเรียน", Body("a b    c\td"));
    }

    [Fact]
    public void A_zero_width_joiner_emoji_sequence_is_kept_whole()
    {
        const string family = "\U0001F468\u200D\U0001F469\u200D\U0001F467";
        // HtmlEncode writes the astral emoji as numeric references; the joiners between them must survive.
        Assert.Contains("หมายเหตุ: &#128104;\u200D&#128105;\u200D&#128103;<br><br>จึงเรียน", Body(family));
    }

    [Fact]
    public void Html_in_the_remark_is_still_encoded()
    {
        Assert.Contains("หมายเหตุ: &lt;b&gt;x&lt;/b&gt;<br>&nbsp;A&amp;B<br><br>จึงเรียน", Body("<b>x</b>\n A&B"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \r\n \n")]
    public void An_empty_remark_reads_as_a_dash(string? remark)
    {
        Assert.Contains("หมายเหตุ: -<br><br>จึงเรียน", Body(remark));
    }
}
