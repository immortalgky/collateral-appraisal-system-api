namespace Request.Contracts.Requests.Dtos;

public record AddressDto(
    string? HouseNo,
    string? RoomNo,
    string? FloorNo,
    string? LocationIdentifier,
    string? Moo,
    string? Soi,
    string? Road,
    string SubDistrict,
    string District,
    string Province,
    string? Postcode
);