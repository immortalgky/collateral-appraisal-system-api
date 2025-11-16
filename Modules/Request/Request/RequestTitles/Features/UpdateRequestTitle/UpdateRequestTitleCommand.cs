namespace Request.RequestTitles.Features.UpdateRequestTitle;

public record UpdateRequestTitleCommand(
    Guid Id,
    Guid RequestId,
    string? CollateralType,
    bool? CollateralStatus,
    TitleDeedInfoDto TitleDeedInfoDto,
    SurveyInfoDto SurveyInfoDto,
    LandAreaDto LandAreaDto,
    string? OwnerName,
    string? RegistrationNumber,
    VehicleDto VehicleDto,
    MachineDto MachineDto,
    BuildingInfoDto BuildingInfoDto,
    CondoInfoDto CondoInfoDto,
    AddressDto TitleAddress,
    AddressDto DopaAddress,
    string? Notes
) : ICommand<UpdateRequestTitleResult>;