namespace Appraisal.Domain.SupportingDataMaintenance;

public class SupportingAddress : ValueObject
{
    public string? HouseNo { get; }
    public string? SubDistrict { get; }
    public string? District { get; }
    public string? Province { get; }

    private SupportingAddress() { /* EF */ }
    private SupportingAddress(string? h, string? s, string? d, string? p)
    { HouseNo = h; SubDistrict = s; District = d; Province = p; }

    public static SupportingAddress Create(string? houseNo, string? subDistrict,
                                           string? district, string? province)
        => new(houseNo, subDistrict, district, province);
}