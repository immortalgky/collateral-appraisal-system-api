namespace Appraisal.Domain.SupportingDataMaintenance;

public class GeoLocation : ValueObject
{
    public decimal Latitude  { get; }
    public decimal Longitude { get; }

    private GeoLocation() { }
    private GeoLocation(decimal lat, decimal lng) { Latitude = lat; Longitude = lng; }

    public static GeoLocation Create(decimal lat, decimal lng)
    {
        if (lat < -90  || lat > 90)  throw new ArgumentException("Latitude out of range.",  nameof(lat));
        if (lng < -180 || lng > 180) throw new ArgumentException("Longitude out of range.", nameof(lng));
        return new GeoLocation(lat, lng);
    }
}