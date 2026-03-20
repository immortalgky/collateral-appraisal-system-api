using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetMachinerySummary;

/// <summary>
/// Handler for getting the machinery appraisal summary
/// </summary>
public class GetMachinerySummaryQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetMachinerySummaryQuery, GetMachinerySummaryResult>
{
    public async Task<GetMachinerySummaryResult> Handle(
        GetMachinerySummaryQuery query,
        CancellationToken cancellationToken)
    {
        var summary = await dbContext.MachineryAppraisalSummaries
            .FirstOrDefaultAsync(s => s.AppraisalId == query.AppraisalId, cancellationToken)
            ?? throw new NotFoundException($"Machinery summary not found for appraisal {query.AppraisalId}");

        return new GetMachinerySummaryResult(
            SummaryId: summary.Id,
            AppraisalId: summary.AppraisalId,
            InIndustrial: summary.InIndustrial,
            SurveyedNumber: summary.SurveyedNumber,
            AppraisalNumber: summary.AppraisalNumber,
            InstalledAndUseCount: summary.InstalledAndUseCount,
            AppraisalScrapCount: summary.AppraisalScrapCount,
            AppraisedByDocumentCount: summary.AppraisedByDocumentCount,
            NotInstalledCount: summary.NotInstalledCount,
            Maintenance: summary.Maintenance,
            Exterior: summary.Exterior,
            Performance: summary.Performance,
            MarketDemandAvailable: summary.MarketDemandAvailable,
            MarketDemand: summary.MarketDemand,
            Proprietor: summary.Proprietor,
            Owner: summary.Owner,
            MachineAddress: summary.MachineAddress,
            Latitude: summary.Latitude,
            Longitude: summary.Longitude,
            Obligation: summary.Obligation,
            Other: summary.Other);
    }
}
