using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CreateOrUpdateMachinerySummary;

/// <summary>
/// Handler for creating or updating the machinery appraisal summary
/// </summary>
public class CreateOrUpdateMachinerySummaryCommandHandler(
    AppraisalDbContext dbContext
) : ICommandHandler<CreateOrUpdateMachinerySummaryCommand, CreateOrUpdateMachinerySummaryResult>
{
    public async Task<CreateOrUpdateMachinerySummaryResult> Handle(
        CreateOrUpdateMachinerySummaryCommand command,
        CancellationToken cancellationToken)
    {
        // 1. Verify appraisal exists
        var appraisalExists = await dbContext.Appraisals
            .AnyAsync(a => a.Id == command.AppraisalId, cancellationToken);

        if (!appraisalExists)
            throw new AppraisalNotFoundException(command.AppraisalId);

        // 2. Load existing summary or create new one
        var summary = await dbContext.MachineryAppraisalSummaries
            .FirstOrDefaultAsync(s => s.AppraisalId == command.AppraisalId, cancellationToken);

        if (summary is null)
        {
            summary = MachineryAppraisalSummary.Create(command.AppraisalId);
            dbContext.MachineryAppraisalSummaries.Add(summary);
        }

        // 3. Update fields
        summary.Update(
            inIndustrial: command.InIndustrial,
            surveyedNumber: command.SurveyedNumber,
            appraisalNumber: command.AppraisalNumber,
            installedAndUseCount: command.InstalledAndUseCount,
            appraisalScrapCount: command.AppraisalScrapCount,
            appraisedByDocumentCount: command.AppraisedByDocumentCount,
            notInstalledCount: command.NotInstalledCount,
            maintenance: command.Maintenance,
            exterior: command.Exterior,
            performance: command.Performance,
            marketDemandAvailable: command.MarketDemandAvailable,
            marketDemand: command.MarketDemand,
            proprietor: command.Proprietor,
            owner: command.Owner,
            machineAddress: command.MachineAddress,
            latitude: command.Latitude,
            longitude: command.Longitude,
            obligation: command.Obligation,
            other: command.Other);

        // 4. Save
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateOrUpdateMachinerySummaryResult(summary.Id);
    }
}
