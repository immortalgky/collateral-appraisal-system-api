using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;

namespace Appraisal.Application.Features.Appraisals.CorrectPropertyData;

/// <summary>
/// What a correction is diffed on: the property's own detail rows, one JSON section per detail
/// (<c>Land</c>, <c>Building</c>, <c>Condo</c>, <c>Machinery</c>, <c>Vehicle</c>, <c>Vessel</c>,
/// <c>LeaseAgreement</c>, <c>Rental</c>, <c>ConstructionInspection</c>). Taken from the aggregate itself
/// rather than a GET result so the field names are the ones the audit history already used
/// (<c>Land.OwnerName</c>) and nothing a page shows but does not store can leak in.
///
/// Not part of the snapshot: audit stamps, foreign keys, and members the domain derives from other
/// members (the deduction total, the rental schedule, construction work-item values) — they change
/// whenever their inputs do, so listing them would only repeat the change that caused them.
/// </summary>
internal static class PropertySnapshot
{
    private static readonly HashSet<string> AuditStamps =
    [
        "CreatedAt", "CreatedBy", "CreatedWorkstation", "UpdatedAt", "UpdatedBy", "UpdatedWorkstation",
    ];

    // Foreign keys back to the parent row. Other Guid members (ConstructionWorkGroupId, DocumentId) are data.
    private static readonly HashSet<string> ParentKeys =
    [
        "AppraisalPropertyId", "LandAppraisalDetailId", "BuildingAppraisalDetailId", "CondoAppraisalDetailId",
        "BuildingDepreciationDetailId", "ConstructionInspectionId", "RentalInfoId",
    ];

    // LandTitle.SequenceNumber is list-position bookkeeping stamped on every title save, so rows that predate
    // the column would log 0 -> n on the first correction and a no-op correction would stop being rejected.
    // A real reorder is audited once, as Land.TitleOrder, by SnapshotDiff.
    private static readonly HashSet<string> Derived =
    [
        "DeductedAreaInSqWa", "ScheduleEntries", "SequenceNumber",
        "ConstructionValue", "CurrentProportionPct", "PreviousPropertyValue", "CurrentPropertyValue",
    ];

    // Value objects are hoisted into their parent so the path reads like the form field
    // (Land.Province, Land.DopaProvince, Land.Latitude, Titles[#1].Rai) instead of Land.Address.Province.
    private static readonly Dictionary<string, string> HoistedValueObjects = new()
    {
        ["Address"] = "",
        ["DopaAddress"] = "Dopa",
        ["Coordinates"] = "",
        ["Area"] = "",
    };

    private static readonly JsonSerializerOptions Options = new()
    {
        TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipStoredButNotOwnedMembers } },
    };

    public static JsonObject Take(AppraisalProperty property)
    {
        var snapshot = new JsonObject();
        Add(snapshot, "Land", property.LandDetail);
        Add(snapshot, "Building", property.BuildingDetail);
        Add(snapshot, "Condo", property.CondoDetail);
        Add(snapshot, "Machinery", property.MachineryDetail);
        Add(snapshot, "Vehicle", property.VehicleDetail);
        Add(snapshot, "Vessel", property.VesselDetail);
        Add(snapshot, "LeaseAgreement", property.LeaseAgreementDetail);
        Add(snapshot, "Rental", property.RentalInfo);
        Add(snapshot, "ConstructionInspection", property.ConstructionInspection);
        return snapshot;
    }

    private static void Add<T>(JsonObject snapshot, string section, T? detail) where T : class
    {
        if (detail is null)
            return;

        var node = JsonSerializer.SerializeToNode(detail, Options);
        Hoist(node);
        snapshot[section] = node;
    }

    private static void Hoist(JsonNode? node)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
                Hoist(item);
            return;
        }

        if (node is not JsonObject obj)
            return;

        foreach (var (name, value) in obj.ToList())
        {
            if (!HoistedValueObjects.TryGetValue(name, out var prefix) || value is not (null or JsonObject))
            {
                Hoist(value); // also covers a scalar that only shares the name (a depreciation row's Area)
                continue;
            }

            obj.Remove(name);
            if (value is not JsonObject nested)
                continue; // absent value object reads the same as null

            foreach (var (childName, child) in nested.ToList())
            {
                nested.Remove(childName);
                obj[prefix + childName] = child;
            }
        }
    }

    private static void SkipStoredButNotOwnedMembers(JsonTypeInfo type)
    {
        if (type.Kind != JsonTypeInfoKind.Object)
            return;

        for (var i = type.Properties.Count - 1; i >= 0; i--)
        {
            var p = type.Properties[i];
            if (AuditStamps.Contains(p.Name) || ParentKeys.Contains(p.Name) || Derived.Contains(p.Name) || IsComputedScalar(p))
                type.Properties.RemoveAt(i);
        }
    }

    // Get-only scalars are computed from other members; get-only collections are the owned child rows.
    private static bool IsComputedScalar(JsonPropertyInfo property) =>
        property.AttributeProvider is PropertyInfo { SetMethod: null } info
        && !(info.PropertyType.IsGenericType && info.PropertyType.GetGenericTypeDefinition() == typeof(IReadOnlyList<>));
}
