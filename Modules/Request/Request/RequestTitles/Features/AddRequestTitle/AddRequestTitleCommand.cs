namespace Request.RequestTitles.Features.AddRequestTitle;

public record AddRequestTitleCommand(
    Guid RequestId,
    string? CollateralType,
    bool? CollateralStatus,
    // == Title Deed Information ==
    string? TitleNo,
    string? DeedType,
    string? TitleDetail,
    // == Survey Information ==
    string? Rawang,
    string? LandNo,
    string? SurveyNo,
    // AreaRai, AreaNgan, AreaSquareWa
    int? AreaRai,
    int? AreaNgan,
    decimal? AreaSquareWa,
    // == Ownership ==
    string? OwnerName,
    // == Vehicle/Vessel/ Machine ==
    string? RegistrationNo,
    // == Vehicle/Vessel ==
    string? VehicleType,
    string? VehicleAppointmentLocation,
    string? ChassisNumber,
    // == Machine ==
    string? MachineStatus,
    string? MachineType,
    string? InstallationStatus,
    string? InvoiceNumber,
    int? NumberOfMachinery,
    // == Building Information ==
    string? BuildingType,
    decimal? UsableArea,
    int? NoOfBuilding,
    //  == Condo Information ==
    string? CondoName,
    string? BuildingNo,
    string? RoomNo,
    string? FloorNo,
    // == Address ==
    AddressDto TitleAddress,
    // == Dopa Address ==
    AddressDto DopaAddress,
    // == Notes ==
    string? Notes
) : ICommand<AddRequestTitleResult>;