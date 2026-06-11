using System.Security.Cryptography;
using System.Text;
using Integration.Contracts.Reappraisal;

namespace Integration.FileInterface.Format.Reappraisal;

/// <summary>
/// Parses the AS400 COLLATREV inbound interface — a <b>fixed-width</b> (649-char Detail) UTF-8 text file
/// with Header (H) / Detail (D) / Trailer (T) records.
///
///   Header:  pos 1 = 'H', pos 2–9 = EffectiveDate (DDMMYYYY), pos 10–640 = filler (640 chars total).
///   Detail:  pos 1 = 'D', 649 chars total; field map documented in the constants below.
///   Trailer: pos 1 = 'T', pos 2–10 = TotalDetailRecord (dec9), pos 11–640 = filler (640 chars total).
///
/// IMPORTANT — char vs byte positions:
///   The spec uses string(N) <b>character</b> widths. Files are UTF-8 with Thai text in
///   Name/Address/Description fields. We index by Unicode code-point position
///   (string indexer), NOT by byte offset. Thai characters are multibyte in UTF-8 but
///   counted as 1 character position in .NET strings after UTF-8 decoding.
/// </summary>
public class CollatrevFileParser
{
    private const int DetailRecordLength = 649;

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

        if (!sawTrailer)
            throw new FormatException("File has no Trailer (T) record — cannot validate completeness.");
        if (expectedCount != details.Count)
            throw new FormatException(
                $"Trailer count mismatch: file says {expectedCount} detail records, parsed {details.Count}.");

        return new ParsedReappraisalFile(effectiveDate, details);
    }

    private static ParsedDetailRecord ParseDetailLine(string line)
    {
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

    private static DateOnly ParseDdmmyyyy(string s, string fieldName)
    {
        var v = s.Trim();
        if (v.Length < 8 || v == "00000000")
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        if (!int.TryParse(v[..2], out var dd) ||
            !int.TryParse(v[2..4], out var mm) ||
            !int.TryParse(v[4..8], out var yyyy))
            throw new FormatException($"Invalid {fieldName}: '{v}'");

        // A syntactically-valid but out-of-range date (e.g. dd=32, mm=13) must surface as a
        // FormatException so the per-file handler treats it as bad data — not an unexpected
        // ArgumentOutOfRangeException leaking through.
        try
        {
            return new DateOnly(yyyy, mm, dd);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new FormatException($"Invalid {fieldName}: '{v}'");
        }
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
        if (string.IsNullOrWhiteSpace(v)) return null;
        if (!long.TryParse(v, out var raw)) return null;
        if (raw == 0L) return null;
        return raw / 100m;
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
