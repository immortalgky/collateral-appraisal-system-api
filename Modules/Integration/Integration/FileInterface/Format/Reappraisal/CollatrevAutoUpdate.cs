namespace Integration.FileInterface.Format.Reappraisal;

/// <summary>
/// The COLLATREV Auto Update flag (position 650): "Y" or "N" in any case, or blank. One rule for the
/// parser and the dev test-file generator.
/// </summary>
public static class CollatrevAutoUpdate
{
    /// <summary>Blank gives true with a null flag; anything other than Y/N gives false.</summary>
    public static bool TryNormalize(string? input, out string? flag)
    {
        flag = null;
        var t = input?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(t)) return true;
        if (t is not ("Y" or "N")) return false;
        flag = t;
        return true;
    }
}
