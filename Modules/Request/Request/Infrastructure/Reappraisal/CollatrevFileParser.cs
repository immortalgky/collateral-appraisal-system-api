using System.Security.Cryptography;
using System.Text;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// Parses the AS400 COLLATREV inbound interface — a **fixed-width** (660-char Detail) UTF-8 text file
/// with Header (H) / Detail (D) / Trailer (T) records.
///
///   Header:  pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), pos 10–640 = filler.
///   Detail:  pos 1 = 'D', 640 chars total; field map documented in the constants below.
///   Trailer: pos 1 = 'T', pos 2–10 = TotalDetailRecord (dec9), pos 11–640 = filler.
///
/// IMPORTANT — char vs byte positions:
///   The spec uses string(N) **character** widths. Files are UTF-8 with Thai text in
///   Name/Address/Description fields. We index by Unicode code-point position
///   (string indexer), NOT by byte offset. Thai characters are multibyte in UTF-8 but
///   counted as 1 character position in .NET strings after UTF-8 decoding.
/// </summary>
public class CollatrevFileParser
{
    private const int DetailRecordLength = 660;

    // Detail record field offsets (0-based start in the decoded line + char length).
    // Maps to the 1-based positions in the vendor spec: pos N (1-based) = start N-1 (0-based).
    //   pos 1     → RecordType (1 char, 'D')
    //   pos 2     → ReviewType (1 char)
    //   pos 3–10  → ReviewDate (8 chars, DDMMYYYY)
    //   pos 11–29 → CollateralId (19 chars, numeric string)
    //   pos 30–39 → SurveyNumber (10 chars; = our AppraisalNumber)
    //   pos 40–42 → CollateralCode (3 chars)
    //   pos 43–45 → CollateralCategory (3 chars)
    //   pos 46–75 → CollateralName (30 chars)
    //   pos 76–175 → CollateralAddress (100 chars)
    //   pos 176–194 → CifNumber (19 chars)
    //   pos 195–214 → CifName (20 chars)
    //   pos 215–224 → AoCode (10 chars)
    //   pos 225–264 → AoName (40 chars)
    //   pos 265–284 → TitleNumber (20 chars)
    //   pos 285–300 → CurrentValue (16 chars, dec15,2)
    //   pos 301–308 → ValuationDate (8 chars, DDMMYYYY)
    //   pos 309    → InternalExternal (1 char)
    //   pos 310    → BusinessSize (1 char)
    //   pos 311–350 → BusinessSizeDesc (40 chars)
    //   pos 351–366 → MortgageAmount (16 chars, dec15,2)
    //   pos 367–371 → PastDueDay (5 chars, dec5)
    //   pos 372–390 → ApplicationNumber (19 chars)
    //   pos 391–393 → FacilityCode (3 chars)
    //   pos 394–412 → FacilitySequence (19 chars)
    //   pos 413–428 → CpNumber (16 chars)
    //   pos 429–431 → CarCode (3 chars)
    //   pos 432–447 → FacilityLimit (16 chars, dec15,2)
    //   pos 448    → FlagLessAge4Y (1 char)
    //   pos 449    → FlagGreaterAge4Y (1 char)
    //   pos 450–459 → CountAgeingDate (10 chars)
    //   pos 460–509 → CollateralDescription (50 chars)
    //   pos 510–549 → ExternalValuerName (40 chars)
    //   pos 550–589 → InternalValuerName (40 chars)
    //   pos 590    → SllOver100M (1 char)
    //   pos 591–640 → SllDescription (50 chars)
    //   pos 641     → Stage (1 char) — CIF stage indicator
    //   pos 642–651 → IBGRetail (10 chars) — banking segment (RB / IBG / …)
    //   pos 652     → Group (1 char) — review group code 1/2/3
    //   pos 653–660 → EffectiveDateAppraisal (8 chars, DDMMYYYY)

    public ParsedReappraisalFile ParseStream(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var lines = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);
        return ParseLines(lines);
    }

    private ParsedReappraisalFile ParseLines(List<string> lines)
    {
        DateOnly effectiveDate = DateOnly.MinValue;
        var details = new List<ParsedDetailRecord>();
        int expectedCount = -1;
        var sawTrailer = false;

        foreach (var line in lines)
        {
            if (line.Length == 0) continue;

            var type = line[0];
            switch (type)
            {
                case 'H':
                    effectiveDate = ParseDdmmyyyy(Slice(line, 1, 8), "Header.EffectiveDate");
                    break;

                case 'D':
                    if (line.Length < DetailRecordLength)
                        throw new FormatException(
                            $"Detail record is {line.Length} chars (expected {DetailRecordLength}). " +
                            $"First 40 chars: '{line[..Math.Min(40, line.Length)]}'");
                    details.Add(ParseDetailLine(line));
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

        // The trailer is the file-integrity check: it must be present and match the parsed rows.
        if (!sawTrailer)
            throw new FormatException("File has no Trailer (T) record — cannot validate completeness.");
        if (expectedCount != details.Count)
            throw new FormatException(
                $"Trailer count mismatch: file says {expectedCount} detail records, parsed {details.Count}.");

        return new ParsedReappraisalFile(effectiveDate, details);
    }

    private static ParsedDetailRecord ParseDetailLine(string line)
    {
        // SHA-256 of the raw 640-char line for dedup (RowHash).
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(line)));

        return new ParsedDetailRecord(
            RowHash: hash,
            ReviewType: Slice(line, 1, 1).Trim(),
            ReviewDate: ParseDdmmyyyy(Slice(line, 2, 8), "ReviewDate"),
            CollateralId: Slice(line, 10, 19).Trim(),
            SurveyNumber: Slice(line, 29, 10).Trim(),
            CollateralCode: Slice(line, 39, 3).Trim(),
            CollateralCategory: Slice(line, 42, 3).Trim(),
            CollateralName: TrimOrNull(Slice(line, 45, 30)),
            CollateralAddress: TrimOrNull(Slice(line, 75, 100)),
            CifNumber: Slice(line, 175, 19).Trim(),
            CifName: TrimOrNull(Slice(line, 194, 20)),
            AoCode: TrimOrNull(Slice(line, 214, 10)),
            AoName: TrimOrNull(Slice(line, 224, 40)),
            TitleNumber: TrimOrNull(Slice(line, 264, 20)),
            CurrentValue: ParseDecimalOrNull(Slice(line, 284, 16)),
            ValuationDate: ParseDdmmyyyyOrNull(Slice(line, 300, 8)),
            InternalExternal: TrimOrNull(Slice(line, 308, 1)),
            BusinessSize: TrimOrNull(Slice(line, 309, 1)),
            BusinessSizeDesc: TrimOrNull(Slice(line, 310, 40)),
            MortgageAmount: ParseDecimalOrNull(Slice(line, 350, 16)),
            PastDueDay: ParseIntOrNull(Slice(line, 366, 5)),
            ApplicationNumber: TrimOrNull(Slice(line, 371, 19)),
            FacilityCode: TrimOrNull(Slice(line, 390, 3)),
            FacilitySequence: TrimOrNull(Slice(line, 393, 19)),
            CpNumber: TrimOrNull(Slice(line, 412, 16)),
            CarCode: TrimOrNull(Slice(line, 428, 3)),
            FacilityLimit: ParseDecimalOrNull(Slice(line, 431, 16)),
            FlagLessAge4Y: TrimOrNull(Slice(line, 447, 1)),
            FlagGreaterAge4Y: TrimOrNull(Slice(line, 448, 1)),
            CountAgeingDate: TrimOrNull(Slice(line, 449, 10)),
            CollateralDescription: TrimOrNull(Slice(line, 459, 50)),
            ExternalValuerName: TrimOrNull(Slice(line, 509, 40)),
            InternalValuerName: TrimOrNull(Slice(line, 549, 40)),
            SllOver100M: TrimOrNull(Slice(line, 589, 1)),
            SllDescription: TrimOrNull(Slice(line, 590, 50)),
            Stage: TrimOrNull(Slice(line, 640, 1)),
            IBGRetail: TrimOrNull(Slice(line, 641, 10)),
            Group: TrimOrNull(Slice(line, 651, 1)),
            EffectiveDateAppraisal: ParseDdmmyyyyOrNull(Slice(line, 652, 8))
        );
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a substring using 0-based start and explicit length.
    /// Pads with spaces when the line is shorter than expected (lenient parsing).
    /// </summary>
    private static string Slice(string line, int start, int length)
    {
        if (start >= line.Length) return new string(' ', length);
        var available = Math.Min(length, line.Length - start);
        var result = line.Substring(start, available);
        if (available < length)
            result = result.PadRight(length);
        return result;
    }

    private static string? TrimOrNull(string s)
    {
        var t = s.Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>Parses a DDMMYYYY date string. Throws <see cref="FormatException"/> on invalid input.</summary>
    private static DateOnly ParseDdmmyyyy(string s, string fieldName)
    {
        var v = s.Trim();
        if (v.Length < 8 || v == "00000000")
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        if (!int.TryParse(v[..2], out var dd) ||
            !int.TryParse(v[2..4], out var mm) ||
            !int.TryParse(v[4..8], out var yyyy))
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        return new DateOnly(yyyy, mm, dd);
    }

    private static DateOnly? ParseDdmmyyyyOrNull(string s)
    {
        var v = s.Trim();
        if (v.Length < 8 || v == "00000000" || v.All(c => c == ' '))
            return null;

        if (!int.TryParse(v[..2], out var dd) ||
            !int.TryParse(v[2..4], out var mm) ||
            !int.TryParse(v[4..8], out var yyyy) ||
            yyyy == 0)
            return null;

        try { return new DateOnly(yyyy, mm, dd); }
        catch { return null; }
    }

    private static decimal? ParseDecimalOrNull(string s)
    {
        var v = s.Trim();
        if (string.IsNullOrWhiteSpace(v) || v.All(c => c == '0' || c == ' '))
            return null;
        return decimal.TryParse(v, out var d) ? d : null;
    }

    private static int? ParseIntOrNull(string s)
    {
        var v = s.Trim();
        if (string.IsNullOrWhiteSpace(v)) return null;
        return int.TryParse(v, out var i) ? i : null;
    }

    // ── Filename parser ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses the file date from a COLLATREV filename like <c>AS400_COLLATREV_20260501.txt</c>.
    /// Returns null if the filename does not match the expected pattern.
    /// </summary>
    public static DateOnly? ParseFilenameDate(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName);
        var parts = name.Split('_');
        if (parts.Length < 3) return null;

        var datePart = parts[^1]; // last segment, expected YYYYMMDD
        if (datePart.Length != 8) return null;

        if (!int.TryParse(datePart[..4], out var yyyy) ||
            !int.TryParse(datePart[4..6], out var mm) ||
            !int.TryParse(datePart[6..8], out var dd))
            return null;

        try { return new DateOnly(yyyy, mm, dd); }
        catch { return null; }
    }
}

/// <summary>Result of parsing a single COLLATREV file.</summary>
public record ParsedReappraisalFile(
    DateOnly EffectiveDate,
    List<ParsedDetailRecord> Details
);

/// <summary>Parsed values from a single Detail ('D') record line.</summary>
public record ParsedDetailRecord(
    string RowHash,
    string ReviewType,
    DateOnly ReviewDate,
    string CollateralId,
    string SurveyNumber,
    string CollateralCode,
    string CollateralCategory,
    string? CollateralName,
    string? CollateralAddress,
    string CifNumber,
    string? CifName,
    string? AoCode,
    string? AoName,
    string? TitleNumber,
    decimal? CurrentValue,
    DateOnly? ValuationDate,
    string? InternalExternal,
    string? BusinessSize,
    string? BusinessSizeDesc,
    decimal? MortgageAmount,
    int? PastDueDay,
    string? ApplicationNumber,
    string? FacilityCode,
    string? FacilitySequence,
    string? CpNumber,
    string? CarCode,
    decimal? FacilityLimit,
    string? FlagLessAge4Y,
    string? FlagGreaterAge4Y,
    string? CountAgeingDate,
    string? CollateralDescription,
    string? ExternalValuerName,
    string? InternalValuerName,
    string? SllOver100M,
    string? SllDescription,
    string? Stage,
    string? IBGRetail,
    string? Group,
    DateOnly? EffectiveDateAppraisal
);
