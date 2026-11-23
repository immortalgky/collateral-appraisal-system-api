namespace Reporting.Contracts;

/// <summary>How a wide table that does not fit the page is handled in a PDF export.</summary>
public enum PdfOverflow
{
    /// <summary>Squeeze every column onto one page width (text wraps).</summary>
    Fit,

    /// <summary>Spread the columns over several column-sets, each printed on its own pages.</summary>
    Split,
}

/// <summary>Per-export PDF layout chosen by the caller. Ignored for Excel / CSV.</summary>
public sealed record PdfLayout(bool Landscape, PdfOverflow Overflow, int FontSizePx)
{
    public const int MinFontSizePx = 6;
    public const int MaxFontSizePx = 16;
    public const int DefaultFontSizePx = 11;

    public static PdfLayout Default { get; } = new(true, PdfOverflow.Fit, DefaultFontSizePx);
}
