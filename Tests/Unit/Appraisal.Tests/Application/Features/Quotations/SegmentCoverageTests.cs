using Appraisal.Application.Features.Quotations.Shared;

namespace Appraisal.Tests.Application.Features.Quotations;

public class SegmentCoverageTests
{
    [Fact]
    public void BuildSegmentSet_DedupesCaseInsensitively_AndDropsBlanks()
    {
        var set = SegmentCoverage.BuildSegmentSet(["Retail", "retail", "IBG", null, " ", "IBG "]);

        Assert.Equal(["Retail", "IBG"], set);
    }

    [Fact]
    public void MissingSegments_CompanyCoversEverySegment_ReturnsEmpty()
    {
        var missing = SegmentCoverage.MissingSegments(["Retail", "IBG"], ["Retail", "IBG"]);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingSegments_ExtraCompanyLoanTypes_AreIgnored()
    {
        var missing = SegmentCoverage.MissingSegments(["Retail", "IBG"], ["Retail", "IBG", "SME"]);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingSegments_CompanyCoversOnlyPartOfUnion_ReturnsTheGap()
    {
        var missing = SegmentCoverage.MissingSegments(["Retail", "IBG"], ["Retail"]);

        Assert.Equal(["IBG"], missing);
    }

    [Fact]
    public void MissingSegments_IsCaseInsensitive()
    {
        var missing = SegmentCoverage.MissingSegments(["Retail", "IBG"], ["RETAIL", "ibg"]);

        Assert.Empty(missing);
    }

    [Fact]
    public void MissingSegments_CompanyWithNoLoanTypes_MissesEverything()
    {
        Assert.Equal(["Retail"], SegmentCoverage.MissingSegments(["Retail"], []));
        Assert.Equal(["Retail"], SegmentCoverage.MissingSegments(["Retail"], null));
    }

    [Fact]
    public void MissingSegments_EmptySegmentSet_AlwaysCovered()
    {
        Assert.Empty(SegmentCoverage.MissingSegments([], []));
        Assert.Empty(SegmentCoverage.MissingSegments([], ["Retail"]));
    }
}
