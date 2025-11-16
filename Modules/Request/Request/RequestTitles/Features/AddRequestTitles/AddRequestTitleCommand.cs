namespace Request.RequestTitles.Features.AddRequestTitles;

public record AddRequestTitlesCommand(
    Guid RequestId,
    List<RequestTitlesCommandDto> AddRequestTitleCommandDtos
) : ICommand<AddRequestTitlesResult>;

public record RequestTitlesCommandDto(
    string? CollateralType,
    bool? CollateralStatus,
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
    string? Notes,
    List<RequestTitleDocumentDto> RequestTitleDocumentDtos
);