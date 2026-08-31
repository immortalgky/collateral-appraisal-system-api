using System.Text;
using Integration.Contracts.Reappraisal;
using Integration.FileInterface.Format.Reappraisal;

namespace Request.Tests.Request.Reappraisal;

/// <summary>
/// Pins how <see cref="CollatrevFileParser"/> treats detail-record length.
///
/// AS400 appended an Auto Update flag at position 650, taking the record from 649 to 650. Both
/// layouts have to be readable during the cut-over, but "both" must not soften into "anything long
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
    /// the 650 layout and part of the trailing filler on 649.
    /// </summary>
    private static string Detail(int totalLength, char lastChar = ' ')
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
        line[^1] = lastChar;

        return line.ToString();

        static void Put(StringBuilder sb, int start, string value)
        {
            for (var i = 0; i < value.Length; i++)
                sb[start + i] = value[i];
        }
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
    /// The extra character on the 650 layout is AS400's own Auto Update flag. We deliberately do not
    /// read it — whether a result may be applied automatically is decided by whether we can tie the
    /// appraisal to exactly one collateral — so every field we DO read must come out identical.
    ///
    /// RowHash is excluded because it hashes the raw line, which legitimately differs between the two
    /// layouts; it exists to detect a changed row on re-ingest, not to compare across layouts.
    /// </summary>
    [Fact]
    public void TheExtraFlagCharacterChangesNothingWeRead()
    {
        var v1 = Parse(Detail(V1Length)).Details[0];
        var v2 = Parse(Detail(V2Length, lastChar: 'Y')).Details[0];

        Assert.Equal(v1 with { RowHash = string.Empty }, v2 with { RowHash = string.Empty });
        Assert.NotEqual(v1.RowHash, v2.RowHash);
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
    /// One file is one layout. Mixed lengths mean damage in transit, not a mid-file spec change — and
    /// the shorter rows would read as complete records that quietly lost their trailing fields.
    /// </summary>
    [Fact]
    public void RejectsAFileThatMixesTheTwoLayouts()
    {
        var ex = Assert.Throws<FormatException>(() => Parse(Detail(V1Length), Detail(V2Length)));

        Assert.Contains("mixed lengths", ex.Message);
    }
}
