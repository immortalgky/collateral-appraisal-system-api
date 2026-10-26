using Appraisal.Application.Features.Appraisals.CreateCondoProperty;
using Appraisal.Application.Features.Appraisals.Shared;
using Appraisal.Application.Features.Appraisals.CreateLeaseAgreementCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateCondoProperty;
using Appraisal.Application.Features.Appraisals.UpdateLandAndBuildingProperty;
using Appraisal.Application.Features.Appraisals.UpdateLeaseAgreementCondoProperty;
using Appraisal.Application.Services;
using Appraisal.Domain.Appraisals;
using MediatR;
using NSubstitute;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// A condo unit has no server-derived 100% base, so the construction-inspection base is pinned to 0
/// on all four write paths (create/update x condo/lease condo) and a forged payload TotalValue is ignored.
/// </summary>
public class CondoConstructionBaseTests
{
    private const decimal Forged = 9_999_999m;
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly IAppraisalRepository _repository = Substitute.For<IAppraisalRepository>();
    private readonly IAppraisalUnitOfWork _unitOfWork = Substitute.For<IAppraisalUnitOfWork>();
    private readonly ISender _mediator = Substitute.For<ISender>();

    private readonly AppraisalValuationSummaryService _valuationSummary =
        Substitute.For<AppraisalValuationSummaryService>(null, null, null, null, null);

    private (AppraisalAggregate Appraisal, AppraisalProperty Property) Seed(
        Func<AppraisalAggregate, AppraisalProperty> add)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        var property = add(appraisal);
        property.Id = Guid.NewGuid();
        _repository.GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return (appraisal, property);
    }

    private static ConstructionInspectionData Summary() => new(IsFullDetail: false, TotalValue: Forged);

    private static ConstructionInspectionData Full() => new(IsFullDetail: true, TotalValue: Forged);

    public static TheoryData<bool> Modes => new() { true, false };

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Update_condo_ignores_the_forged_total_when_creating_the_inspection(bool full)
    {
        var (appraisal, property) = Seed(a => a.AddCondoProperty());

        await new UpdateCondoPropertyCommandHandler(_repository, _mediator, _valuationSummary).Handle(
            new UpdateCondoPropertyCommand(appraisal.Id, property.Id, IsUnderConstruction: true,
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, property.ConstructionInspection!.TotalValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Update_condo_resets_an_existing_inspection_base_to_zero(bool full)
    {
        var (appraisal, property) = Seed(a => a.AddCondoProperty());
        property.SetConstructionInspection(ConstructionInspection.CreateSummary(property.Id, 100m, null, null, null, null));

        await new UpdateCondoPropertyCommandHandler(_repository, _mediator, _valuationSummary).Handle(
            new UpdateCondoPropertyCommand(appraisal.Id, property.Id, IsUnderConstruction: true,
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, property.ConstructionInspection!.TotalValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Update_lease_condo_ignores_the_forged_total(bool full)
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementCondoProperty());

        await new UpdateLeaseAgreementCondoPropertyCommandHandler(_repository, _mediator, _valuationSummary).Handle(
            new UpdateLeaseAgreementCondoPropertyCommand(appraisal.Id, property.Id, IsUnderConstruction: true,
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, property.ConstructionInspection!.TotalValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Update_lease_condo_resets_an_existing_inspection_base_to_zero(bool full)
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementCondoProperty());
        property.SetConstructionInspection(ConstructionInspection.CreateSummary(property.Id, 100m, null, null, null, null));

        await new UpdateLeaseAgreementCondoPropertyCommandHandler(_repository, _mediator, _valuationSummary).Handle(
            new UpdateLeaseAgreementCondoPropertyCommand(appraisal.Id, property.Id, IsUnderConstruction: true,
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, property.ConstructionInspection!.TotalValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Create_condo_ignores_the_forged_total(bool full)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        _repository.GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);

        await new CreateCondoPropertyCommandHandler(_unitOfWork, _repository, _mediator, _valuationSummary).Handle(
            new CreateCondoPropertyCommand(appraisal.Id, IsUnderConstruction: true,
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, appraisal.Properties.Single().ConstructionInspection!.TotalValue);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public async Task Create_lease_condo_ignores_the_forged_total(bool full)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        _repository.GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);

        await new CreateLeaseAgreementCondoPropertyCommandHandler(_repository, _unitOfWork, _mediator, _valuationSummary).Handle(
            new CreateLeaseAgreementCondoPropertyCommand(appraisal.Id, IsUnderConstruction: true,
                LeaseAgreement: new LeaseAgreementData(LesseeName: "Lessee"),
                ConstructionInspection: full ? Full() : Summary()), Ct);

        Assert.Equal(0m, appraisal.Properties.Single().ConstructionInspection!.TotalValue);
    }
}
