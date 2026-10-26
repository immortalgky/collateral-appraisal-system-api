using System.Reflection;
using Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;
using Appraisal.Application.Features.Project.GetProjectPricingAssumptions;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using Appraisal.Domain.Projects;
using MediatR;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shared.Configuration;
using Shared.Data;

namespace Appraisal.Tests.Application.Features.GetProjectPricingAssumptions;

public class GetProjectPricingAssumptionsShellTests
{
    // Land has no models; one left over from before the type had none must not reach the shell.
    private static Project ProjectOfType(ProjectType type, bool savedAssumption = false)
    {
        var project = Project.Create(Guid.NewGuid(), ProjectType.LandAndBuilding);
        project.AddModel(modelName: "H1");
        // A persisted model row too, so the saved-rows branch (not only the derive-from-models fallback) is exercised.
        if (savedAssumption)
            project.GetOrCreatePricingAssumption().SetModelAssumptions(
                [ProjectModelAssumption.Create(project.Models[0].Id, "H1", null, null, null, null, null, null)]);
        typeof(Project).GetProperty(nameof(Project.ProjectType))!.SetValue(project, type);
        return project;
    }

    private static async Task<int> ShellModelCount(Project project)
    {
        var projects = Substitute.For<IProjectRepository>();
        projects.GetWithFullGraphAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(project);
        var analyses = Substitute.For<IPricingAnalysisRepository>();
        analyses.GetProjectModelPricingSummariesAsync(Arg.Any<IEnumerable<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, ProjectModelPricingSummary>());
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetFireInsuranceRatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetFireInsuranceRatesResult([]));

        var handler = new GetProjectPricingAssumptionsQueryHandler(
            projects, analyses,
            new ForceSaleRateResolver(
                Substitute.For<ISqlConnectionFactory>(),
                Substitute.For<ISystemConfigurationReader>(),
                Substitute.For<ILogger<ForceSaleRateResolver>>()),
            sender);

        var result = await handler.Handle(
            new GetProjectPricingAssumptionsQuery(Guid.NewGuid()), CancellationToken.None);
        return result.Assumption!.ModelAssumptions.Count;
    }

    [Fact]
    public async Task Land_shell_lists_no_models()
    {
        Assert.Equal(0, await ShellModelCount(ProjectOfType(ProjectType.Land)));
    }

    [Fact]
    public async Task Land_with_a_saved_assumption_lists_no_models()
    {
        Assert.Equal(0, await ShellModelCount(ProjectOfType(ProjectType.Land, savedAssumption: true)));
    }

    [Fact]
    public async Task LandAndBuilding_with_a_saved_assumption_still_lists_its_saved_model_rows()
    {
        Assert.Equal(1, await ShellModelCount(ProjectOfType(ProjectType.LandAndBuilding, savedAssumption: true)));
    }

    [Fact]
    public async Task LandAndBuilding_shell_still_lists_its_models()
    {
        Assert.Equal(1, await ShellModelCount(ProjectOfType(ProjectType.LandAndBuilding)));
    }
}
