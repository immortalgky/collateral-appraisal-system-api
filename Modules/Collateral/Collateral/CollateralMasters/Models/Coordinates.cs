namespace Collateral.CollateralMasters.Models;

public class Coordinates
{
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    private Coordinates() { }

    public Coordinates(decimal? latitude, decimal? longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public void Update(decimal? latitude, decimal? longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}
