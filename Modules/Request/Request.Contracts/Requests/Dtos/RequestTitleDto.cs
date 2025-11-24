namespace Request.Contracts.Requests.Dtos;

public record RequestTitleDto
{
    public Guid? Id { get; init; }
    public Guid? RequestId { get; init; }
    public string? CollateralType { get; init; }
    public bool CollateralStatus { get; init; }
    public string? TitleNo { get; init; }
    public string? DeedType { get; init; }
    public string? TitleDetail { get; init; }
    public string? Rawang { get; init; }
    public string? LandNo { get; init; }
    public string? SurveyNo { get; init; }
    public int? AreaRai { get; init; }
    public int? AreaNgan { get; init; }
    public decimal? AreaSquareWa { get; init; }
    public string? OwnerName { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? VehicleType { get; init; }
    public string? VehicleAppointmentLocation { get; init; }
    public string? ChassisNumber { get; init; }
    public string? MachineStatus { get; init; }
    public string? MachineType { get; init; }
    public string? InstallationStatus { get; init; }
    public string? InvoiceNumber { get; init; }
    public int? NumberOfMachine { get; init; }
    public string? BuildingType { get; init; }
    public decimal? UsableArea { get; init; }
    public int? NumberOfBuilding { get; init; }
    public string? CondoName { get; init; }
    public string? BuildingNo { get; init; }
    public string? RoomNo { get; init; }
    public string? FloorNo { get; init; }
    public AddressDto TitleAddress { get; init; } = default!;
    public AddressDto DopaAddress { get; init; } = default!;
    public string? Notes { get; init; }
    public List<RequestTitleDocumentDto> RequestTitleDocumentDtos { get; init; } = default!;
}