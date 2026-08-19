using System.Security.Cryptography;
using System.Text;
using Integration.Contracts.FixedWidth;
using Integration.Contracts.HostLink;

namespace Integration.FileInterface.Format.HostLink;

/// <summary>
/// Parses the AS400 COLLATLINK inbound interface — a <b>fixed-width</b> (39-char) UTF-8 text file
/// with Header (H) / Detail (D) / Trailer (T) records. Delivered nightly.
///
///   Header:  pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), pos 10–39 = filler.
///   Detail:  pos 1 = 'D', 39 chars total:
///              pos  2–11  AppraisalReportNumber  string(10)   (AS400 CCSURV)
///              pos 12–30  HostCollateralId       dec(19)      (AS400 CCDCID)
///              pos 31–38  RecordDate             DDMMYYYY
///              pos 39     RecordIndicator        'D' | 'R'
///   Trailer: pos 1 = 'T', pos 2–10 = TotalDetailRecord (dec9), pos 11–39 = filler.
///
/// IMPORTANT — char vs byte positions: this record carries no Thai text, but we index by
/// Unicode code-point position (string indexer) for consistency with the other parsers.
/// </summary>
public class HostCollateralLinkFileParser
{
    private const int DetailRecordLength = 39;

    public ParsedHostLinkFile ParseStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return ParseLines(lines);
    }

    private static ParsedHostLinkFile ParseLines(List<string> lines)
    {
        var effectiveDate = DateOnly.MinValue;
        var records = new List<ParsedHostLinkRecord>();
        var expectedCount = -1;
        var sawTrailer = false;
        // Detail rows read but deliberately not linked (zero HostCollateralId). Counted so the
        // trailer completeness check still balances.
        var skippedCount = 0;

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            switch (line[0])
            {
                case 'H':
                    effectiveDate = ParseDdmmyyyy(Slice(line, 1, 8), "Header.EffectiveDate");
                    break;

                case 'D':
                    if (line.Length < DetailRecordLength)
                        throw new FormatException(
                            $"Detail record is {line.Length} chars (expected {DetailRecordLength}). " +
                            $"First 40 chars: '{line[..Math.Min(40, line.Length)]}'");
                    var record = ParseDetailLine(line);
                    if (record is null) skippedCount++;
                    else records.Add(record);
                    break;

                case 'T':
                    var countStr = Slice(line, 1, 9).Trim();
                    if (!int.TryParse(countStr, out var c))
                        throw new FormatException(
                            $"Trailer record has a non-numeric total-record count: '{countStr}'.");
                    expectedCount = c;
                    sawTrailer = true;
                    break;
            }
        }

        if (!sawTrailer)
            throw new FormatException("File has no Trailer (T) record — cannot validate completeness.");
        if (expectedCount != records.Count + skippedCount)
            throw new FormatException(
                $"Trailer count mismatch: file says {expectedCount} detail records, parsed " +
                $"{records.Count + skippedCount} ({records.Count} linkable, {skippedCount} skipped " +
                "for a zero HostCollateralId).");

        return new ParsedHostLinkFile(effectiveDate, records);
    }

    /// <summary>
    /// Parses one detail record, or returns null when the row carries a zero HostCollateralId —
    /// AS400 emits all-zeros for a collateral it has not issued a CCDCID for (e.g. a row already
    /// closed out). Such a row has nothing to link, so it is skipped rather than failing the whole
    /// file; the caller still counts it toward the trailer total. A genuinely blank (all-spaces)
    /// field is malformed and still throws.
    /// </summary>
    private static ParsedHostLinkRecord? ParseDetailLine(string line)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(line)));

        var appraisalNumber = Slice(line, 1, 10).Trim();
        if (appraisalNumber.Length == 0)
            throw new FormatException(
                $"Detail record has a blank AppraisalReportNumber. First 40 chars: '{line[..Math.Min(40, line.Length)]}'");

        // dec(19), typically zero-filled. Strip leading zeros so the value matches what the
        // outbound writer emits (RightZeroFill re-pads on the way out) and what a human sees.
        var rawHostCollateralId = Slice(line, 11, 19).Trim();
        if (rawHostCollateralId.Length == 0)
            throw new FormatException(
                $"Detail record for appraisal '{appraisalNumber}' has a blank HostCollateralId.");

        var hostCollateralId = rawHostCollateralId.TrimStart('0');
        // All zeros — AS400 has no CCDCID for this collateral. Nothing to link; skip the row
        // instead of quarantining the entire nightly file over it.
        if (hostCollateralId.Length == 0)
            return null;

        var indicator = Slice(line, 38, 1).Trim().ToUpperInvariant();
        if (indicator != HostLinkRecordIndicators.Drawdown &&
            indicator != HostLinkRecordIndicators.Redeemed)
            throw new FormatException(
                $"Detail record for appraisal '{appraisalNumber}' has an unrecognised RecordIndicator " +
                $"'{indicator}' (expected '{HostLinkRecordIndicators.Drawdown}' or " +
                $"'{HostLinkRecordIndicators.Redeemed}').");

        return new ParsedHostLinkRecord(
            AppraisalReportNumber: appraisalNumber,
            HostCollateralId: hostCollateralId,
            RecordDate: ParseDdmmyyyyOrNull(Slice(line, 30, 8)),
            RecordIndicator: indicator,
            RowHash: hash);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Lenient, char-indexed column read. Delegates to the shared
    /// <see cref="FixedWidthRecordReader.Slice"/> rather than carrying a private copy — its own doc
    /// notes it mirrors <c>CollatrevFileParser.Slice</c> exactly so a refactor is drop-in.
    /// </summary>
    private static string Slice(string line, int start, int length)
        => FixedWidthRecordReader.Slice(line, start, length);

    private static DateOnly ParseDdmmyyyy(string s, string fieldName)
        => FixedWidthDateParser.ParseDdmmyyyy(s, fieldName);

    private static DateOnly? ParseDdmmyyyyOrNull(string s)
        => FixedWidthDateParser.ParseDdmmyyyyOrNull(s);

    // ── Filename parser ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses the file date from a COLLATLINK filename like <c>AS400_COLLATLINK_20260807.txt</c>.
    /// Returns null if the filename does not match the expected pattern.
    /// </summary>
    public static DateOnly? ParseFilenameDate(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var parts = name.Split('_');
        if (parts.Length < 3) return null;

        var datePart = parts[^1];
        if (datePart.Length != 8) return null;

        if (!int.TryParse(datePart[..4], out var yyyy) ||
            !int.TryParse(datePart[4..6], out var mm) ||
            !int.TryParse(datePart[6..8], out var dd))
            return null;

        try { return new DateOnly(yyyy, mm, dd); }
        catch { return null; }
    }
}
