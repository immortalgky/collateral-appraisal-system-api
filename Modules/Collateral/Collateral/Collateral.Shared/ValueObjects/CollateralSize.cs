namespace Collateral.Collateral.Shared.ValueObjects;

public class CollateralSize : ValueObject
{
    public string? Capacity { get; }
    public decimal? Width { get; }
    public decimal? Length { get; }
    public decimal? Height { get; }

    private CollateralSize() { }

    private CollateralSize(
        string? capacity,
        decimal? width,
        decimal? length,
        decimal? height
    )
    {
        Capacity = capacity;
        Width = width;
        Length = length;
        Height = height;
    }

    public static CollateralSize Create(
        string? capacity,
        decimal? width,
        decimal? length,
        decimal? height
    )
    {
        return new CollateralSize(capacity, width, length, height);
    }
}