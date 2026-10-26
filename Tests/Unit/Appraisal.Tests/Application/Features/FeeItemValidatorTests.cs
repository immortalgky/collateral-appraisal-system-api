using Appraisal.Application.Features.Fees.AddFeeItem;
using Appraisal.Application.Features.Fees.UpdateFeeItem;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using FluentValidation;
using NSubstitute;
using Parameter.Contracts.Parameters;
using Parameter.Contracts.Parameters.Dtos;

namespace Appraisal.Tests.Application.Features;

/// <summary>Input rules of an individual fee line, identical for add and update.</summary>
public class FeeItemValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly IParameterLookupService _lookup = Substitute.For<IParameterLookupService>();

    public FeeItemValidatorTests() =>
        // The TypeOfFee group holds 01, 02 and 99; the lookup answers per requested code like the real one.
        _lookup.GetValidCodesAsync(Arg.Any<ParameterDto>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var dto = call.Arg<ParameterDto>();
                IReadOnlySet<string> known = new HashSet<string> { "01", "02", "99" };
                return Task.FromResult<IReadOnlySet<string>>(
                    dto.Group == "TypeOfFee" && dto.Code is not null && known.Contains(dto.Code)
                        ? new HashSet<string> { dto.Code }
                        : new HashSet<string>());
            });

    private Task<FluentValidation.Results.ValidationResult> Add(string code, string description, decimal amount) =>
        new AddFeeItemCommandValidator(_lookup)
            .ValidateAsync(new AddFeeItemCommand(Guid.NewGuid(), Guid.NewGuid(), code, description, amount), Ct);

    private Task<FluentValidation.Results.ValidationResult> Update(string code, string description, decimal amount) =>
        UpdateOf(Guid.NewGuid(), code, description, amount);

    private readonly AppraisalDbContext _db = Appraisal.Tests.Application.Services.FeeTestSeed.NewDb();

    private Task<FluentValidation.Results.ValidationResult> UpdateOf(Guid itemId, string code, string description, decimal amount) =>
        new UpdateFeeItemCommandValidator(_lookup, _db)
            .ValidateAsync(
                new UpdateFeeItemCommand(Guid.NewGuid(), Guid.NewGuid(), itemId, code, description, amount), Ct);

    // A legacy item whose code is outside TypeOfFee (here 03, "Urgent" in an old comment).
    private async Task<Guid> LegacyItemAsync(string code)
    {
        var assignment = Guid.NewGuid();
        var fee = AppraisalFee.Create(assignment);
        var item = fee.AddItem(code, "legacy", 100m);
        _db.AppraisalFees.Add(fee);
        await _db.SaveChangesAsync(Ct);
        return item.Id;
    }

    [Fact]
    public async Task ValidItem_Passes_ForBoth()
    {
        Assert.True((await Add("02", "Travel", 500m)).IsValid);
        Assert.True((await Update("02", "Travel", 500m)).IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task AmountMustBePositive(double amount)
    {
        var add = await Add("02", "Travel", (decimal)amount);
        var update = await Update("02", "Travel", (decimal)amount);

        Assert.Contains(add.Errors, e => e.PropertyName == "FeeAmount");
        Assert.Contains(update.Errors, e => e.PropertyName == "FeeAmount");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DescriptionMustNotBeBlank(string description)
    {
        Assert.Contains((await Add("02", description, 500m)).Errors, e => e.PropertyName == "FeeDescription");
        Assert.Contains((await Update("02", description, 500m)).Errors, e => e.PropertyName == "FeeDescription");
    }

    [Theory]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("03")]
    public async Task CodeMustBeInTheTypeOfFeeGroup(string code)
    {
        Assert.Contains((await Add(code, "Travel", 500m)).Errors, e => e.PropertyName == "FeeCode");
        Assert.Contains((await Update(code, "Travel", 500m)).Errors, e => e.PropertyName == "FeeCode");
    }

    [Fact]
    public async Task LegacyItem_StaysEditable_WhileItsCodeIsLeftAlone()
    {
        var id = await LegacyItemAsync("03");

        Assert.True((await UpdateOf(id, "03", "Urgent fee", 150m)).IsValid);
    }

    [Fact]
    public async Task LegacyItem_ChangedToAnotherCodeOutsideTypeOfFee_IsRejected()
    {
        var id = await LegacyItemAsync("03");

        Assert.Contains((await UpdateOf(id, "XX", "Urgent fee", 150m)).Errors, e => e.PropertyName == "FeeCode");
    }

    [Fact]
    public async Task ValidItem_ChangedToACodeOutsideTypeOfFee_IsRejected_AndAddNeverGetsTheExemption()
    {
        var id = await LegacyItemAsync("02");

        Assert.Contains((await UpdateOf(id, "03", "Travel", 150m)).Errors, e => e.PropertyName == "FeeCode");
        Assert.Contains((await Add("03", "Urgent", 150m)).Errors, e => e.PropertyName == "FeeCode");
    }

    [Fact]
    public async Task EmptyCode_IsRejectedWithoutAskingTheLookup()
    {
        await Add("", "Travel", 500m);

        await _lookup.DidNotReceiveWithAnyArgs().GetValidCodesAsync(default!, default);
    }
}
