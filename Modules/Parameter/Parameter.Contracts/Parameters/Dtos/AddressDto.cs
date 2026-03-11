namespace Parameter.Contracts.Parameters.Dtos;

public record AddressDto(
    string ProvinceCode,
    string ProvinceName,
    string ProvinceNameEn,
    string DistrictCode,
    string DistrictName,
    string DistrictNameEn,
    string SubDistrictCode,
    string SubDistrictName,
    string SubDistrictNameEn,
    string Postcode);
