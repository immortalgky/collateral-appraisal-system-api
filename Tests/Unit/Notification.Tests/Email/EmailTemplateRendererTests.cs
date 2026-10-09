using NSubstitute;
using Notification.Infrastructure.Email.Templates;
using Shared.Time;

namespace Notification.Tests.Email;

/// <summary>
/// Guards <see cref="EmailTemplateRenderer"/>'s body rendering. The two public entry points take
/// different kinds of content, so they are pinned separately:
/// <list type="bullet">
/// <item><c>MeetingInvitation</c> takes admin-typed plain text. It is HTML-encoded (no markup/XSS
/// injection) and line breaks survive as &lt;br/&gt;, because Outlook's Word engine ignores the CSS
/// white-space:pre-wrap the body also carries.</item>
/// <item><c>QuotationSent</c> takes a ready-made HTML fragment (since CA-280 the frontend builds the
/// quotation table and escapes each value itself), so it is emitted as-is — encoding it again would
/// print the markup as literal text.</item>
/// </list>
/// The subject is HTML-encoded on both.
/// </summary>
public class EmailTemplateRendererTests
{
    // Unique to the admin-content <p>; the footer/header <p> tags never use pre-wrap, so its presence
    // cleanly distinguishes "body rendered" from "body suppressed" without colliding on shell markup.
    private const string BodyMarker = "white-space:pre-wrap";

    /// <summary>The renderer's two public templates.</summary>
    public enum Template { MeetingInvitation, QuotationSent }

    public static TheoryData<Template> Templates =>
        new() { Template.MeetingInvitation, Template.QuotationSent };

    private static string Render(Template template, string subject, string? content)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.ApplicationNow.Returns(new DateTime(2026, 6, 12));
        var renderer = new EmailTemplateRenderer(clock);

        return template switch
        {
            Template.MeetingInvitation => renderer.MeetingInvitation(subject, content),
            Template.QuotationSent => renderer.QuotationSent(subject, content),
            _ => throw new ArgumentOutOfRangeException(nameof(template)),
        };
    }

    [Fact]
    public void MeetingInvitation_Content_HtmlTags_AreEncoded_NotInjected()
    {
        var html = Render(Template.MeetingInvitation, "Subject", "<script>alert('xss')</script>");

        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
    }

    [Fact]
    public void QuotationSent_Content_IsEmittedAsHtml_NotEncoded()
    {
        const string fragment = "<p style=\"margin:0 0 8px;\">Dear &amp; all</p><table><tr><td>1.</td></tr></table>";

        var html = Render(Template.QuotationSent, "Subject", fragment);

        Assert.Contains(fragment, html);
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void Subject_HtmlTags_AreEncoded(Template template)
    {
        var html = Render(template, "<b>Title</b>", "body");

        Assert.Contains("&lt;b&gt;Title&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>Title</b>", html);
    }

    [Theory]
    [InlineData("Line1\r\nLine2")] // Windows CRLF
    [InlineData("Line1\rLine2")]   // lone CR (old Mac)
    [InlineData("Line1\nLine2")]   // Unix LF
    public void MeetingInvitation_SingleNewline_BecomesExactlyOneBr(string content)
    {
        var html = Render(Template.MeetingInvitation, "Subject", content);

        Assert.Contains("Line1<br/>Line2", html);
        Assert.DoesNotContain("Line1<br/><br/>Line2", html); // CRLF must not double up
    }

    [Fact]
    public void MeetingInvitation_BlankLine_BecomesTwoBr_ForParagraphSpacing()
    {
        var html = Render(Template.MeetingInvitation, "Subject", "Para1\n\nPara2");

        Assert.Contains("Para1<br/><br/>Para2", html);
    }

    [Theory]
    [InlineData(Template.MeetingInvitation, null)]
    [InlineData(Template.MeetingInvitation, "")]
    [InlineData(Template.MeetingInvitation, "   ")]
    [InlineData(Template.QuotationSent, null)]
    [InlineData(Template.QuotationSent, "")]
    [InlineData(Template.QuotationSent, "   ")]
    public void NullOrWhitespaceContent_SuppressesBody(Template template, string? content)
    {
        var html = Render(template, "Subject", content);

        Assert.DoesNotContain(BodyMarker, html);
    }

    [Fact]
    public void MeetingInvitation_NonEmptyContent_RendersBody()
    {
        var html = Render(Template.MeetingInvitation, "Subject", "Hello");

        Assert.Contains(BodyMarker, html);
        Assert.Contains("Hello", html);
    }
}
