namespace Request.Application.ReadModels;

internal record RequestRow
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; }
    public string Status { get; set; }
    public string Purpose { get; set; }
    public string Channel { get; set; }
    public string Requestor { get; set; }
    public string RequestorName { get; set; }
    public string Creator { get; set; }
    public string CreatorName { get; set; }
    public string Priority { get; set; }
    public bool IsPma { get; set; }
    public bool HasAppraisalBook { get; set; }
    public string BankingSegment { get; set; }
    public string LoanApplicationNumber { get; set; }
    public decimal FacilityLimit { get; set; }
    public decimal AdditionalFacilityLimit { get; set; }
    public decimal PreviousFacilityLimit { get; set; }
    public decimal TotalSellingPrice { get; set; }
    public Guid? PrevAppraisalId { get; set; }
    public string HouseNumber { get; set; }
    public string ProjectName { get; set; }
    public string Moo { get; set; }
    public string Soi { get; set; }
    public string Road { get; set; }
    public string SubDistrict { get; set; }
    public string District { get; set; }
    public string Province { get; set; }
    public string Postcode { get; set; }
    public string ContactPersonName { get; set; }
    public string ContactPersonPhone { get; set; }
    public string DealerCode { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string AppointmentLocation { get; set; }
    public string FeePaymentType { get; set; }
    public decimal AbsorbedAmount { get; set; }
    public string FeeNotes { get; set; }
}

internal record RequestCustomerRow
{
    public Guid RequestId { get; set; }
    public string CustomerName { get; set; }
    public string ContactNumber { get; set; }
}

internal record RequestPropertyRow
{
    public Guid RequestId { get; set; }
    public string PropertyType { get; set; }
    public string BuildingType { get; set; }
    public decimal SellingPrice { get; set; }
}

internal record RequestDocumentRow
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid? DocumentId { get; set; }
    public string DocumentType { get; set; }
    public string? FileName { get; set; }
    public int Set { get; set; }
    public string Notes { get; set; }
    public string UploadedBy { get; set; }
    public string UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
}

internal record RequestTitleRow
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public string CollateralType { get; set; }
    public bool CollateralStatus { get; set; }
    public string? TitleNo { get; set; }
    public string? DeedType { get; set; }
    public string? TitleDetail { get; set; }
    public string? Rawang { get; set; }
    public string? LandNo { get; set; }
    public string? SurveyNo { get; set; }
    public int? AreaRai { get; set; }
    public int? AreaNgan { get; set; }
    public decimal? AreaSquareWa { get; set; }
    public string? OwnerName { get; set; }
    public string? VehicleType { get; set; }
    public string? VehicleAppointmentLocation { get; set; }
    public string? VIN { get; set; }
    public string? LicensePlateNumber { get; set; }
    public string? VesselType { get; set; }
    public string? VesselAppointmentLocation { get; set; }
    public string? HullIdentificationNumber { get; set; }
    public string? VesselRegistrationNumber { get; set; }
    public bool RegistrationStatus { get; set; }
    public string? RegistrationNo { get; set; }
    public string? MachineType { get; set; }
    public string? InstallationStatus { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? NumberOfMachine { get; set; }
    public string? BuildingType { get; set; }
    public decimal? UsableArea { get; set; }
    public int? NumberOfBuilding { get; set; }
    public string? CondoName { get; set; }
    public string? BuildingNo { get; set; }
    public string? RoomNo { get; set; }
    public string? FloorNo { get; set; }

    // TitleAddress flat fields
    public string? HouseNumber { get; set; }
    public string? ProjectName { get; set; }
    public string? Moo { get; set; }
    public string? Soi { get; set; }
    public string? Road { get; set; }
    public string? SubDistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? Postcode { get; set; }

    // DopaAddress flat fields
    public string? DopaHouseNumber { get; set; }
    public string? DopaProjectName { get; set; }
    public string? DopaMoo { get; set; }
    public string? DopaSoi { get; set; }
    public string? DopaRoad { get; set; }
    public string? DopaSubDistrict { get; set; }
    public string? DopaDistrict { get; set; }
    public string? DopaProvince { get; set; }
    public string? DopaPostcode { get; set; }

    public string? Notes { get; set; }
}

internal record RequestTitleDocumentRow
{
    public Guid? Id { get; set; }
    public Guid TitleId { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentType { get; set; }
    public string? Filename { get; set; }
    public string? Prefix { get; set; }
    public int Set { get; set; }
    public string? DocumentDescription { get; set; }
    public string? FilePath { get; set; }
    public string? CreatedWorkstation { get; set; }
    public bool IsRequired { get; set; }
    public string? UploadedBy { get; set; }
    public string? UploadedByName { get; set; }
    public DateTime UploadedAt { get; set; }
}
