namespace Appraisal.Application.Features.Fees.GetFeePreview;

/// <param name="AssignmentType">internal | external | external-offline; null = the appraisal's current assignment.</param>
/// <param name="AssignmentMethod">manual | roundrobin | quotation; null = the appraisal's current assignment.</param>
/// <param name="CompanyId">The company the admin is considering; only meaningful for quotation.</param>
public record GetFeePreviewQuery(
    Guid AppraisalId,
    string? AssignmentType,
    string? AssignmentMethod,
    Guid? CompanyId) : IQuery<FeePreviewResult>;
