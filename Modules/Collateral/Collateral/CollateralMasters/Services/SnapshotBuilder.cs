using System.Text.Json;
using Appraisal.Contracts.Appraisals;

namespace Collateral.CollateralMasters.Services;

/// <summary>
/// Builds the immutable JSON snapshot stored on CollateralEngagement.
///
/// PR-4 shape (single engagement per appraisal):
/// {
///   "groups": [
///     {
///       "groupId": "...",          // PropertyGroup.Id (null for ungrouped)
///       "groupNumber": 1,          // PropertyGroup.GroupNumber
///       "isMasterId": "...",        // CollateralMaster.Id of the IsMaster row
///       "isPrimary": true,         // true for the principal group (engagement anchor)
///       "buildingCost": 1234.00,   // sum of building values (IsMaster only)
///       "appraisalValue": 5678.00, // group's final appraised value (IsMaster only)
///       "properties": [
///         {
///           "role": "isMaster",    // or "alias"
///           "propertyId": "...",   // AppraisalProperty.Id
///           "unitPrice": 50000,    // cost-approach per sq.wa (null until PR-2-pricing wired)
///           "titleNumber": "...",  // Land entries only (first title); condo entries omit it
///           "titleType": "...",    // Land entries only
///           "province": "...",
///           ... type-specific fields ...
///         }
///       ],
///       "constructionInspections": [ ... ]   // PR-5: list of CIs for properties in this group
///     }
///   ]
/// }
///
/// For backward compatibility with consumers that read flat snapshots, this builder always
/// wraps everything in the groups[] array. A single-group appraisal produces groups with one entry.
/// </summary>
internal static class SnapshotBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Builds the full groups-based snapshot for an engagement that covers an entire appraisal.
    /// Each group has one entry. isPrimary=true marks the anchor group.
    /// </summary>
    public static string BuildAppraisalSnapshot(
        IReadOnlyList<PropertyGroupSnapshot> groups)
    {
        var snapshot = new { groups };
        return JsonSerializer.Serialize(snapshot, JsonOptions);
    }

    // -----------------------------------------------------------------------
    // Per-master helper methods — build the per-group snapshot entries.
    // Each entry represents exactly ONE CollateralMaster row (IsMaster or alias).
    // collateralMasterId is always included so consumers can correlate snapshot entries
    // back to the CollateralMaster table without an additional join. (Fix: MAJOR 3)
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the snapshot entry for a single Land CollateralMaster row (IsMaster or alias).
    /// Each entry carries its own collateralMasterId and the specific title for that master,
    /// so multi-title groups emit one entry per master rather than collapsing all titles
    /// into a single entry. (Fix: MAJOR 2)
    /// </summary>
    public static object BuildLandMasterEntry(
        Guid collateralMasterId,
        AppraisalPropertyForCollateral property,
        string role,
        string titleNumber,
        string titleType,
        decimal? unitPrice = null)
    {
        var land = property.LandIdentity!;

        return new
        {
            collateralMasterId = collateralMasterId.ToString(),
            role,
            propertyId = property.PropertyId.ToString(),
            type = "Land",
            titleNumber,
            titleType,
            province = land.Province,
            landOffice = land.LandOffice,
            district = land.District,
            subDistrict = land.SubDistrict,
            unitPrice
        };
    }

    /// <summary>
    /// Builds the property entry for a Condo property.
    /// </summary>
    public static object BuildCondoPropertyEntry(
        Guid collateralMasterId,
        AppraisalPropertyForCollateral property,
        string role,
        decimal? unitPrice = null)
    {
        var condo = property.CondoIdentity!;
        return new
        {
            collateralMasterId = collateralMasterId.ToString(),
            role,
            propertyId = property.PropertyId.ToString(),
            type = "Condo",
            landOffice = condo.LandOffice,
            condoRegistrationOrProject = condo.CondoRegistrationNumber,
            building = condo.BuildingNumber,
            floor = condo.FloorNumber,
            unit = condo.RoomNumber,
            province = condo.Province,
            unitPrice
        };
    }

    /// <summary>
    /// Builds the property entry for a Machine property.
    /// </summary>
    public static object BuildMachinePropertyEntry(
        Guid collateralMasterId,
        AppraisalPropertyForCollateral property,
        string role)
    {
        var m = property.MachineryIdentity!;
        return new
        {
            collateralMasterId = collateralMasterId.ToString(),
            role,
            propertyId = property.PropertyId.ToString(),
            type = "Machine",
            machineRegistrationNo = m.RegistrationNumber,
            serialNo = m.SerialNo,
            brand = m.Brand,
            model = m.Model,
            manufacturer = m.Manufacturer,
            location = m.Location,
            ownerName = m.OwnerName
        };
    }

    /// <summary>
    /// Builds the property entry for a Leasehold property.
    /// </summary>
    public static object BuildLeaseholdPropertyEntry(
        Guid collateralMasterId,
        AppraisalPropertyForCollateral property,
        string role,
        Guid underlyingMasterId,
        string underlyingType)
    {
        var lh = property.LeaseholdIdentity!;
        return new
        {
            collateralMasterId = collateralMasterId.ToString(),
            role,
            propertyId = property.PropertyId.ToString(),
            type = "Leasehold",
            leaseRegistrationNo = lh.ContractNo,
            underlyingMasterId = underlyingMasterId.ToString(),
            underlyingType,
            lessor = lh.LessorName,
            lessee = lh.LesseeName,
            leaseTermStart = lh.LeaseStartDate?.ToString("yyyy-MM-dd"),
            leaseTermEnd = lh.LeaseEndDate?.ToString("yyyy-MM-dd")
        };
    }

    /// <summary>
    /// Builds the constructionInspections[] list for a group, gathering CIs from a set of
    /// relevant properties (land + buildings on that land).
    /// </summary>
    public static IReadOnlyList<object> BuildConstructionInspectionsForGroup(
        IEnumerable<AppraisalPropertyForCollateral> groupProperties)
    {
        return groupProperties
            .Where(p => p.ConstructionInspection is not null)
            .Select(p =>
            {
                var ci = p.ConstructionInspection!;
                if (ci.IsFullDetail)
                {
                    return (object)new
                    {
                        propertyId = p.PropertyId.ToString(),
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
                    return (object)new
                    {
                        propertyId = p.PropertyId.ToString(),
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
            })
            .ToList();
    }
}

/// <summary>
/// Represents a single property group entry within the snapshot's groups[] array.
/// Serialized as part of the engagement snapshot JSON.
/// </summary>
internal sealed class PropertyGroupSnapshot
{
    public string? GroupId { get; init; }
    public int? GroupNumber { get; init; }
    public string IsMasterId { get; init; } = null!;
    public bool IsPrimary { get; init; }
    public decimal? BuildingValue { get; init; }
    public decimal? AppraisalValue { get; init; }
    public IReadOnlyList<object> Properties { get; init; } = [];
    public IReadOnlyList<object> ConstructionInspections { get; init; } = [];
}
