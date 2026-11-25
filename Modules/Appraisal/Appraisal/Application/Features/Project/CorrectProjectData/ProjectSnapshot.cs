using System.Text.Json.Nodes;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Infrastructure;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

/// <summary>
/// What a project correction is diffed on: only the part the target writes, read untracked from the database
/// so the before and after sides are what is stored, whatever the real handlers did with their own tracked
/// copies. Rows serialize as PropertySnapshot's do (stamps, parent keys and derived members dropped, value
/// objects hoisted), so paths read like the property audit's: <c>Project.ProjectName</c>,
/// <c>Land.Titles[#1234].Rai</c>, <c>Model.DepreciationDetails[#…].Periods…</c>.
///
/// Unit prices are keyed by the unit (<c>UnitPrices.#12 A-101.TotalAppraisalValue</c>) and model assumptions by
/// the model's name (<c>Assumption.ModelAssumptions.#Type A.CoverageAmount</c>), so a history row names the unit or
/// model rather than a row position. The <c>#</c> marks the segment as a row key for the client's labels.
/// </summary>
internal static class ProjectSnapshot
{
    public static async Task<JsonObject> TakeAsync(
        AppraisalDbContext db, Guid projectId, string target, Guid? targetId, CancellationToken ct)
    {
        var snapshot = new JsonObject();
        switch (target)
        {
            case ProjectCorrectionTargets.Info:
                var project = await db.Projects.AsNoTracking().FirstAsync(p => p.Id == projectId, ct);
                var node = PropertySnapshot.Node(project) as JsonObject;
                foreach (var name in new[]
                            {
                                "Towers", "Models", "Units", "PricingAssumption", "Land", "UnitPriceSource",
                                "UnitPriceImportFileName", "UnitPriceImportedAt", "UnitPriceImportedBy",
                            })
                    node?.Remove(name);
                snapshot["Project"] = node;
                break;

            case ProjectCorrectionTargets.Land:
                var land = await db.ProjectLands.AsNoTracking()
                    .Include(l => l.Titles)
                    .FirstOrDefaultAsync(l => l.ProjectId == projectId, ct);
                if (land is not null)
                    snapshot["Land"] = PropertySnapshot.Node(land);
                break;

            case ProjectCorrectionTargets.Model:
                var model = await db.ProjectModels.AsNoTracking()
                    .Include(m => m.AreaDetails)
                    .Include(m => m.Surfaces)
                    .Include(m => m.DepreciationDetails).ThenInclude(d => d.DepreciationPeriods)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(m => m.Id == targetId && m.ProjectId == projectId, ct);
                if (model is not null)
                {
                    var modelNode = PropertySnapshot.Node(model) as JsonObject;
                    modelNode?.Remove("Images"); // photos are not corrected here
                    snapshot["Model"] = modelNode;
                }
                break;

            case ProjectCorrectionTargets.Tower:
                var tower = await db.ProjectTowers.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Id == targetId && t.ProjectId == projectId, ct);
                if (tower is not null)
                {
                    var towerNode = PropertySnapshot.Node(tower) as JsonObject;
                    towerNode?.Remove("Images");
                    snapshot["Tower"] = towerNode;
                }
                break;

            case ProjectCorrectionTargets.UnitPrices:
            case ProjectCorrectionTargets.UnitPriceImport:
                await AddUnitPricesAsync(db, projectId, snapshot, ct);
                break;
        }

        return snapshot;
    }

    private static async Task AddUnitPricesAsync(
        AppraisalDbContext db, Guid projectId, JsonObject snapshot, CancellationToken ct)
    {
        var source = await db.Projects.AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.UnitPriceSource, p.UnitPriceImportFileName })
            .FirstAsync(ct);
        snapshot["UnitPriceSource"] = PropertySnapshot.Node(source);

        var modelNames = await db.ProjectModels.AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .ToDictionaryAsync(m => m.Id, m => m.ModelName, ct);

        var assumption = await db.ProjectPricingAssumptions.AsNoTracking()
            .Include(a => a.ModelAssumptions)
            .FirstOrDefaultAsync(a => a.ProjectId == projectId, ct);
        if (assumption is not null)
        {
            var assumptionNode = PropertySnapshot.Node(assumption) as JsonObject;
            assumptionNode?.Remove("ModelAssumptions");
            var byModel = new JsonObject();
            foreach (var row in assumption.ModelAssumptions)
                byModel[Unique(byModel, $"#{modelNames.GetValueOrDefault(row.ProjectModelId) ?? row.ModelType ?? "—"}")] =
                    PropertySnapshot.Node(row);
            if (assumptionNode is not null)
                assumptionNode["ModelAssumptions"] = byModel;
            snapshot["Assumption"] = assumptionNode;
        }

        var units = await db.ProjectUnits.AsNoTracking()
            .Where(u => u.ProjectId == projectId)
            .Select(u => new { u.Id, u.SequenceNumber, Label = u.RoomNumber ?? u.HouseNumber ?? u.PlotNumber })
            .ToListAsync(ct);
        var unitIds = units.Select(u => u.Id).ToList();
        var prices = await db.ProjectUnitPrices.AsNoTracking()
            .Where(p => unitIds.Contains(p.ProjectUnitId))
            .ToDictionaryAsync(p => p.ProjectUnitId, ct);

        var byUnit = new JsonObject();
        foreach (var unit in units.OrderBy(u => u.SequenceNumber))
        {
            if (!prices.TryGetValue(unit.Id, out var price))
                continue;
            var key = string.IsNullOrWhiteSpace(unit.Label)
                ? $"#{unit.SequenceNumber}"
                : $"#{unit.SequenceNumber} {unit.Label}";
            byUnit[key] = PropertySnapshot.Node(price);
        }

        snapshot["UnitPrices"] = byUnit;
    }

    private static string Unique(JsonObject obj, string key)
    {
        var candidate = key;
        for (var n = 2; obj.ContainsKey(candidate); n++)
            candidate = $"{key} ({n})";
        return candidate;
    }
}
