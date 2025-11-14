namespace Request.RequestTitles.Features.AddRequestTitles;

public record AddRequestTitlesCommand(
    Guid RequestId,
    List<AddRequestTitlesCommandDto> AddRequestTitleCommandDtos
) : ICommand<AddRequestTitlesResult>;

public record AddRequestTitlesCommandDto(
    string? CollateralType,
    bool? CollateralStatus,
    TitleDeedInfoDto TitleDeedInfoDto,
    SurveyInfoDto SurveyInfoDto,
    LandAreaDto LandAreaDto,
    string? OwnerName,
    string? RegistrationNumber,
    VehicleDto VehicleDto,
    MachineryDto MachineryDto,
    BuildingInfoDto BuildingInfoDto,
    CondoInfoDto CondoInfoDto,
    AddressDto TitleAddress,
    AddressDto DopaAddress,
    string? Notes,
    List<RequestTitleDocumentDto> RequestTitleDocumentDtos
);