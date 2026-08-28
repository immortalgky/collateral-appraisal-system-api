using System.Text;
using Integration.Contracts.HostLink;
using Integration.FileInterface.Format.HostLink;

namespace Collateral.Tests.HostLink;

/// <summary>
/// Pins <see cref="HostCollateralLinkFileParser"/> to the 172-char AS400 COLLATLINK layout:
///
///   pos   1     RecordType             'H' | 'D' | 'T'
///   pos   2–11  AppraisalReportNumber  string(10)   (AS400 CCSURV = our AppraisalNumber)
///   pos  12–30  HostCollateralId       dec(19)      (AS400 CCDCID)
///   pos  31–70  CollateralName         string(40)
///   pos  71–110 Address1               string(40)
///   pos 111–118 RecordDate             DDMMYYYY
///   pos 119     RecordIndicator        'D' (drawdown) | 'R' (redeemed)
///   pos 120–125 LocationCode           dec(6)
///   pos 126–128 CollateralCode         string(3)
///   pos 129–131 PropertyType           string(3)
///   pos 132–171 PropertyTypeDesc       string(40)
///   pos 172     MasterTitle            'Y' | 'N'
///
/// Address1 arrived on 2026-08-26 and pushed everything after CollateralName on by 40. The parser
/// reads this layout only — see its own summary for why supporting the previous 132-char layout at
/// the same time is not safe.
///
/// Two properties of this layout are load-bearing and have their own tests below:
///   • CollateralName, Address1 and PropertyTypeDesc carry Thai, so positions are CHARACTER offsets,
///     not byte offsets — a 172-character record is far longer than 172 bytes on disk.
///   • AS400 truncates trailing spaces, so a record can legitimately arrive short. Everything through
///     RecordIndicator (pos 119) is always present; the fields after it are optional.
///
/// The error cases matter as much as the happy path: the job maps FormatException to
/// "quarantine this file" and every other exception to "leave it for retry", so anything
/// that is permanently bad data MUST surface as FormatException rather than leaking a
/// different exception type and being retried forever.
/// </summary>
public class HostCollateralLinkFileParserTests
{
    private const int RecordLength = 172;

    private static string Header(string ddmmyyyy = "01082026")
        => ("H" + ddmmyyyy).PadRight(RecordLength);

    private static string Detail(
        string appraisalNumber = "69000001",
        string hostCollateralId = "12345",
        string ddmmyyyy = "07082026",
        string indicator = "D",
        string collateralName = "",
        string address1 = "",
        string locationCode = "",
        string collateralCode = "",
        string propertyType = "",
        string propertyTypeDesc = "",
        string masterTitle = "Y")
    {
        var line = "D"
                   + appraisalNumber.PadRight(10)
                   + hostCollateralId.PadLeft(19, '0')
                   + collateralName.PadRight(40)
                   + address1.PadRight(40)
                   + ddmmyyyy
                   + indicator
                   + locationCode.PadRight(6)
                   + collateralCode.PadRight(3)
                   + propertyType.PadRight(3)
                   + propertyTypeDesc.PadRight(40)
                   + masterTitle;
        Assert.Equal(RecordLength, line.Length);
        return line;
    }

    private static string Trailer(int count)
        => ("T" + count.ToString("D9")).PadRight(RecordLength);

    private static ParsedHostLinkFile Parse(params string[] lines)
    {
        var content = string.Join("\r\n", lines);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new HostCollateralLinkFileParser().ParseStream(stream);
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void ParseStream_ValidFile_ReadsHeaderDateAndAllFields()
    {
        var parsed = Parse(
            Header("01082026"),
            Detail(appraisalNumber: "69000001", hostCollateralId: "12345",
                   ddmmyyyy: "07082026", indicator: "D"),
            Trailer(1));

        Assert.Equal(new DateOnly(2026, 8, 1), parsed.EffectiveDate);

        var record = Assert.Single(parsed.Records);
        Assert.Equal("69000001", record.AppraisalReportNumber);
        Assert.Equal("12345", record.HostCollateralId);
        Assert.Equal(new DateOnly(2026, 8, 7), record.RecordDate);
        Assert.Equal(HostLinkRecordIndicators.Drawdown, record.RecordIndicator);
        Assert.Equal(64, record.RowHash.Length); // SHA-256 hex
    }

    [Fact]
    public void ParseStream_StripsLeadingZerosFromHostCollateralId()
    {
        // AS400 zero-fills dec(19). The outbound writer re-pads with RightZeroFill, so stripping
        // here round-trips and keeps the stored value human-readable.
        var parsed = Parse(Header(), Detail(hostCollateralId: "42"), Trailer(1));

        Assert.Equal("42", Assert.Single(parsed.Records).HostCollateralId);
    }

    [Fact]
    public void ParseStream_FullWidthHostCollateralId_IsPreserved()
    {
        var nineteenDigits = "1234567890123456789";
        var parsed = Parse(Header(), Detail(hostCollateralId: nineteenDigits), Trailer(1));

        Assert.Equal(nineteenDigits, Assert.Single(parsed.Records).HostCollateralId);
    }

    [Fact]
    public void ParseStream_RedeemedIndicator_IsParsed()
    {
        var parsed = Parse(Header(), Detail(indicator: "R"), Trailer(1));

        var record = Assert.Single(parsed.Records);
        Assert.Equal(HostLinkRecordIndicators.Redeemed, record.RecordIndicator);
    }

    [Fact]
    public void ParseStream_ZeroRecordDate_YieldsNull()
    {
        var parsed = Parse(Header(), Detail(ddmmyyyy: "00000000"), Trailer(1));

        Assert.Null(Assert.Single(parsed.Records).RecordDate);
    }

    [Fact]
    public void ParseStream_MultipleDetails_AreAllReturnedInOrder()
    {
        var parsed = Parse(
            Header(),
            Detail(appraisalNumber: "69000001", hostCollateralId: "111"),
            Detail(appraisalNumber: "69000002", hostCollateralId: "222"),
            Detail(appraisalNumber: "69000003", hostCollateralId: "333"),
            Trailer(3));

        Assert.Equal(3, parsed.Records.Count);
        Assert.Equal(["69000001", "69000002", "69000003"],
            parsed.Records.Select(r => r.AppraisalReportNumber));
        Assert.Equal(["111", "222", "333"],
            parsed.Records.Select(r => r.HostCollateralId));
    }

    [Fact]
    public void ParseStream_IdenticalLines_ProduceIdenticalRowHash_DifferentLinesDoNot()
    {
        // RowHash is what lets re-ingest skip unchanged rows, so it must be stable per line
        // and sensitive to any change.
        var a = Parse(Header(), Detail(hostCollateralId: "111"), Trailer(1)).Records[0];
        var again = Parse(Header(), Detail(hostCollateralId: "111"), Trailer(1)).Records[0];
        var different = Parse(Header(), Detail(hostCollateralId: "222"), Trailer(1)).Records[0];

        Assert.Equal(a.RowHash, again.RowHash);
        Assert.NotEqual(a.RowHash, different.RowHash);
    }

    // ── Permanently-bad data must be FormatException (⇒ quarantine, not retry) ─

    [Fact]
    public void ParseStream_DetailShorterThanRecordLength_Throws()
    {
        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), "D69000001", Trailer(1)));

        Assert.Contains("needs at least 119", ex.Message);
    }

    [Fact]
    public void ParseStream_TrailerCountMismatch_Throws()
    {
        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), Detail(), Trailer(5)));

        Assert.Contains("Trailer count mismatch", ex.Message);
    }

    [Fact]
    public void ParseStream_MissingTrailer_Throws()
    {
        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), Detail()));

        Assert.Contains("no Trailer", ex.Message);
    }

    [Fact]
    public void ParseStream_NonNumericTrailerCount_Throws()
    {
        var badTrailer = ("T" + "ABCDEFGHI").PadRight(RecordLength);

        Assert.Throws<FormatException>(() => Parse(Header(), Detail(), badTrailer));
    }

    [Theory]
    [InlineData("X")]
    [InlineData(" ")]
    [InlineData("1")]
    public void ParseStream_UnrecognisedIndicator_Throws(string indicator)
    {
        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), Detail(indicator: indicator), Trailer(1)));

        Assert.Contains("RecordIndicator", ex.Message);
    }

    [Fact]
    public void ParseStream_BlankAppraisalNumber_Throws()
    {
        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), Detail(appraisalNumber: "          "), Trailer(1)));

        Assert.Contains("AppraisalReportNumber", ex.Message);
    }

    [Fact]
    public void ParseStream_ZeroHostCollateralId_IsSkippedWithoutDiscardingTheFile()
    {
        // All-zeros means AS400 sent no id — a link row with no id is meaningless, and silently
        // storing "" would make the export emit a blank CCDCID against a real appraisal. So the row
        // must not be linked. It must NOT, however, cost us the rest of the file: a single zero row
        // used to throw, and the job quarantines the whole nightly feed on FormatException.
        var file = Parse(
            Header(),
            Detail(appraisalNumber: "69000001", hostCollateralId: "0"),
            Detail(appraisalNumber: "69000002", hostCollateralId: "12345"),
            Trailer(2));

        var record = Assert.Single(file.Records);
        Assert.Equal("69000002", record.AppraisalReportNumber);
        Assert.Equal("12345", record.HostCollateralId);
    }

    [Fact]
    public void ParseStream_BlankHostCollateralId_Throws()
    {
        // An all-spaces field is malformed rather than "no id" (AS400 sends a zero-filled dec(19)),
        // so it still fails the file. Built by hand: the Detail helper zero-pads, which would turn
        // spaces into a zero id and exercise the skip path instead.
        var blankIdLine = ("D" + "69000001".PadRight(10) + new string(' ', 19)
                           + new string(' ', 40) + new string(' ', 40)
                           + "07082026" + "D").PadRight(RecordLength);

        var ex = Assert.Throws<FormatException>(() =>
            Parse(Header(), blankIdLine, Trailer(1)));

        Assert.Contains("HostCollateralId", ex.Message);
    }

    [Fact]
    public void ParseStream_OutOfRangeHeaderDate_ThrowsFormatNotArgumentOutOfRange()
    {
        // dd=32 is syntactically fine but not a real date; it must not leak
        // ArgumentOutOfRangeException, which the job would treat as transient and retry forever.
        Assert.Throws<FormatException>(() =>
            Parse(Header("32012026"), Detail(), Trailer(1)));
    }

    [Fact]
    public void ParseStream_IndicatorIsCaseInsensitive()
    {
        var parsed = Parse(Header(), Detail(indicator: "r"), Trailer(1));

        Assert.Equal(HostLinkRecordIndicators.Redeemed, Assert.Single(parsed.Records).RecordIndicator);
    }

    // ── The 172-char layout's two load-bearing properties ────────────────────

    [Fact]
    public void ParseStream_ReadsEveryTailField()
    {
        var parsed = Parse(
            Header(),
            Detail(collateralName: "ฉ.212567", address1: "129/517 โครงการเพอร์เฟคเพลส",
                   locationCode: "120110", collateralCode: "114",
                   propertyType: "PSH", propertyTypeDesc: "บ้านเดี่ยว (SINGLE HOUSE)", masterTitle: "Y"),
            Trailer(1));

        var r = Assert.Single(parsed.Records);
        Assert.Equal("ฉ.212567", r.CollateralName);
        Assert.Equal("129/517 โครงการเพอร์เฟคเพลส", r.Address1);
        Assert.Equal("120110", r.LocationCode);
        Assert.Equal("114", r.CollateralCode);
        Assert.Equal("PSH", r.PropertyType);
        Assert.Equal("บ้านเดี่ยว (SINGLE HOUSE)", r.PropertyTypeDesc);
        Assert.Equal("Y", r.MasterTitle);
    }

    /// <summary>
    /// Address1 is the field the regulatory export takes a block-project unit key from, and it sits
    /// between CollateralName and RecordDate. If its 40 characters were ever dropped or double-counted
    /// the date would be read out of the middle of an address and every field after it would shift, so
    /// this pins the boundary on both sides at once.
    /// </summary>
    [Fact]
    public void ParseStream_Address1_DoesNotDisturbTheFieldsEitherSideOfIt()
    {
        var r = Assert.Single(Parse(
            Header(),
            Detail(collateralName: "ฉ.212567", address1: "129/517 โครงการเพอร์เฟคเพลส",
                   ddmmyyyy: "07082026", indicator: "R"),
            Trailer(1)).Records);

        Assert.Equal("ฉ.212567", r.CollateralName);
        Assert.Equal("129/517 โครงการเพอร์เฟคเพลส", r.Address1);
        Assert.Equal(new DateOnly(2026, 8, 7), r.RecordDate);
        Assert.Equal(HostLinkRecordIndicators.Redeemed, r.RecordIndicator);
    }

    /// <summary>
    /// 2,030 rows of the 2026-08-03 feed carry no address. Blank must become NULL, not an empty
    /// string: the export tests the token for emptiness before matching, and "" would otherwise
    /// match any unit whose room number is blank.
    /// </summary>
    [Fact]
    public void ParseStream_BlankAddress1_YieldsNull()
    {
        var r = Assert.Single(Parse(Header(), Detail(address1: ""), Trailer(1)).Records);

        Assert.Null(r.Address1);
    }

    /// <summary>
    /// Thai is three BYTES per character in UTF-8, so a record whose name, address and description
    /// columns are full of it runs well past 172 bytes while still being exactly 172 characters. Every
    /// field after the name must still land, which only holds if the parser slices the decoded string
    /// rather than the raw bytes. Getting this wrong shifts every field from RecordDate onwards.
    /// </summary>
    [Fact]
    public void ParseStream_ThaiText_DoesNotShiftLaterFields()
    {
        var line = Detail(collateralName: "โฉนดที่ดินเลขที่ ๑๒๓๔๕๖",
                          address1: "๙๙/๑ หมู่บ้านทดสอบ ซอยตัวอย่าง",
                          ddmmyyyy: "07082026", indicator: "R",
                          locationCode: "105002", collateralCode: "14A", propertyType: "PTH",
                          propertyTypeDesc: "ทาวน์เฮ้าส์ (TOWN HOUSE)", masterTitle: "N");

        Assert.Equal(172, line.Length);
        Assert.True(Encoding.UTF8.GetByteCount(line) > 172, "the fixture must actually be multi-byte");

        var r = Assert.Single(Parse(Header(), line, Trailer(1)).Records);
        Assert.Equal(new DateOnly(2026, 8, 7), r.RecordDate);
        Assert.Equal(HostLinkRecordIndicators.Redeemed, r.RecordIndicator);
        Assert.Equal("105002", r.LocationCode);
        Assert.Equal("14A", r.CollateralCode);
        Assert.Equal("PTH", r.PropertyType);
        Assert.Equal("N", r.MasterTitle);
    }

    /// <summary>
    /// AS400 strips trailing spaces: 1,516 rows of the 2026-08-03 feed stop short of pos 172, the
    /// shortest at 128. Everything through RecordIndicator is still there, so the row must parse with
    /// the tail fields null rather than failing the whole file.
    /// </summary>
    [Theory]
    [InlineData(128)]
    [InlineData(142)]
    [InlineData(119)]
    public void ParseStream_TruncatedRow_ParsesWithNullTailFields(int length)
    {
        var truncated = Detail(propertyType: "PSH", masterTitle: "Y")[..length];

        var r = Assert.Single(Parse(Header(), truncated, Trailer(1)).Records);
        Assert.Equal("69000001", r.AppraisalReportNumber);
        Assert.Equal("12345", r.HostCollateralId);
        Assert.Equal(HostLinkRecordIndicators.Drawdown, r.RecordIndicator);
        Assert.Null(r.PropertyTypeDesc);
        // A row that never reaches pos 172 stated nothing at all. That is NOT the same as 'N', which
        // the regulatory export does report — only the unstated ones are dropped.
        Assert.Null(r.MasterTitle);
    }

    /// <summary>
    /// A full record of the PREVIOUS 132-char layout must not parse quietly. Read with the new
    /// offsets its pos 119 falls inside PropertyTypeDesc, so the indicator check rejects it and the
    /// job quarantines the file — which is the whole reason this parser does not try to support both
    /// layouts at once. Silence here would mean writing a date and a property type sliced out of the
    /// middle of a Thai description.
    /// </summary>
    [Fact]
    public void ParseStream_OldLayoutRecord_IsRejectedRatherThanMisread()
    {
        var oldLayout = "D" + "69000001".PadRight(10) + "12345".PadLeft(19, '0')
                        + "ฉ.212567".PadRight(40)
                        + "07082026" + "D" + "120110" + "114" + "PSH"
                        + "บ้านเดี่ยว (SINGLE HOUSE)".PadRight(40) + "Y";
        Assert.Equal(132, oldLayout.Length);

        var ex = Assert.Throws<FormatException>(() => Parse(Header(), oldLayout, Trailer(1)));

        Assert.Contains("RecordIndicator", ex.Message);
    }

    /// <summary>
    /// Kept raw, and upper-cased. A blank becomes NULL rather than "N": the export reports collateral
    /// flagged 'N' but not one the feed never stated a flag for, so the two must stay distinguishable.
    /// </summary>
    [Theory]
    [InlineData("N", "N")]
    [InlineData("Y", "Y")]
    [InlineData("y", "Y")]
    [InlineData(" ", null)]
    public void ParseStream_MasterTitleFlag_IsStoredRaw(string flag, string? expected)
    {
        var r = Assert.Single(Parse(Header(), Detail(masterTitle: flag), Trailer(1)).Records);

        Assert.Equal(expected, r.MasterTitle);
    }

    // ── Filename date ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("AS400_COLLATLINK_20260807.txt", 2026, 8, 7)]
    [InlineData("AS400_COLLATLINK_20251231.txt", 2025, 12, 31)]
    public void ParseFilenameDate_ValidName_ReturnsDate(string fileName, int y, int m, int d)
    {
        Assert.Equal(new DateOnly(y, m, d), HostCollateralLinkFileParser.ParseFilenameDate(fileName));
    }

    [Theory]
    [InlineData("AS400_COLLATLINK.txt")]          // no date part
    [InlineData("AS400_COLLATLINK_2026087.txt")]  // wrong length
    [InlineData("AS400_COLLATLINK_20261307.txt")] // month 13
    [InlineData("nonsense.txt")]
    public void ParseFilenameDate_InvalidName_ReturnsNull(string fileName)
    {
        Assert.Null(HostCollateralLinkFileParser.ParseFilenameDate(fileName));
    }
}
