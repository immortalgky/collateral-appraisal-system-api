namespace Appraisal.Application.Features.Appraisals.GetAppraisalById;

public record GetAppraisalByIdResponse
{
    public Guid Id { get; set; }
    public string? AppraisalNumber { get; set; }
    public Guid RequestId { get; set; }
    public string Status { get; set; } = null!;
    public string AppraisalType { get; set; } = null!;
    public string Priority { get; set; } = null!;
    public int? SLADays { get; set; }
    public DateTime? SLADueDate { get; set; }
    public string? SLAStatus { get; set; }
    public int? ActualDaysToComplete { get; set; }
    public bool? IsWithinSLA { get; set; }
    public int CollateralCount { get; set; }
    public int GroupCount { get; set; }
    public int AssignmentCount { get; set; }
    public DateTime? CreatedOn { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}