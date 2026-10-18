using Appraisal.Application.Services;
using Appraisal.Domain.Projects;
using Collateral.Contracts.BlockUnits;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Appraisal.Tests.Application.Services;

/// <summary>
/// Regression: the collateral master stores ProjectType as a code ("U"/"LB"/"L"), never "Condo".
/// The seeded unit shape must follow the new project's own type, or a Condo reappraisal builds
/// land-and-building units and ImportUnits crashes while linking towers.
/// </summary>
public class AppraisalCreationServiceSeedUnitsTests
{
    private static ProjectMasterUnitDto Unit(string? towerName = "A") => new(
        SequenceNumber: 1, IsSold: false, PurchaseBy: null, LoanBankName: null,
        ModelType: "TypeA", UsableArea: 35m, SellingPrice: 2_000_000m,
        Floor: 3, TowerName: towerName, CondoRegistrationNumber: "1/2567", RoomNumber: "301",
        PlotNumber: null, HouseNumber: null, NumberOfFloors: null, LandArea: null);

    // The seed method only touches unitOfWork, logger and sender; the rest are never used.
    private static AppraisalCreationService CreateService(string masterProjectType)
    {
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<GetProjectMasterUnitsByPrevAppraisalQuery>(), Arg.Any<CancellationToken>())
            .Returns(new ProjectMasterUnitsResult(masterProjectType, [Unit(masterProjectType == "U" ? "A" : null)]));

        return new AppraisalCreationService(
            null!, Substitute.For<IAppraisalUnitOfWork>(), null!, null!,
            NullLogger<AppraisalCreationService>.Instance, null!, null!, null!, sender);
    }

    [Fact]
    public async Task SeedProjectUnitsFromPrior_CondoProject_MasterTypeCode_SeedsLinkedCondoUnits()
    {
        var service = CreateService("U");
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);

        await service.SeedProjectUnitsFromPriorAsync(project, Guid.NewGuid(), CancellationToken.None);

        var unit = Assert.Single(project.Units);
        Assert.Equal("A", unit.TowerName);
        Assert.NotNull(unit.ProjectTowerId);
        Assert.NotNull(unit.ProjectModelId);
    }

    [Fact]
    public async Task SeedProjectUnitsFromPrior_MasterTypeDiffersFromProjectType_SeedsNothingAndDoesNotThrow()
    {
        var service = CreateService("LB");
        var project = Project.Create(Guid.NewGuid(), ProjectType.Condo);

        await service.SeedProjectUnitsFromPriorAsync(project, Guid.NewGuid(), CancellationToken.None);

        Assert.Empty(project.Units);
    }

    [Fact]
    public async Task SeedProjectUnitsFromPrior_LandMasterIntoLandAndBuildingProject_StillSeeds()
    {
        var service = CreateService("L");
        var project = Project.Create(Guid.NewGuid(), ProjectType.LandAndBuilding);

        await service.SeedProjectUnitsFromPriorAsync(project, Guid.NewGuid(), CancellationToken.None);

        Assert.Single(project.Units);
    }
}
