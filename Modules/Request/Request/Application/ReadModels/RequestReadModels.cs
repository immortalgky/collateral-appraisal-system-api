namespace Request.Application.ReadModels;

internal record RequestRow
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Purpose { get; set; }
    public string? Channel { get; set; }
    public string Requestor { get; set; } = default!;
    public string RequestorName { get; set; } = default!;
    public string Creator { get; set; } = default!;
    public string CreatorName { get; set; } = default!;
    public string Priority { get; set; } = default!;
    public bool IsPma { get; set; }
    public bool HasAppraisalBook { get; set; }
    public string? BankingSegment { get; set; }
    public string? LoanApplicationNumber { get; set; }
    public decimal? FacilityLimit { get; set; }
    public decimal? AdditionalFacilityLimit { get; set; }
    public decimal? PreviousFacilityLimit { get; set; }
    public decimal? TotalSellingPrice { get; set; }
    public Guid? PrevAppraisalId { get; set; }
    public string? PrevAppraisalNumber { get; set; }
    public decimal? PrevAppraisalValue { get; set; }
    public DateTime? PrevAppraisalDate { get; set; }
    public string? HouseNumber { get; set; }
    public string? ProjectName { get; set; }
    public string? Moo { get; set; }
    public string? Soi { get; set; }
    public string? Road { get; set; }
    public string? SubDistrict { get; set; }
    public string? District { get; set; }
    public string? Province { get; set; }
    public string? Postcode { get; set; }
    public string? ContactPersonName { get; set; }
    public string? ContactPersonPhone { get; set; }
    public string? DealerCode { get; set; }
    public DateTime? AppointmentDateTime { get; set; }
    public string? AppointmentLocation { get; set; }
    public string? FeePaymentType { get; set; }
    public decimal? AbsorbedAmount { get; set; }
    public string? FeeNotes { get; set; }
}

internal record RequestCustomerRow
{
    public Guid RequestId { get; set; }
    public string? CustomerName { get; set; }
    public string? ContactNumber { get; set; }
}

internal record RequestPropertyRow
{
    public Guid RequestId { get; set; }
    public string? PropertyType { get; set; }
    public string? BuildingType { get; set; }
    public decimal? SellingPrice { get; set; }
}

internal record RequestDocumentRow
{
    public Guid Id { get; set; }
    public Guid RequestId { get; set; }
    public Guid? DocumentId { get; set; }
    public string DocumentType { get; set; } = default!;
    public string? FileName { get; set; }
    public int Set { get; set; }
    public string? Notes { get; set; }
    public string UploadedBy { get; set; } = default!;
    public string UploadedByName { get; set; } = default!;
    public DateTime UploadedAt { get; set; }
}

internal record RequestTitleDocumentRow
{
    public Guid? Id { get; set; }
    public Guid TitleId { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentType { get; set; }
    public string? FileName { get; set; }
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