using System;

namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public record UpdateDraftRequestTitleCommand(
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
    MachineryDto MachineryDto,
    BuildingInfoDto BuildingInfoDto,
    CondoInfoDto CondoInfoDto,
    AddressDto TitleAddress,
    AddressDto DopaAddress,
    string? Notes
) : ICommand<UpdateDraftRequestTitleResult>;
