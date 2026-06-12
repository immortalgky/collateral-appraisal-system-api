using NSubstitute;
using Notification.Infrastructure.Email.Templates;
using Shared.Time;

namespace Notification.Tests.Email;

/// <summary>
/// Guards <see cref="EmailTemplateRenderer"/>'s body rendering — the sole HTML-encoding boundary for
/// admin-typed content. Two behaviours are pinned: (1) all user text is HTML-encoded (no markup/XSS
/// injection), and (2) line breaks survive as &lt;br/&gt; because Outlook's Word engine ignores the
/// CSS white-space:pre-wrap the body also carries. Every case runs against BOTH public entry points
/// (<c>MeetingInvitation</c> and <c>QuotationSent</c>) so the guarantee holds even if their body paths
/// later diverge — rather than relying on them sharing the same private <c>BuildBody</c> today.
/// </summary>
public class EmailTemplateRendererTests
{
    // Unique to the admin-content <p>; the footer/header <p> tags never use pre-wrap, so its presence
    // cleanly distinguishes "body rendered" from "body suppressed" without colliding on shell markup.
    private const string BodyMarker = "white-space:pre-wrap";

    /// <summary>The renderer's two public templates, exercised identically by every test.</summary>
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

    [Theory]
    [MemberData(nameof(Templates))]
    public void Content_HtmlTags_AreEncoded_NotInjected(Template template)
    {
        var html = Render(template, "Subject", "<script>alert('xss')</script>");

        Assert.Contains("&lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
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
    [InlineData(Template.MeetingInvitation, "Line1\r\nLine2")] // Windows CRLF
    [InlineData(Template.MeetingInvitation, "Line1\rLine2")]   // lone CR (old Mac)
    [InlineData(Template.MeetingInvitation, "Line1\nLine2")]   // Unix LF
    [InlineData(Template.QuotationSent, "Line1\r\nLine2")]
    [InlineData(Template.QuotationSent, "Line1\rLine2")]
    [InlineData(Template.QuotationSent, "Line1\nLine2")]
    public void SingleNewline_BecomesExactlyOneBr(Template template, string content)
    {
        var html = Render(template, "Subject", content);

        Assert.Contains("Line1<br/>Line2", html);
        Assert.DoesNotContain("Line1<br/><br/>Line2", html); // CRLF must not double up
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public void BlankLine_BecomesTwoBr_ForParagraphSpacing(Template template)
    {
        var html = Render(template, "Subject", "Para1\n\nPara2");

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

    [Theory]
    [MemberData(nameof(Templates))]
    public void NonEmptyContent_RendersBody(Template template)
    {
        var html = Render(template, "Subject", "Hello");

        Assert.Contains(BodyMarker, html);
        Assert.Contains("Hello", html);
    }
}
