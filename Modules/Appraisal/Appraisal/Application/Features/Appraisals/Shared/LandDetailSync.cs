using Appraisal.Application.Features.Appraisals.CreateLandProperty;

namespace Appraisal.Application.Features.Appraisals.Shared;

/// <summary>
/// Add / update / remove sync of a land detail's titles and area deductions from the update payload.
/// Shared by the land, land-and-building and lease-agreement variants of the real page update and the
/// data-correction command. The caller runs <c>RecalculateDeductedArea</c> once both are synced.
/// </summary>
public static class LandDetailSync
{
    public static void SyncTitles(LandAppraisalDetail landDetail, List<LandTitleItemData> incomingTitles)
    {
        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // Delete titles not in the incoming list
        var titlesToRemove = landDetail.Titles
            .Where(t => !incomingIds.Contains(t.Id))
            .Select(t => t.Id)
            .ToList();
        foreach (var id in titlesToRemove)
            landDetail.RemoveTitle(id);

        // Add or update; the incoming list order wins, so every title is stamped with its 1-based position
        var sequence = 0;
        // Looked up once: Titles sorts and copies on every read.
        var existingById = landDetail.Titles.Where(t => t.Id != Guid.Empty).ToDictionary(t => t.Id);
        foreach (var titleData in incomingTitles)
        {
            sequence++;
            LandArea? area = null;
            if (titleData.Rai.HasValue || titleData.Ngan.HasValue || titleData.SquareWa.HasValue)
                area = LandArea.Create(titleData.Rai, titleData.Ngan, titleData.SquareWa);

            if (titleData.Id.HasValue)
            {
                // Update existing
                var existing = existingById.GetValueOrDefault(titleData.Id.Value);
                existing?.ChangeTitle(titleData.TitleNumber, titleData.TitleType);
                existing?.SetSequenceNumber(sequence);
                existing?.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
            }
            else
            {
                // Create new
                var title = LandTitle.Create(landDetail.Id, titleData.TitleNumber, titleData.TitleType);
                title.SetSequenceNumber(sequence);
                title.Update(
                    titleData.BookNumber, titleData.PageNumber,
                    titleData.LandParcelNumber, titleData.SurveyNumber,
                    titleData.MapSheetNumber, titleData.Rawang,
                    titleData.AerialMapName, titleData.AerialMapNumber,
                    area, titleData.BoundaryMarkerType, titleData.BoundaryMarkerRemark,
                    titleData.DocumentValidationResultType, titleData.IsMissingFromSurvey,
                    titleData.GovernmentPricePerSqWa, titleData.GovernmentPrice,
                    titleData.Remark);
                landDetail.AddTitle(title);
            }
        }
    }

    /// <summary>
    /// Same add / update / remove shape as <see cref="SyncTitles"/>. Rows edited in place leave the
    /// stored total stale until the caller's <c>RecalculateDeductedArea</c>, which runs after both syncs.
    /// </summary>
    public static void SyncDeductions(
        LandAppraisalDetail landDetail,
        List<LandAreaDeductionData> incomingDeductions)
    {
        var incomingIds = incomingDeductions
            .Where(d => d.Id.HasValue)
            .Select(d => d.Id!.Value)
            .ToHashSet();

        var deductionsToRemove = landDetail.Deductions
            .Where(d => !incomingIds.Contains(d.Id))
            .Select(d => d.Id)
            .ToList();
        foreach (var id in deductionsToRemove)
            landDetail.RemoveDeduction(id);

        foreach (var data in incomingDeductions)
        {
            if (data.Id.HasValue)
            {
                var existing = landDetail.Deductions.FirstOrDefault(d => d.Id == data.Id.Value);
                if (existing is not null)
                {
                    existing.ChangeReason(data.ReasonCode);
                    existing.Update(data.ReasonOther, data.AreaInSqWa, data.Remark);
                }
            }
            else
            {
                var deduction = LandAreaDeduction.Create(landDetail.Id, data.ReasonCode);
                deduction.Update(data.ReasonOther, data.AreaInSqWa, data.Remark);
                landDetail.AddDeduction(deduction);
            }
        }
    }
}
