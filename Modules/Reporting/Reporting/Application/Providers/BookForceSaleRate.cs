namespace Reporting.Application.Providers;

/// <summary>
/// The force-sale % the book prints, resolved the way the Appraisal module's ForceSaleRateResolver resolves the rate
/// the value was calculated with: appraisal override, else the block project's %, else the system default (already
/// 70 when unset). A result of 0 or less, or above 100, is a config error and becomes 70 (the resolver does the same),
/// so a bad project % can never print "(0%)" beside a value that was computed at 70.
/// </summary>
internal static class BookForceSaleRate
{
    private const decimal Fallback = 70m;

    public static decimal Resolve(decimal? overrideRate, decimal? projectRate, decimal defaultRate)
    {
        var rate = overrideRate ?? projectRate ?? defaultRate;
        return rate is <= 0 or > 100 ? Fallback : rate;
    }
}
