namespace Appraisal.Application.Features.PricingAnalysis.UpdateRemark;

/// <summary>
/// Request to update the remark for a pricing analysis.
/// </summary>
/// <param name="Remark"></param>
public record UpdateRemarkRequest(string? Remark);
