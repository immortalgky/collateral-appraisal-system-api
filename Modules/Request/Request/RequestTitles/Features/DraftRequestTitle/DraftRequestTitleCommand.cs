namespace Request.RequestTitles.Features.DraftRequestTitle;

public record DraftRequestTitleCommand(
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
) : ICommand<DraftRequestTitleResult>;