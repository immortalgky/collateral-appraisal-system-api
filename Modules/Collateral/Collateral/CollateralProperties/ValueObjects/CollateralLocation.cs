namespace Collateral.CollateralProperties.ValueObjects;

public class CollateralLocation : ValueObject
{
    public string SubDistrict { get; }
    public string District { get; }
    public string Province { get; }
    public string LandOffice { get; }

    private CollateralLocation(
        string subDistrict,
        string district,
        string province,
        string landOffice
    )
    {
        SubDistrict = subDistrict;
        District = district;
        Province = province;
        LandOffice = landOffice;
    }

    public static CollateralLocation Create(
        string subDistrict,
        string district,
        string province,
        string landOffice
    )
    {
        return new CollateralLocation(subDistrict, district, province, landOffice);
    }
}
