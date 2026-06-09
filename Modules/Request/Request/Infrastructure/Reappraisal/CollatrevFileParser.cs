using System.Security.Cryptography;
using System.Text;

namespace Request.Infrastructure.Reappraisal;

/// <summary>
/// Parses the AS400 COLLATREV inbound interface — a **fixed-width** (649-char Detail) UTF-8 text file
/// with Header (H) / Detail (D) / Trailer (T) records.
///
///   Header:  pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), pos 10–640 = filler (640 chars total).
///   Detail:  pos 1 = 'D', 649 chars total; field map documented in the constants below.
///   Trailer: pos 1 = 'T', pos 2–10 = TotalDetailRecord (dec9), pos 11–640 = filler (640 chars total).
///
/// IMPORTANT — char vs byte positions:
///   The spec uses string(N) **character** widths. Files are UTF-8 with Thai text in
///   Name/Address/Description fields. We index by Unicode code-point position
///   (string indexer), NOT by byte offset. Thai characters are multibyte in UTF-8 but
///   counted as 1 character position in .NET strings after UTF-8 decoding.
/// </summary>
public class CollatrevFileParser
{
    private const int DetailRecordLength = 649;

    // Detail record field offsets (0-based start in the decoded line + char length).
    // Maps to the 1-based positions in the vendor spec: pos N (1-based) = start N-1 (0-based).
    //   pos 1     → RecordType (1 char, 'D')
    //   pos 2     → ReviewType (1 char)
    //   pos 3–10  → ReviewDate (8 chars, DDMMYYYY)
    //   pos 11–29 → CollateralId (19 chars, numeric string)
    //   pos 30–39 → SurveyNumber (10 chars; = our AppraisalNumber)
    //   pos 40–42 → CollateralCode (3 chars)
    //   pos 43–47 → CollateralCategory (5 chars)
    //   pos 48–87 → CollateralName (40 chars)
    //   pos 88–207 → CollateralAddress (120 chars)
    //   pos 208–226 → CifNumber (19 chars)
    //   pos 227–246 → CifName (20 chars)
    //   pos 247–256 → AoCode (10 chars)
    //   pos 257–276 → AoName (20 chars)
    //   pos 277–296 → TitleNumber (20 chars)
    //   pos 297–311 → CurrentValue (15 chars, dec15,2)
    //   pos 312–319 → ValuationDate (8 chars, DDMMYYYY)
    //   pos 320    → InternalExternal (1 char)
    //   pos 321    → BusinessSize (1 char)
    //   pos 322–341 → BusinessSizeDesc (20 chars)
    //   pos 342–356 → MortgageAmount (15 chars, dec15,2)
    //   pos 357–361 → PastDueDay (5 chars, dec5)
    //   pos 362–380 → ApplicationNumber (19 chars)
    //   pos 381–383 → FacilityCode (3 chars)
    //   pos 384–402 → FacilitySequence (19 chars)
    //   pos 403–418 → CpNumber (16 chars)
    //   pos 419–421 → CarCode (3 chars)
    //   pos 422–436 → FacilityLimit (15 chars, dec15,2)
    //   pos 437    → FlagLessAge4Y (1 char)
    //   pos 438    → FlagGreaterAge4Y (1 char)
    //   pos 439–448 → CountAgeingDate (10 chars)
    //   pos 449–498 → CollateralDescription (50 chars)
    //   pos 499–538 → ExternalValuerName (40 chars)
    //   pos 539–578 → InternalValuerName (40 chars)
    //   pos 579    → SllOver100M (1 char)
    //   pos 580–629 → SllDescription (50 chars)
    //   pos 630     → Stage (1 char) — CIF stage indicator
    //   pos 631–640 → IBGRetail (10 chars) — banking segment (RB / IBG / …)
    //   pos 641     → Group (1 char) — review group code 1/2/3
    //   pos 642–649 → EffectiveDateAppraisal (8 chars, DDMMYYYY)

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
        // SHA-256 of the raw Detail line for dedup (RowHash).
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(line)));

        return new ParsedDetailRecord(
            RowHash: hash,
            ReviewType: Slice(line, 1, 1).Trim(),
            ReviewDate: ParseDdmmyyyy(Slice(line, 2, 8), "ReviewDate"),
            CollateralId: Slice(line, 10, 19).Trim(),
            SurveyNumber: Slice(line, 29, 10).Trim(),
            CollateralCode: Slice(line, 39, 3).Trim(),
            CollateralCategory: Slice(line, 42, 5).Trim(),
            CollateralName: TrimOrNull(Slice(line, 47, 40)),
            CollateralAddress: TrimOrNull(Slice(line, 87, 120)),
            CifNumber: Slice(line, 207, 19).Trim(),
            CifName: TrimOrNull(Slice(line, 226, 20)),
            AoCode: TrimOrNull(Slice(line, 246, 10)),
            AoName: TrimOrNull(Slice(line, 256, 20)),
            TitleNumber: TrimOrNull(Slice(line, 276, 20)),
            CurrentValue: ParseDecimalOrNull(Slice(line, 296, 15)),
            ValuationDate: ParseDdmmyyyyOrNull(Slice(line, 311, 8)),
            InternalExternal: TrimOrNull(Slice(line, 319, 1)),
            BusinessSize: TrimOrNull(Slice(line, 320, 1)),
            BusinessSizeDesc: TrimOrNull(Slice(line, 321, 20)),
            MortgageAmount: ParseDecimalOrNull(Slice(line, 341, 15)),
            PastDueDay: ParseIntOrNull(Slice(line, 356, 5)),
            ApplicationNumber: TrimOrNull(Slice(line, 361, 19)),
            FacilityCode: TrimOrNull(Slice(line, 380, 3)),
            FacilitySequence: TrimOrNull(Slice(line, 383, 19)),
            CpNumber: TrimOrNull(Slice(line, 402, 16)),
            CarCode: TrimOrNull(Slice(line, 418, 3)),
            FacilityLimit: ParseDecimalOrNull(Slice(line, 421, 15)),
            FlagLessAge4Y: TrimOrNull(Slice(line, 436, 1)),
            FlagGreaterAge4Y: TrimOrNull(Slice(line, 437, 1)),
            CountAgeingDate: TrimOrNull(Slice(line, 438, 10)),
            CollateralDescription: TrimOrNull(Slice(line, 448, 50)),
            ExternalValuerName: TrimOrNull(Slice(line, 498, 40)),
            InternalValuerName: TrimOrNull(Slice(line, 538, 40)),
            SllOver100M: TrimOrNull(Slice(line, 578, 1)),
            SllDescription: TrimOrNull(Slice(line, 579, 50)),
            Stage: TrimOrNull(Slice(line, 629, 1)),
            IBGRetail: TrimOrNull(Slice(line, 630, 10)),
            Group: TrimOrNull(Slice(line, 640, 1)),
            EffectiveDateAppraisal: ParseDdmmyyyyOrNull(Slice(line, 641, 8))
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
