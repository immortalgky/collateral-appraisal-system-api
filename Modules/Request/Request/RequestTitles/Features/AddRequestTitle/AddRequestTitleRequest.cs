namespace Request.RequestTitles.Features.AddRequestTitle;

public record AddRequestTitleRequest(
    long RequestId,
    string CollateralType,
    string? TitleNo,
    string? TitleDetail,
    string? Owner,
    int? Rai,
    int? Ngan,
    decimal? Wa,
    string? BuildingType,
    decimal? UsageArea,
    int? NoOfBuilding,
    AddressDto TitleAddress,
    AddressDto? DopaAddress,
    string? VehicleType,
    string? VehicleRegistrationNo,
    string? VehicleLocation,
    string? MachineStatus,
    string? MachineType,
    string? MachineRegistrationStatus,
    string? MachineRegistrationNo,
    string? MachineInvoiceNo,
    int? NoOfMachine
);