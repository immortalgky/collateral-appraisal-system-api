using System;

namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public record UpdateDraftRequestTitleCommand(
    Guid RequestId,
    Guid TitleId,
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
    List<RequestTitleDto> RequestTitleDtos
) : ICommand<UpdateDraftRequestTitleResult>;
