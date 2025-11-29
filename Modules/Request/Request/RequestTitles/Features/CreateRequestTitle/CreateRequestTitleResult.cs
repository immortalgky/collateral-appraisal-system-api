namespace Request.RequestTitles.Features.CreateRequestTitle;

public record CreateRequestTitleResult
{
    public Guid Id { get; init; }
    public Guid RequestId { get; init; }
    public string? CollateralType { get; init; }
    public bool? CollateralStatus { get; init; }
    public TitleDeedInfoDto TitleDeedInfoDto { get; init; } = default!;
    public SurveyInfoDto SurveyInfoDto { get; init; } = default!;
    public LandAreaDto LandAreaDto { get; init; } = default!;
    public string? OwnerName { get; init; }
    public string? RegistrationNo { get; init; }
    public VehicleDto VehicleInfoDto { get; init; } = default!;
    public MachineDto MachineInfoDto { get; init; } = default!;
    public BuildingInfoDto BuildingInfoDto { get; init; } = default!;
    public CondoInfoDto CondoInfoDto { get; init; } = default!;
    public AddressDto TitleAddressDto { get; init; } = default!;
    public AddressDto DopaAddressDto { get; init; } = default!;
    public string? Notes { get; init; }
};