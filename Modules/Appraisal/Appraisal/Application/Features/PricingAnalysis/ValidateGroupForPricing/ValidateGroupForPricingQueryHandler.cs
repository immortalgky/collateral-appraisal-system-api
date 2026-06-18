using Dapper;
using Shared.Data;

namespace Appraisal.Application.Features.PricingAnalysis.ValidateGroupForPricing;

/// <summary>
/// Loads a property group's data and runs the backend pricing-analysis pre-flight checks
/// (see <see cref="PricingGroupValidator"/> for the rules).
///
/// PropertyGroup and its related entities are EF owned types of the Appraisal aggregate
/// and cannot be queried via root DbSets, so this reads through the appraisal SQL views/
/// tables with Dapper — the same pattern used by GetPropertyGroupById — then delegates the
/// rule evaluation to the pure validator.
///
/// The per-property mandatory-field rule is validated on the front-end (it reuses the shared
/// field configs), so this handler only needs building-detail and rental-schedule presence.
/// </summary>
public class ValidateGroupForPricingQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<ValidateGroupForPricingQuery, ValidateGroupForPricingResult>
{
    private sealed record GroupRow(Guid AppraisalId, Guid? PropertyId, string? PropertyType, int? SequenceInGroup);

    public async Task<ValidateGroupForPricingResult> Handle(
        ValidateGroupForPricingQuery query,
        CancellationToken cancellationToken)
    {
        using var connection = connectionFactory.GetOpenConnection();

        // One row per group item (the view LEFT JOINs items, so an empty group yields a
        // single row with a null PropertyId; a missing group yields no rows at all).
        var groupRows = (await connection.QueryAsync<GroupRow>(
            """
            SELECT AppraisalId, PropertyId, PropertyType, SequenceInGroup
            FROM appraisal.vw_PropertyGroupDetail
            WHERE PropertyGroupId = @GroupId
            """,
            new { GroupId = query.PropertyGroupId })).ToList();

        if (groupRows.Count == 0)
        {
            return new ValidateGroupForPricingResult(false,
            [
                new PricingValidationStep("GroupExists", "Property group",
                    PricingValidationStatus.Failed,
                    ["The property group could not be found."])
            ]);
        }

        var appraisalId = groupRows[0].AppraisalId;

        var propertyRows = groupRows
            .Where(r => r.PropertyId is not null && r.PropertyType is not null)
            .ToList();
        var propertyIds = propertyRows.Select(r => r.PropertyId!.Value).ToList();

        var surveyCount = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM appraisal.AppraisalComparables WHERE AppraisalId = @AppraisalId",
            new { AppraisalId = appraisalId });

        var buildingDetailProperties = new HashSet<Guid>();
        var rentalScheduleProperties = new HashSet<Guid>();

        if (propertyIds.Count > 0)
        {
            // Building properties that have at least one building-detail LINE ITEM
            // (the depreciation-detail rows shown in the Building Detail table).
            // The header row in BuildingAppraisalDetails can exist (e.g. Total Building Area
            // filled) with zero line items, so we count BuildingDepreciationDetails — not the
            // header — to honour "each building must have building detail at least 1 record".
            var buildingRows = await connection.QueryAsync<Guid>(
                """
                SELECT DISTINCT bad.AppraisalPropertyId
                FROM appraisal.BuildingDepreciationDetails bdd
                INNER JOIN appraisal.BuildingAppraisalDetails bad
                    ON bad.Id = bdd.BuildingAppraisalDetailId
                WHERE bad.AppraisalPropertyId IN @Ids
                """,
                new { Ids = propertyIds });
            buildingDetailProperties = buildingRows.ToHashSet();

            // Lease properties that have at least one rental-schedule entry (rule 4).
            var rentalRows = await connection.QueryAsync<Guid>(
                """
                SELECT DISTINCT ri.AppraisalPropertyId
                FROM appraisal.RentalInfos ri
                INNER JOIN appraisal.RentalScheduleEntries se ON se.RentalInfoId = ri.Id
                WHERE ri.AppraisalPropertyId IN @Ids
                """,
                new { Ids = propertyIds });
            rentalScheduleProperties = rentalRows.ToHashSet();
        }

        var snapshots = propertyRows.Select(r => new PricingValidationProperty(
            SequenceNumber: r.SequenceInGroup ?? 0,
            TypeCode: r.PropertyType!,
            HasBuildingDetail: buildingDetailProperties.Contains(r.PropertyId!.Value),
            HasRentalSchedule: rentalScheduleProperties.Contains(r.PropertyId!.Value)))
            .ToList();

        return PricingGroupValidator.Evaluate(snapshots, surveyCount);
    }
}
