namespace Request.Contracts.Requests.Dtos;

public record RequestTitleDto
{
    public Guid? Id { get; init; }
    public Guid RequestId { get; init; }
    public string CollateralType { get; init; } = default!;
    public bool CollateralStatus { get; init; }

    // TitleDeedInfo fields
    public string? TitleNo { get; init; }
    public string? DeedType { get; init; }
    public string? TitleDetail { get; init; }

    // LandLocationInfo fields (renamed from SurveyInfo)
    public string? BookNumber { get; init; }
    public string? PageNumber { get; init; }
    public string? LandParcelNumber { get; init; }
    public string? SurveyNumber { get; init; }
    public string? MapSheetNumber { get; init; }
    public string? Rawang { get; init; }
    public string? AerialMapName { get; set; }
    public string? AerialMapNumber { get; set; }

    // LandArea fields
    public int? AreaRai { get; init; }
    public int? AreaNgan { get; init; }
    public decimal? AreaSquareWa { get; init; }

    // Shared field
    public string? OwnerName { get; init; }

    // VehicleInfo fields
    public string? VehicleType { get; init; }
    public string? VehicleAppointmentLocation { get; init; }
    public string? VIN { get; init; }
    public string? LicensePlateNumber { get; init; }

    // VesselInfo fields
    public string? VesselType { get; init; }
    public string? VesselAppointmentLocation { get; init; }
    public string? HullIdentificationNumber { get; init; }
    public string? VesselRegistrationNumber { get; init; }

    // MachineInfo fields
    public bool RegistrationStatus { get; init; }
    public string? RegistrationNo { get; init; }
    public string? MachineType { get; init; }
    public string? InstallationStatus { get; init; }
    public string? InvoiceNumber { get; init; }
    public int? NumberOfMachine { get; init; }

    // BuildingInfo fields
    public string? BuildingType { get; init; }
    public decimal? UsableArea { get; init; }
    public int? NumberOfBuilding { get; init; }

    // CondoInfo fields (UsableArea is shared with Building)
    public string? CondoName { get; init; }
    public string? BuildingNo { get; init; }
    public string? RoomNo { get; init; }
    public string? FloorNo { get; init; }

    // Address fields
    public AddressDto TitleAddress { get; init; } = default!;
    public AddressDto DopaAddress { get; init; } = default!;

    public string? Notes { get; init; }
    public List<RequestTitleDocumentDto> Documents { get; init; } = default!;
}