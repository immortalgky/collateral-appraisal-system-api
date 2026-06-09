using System.Text;
using Request.Infrastructure.Reappraisal;

namespace Request.Tests.Request.Reappraisal;

/// <summary>
/// Pins <see cref="CollatrevFileWriter"/> to <see cref="CollatrevFileParser"/>: a record written by the
/// writer must parse back to the same field values. This is the drift guard that lets the writer carry
/// its own field-layout table without re-deriving the parser's substring offsets.
/// </summary>
public class CollatrevWriterRoundTripTests
{
    private static string BuildFile(CollatrevFileWriter writer, DateOnly effective,
        params IReadOnlyDictionary<string, string?>[] rows)
    {
        var lines = new List<string> { writer.BuildHeader(effective) };
        lines.AddRange(rows.Select(writer.BuildDetail));
        lines.Add(writer.BuildTrailer(rows.Length));
        return string.Join("\r\n", lines) + "\r\n";
    }

    [Fact]
    public void WriterOutput_RoundTrips_ThroughParser()
    {
        var writer = new CollatrevFileWriter();
        var effective = new DateOnly(2026, 6, 1);

        var row = new Dictionary<string, string?>
        {
            ["ReviewType"] = "1",
            ["ReviewDate"] = "01122026",
            ["CollateralId"] = "60000001",
            ["SurveyNo"] = "68A000001",
            ["CollateralName"] = "Sample Land Plot",
            ["CifName"] = "Somchai Jaidee",
            ["TitleNo"] = "NS3K-1827",
            ["CurrentValue"] = "4500000.00",
            ["ValuationDate"] = "28112019",
            ["MortgageAmount"] = "3600000.00",
            ["IBGRetail"] = "Retail",
            ["Stage"] = "1",
            ["Group"] = "1",
            ["EffectiveDateAppraisal"] = "28112019",
        };

        var header = writer.BuildHeader(effective);
        var detail = writer.BuildDetail(row);

        Assert.Equal(CollatrevFileWriter.HeaderTrailerLength, header.Length);
        Assert.Equal(CollatrevFileWriter.DetailRecordLength, detail.Length);

        var content = BuildFile(writer, effective, row);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var parsed = new CollatrevFileParser().ParseStream(ms);

        Assert.Equal(effective, parsed.EffectiveDate);
        var d = Assert.Single(parsed.Details);
        Assert.Equal("1", d.ReviewType);
        Assert.Equal(new DateOnly(2026, 12, 1), d.ReviewDate);
        Assert.Equal("60000001", d.CollateralId);
        Assert.Equal("68A000001", d.SurveyNumber);
        Assert.Equal("Sample Land Plot", d.CollateralName);
        Assert.Equal("Somchai Jaidee", d.CifName);
        Assert.Equal("NS3K-1827", d.TitleNumber);
        Assert.Equal(4500000.00m, d.CurrentValue);
        Assert.Equal(new DateOnly(2019, 11, 28), d.ValuationDate);
        Assert.Equal(3600000.00m, d.MortgageAmount);
        Assert.Equal("Retail", d.IBGRetail);
        Assert.Equal("1", d.Stage);
        Assert.Equal("1", d.Group);
        Assert.Equal(new DateOnly(2019, 11, 28), d.EffectiveDateAppraisal);
    }

    [Fact]
    public void WriterOutput_PreservesThaiText_AsCharacters()
    {
        // Thai chars are multibyte in UTF-8 but counted as one position by both writer (PadRight on
        // string length) and parser (char-index slicing). The round-trip proves the contract holds.
        var writer = new CollatrevFileWriter();
        var effective = new DateOnly(2026, 6, 1);

        var row = new Dictionary<string, string?>
        {
            ["ReviewType"] = "2",
            ["ReviewDate"] = "15012025",
            ["CollateralId"] = "60000002",
            ["SurveyNo"] = "68A000002",
            ["CollateralName"] = "คอนโดสุขุมวิท ชั้น 8",
            ["CifName"] = "วิภา ใจดี",
            ["Stage"] = "2",
        };

        var content = BuildFile(writer, effective, row);
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var parsed = new CollatrevFileParser().ParseStream(ms);

        var d = Assert.Single(parsed.Details);
        Assert.Equal("คอนโดสุขุมวิท ชั้น 8", d.CollateralName);
        Assert.Equal("วิภา ใจดี", d.CifName);
        Assert.Equal("68A000002", d.SurveyNumber);
    }

    [Fact]
    public void TrailerCount_MatchesEmittedDetailRows()
    {
        var writer = new CollatrevFileWriter();
        var effective = new DateOnly(2026, 6, 1);

        IReadOnlyDictionary<string, string?> Row(int i) => new Dictionary<string, string?>
        {
            ["ReviewType"] = "1",
            ["ReviewDate"] = "01122026",
            ["CollateralId"] = (60000000 + i).ToString(),
            ["SurveyNo"] = $"68A00000{i}",
        };

        var content = BuildFile(writer, effective, Row(1), Row(2), Row(3));
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(content));

        var parsed = new CollatrevFileParser().ParseStream(ms);

        Assert.Equal(3, parsed.Details.Count);
    }
}
