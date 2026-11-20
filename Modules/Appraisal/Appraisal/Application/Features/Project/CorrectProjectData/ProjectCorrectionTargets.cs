using System.Text.Json;
using Appraisal.Application.Features.Project.CalculateProjectUnitPrices;
using Appraisal.Application.Features.Project.ImportProjectUnitPrices;
using Appraisal.Application.Features.Project.SaveProject;
using Appraisal.Application.Features.Project.SaveProjectLand;
using Appraisal.Application.Features.Project.SaveProjectPricingAssumptions;
using Appraisal.Application.Features.Project.SaveProjectUnitPrices;
using Appraisal.Application.Features.Project.UpdateProjectModel;
using Appraisal.Application.Features.Project.UpdateProjectTower;
using DomainProject = Appraisal.Domain.Projects.Project;

namespace Appraisal.Application.Features.Project.CorrectProjectData;

internal sealed record ProjectCorrectionInput(
    CorrectProjectDataCommand Command,
    DomainProject Project,
    JsonSerializerOptions Json,
    ISender Mediator,
    CancellationToken Ct);

internal sealed record ProjectCorrectionTarget(string EntryType, Func<ProjectCorrectionInput, Task> Apply);

internal static class ProjectCorrectionTargets
{
    public const string Info = "info";
    public const string Land = "land";
    public const string Model = "models";
    public const string Tower = "towers";
    public const string UnitPrices = "unit-prices";
    public const string UnitPriceImport = "unit-price-import";

    private static readonly IReadOnlyDictionary<string, ProjectCorrectionTarget> ByName =
        new Dictionary<string, ProjectCorrectionTarget>
        {
            [Info] = new(AppraisalPropertyCorrectionLog.ProjectEntryType, async input =>
            {
                var command = Bind<SaveProjectRequest>(input).Adapt<SaveProjectCommand>() with
                {
                    AppraisalId = input.Command.AppraisalId,
                    ProjectType = input.Project.ProjectType.Code,
                };
                await input.Mediator.Send(command, input.Ct);
            }),
            [Land] = new(AppraisalPropertyCorrectionLog.ProjectLandEntryType, async input =>
            {
                var command = Bind<SaveProjectLandRequest>(input).Adapt<SaveProjectLandCommand>() with
                {
                    AppraisalId = input.Command.AppraisalId,
                };
                await input.Mediator.Send(command, input.Ct);
            }),
            [Model] = new(AppraisalPropertyCorrectionLog.ProjectModelEntryType, async input =>
            {
                var command = Bind<UpdateProjectModelRequest>(input).Adapt<UpdateProjectModelCommand>() with
                {
                    AppraisalId = input.Command.AppraisalId,
                    ModelId = input.Command.TargetId!.Value,
                };
                await input.Mediator.Send(command, input.Ct);
            }),
            [Tower] = new(AppraisalPropertyCorrectionLog.ProjectTowerEntryType, async input =>
            {
                var command = Bind<UpdateProjectTowerRequest>(input).Adapt<UpdateProjectTowerCommand>() with
                {
                    AppraisalId = input.Command.AppraisalId,
                    TowerId = input.Command.TargetId!.Value,
                };
                await input.Mediator.Send(command, input.Ct);
            }),
            [UnitPrices] = new(AppraisalPropertyCorrectionLog.ProjectUnitPriceEntryType, async input =>
            {
                var body = Bind<UnitPriceCorrectionData>(input);
                var appraisalId = input.Command.AppraisalId;

                if (body.Assumption is { } a)
                    await input.Mediator.Send(new SaveProjectPricingAssumptionsCommand(
                        appraisalId,
                        a.LocationMethod,
                        a.CornerAdjustment,
                        a.EdgeAdjustment,
                        a.OtherAdjustment,
                        a.ForceSalePercentage,
                        a.PoolViewAdjustment,
                        a.SouthAdjustment,
                        a.FloorIncrementEveryXFloor,
                        a.FloorIncrementAmount,
                        a.NearGardenAdjustment,
                        a.LandIncreaseDecreaseRate,
                        a.ModelAssumptions), input.Ct);

                if (body.UnitPriceFlags is { Count: > 0 } flags)
                    await input.Mediator.Send(new SaveProjectUnitPricesCommand(appraisalId, flags), input.Ct);

                await input.Mediator.Send(
                    new CalculateProjectUnitPricesCommand(appraisalId, RecomputeValuation: false), input.Ct);
            }),
            [UnitPriceImport] = new(AppraisalPropertyCorrectionLog.ProjectUnitPriceEntryType, async input =>
            {
                var file = input.Command.Import!;
                await input.Mediator.Send(
                    new ImportUnitPricesCommand(
                        input.Command.AppraisalId, file.FileName, file.FileContent, RecomputeValuation: false),
                    input.Ct);
            }),
        };

    public static bool IsKnown(string target) => ByName.ContainsKey(target);

    public static bool NeedsId(string target) => target is Model or Tower;

    public static ProjectCorrectionTarget Get(string target) => ByName[target];

    private static TRequest Bind<TRequest>(ProjectCorrectionInput input) where TRequest : class
    {
        try
        {
            return input.Command.Data!.Value.Deserialize<TRequest>(input.Json)
                   ?? throw new BadRequestException("'data' must be the JSON object the block page would send.");
        }
        catch (JsonException ex)
        {
            throw new BadRequestException(
                $"Invalid value at '{ex.Path}'. Please check the data type and format.");
        }
    }
}
public record UnitPriceCorrectionData(
    SaveProjectPricingAssumptionsRequest? Assumption,
    List<ProjectUnitPriceFlagData>? UnitPriceFlags);
