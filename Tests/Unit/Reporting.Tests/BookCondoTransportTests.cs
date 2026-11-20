using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Reporting.Application.Models;
using Reporting.Application.Models.Sections;
using Reporting.Infrastructure.Rendering;

namespace Reporting.Tests;

/// <summary>Condo book section 1.3: "ถนนผ่านหน้าโครงการ" prints the new RoadPassInFrontOfLand and falls back to the street; "การเดินทาง" prints the decoded transport.</summary>
public class BookCondoTransportTests
{
    private static Task<string> RenderCondoAsync(CondoSection condo) =>
        new ScribanTemplateRenderer(NullLogger<ScribanTemplateRenderer>.Instance)
            .RenderRawAsync("{{ include 'partials/book-flow-condo' }}",
                new AppraisalSummaryModel { CondoSection = condo }, CancellationToken.None);

    [Fact]
    public async Task Road_in_front_prints_the_new_field_over_the_street()
    {
        var html = await RenderCondoAsync(new CondoSection { RoadPassInFrontOfLand = "ถนนสุขุมวิท", LocatedOnRoad = "ถนนอื่น" });

        html.Should().Contain("ถนนสุขุมวิท");
    }

    [Fact]
    public async Task Road_in_front_falls_back_to_the_street_when_the_new_field_is_empty()
    {
        var html = await RenderCondoAsync(new CondoSection { RoadPassInFrontOfLand = null, LocatedOnRoad = "ถนนรัชดา" });

        html.Should().Contain("ถนนรัชดา");
    }

    [Fact]
    public async Task Transport_prints_the_decoded_value_and_no_longer_the_missing_placeholder()
    {
        var html = await RenderCondoAsync(new CondoSection { TransportationAccessType = "รถไฟฟ้า, รถประจำทาง" });

        html.Should().Contain("รถไฟฟ้า, รถประจำทาง").And.NotContain("l miss\">การเดินทาง");
    }
}
