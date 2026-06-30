namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

public record GetAppraisalByIdResponse
{
    public Guid Id { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid RequestId { get; set; }
    public DateTime? RequestedAt { get; set; }
    public string Status { get; set; } = null!;
    public string AppraisalType { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public bool IsPma { get; set; }
    public bool IsBlock { get; set; }
    public string? BlockProjectType { get; set; }
    public string? Purpose { get; set; }
    public string? Channel { get; set; }
    public string? BankingSegment { get; set; }
    public decimal? FacilityLimit { get; set; }
    public int? SLAHours { get; set; }
    public DateTime? SLADueDate { get; set; }
    public string? SLAStatus { get; set; }
    public int? ActualHoursToComplete { get; set; }
    public bool? IsWithinSLA { get; set; }
    public int PropertyCount { get; set; }
    public int GroupCount { get; set; }
    public int AssignmentCount { get; set; }
    public string? CompanyName { get; set; }
    public string? AppraiserName { get; set; }
    public DateTime? AppraisalDate { get; set; }
    public decimal? AppraisalValue { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}