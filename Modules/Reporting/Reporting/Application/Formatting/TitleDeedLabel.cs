namespace Reporting.Application.Formatting;

/// <summary>
/// Names the land title document a report is talking about — "โฉนดที่ดินเลขที่ 1234",
/// "น.ส.3 เลขที่ 5678". Before CA-609 every report hardcoded the โฉนด wording, so a parcel held
/// under a น.ส.3 still printed as a full title deed.
///
/// The wording is deliberately kept here rather than resolved from parameter group DeedType the way
/// other coded labels are: that master text is a dropdown caption which has to cover both a land
/// deed and a condominium unit deed in a single option ("โฉนดที่ดิน / อ.ช.2"), so it cannot be
/// suffixed with "เลขที่" inside a sentence. The two forms are also spaced differently — a name
/// ending in a Thai word runs straight into "เลขที่", one ending in an abbreviation needs a space —
/// which is why both are spelled out per code instead of being derived from one another.
///
/// The accepted codes mirror TitleDeedInfo.ValidDeedTypes in Modules/Request — add a code here too
/// if one is added there, otherwise it silently falls back to the generic wording.
/// </summary>
public static class TitleDeedLabel
{
    /// <summary>
    /// Used for OTHER, for blanks, and for any code not listed below: "อื่นๆเลขที่ 1234" does not read.
    /// </summary>
    public const string GenericNoun = "เอกสารสิทธิ์";

    private const string GenericNumberPrefix = $"{GenericNoun}เลขที่";

    private static readonly Dictionary<string, (string Noun, string NumberPrefix)> Labels =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["DEED"] = ("โฉนดที่ดิน", "โฉนดที่ดินเลขที่"),
            ["NS3"] = ("น.ส.3", "น.ส.3 เลขที่"),
            ["NS3K"] = ("น.ส.3 ก.", "น.ส.3 ก. เลขที่"),
            ["NS3KO"] = ("น.ส.3 ข.", "น.ส.3 ข. เลขที่"),
            ["POSR"] = ("ตราจอง", "ตราจองเลขที่")
        };

    /// <summary>The document's name on its own, for a table cell that already has its own header.</summary>
    public static string Noun(string? titleTypeCode) => Lookup(titleTypeCode).Noun;

    /// <summary>The name plus "เลขที่", for running text that continues with the number itself.</summary>
    public static string NumberPrefix(string? titleTypeCode) => Lookup(titleTypeCode).NumberPrefix;

    private static (string Noun, string NumberPrefix) Lookup(string? titleTypeCode) =>
        titleTypeCode is not null && Labels.TryGetValue(titleTypeCode.Trim(), out var label)
            ? label
            : (GenericNoun, GenericNumberPrefix);
}
