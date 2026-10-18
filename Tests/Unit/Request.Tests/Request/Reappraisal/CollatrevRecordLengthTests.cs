using System.Text;
using Integration.Contracts.Reappraisal;
using Integration.FileInterface.Format.Reappraisal;

namespace Request.Tests.Request.Reappraisal;

/// <summary>
/// Pins how <see cref="CollatrevFileParser"/> treats detail-record length.
///
/// AS400 appended an Auto Update flag at position 650, taking the record from 649 to 650. Both
/// layouts have to be readable, even in one file, but "both" must not soften into "anything long
/// enough": the original guard was <c>length &lt; 649</c>, which would have accepted the 650-char file
/// silently AND accepted any longer or shifted record, parsing every field from the wrong offset with
/// no error raised.
/// </summary>
public class CollatrevRecordLengthTests
{
    private const int V1Length = 649;
    private const int V2Length = 650;

    private static ParsedReappraisalFile Parse(params string[] detailLines)
    {
        var content = string.Join("\r\n",
            new[] { Header(new DateOnly(2026, 5, 1)) }
                .Concat(detailLines)
                .Append(Trailer(detailLines.Length))) + "\r\n";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new CollatrevFileParser().ParseStream(stream);
    }

    /// <summary>Header is 640 chars: 'H' + DDMMYYYY + filler.</summary>
    private static string Header(DateOnly date) => ("H" + date.ToString("ddMMyyyy")).PadRight(640);

    private static string Trailer(int count) => ("T" + count.ToString("D9")).PadRight(640);

    /// <summary>
    /// A detail line that parses: the fields the parser insists on carry values, everything else is
    /// blank. <paramref name="lastChar"/> fills the final position, which is the Auto Update flag on
    /// the 650 layout and the last digit of EffectiveDateAppraisal (positions 642-649) on 649 — so on a 649 row
    /// leave it blank, or pass <paramref name="effectiveDate"/> (DDMMYYYY) to fill 642-649 with a real date.
    /// </summary>
    private static string Detail(int totalLength, char lastChar = ' ', string? effectiveDate = null)
    {
        var line = new StringBuilder(new string(' ', totalLength));

        line[0] = 'D';
        line[1] = '1';                                    // pos 2       ReviewType
        Put(line, 2, "01052026");                         // pos 3-10    ReviewDate
        Put(line, 10, "1234567890123456789");             // pos 11-29   CollateralId
        Put(line, 29, "68A00001  ");                      // pos 30-39   SurveyNumber
        Put(line, 39, "11A");                             // pos 40-42   CollateralCode
        Put(line, 42, "RE   ");                           // pos 43-47   CollateralCategory
        Put(line, 206, "9876543210987654321");            // pos 207-225 CifNumber
        if (effectiveDate is not null) Put(line, 641, effectiveDate);   // pos 642-649 EffectiveDateAppraisal
        if (effectiveDate is null || totalLength == V2Length) line[^1] = lastChar;

        return line.ToString();

        static void Put(StringBuilder sb, int start, string value)
        {
            for (var i = 0; i < value.Length; i++)
                sb[start + i] = value[i];
        }
    }

    // Positions 642-649 of a 649 row are the EffectiveDateAppraisal, not filler: a real date there must parse as a
    // date, give no flag, and not be mistaken for an unrecognised flag.
    [Fact]
    public void A649RowWithAnEffectiveDateHasNoFlagAndIsNotCountedAsUnrecognised()
    {
        var parsed = Parse(Detail(V1Length, effectiveDate: "28112019"));

        var d = Assert.Single(parsed.Details);
        Assert.Equal(new DateOnly(2019, 11, 28), d.EffectiveDateAppraisal);
        Assert.Null(d.AutoUpdate);
        Assert.False(d.HasAutoUpdatePosition);
        Assert.Equal(0, parsed.UnrecognisedAutoUpdateRows);
    }

    [Theory]
    [InlineData(V1Length)]
    [InlineData(V2Length)]
    public void AcceptsBothPublishedLayouts(int length)
    {
        var parsed = Parse(Detail(length));

        Assert.Single(parsed.Details);
        Assert.Equal("68A00001", parsed.Details[0].SurveyNumber);
    }

    /// <summary>
    /// The extra character on the 650 layout is only the Auto Update flag, so every OTHER field must
    /// come out identical to the 649 layout.
    ///
    /// RowHash is excluded because it hashes the raw line, which legitimately differs between the two
    /// layouts; it exists to detect a changed row on re-ingest, not to compare across layouts.
    /// </summary>
    [Fact]
    public void TheExtraFlagCharacterChangesNothingElse()
    {
        var v1 = Parse(Detail(V1Length)).Details[0];
        var v2 = Parse(Detail(V2Length, lastChar: 'Y')).Details[0];

        Assert.Equal(
            v1 with { RowHash = string.Empty, AutoUpdate = null, HasAutoUpdatePosition = false },
            v2 with { RowHash = string.Empty, AutoUpdate = null, HasAutoUpdatePosition = false });
        Assert.NotEqual(v1.RowHash, v2.RowHash);
    }

    /// <summary>Position 650 is AS400's Auto Update flag: only 'Y'/'N' (any case) are kept.</summary>
    [Theory]
    [InlineData('Y', "Y")]
    [InlineData('N', "N")]
    [InlineData('n', "N")]
    [InlineData('y', "Y")]
    [InlineData(' ', null)]
    [InlineData('X', null)]
    public void ReadsTheAutoUpdateFlagFromPosition650(char flag, string? expected)
    {
        Assert.Equal(expected, Parse(Detail(V2Length, lastChar: flag)).Details[0].AutoUpdate);
    }

    /// <summary>The 649 layout has no flag position at all.</summary>
    [Fact]
    public void The649LayoutHasNoAutoUpdateFlag()
    {
        Assert.Null(Parse(Detail(V1Length)).Details[0].AutoUpdate);
    }

    /// <summary>
    /// Anything that is not one of the two published lengths is a damaged or unknown layout. Reading
    /// it would place every field at the wrong offset while still producing plausible-looking values.
    /// </summary>
    [Theory]
    [InlineData(648)]
    [InlineData(651)]
    [InlineData(700)]
    public void RejectsAnyOtherLength(int length)
    {
        var ex = Assert.Throws<FormatException>(() => Parse(Detail(length)));

        Assert.Contains("649", ex.Message);
        Assert.Contains("650", ex.Message);
    }

    /// <summary>
    /// 649 and 650 may share a file: positions 1-649 are identical, so a 649 row is just a row without
    /// the flag. Each row keeps its own.
    /// </summary>
    [Fact]
    public void AcceptsAFileThatMixesTheTwoLayouts_EachRowKeepingItsOwnFlag()
    {
        var parsed = Parse(Detail(V2Length, lastChar: 'Y'), Detail(V2Length, lastChar: 'N'), Detail(V1Length));

        Assert.Equal(["Y", "N", null], parsed.Details.Select(d => d.AutoUpdate));
    }

    /// <summary>A character at 650 that is not Y/N/blank is read as no flag, and counted for the ingestor's warning.</summary>
    [Theory]
    [InlineData('X', 1, null)]
    [InlineData(' ', 0, null)]
    [InlineData('Y', 0, "Y")]
    public void CountsRowsWhoseFlagIsNeitherYNorBlank(char flag, int expectedCount, string? expectedFlag)
    {
        var parsed = Parse(Detail(V2Length, lastChar: flag), Detail(V1Length));

        Assert.Equal(expectedCount, parsed.UnrecognisedAutoUpdateRows);
        Assert.Equal([expectedFlag, null], parsed.Details.Select(d => d.AutoUpdate));
    }

    /// <summary>
    /// A 650 row with a blank or unrecognised flag and a 649 row both have a null flag, but only the
    /// 650 row has a position 650 — the ingestor tells them apart by it.
    /// </summary>
    [Theory]
    [InlineData(V1Length, ' ', false)]
    [InlineData(V2Length, ' ', true)]
    [InlineData(V2Length, 'X', true)]
    public void RecordsWhetherTheRowHasPosition650(int length, char flag, bool expected)
    {
        var d = Assert.Single(Parse(Detail(length, lastChar: length == V2Length ? flag : ' ')).Details);

        Assert.Null(d.AutoUpdate);
        Assert.Equal(expected, d.HasAutoUpdatePosition);
    }

    /// <summary>
    /// The exemption is for 649 and 650 only: any other length in the same file is still a damaged or
    /// shifted record.
    /// </summary>
    [Fact]
    public void StillRejectsAFileThatMixesA650RowWithAnyOtherLength()
    {
        var ex = Assert.Throws<FormatException>(() => Parse(Detail(V2Length, lastChar: 'Y'), Detail(648)));

        Assert.Contains("648", ex.Message);
    }
}
