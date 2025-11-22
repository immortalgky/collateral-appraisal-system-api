namespace Request.RequestTitles.Features.CreateRequestTitle;

public record CreateRequestTitleCommand(
    Guid RequestId,
    string CollateralType,
    bool CollateralStatus,
    TitleDeedInfoDto TitleDeedInfoDto,
    SurveyInfoDto SurveyInfoDto,
    LandAreaDto LandAreaDto,
    string? OwnerName,
    string? RegistrationNo,
    VehicleDto VehicleDto,
    MachineDto MachineDto,
    BuildingInfoDto BuildingInfoDto,
    CondoInfoDto CondoInfoDto,
    AddressDto TitleAddress,
    AddressDto DopaAddress,
    string? Notes
) : ICommand<CreateRequestTitleResult>;