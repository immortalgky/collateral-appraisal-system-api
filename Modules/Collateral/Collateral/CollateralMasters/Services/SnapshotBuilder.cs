using System.Text.Json;
using Appraisal.Application.Features.Appraisals.GetAppraisalForCollateral;

namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Builds the immutable JSON snapshot stored on CollateralEngagement.
/// One static method per collateral type.
/// </summary>
internal static class SnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string BuildLand(
        AppraisalPropertyForCollateral property,
        IEnumerable<AppraisalPropertyForCollateral> buildingsOnLand,
        decimal totalAppraisedValue)
    {
        var land = property.LandIdentity!;
        var title = land.Titles.FirstOrDefault();

        var buildingSnapshots = buildingsOnLand.Select(b => new
        {
            appraisalPropertyId = b.PropertyId.ToString()
        }).ToList();

        object? constructionInspection = null;
        if (property.ConstructionInspection is { } ci)
        {
            if (ci.IsFullDetail)
            {
                constructionInspection = new
                {
                    inspectionId = ci.InspectionId.ToString(),
                    isFullDetail = true,
                    overallCurrentProgressPercent = ci.OverallCurrentProgressPercent,
                    remark = ci.Remark,
                    workDetails = ci.WorkDetails?.Select(d => new
                    {
                        workDetailId = d.WorkDetailId.ToString(),
                        constructionWorkGroupId = d.ConstructionWorkGroupId.ToString(),
                        constructionWorkItemId = d.ConstructionWorkItemId?.ToString(),
                        workItemName = d.WorkItemName,
                        displayOrder = d.DisplayOrder,
                        proportionPct = d.ProportionPct,
                        previousProgressPct = d.PreviousProgressPct,
                        currentProgressPct = d.CurrentProgressPct,
                        currentProportionPct = d.CurrentProportionPct,
                        constructionValue = d.ConstructionValue
                    }).ToList()
                };
            }
            else
            {
                constructionInspection = new
                {
                    inspectionId = ci.InspectionId.ToString(),
                    isFullDetail = false,
                    overallCurrentProgressPercent = ci.OverallCurrentProgressPercent,
                    summaryDetail = ci.SummaryDetail,
                    summaryPreviousProgressPct = ci.SummaryPreviousProgressPct,
                    summaryPreviousValue = ci.SummaryPreviousValue,
                    summaryCurrentProgressPct = ci.SummaryCurrentProgressPct,
                    summaryCurrentValue = ci.SummaryCurrentValue,
                    remark = ci.Remark
                };
            }
        }

        // Emit all titles in the appraisal for this property (multi-title support)
        var titlesSnapshot = land.Titles
            .Where(t => !string.IsNullOrWhiteSpace(t.TitleNumber))
            .Select(t => new { titleDeedNo = t.TitleNumber, titleDeedType = t.TitleType })
            .ToList();

        var snapshot = new
        {
            type = "Land",
            // Legacy single-title fields (kept for backwards compat with consumers reading existing snapshots)
            titleNumber = title?.TitleNumber,
            titleType = title?.TitleType,
            // Multi-title array — all titles in this appraisal for this property
            titles = titlesSnapshot,
            province = land.Province,
            landOffice = land.LandOffice,
            district = land.District,
            subDistrict = land.SubDistrict,
            buildingsOnLand = buildingSnapshots,
            totalAppraisedValue,
            constructionInspection
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string BuildCondo(AppraisalPropertyForCollateral property)
    {
        var condo = property.CondoIdentity!;

        var snapshot = new
        {
            type = "Condo",
            landOffice = condo.LandOffice,
            condoRegistrationOrProject = condo.CondoRegistrationNumber,
            building = condo.BuildingNumber,
            floor = condo.FloorNumber,
            unit = condo.RoomNumber,
            condoTitleDeedNo = condo.TitleNumber,
            condoTitleType = condo.TitleType,
            province = condo.Province
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string BuildLeasehold(
        AppraisalPropertyForCollateral property,
        Guid underlyingMasterId,
        string underlyingType)
    {
        var lh = property.LeaseholdIdentity!;

        var snapshot = new
        {
            type = "Leasehold",
            leaseRegistrationNo = lh.ContractNo,
            underlyingMasterId = underlyingMasterId.ToString(),
            underlyingType,
            lessor = lh.LessorName,
            lessee = lh.LesseeName,
            leaseTermStart = lh.LeaseStartDate?.ToString("yyyy-MM-dd"),
            leaseTermEnd = lh.LeaseEndDate?.ToString("yyyy-MM-dd")
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    public static string BuildMachine(AppraisalPropertyForCollateral property)
    {
        var m = property.MachineryIdentity!;

        var snapshot = new
        {
            type = "Machine",
            machineRegistrationNo = m.RegistrationNo,
            serialNo = m.SerialNo,
            brand = m.Brand,
            model = m.Model,
            manufacturer = m.Manufacturer,
            location = m.Location,
            ownerName = m.OwnerName
        };

        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }
}
