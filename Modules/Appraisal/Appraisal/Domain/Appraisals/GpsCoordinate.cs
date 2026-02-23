namespace Appraisal.Domain.Appraisals;

/// <summary>
/// Value Object representing GPS coordinates (latitude/longitude).
/// </summary>
public record GpsCoordinate
{
    public decimal? Latitude { get; init; }
    public decimal? Longitude { get; init; }

    private GpsCoordinate()
    {
    }

    public static GpsCoordinate Create(decimal? latitude, decimal? longitude)
    {
        return new GpsCoordinate
        {
            Latitude = latitude,
            Longitude = longitude
        };
    }

    public bool HasCoordinates => Latitude.HasValue && Longitude.HasValue;
}