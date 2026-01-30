namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing Thai land area measurements.
/// Thai units: 1 Rai = 4 Ngan = 400 Square Wa = 1,600 sqm
/// </summary>
public record LandArea
{
    public decimal? Rai { get; init; }
    public decimal? Ngan { get; init; }
    public decimal? SquareWa { get; init; }

    private LandArea()
    {
    }

    public static LandArea Create(decimal? rai, decimal? ngan, decimal? squareWa)
    {
        return new LandArea
        {
            Rai = rai,
            Ngan = ngan,
            SquareWa = squareWa
        };
    }

    /// <summary>
    /// Total area in Square Wa (1 Rai = 400 SqWa, 1 Ngan = 100 SqWa)
    /// </summary>
    public decimal? TotalSquareWa =>
        (Rai ?? 0) * 400 + (Ngan ?? 0) * 100 + (SquareWa ?? 0);

    /// <summary>
    /// Total area in square meters (1 SqWa = 4 sqm)
    /// </summary>
    public decimal? TotalSquareMeters => TotalSquareWa * 4;

    public bool HasValue => Rai.HasValue || Ngan.HasValue || SquareWa.HasValue;
}