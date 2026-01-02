namespace Request.Contracts.Requests.Dtos;

public record AddressDto(
    string? HouseNumber,
    string? ProjectName,
    string? Moo,
    string? Soi,
    string? Road,
    string? SubDistrict,
    string? District,
    string? Province,
    string? Postcode
);