namespace Collateral.CollateralProperties.ValueObjects;

public class Coordinate : ValueObject
{
    public decimal Latitude { get; } = default!;
    public decimal Longitude { get; } = default!;

    private Coordinate(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public static Coordinate Create(decimal latitude, decimal longitude)
    {
        return new Coordinate(latitude, longitude);
    }
}