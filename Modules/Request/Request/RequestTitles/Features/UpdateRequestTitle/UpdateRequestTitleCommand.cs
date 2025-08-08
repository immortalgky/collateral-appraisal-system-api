namespace Request.RequestTitles.Features.UpdateRequestTitle;

public record UpdateRequestTitleCommand(
    long RequestId,
    long Id,
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
) : ICommand<UpdateRequestTitleResult>;