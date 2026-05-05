using Appraisal.Domain.Appraisals.Exceptions;
using Appraisal.Infrastructure;
using Shared.CQRS;

namespace Appraisal.Application.Features.ConstructionInspections.GetWorkDetailsForPrefill;

/// <summary>
/// Handler for GET /appraisal/construction-inspections/{inspectionId}/work-details.
///
/// ConstructionInspection is an owned entity under AppraisalProperty (OwnsOne). Direct DbSet
/// queries against owned entities throw at runtime — navigate via the owning aggregate root
/// (AppraisalProperty) instead.
/// </summary>
public class GetWorkDetailsForPrefillQueryHandler(
    AppraisalDbContext dbContext
) : IQueryHandler<GetWorkDetailsForPrefillQuery, GetWorkDetailsForPrefillResult>
{
    public async Task<GetWorkDetailsForPrefillResult> Handle(
        GetWorkDetailsForPrefillQuery query,
        CancellationToken cancellationToken)
    {
        // Navigate via the owning entity to avoid the owned-entity DbSet runtime exception.
        var property = await dbContext.AppraisalProperties
            .AsNoTracking()
            .Include(p => p.ConstructionInspection)
                .ThenInclude(ci => ci!.WorkDetails)
            .FirstOrDefaultAsync(
                p => p.ConstructionInspection != null &&
                     p.ConstructionInspection.Id == query.InspectionId,
                cancellationToken);

        if (property?.ConstructionInspection is null)
            throw new AppraisalNotFoundException(query.InspectionId);

        var inspection = property.ConstructionInspection;

        List<WorkDetailPrefillItem>? workDetails = null;
        WorkDetailSummaryPrefill? summary = null;

        if (inspection.IsFullDetail)
        {
            workDetails = inspection.WorkDetails
                .OrderBy(d => d.DisplayOrder)
                .Select(d => new WorkDetailPrefillItem(
                    WorkDetailId: d.Id,
                    ConstructionWorkGroupId: d.ConstructionWorkGroupId,
                    ConstructionWorkItemId: d.ConstructionWorkItemId,
                    WorkItemName: d.WorkItemName,
                    DisplayOrder: d.DisplayOrder,
                    ProportionPct: d.ProportionPct,
                    CurrentProgressPct: d.CurrentProgressPct,
                    ConstructionValue: d.ConstructionValue
                ))
                .ToList();
        }
        else
        {
            summary = new WorkDetailSummaryPrefill(
                SummaryCurrentProgressPct: inspection.SummaryCurrentProgressPct,
                SummaryCurrentValue: inspection.SummaryCurrentValue,
                SummaryDetail: inspection.SummaryDetail,
                Remark: inspection.Remark
            );
        }

        return new GetWorkDetailsForPrefillResult(
            InspectionId: inspection.Id,
            IsFullDetail: inspection.IsFullDetail,
            OverallCurrentProgressPercent: inspection.OverallCurrentProgressPercent,
            WorkDetails: workDetails,
            Summary: summary
        );
    }
}
