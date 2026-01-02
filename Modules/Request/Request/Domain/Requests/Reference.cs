namespace Request.Domain.Requests;

public class Reference : ValueObject
{
    // keep only AppraisalId
    public string? PrevAppraisalNo { get; } 
    public decimal? PrevAppraisalValue { get; }
    public DateTime? PrevAppraisalDate { get; }
        
    private Reference(string? prevAppraisalNo, decimal? prevAppraisalValue, DateTime? prevAppraisalDate)
    {
        PrevAppraisalNo = prevAppraisalNo;
        PrevAppraisalValue = prevAppraisalValue;
        PrevAppraisalDate = prevAppraisalDate;
    }
    
    public static Reference Create(string? prevAppraisalNo, decimal? prevAppraisalValue, DateTime? prevAppraisalDate)
    {
        return new Reference(prevAppraisalNo, prevAppraisalValue, prevAppraisalDate);
    }
}