using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>The front matter is the contents page only; the conditions / definitions page was removed until the bank supplies its wording.</summary>
public class BookFrontMatterTests
{
    [Fact]
    public async Task The_front_matter_has_no_conditions_page_and_the_contents_starts_at_section_1()
    {
        var model = new AppraisalSummaryModel { BodyType = "standard" };
        var html = await new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync("{{ include 'partials/book-front-matter' }}", model, CancellationToken.None);

        html.Should().NotContain("[[PAGE:cond]]").And.NotContain("เงื่อนไขและข้อจำกัดทั่วไป").And.NotContain("คำนิยาม").And.NotContain("คำอธิบายวิธีการประเมิน");
        html.Should().Contain("สารบัญ").And.Contain("[[PAGE:s3]]");
        html.IndexOf("หน้า", StringComparison.Ordinal).Should().BeLessThan(html.IndexOf("3. ทรัพย์สินที่ประเมินมูลค่า", StringComparison.Ordinal));
    }
}
