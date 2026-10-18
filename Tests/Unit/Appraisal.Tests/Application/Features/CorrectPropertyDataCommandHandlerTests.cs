using System.Text.Json;
using Appraisal.Application.Features.Appraisals.CorrectPropertyData;
using Appraisal.Application.Features.FireInsuranceRates.GetFireInsuranceRates;
using Appraisal.Domain.Appraisals;
using Appraisal.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Exceptions;
using Shared.Identity;
using Shared.Time;
using AppraisalAggregate = Appraisal.Domain.Appraisals.Appraisal;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// Handler-level rules for <see cref="CorrectPropertyDataCommandHandler"/>: the gates (Completed only,
/// suffix must fit the property), what the diff reports for each kind of change, and that the audit row
/// is written with the right actor. Atomicity with the data change, and that the after-snapshot sees
/// flushed rows, are proven against a real database in PropertyCorrectionAuditTests.
///
/// The 403 case is not covered here: authorization is the endpoint's "appraisal.data-correction"
/// policy, which never reaches the handler.
/// </summary>
public class CorrectPropertyDataCommandHandlerTests
{
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;
    private static readonly DateTime Now = new(2026, 9, 29, 10, 0, 0);

    private readonly IAppraisalRepository _repository = Substitute.For<IAppraisalRepository>();
    private readonly ISender _mediator = Substitute.For<ISender>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IDateTimeProvider _clock = Substitute.For<IDateTimeProvider>();
    private readonly AppraisalDbContext _db = new(
        new DbContextOptionsBuilder<AppraisalDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    public CorrectPropertyDataCommandHandlerTests()
    {
        _currentUser.UserCode.Returns("EMP001");
        _clock.ApplicationNow.Returns(Now);
    }

    private CorrectPropertyDataCommandHandler CreateHandler()
    {
        // The options the API registers, which is what binds the real PUT bodies.
        var json = new Microsoft.AspNetCore.Http.Json.JsonOptions();
        json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        json.SerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        return new(_repository, _db, _mediator, _currentUser, _clock, Options.Create(json));
    }

    private (AppraisalAggregate Appraisal, AppraisalProperty Property) Seed(
        Func<AppraisalAggregate, AppraisalProperty> add, bool completed = true)
    {
        var appraisal = AppraisalAggregate.Create(Guid.NewGuid(), "New", "Normal", new DateTime(2026, 1, 1));
        var property = add(appraisal);
        property.Id = Guid.NewGuid();
        if (completed) appraisal.SyncStatusFromWorkflow(AppraisalStatus.Completed);
        _repository.GetByIdWithPropertiesAsync(appraisal.Id, Arg.Any<CancellationToken>()).Returns(appraisal);
        return (appraisal, property);
    }

    private static JsonElement Body(string json) => JsonDocument.Parse(json).RootElement;

    private Task<CorrectPropertyDataResult> Correct(
        AppraisalAggregate appraisal, AppraisalProperty property, string suffix, string data, string reason = "typo") =>
        CreateHandler().Handle(
            new CorrectPropertyDataCommand(appraisal.Id, property.Id, suffix, reason, Body(data)), Ct);

    private JsonElement LoggedChanges() =>
        JsonDocument.Parse(Assert.Single(_db.AppraisalPropertyCorrectionLogs.Local).ChangedFields).RootElement;

    private static LandTitle Title(LandAppraisalDetail land, string number, decimal rai = 0m)
    {
        var title = LandTitle.Create(land.Id, number, "DEED");
        title.Id = Guid.NewGuid();
        title.Update(null, null, null, null, null, null, null, null, LandArea.Create(rai, 0m, 0m),
            null, null, null, null, null, null, null);
        land.AddTitle(title);
        return title;
    }

    // ───────────────────────────── gates ─────────────────────────────

    [Fact]
    public async Task Unknown_appraisal_is_not_found()
    {
        _repository.GetByIdWithPropertiesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((AppraisalAggregate?)null);

        await Assert.ThrowsAsync<Appraisal.Domain.Appraisals.Exceptions.AppraisalNotFoundException>(() =>
            CreateHandler().Handle(
                new CorrectPropertyDataCommand(Guid.NewGuid(), Guid.NewGuid(), "land-detail", "r", Body("{}")), Ct));
    }

    [Fact]
    public async Task Unknown_property_is_not_found()
    {
        var (appraisal, _) = Seed(a => a.AddLandProperty());

        await Assert.ThrowsAsync<Appraisal.Domain.Appraisals.Exceptions.PropertyNotFoundException>(() =>
            CreateHandler().Handle(
                new CorrectPropertyDataCommand(appraisal.Id, Guid.NewGuid(), "land-detail", "r", Body("{}")), Ct));
    }

    [Fact]
    public async Task An_appraisal_still_in_progress_is_refused_with_a_machine_readable_code()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty(), completed: false);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "B" }"""));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_cancelled_appraisal_stays_read_only()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty(), completed: false);
        appraisal.Cancel("EMP999", new DateTime(2026, 2, 1), "withdrawn");

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "B" }"""));

        Assert.Equal("APPRAISAL_NOT_COMPLETED", exception.Code);
    }

    [Fact]
    public async Task An_unknown_suffix_is_a_bad_request()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Correct(appraisal, property, "no-such-detail", """{ "ownerNameLand": "B" }"""));
    }

    [Theory]
    [InlineData("building-detail")]
    [InlineData("lease-agreement-land-detail")]
    public async Task A_suffix_that_does_not_fit_the_property_type_is_a_bad_request_and_writes_nothing(string suffix)
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());

        await Assert.ThrowsAsync<BadRequestException>(() => Correct(appraisal, property, suffix, "{}"));

        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task The_plain_land_route_is_refused_for_a_lease_land_property_whose_lease_it_would_clear()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementLandProperty());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "B" }"""));

        Assert.NotNull(property.LeaseAgreementDetail);
    }

    [Fact]
    public async Task A_value_of_the_wrong_type_is_a_bad_request()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());

        await Assert.ThrowsAsync<BadRequestException>(() =>
            Correct(appraisal, property, "land-detail", """{ "latitude": "not a number" }"""));
    }

    // ───────────────────────────── the audit row ─────────────────────────────

    [Fact]
    public async Task A_change_is_flushed_and_recorded_once_with_the_actor_reason_and_type()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A");

        var result = await Correct(appraisal, property, "land-detail",
            """{ "ownerNameLand": "Owner B" }""", reason: "  owner keyed from the contact person  ");

        Assert.Equal("Owner B", property.LandDetail.OwnerName);
        Assert.Equal(1, result.ChangedFieldCount);
        Assert.Equal(["Land.OwnerName"], result.ChangedFields);
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());

        var log = Assert.Single(_db.AppraisalPropertyCorrectionLogs.Local);
        Assert.Equal(appraisal.Id, log.AppraisalId);
        Assert.Equal(property.Id, log.AppraisalPropertyId);
        Assert.Equal(PropertyType.Land.Code, log.PropertyType);
        Assert.Equal("owner keyed from the contact person", log.Reason);
        Assert.Equal("EMP001", log.ChangedBy);
        Assert.Equal(Now, log.ChangedAt);

        var change = LoggedChanges().GetProperty("Land.OwnerName");
        Assert.Equal("Owner A", change.GetProperty("from").GetString());
        Assert.Equal("Owner B", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task The_actor_falls_back_to_the_username_then_to_unknown()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        _currentUser.UserCode.Returns((string?)null);
        _currentUser.Username.Returns("jsmith");

        await Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "B" }""");

        Assert.Equal("jsmith", Assert.Single(_db.AppraisalPropertyCorrectionLogs.Local).ChangedBy);
    }

    [Fact]
    public async Task A_payload_that_changes_nothing_is_no_changes_and_writes_no_audit_row()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A");

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "Owner A" }"""));

        Assert.Equal("NO_CHANGES", exception.Code);
        Assert.Empty(_db.AppraisalPropertyCorrectionLogs.Local);
    }

    [Fact]
    public async Task An_empty_string_for_a_null_field_is_not_a_change()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A");

        // The real form posts "" for every blank field.
        var result = await Correct(appraisal, property, "land-detail",
            """{ "ownerNameLand": "Owner B", "street": "", "soi": "" }""");

        Assert.Equal(["Land.OwnerName"], result.ChangedFields);
    }

    [Fact]
    public async Task False_for_a_null_flag_is_not_a_change()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A");

        // The real form posts false for every unset flag.
        var result = await Correct(appraisal, property, "land-detail",
            """{ "ownerNameLand": "Owner B", "isExpropriated": false, "hasElectricity": false }""");

        Assert.Equal(["Land.OwnerName"], result.ChangedFields);
    }

    [Fact]
    public async Task Omitting_a_stored_scalar_is_reported_as_a_change_because_the_update_is_a_full_overwrite()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        property.LandDetail!.Update(ownerName: "Owner A", street: "Rama II");

        var result = await Correct(appraisal, property, "land-detail", """{ "ownerNameLand": "Owner A" }""");

        // The real PUT would wipe Street too; the audit row must say so rather than hide it.
        Assert.Equal(["Land.Street"], result.ChangedFields);
    }

    // ───────────────────────────── diff paths ─────────────────────────────

    [Fact]
    public async Task A_title_area_change_is_reported_against_the_title_by_its_deed_number()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var title = Title(property.LandDetail!, "1234", rai: 2m);

        await Correct(appraisal, property, "land-detail", $$"""
            { "titles": [ { "id": "{{title.Id}}", "titleNumber": "1234", "titleType": "DEED", "rai": 3, "ngan": 0, "squareWa": 0 } ] }
            """);

        var change = LoggedChanges().GetProperty("Land.Titles[#1234].Rai");
        Assert.Equal(2m, change.GetProperty("from").GetDecimal());
        Assert.Equal(3m, change.GetProperty("to").GetDecimal());
        Assert.Single(LoggedChanges().EnumerateObject());
    }

    [Fact]
    public async Task A_changed_title_number_is_a_change_of_that_title_not_a_removed_and_an_added_row()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var title = Title(property.LandDetail!, "1234", rai: 2m);

        await Correct(appraisal, property, "land-detail", $$"""
            { "titles": [ { "id": "{{title.Id}}", "titleNumber": "1235", "titleType": "NS3", "rai": 2, "ngan": 0, "squareWa": 0 } ] }
            """);

        Assert.Equal("1235", title.TitleNumber);
        Assert.Equal("NS3", title.TitleType);
        var changes = LoggedChanges();
        Assert.Equal("1234", changes.GetProperty("Land.Titles[#1234].TitleNumber").GetProperty("from").GetString());
        Assert.Equal("1235", changes.GetProperty("Land.Titles[#1234].TitleNumber").GetProperty("to").GetString());
        Assert.Equal(2, changes.EnumerateObject().Count());
    }

    // ───────────────────────────── title order ─────────────────────────────

    private static string TitleJson(LandTitle? existing, string number) =>
        (existing is null ? "{" : $"{{ \"id\": \"{existing.Id}\",")
        + $" \"titleNumber\": \"{number}\", \"titleType\": \"DEED\", \"rai\": 1, \"ngan\": 0, \"squareWa\": 0 }}";

    [Fact]
    public async Task A_correction_that_only_reorders_titles_is_accepted_and_logged_once()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var a1 = Title(property.LandDetail!, "111", rai: 1m);
        var b = Title(property.LandDetail!, "222", rai: 1m);
        var c = Title(property.LandDetail!, "333", rai: 1m);

        var result = await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(c, "333")}}, {{TitleJson(a1, "111")}}, {{TitleJson(b, "222")}} ] }""");

        Assert.Equal(["Land.TitleOrder"], result.ChangedFields);
        var change = LoggedChanges().GetProperty("Land.TitleOrder");
        Assert.Equal("111, 222, 333", change.GetProperty("from").GetString());
        Assert.Equal("333, 111, 222", change.GetProperty("to").GetString());
        Assert.Equal(["333", "111", "222"], property.LandDetail!.Titles.Select(t => t.TitleNumber)); // and it is stored
    }

    [Fact]
    public async Task Legacy_titles_re_saved_in_the_same_order_log_nothing_for_order()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var ids = new[] { "00000000-0000-0000-0000-00000000000a", "00000000-0000-0000-0000-00000000000b", "00000000-0000-0000-0000-00000000000c" };
        _ = new[] { "111", "222", "333" }.Select((n, i) =>
        {
            var title = Title(land, n, rai: 1m);
            title.Id = Guid.Parse(ids[i]);
            title.SetSequenceNumber(0); // as before the column existed: AddTitle numbers unnumbered titles
            return title;
        }).ToList();
        // Sent in the order the screen showed them: Titles order (SequenceNumber, then load order).
        var sent = land.Titles.ToList();
        Assert.All(sent, t => Assert.Equal(0, t.SequenceNumber));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{string.Join(", ", sent.Select(t => TitleJson(t, t.TitleNumber)))}} ] }"""));

        Assert.Equal("NO_CHANGES", exception.Code);
        Assert.Equal([1, 2, 3], land.Titles.Select(t => t.SequenceNumber)); // stamped, not audited
    }

    [Fact]
    public async Task Swapping_two_titles_that_share_a_number_still_reads_as_a_change()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var first = Title(property.LandDetail!, "111", rai: 1m);
        var second = Title(property.LandDetail!, "111", rai: 1m);

        await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(second, "111")}}, {{TitleJson(first, "111")}} ] }""");

        var change = LoggedChanges().GetProperty("Land.TitleOrder");
        Assert.Equal("111, 111 (2)", change.GetProperty("from").GetString());
        Assert.Equal("111 (2), 111", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_new_title_that_repeats_a_number_takes_the_next_suffix()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var existing = Title(property.LandDetail!, "111", rai: 1m);

        await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(null, "111")}}, {{TitleJson(existing, "111")}} ] }""");

        var change = LoggedChanges().GetProperty("Land.TitleOrder");
        Assert.Equal("111", change.GetProperty("from").GetString());
        Assert.Equal("111 (2), 111", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_title_added_at_the_end_is_logged_as_the_added_row_only()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var a1 = Title(property.LandDetail!, "111", rai: 1m);
        var b = Title(property.LandDetail!, "222", rai: 1m);

        var result = await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(a1, "111")}}, {{TitleJson(b, "222")}}, {{TitleJson(null, "444")}} ] }""");

        Assert.Equal(["Land.Titles[#444]"], result.ChangedFields);
    }

    [Fact]
    public async Task A_title_added_at_the_top_is_logged_as_the_added_row_and_the_new_order()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var a1 = Title(property.LandDetail!, "111", rai: 1m);
        var b = Title(property.LandDetail!, "222", rai: 1m);

        var result = await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(null, "444")}}, {{TitleJson(a1, "111")}}, {{TitleJson(b, "222")}} ] }""");

        Assert.Equal(new[] { "Land.TitleOrder", "Land.Titles[#444]" }, result.ChangedFields.Order());
        var change = LoggedChanges().GetProperty("Land.TitleOrder");
        Assert.Equal("111, 222", change.GetProperty("from").GetString());
        Assert.Equal("444, 111, 222", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_renumbered_title_that_moves_shows_its_new_number_in_the_new_order()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var a1 = Title(property.LandDetail!, "111", rai: 1m);
        var b = Title(property.LandDetail!, "222", rai: 1m);

        await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(b, "222")}}, {{TitleJson(a1, "999")}} ] }""");

        var change = LoggedChanges().GetProperty("Land.TitleOrder");
        Assert.Equal("111, 222", change.GetProperty("from").GetString());
        Assert.Equal("222, 999", change.GetProperty("to").GetString());
    }

    [Fact]
    public async Task A_removed_number_added_again_on_top_is_not_suffixed()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        Title(property.LandDetail!, "111", rai: 1m);
        var b = Title(property.LandDetail!, "222", rai: 1m);

        var result = await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(null, "111")}}, {{TitleJson(b, "222")}} ] }""");

        // The numbers read the same before and after, so no TitleOrder: the removed and added rows say it all.
        Assert.DoesNotContain("Land.TitleOrder", result.ChangedFields);
        Assert.Contains("Land.Titles[#111]", result.ChangedFields);
    }

    [Fact]
    public async Task Removing_a_title_is_not_an_order_change()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var a1 = Title(property.LandDetail!, "111", rai: 1m);
        Title(property.LandDetail!, "222", rai: 1m);
        var c = Title(property.LandDetail!, "333", rai: 1m);

        var result = await Correct(appraisal, property, "land-detail",
            $$"""{ "titles": [ {{TitleJson(a1, "111")}}, {{TitleJson(c, "333")}} ] }""");

        Assert.Equal(["Land.Titles[#222]"], result.ChangedFields);
    }

    [Fact]
    public async Task Added_and_removed_rows_are_one_entry_each_with_a_row_summary()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());
        var land = property.LandDetail!;
        var gone = LandAreaDeduction.Create(land.Id, "01");
        gone.Id = Guid.NewGuid();
        gone.Update(null, 5m, null);
        land.AddDeduction(gone);

        await Correct(appraisal, property, "land-detail", """
            { "landAreaDeductions": [ { "reasonCode": "99", "reasonOther": "canal", "areaInSqWa": 4 } ] }
            """);

        var changes = LoggedChanges();
        Assert.Equal(JsonValueKind.Null, changes.GetProperty("Land.Deductions[1]").GetProperty("to").ValueKind);
        Assert.Contains("01", changes.GetProperty("Land.Deductions[1]").GetProperty("from").GetString());
        var added = changes.GetProperty("Land.Deductions[1] (2)"); // second entry that shares the label
        Assert.Equal(JsonValueKind.Null, added.GetProperty("from").ValueKind);
        Assert.Contains("canal", added.GetProperty("to").GetString());
    }

    [Fact]
    public async Task Depreciation_rows_and_their_periods_are_reported_by_position_and_content()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        var building = property.BuildingDetail!;
        var dep = building.AddDepreciationDetail("Period", "main", 10m, 2020, true, 1m, 1m, 1m, 1m, 1m, 1m, 1m);
        dep.Id = Guid.NewGuid();
        dep.AddPeriod(1, 5, 2m, 10m, 100m).Id = Guid.NewGuid();
        building.ResolveDerivedValues(); // a building saved since insurance became a stored value

        await Correct(appraisal, property, "building-detail", $$"""
            { "isAppraisable": true,
              "depreciationDetails": [ { "id": "{{dep.Id}}", "depreciationMethod": "Period", "areaDescription": "main",
                "area": 10, "year": 2020, "isBuilding": true, "pricePerSqMBeforeDepreciation": 1, "priceBeforeDepreciation": 1,
                "pricePerSqMAfterDepreciation": 1, "priceAfterDepreciation": 1, "depreciationYearPct": 1,
                "totalDepreciationPct": 1, "priceDepreciation": 1,
                "depreciationPeriods": [ { "atYear": 1, "toYear": 5, "depreciationPerYear": 3, "totalDepreciationPct": 10, "priceDepreciation": 100 } ] } ] }
            """);

        var changes = LoggedChanges();
        var change = changes.GetProperty("Building.DepreciationDetails[1].DepreciationPeriods[1].DepreciationPerYear");
        Assert.Equal(2m, change.GetProperty("from").GetDecimal());
        Assert.Equal(3m, change.GetProperty("to").GetDecimal());
        // The period row was deleted and re-created with a new id; that must not count as a change.
        Assert.Single(changes.EnumerateObject());
    }

    [Fact]
    public async Task A_building_whose_insurance_was_never_stored_gets_the_computed_value_and_it_is_logged()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        var dep = property.BuildingDetail!.AddDepreciationDetail("Gross", "main", 10m, 2020, true, priceAfterDepreciation: 250_400m);
        dep.Id = Guid.NewGuid();

        await Correct(appraisal, property, "building-detail", $$"""
            { "isAppraisable": true,
              "depreciationDetails": [ { "id": "{{dep.Id}}", "depreciationMethod": "Gross", "areaDescription": "main",
                "area": 10, "year": 2020, "isBuilding": true, "priceAfterDepreciation": 250400 } ] }
            """);

        Assert.Equal(250_000m, property.BuildingDetail!.BuildingInsurancePrice);
        var change = LoggedChanges().GetProperty("Building.BuildingInsurancePrice");
        Assert.Equal(JsonValueKind.Null, change.GetProperty("from").ValueKind);
        Assert.Equal(250_000m, change.GetProperty("to").GetDecimal());
    }

    [Fact]
    public async Task Re_saving_identical_child_rows_is_no_changes_even_though_their_ids_are_regenerated()
    {
        var (appraisal, property) = Seed(a => a.AddBuildingProperty());
        var building = property.BuildingDetail!;
        var dep = building.AddDepreciationDetail("Period", "main", 10m, 2020);
        dep.Id = Guid.NewGuid();
        dep.AddPeriod(1, 5, 2m, 10m, 100m).Id = Guid.NewGuid();
        var surface = building.AddSurface(1, 2, "F");
        surface.Id = Guid.NewGuid();
        building.ResolveDerivedValues(); // a building saved since insurance became a stored value

        var exception = await Assert.ThrowsAsync<BadRequestException>(() => Correct(appraisal, property, "building-detail", $$"""
            { "isAppraisable": true,
              "depreciationDetails": [ { "id": "{{dep.Id}}", "depreciationMethod": "Period", "areaDescription": "main",
                "area": 10, "year": 2020,
                "depreciationPeriods": [ { "atYear": 1, "toYear": 5, "depreciationPerYear": 2.00, "totalDepreciationPct": 10, "priceDepreciation": 100 } ] } ],
              "surfaces": [ { "id": "{{surface.Id}}", "fromFloorNumber": 1, "toFloorNumber": 2, "floorType": "F" } ] }
            """));

        Assert.Equal("NO_CHANGES", exception.Code);
    }

    [Fact]
    public async Task A_rental_and_lease_section_created_by_the_payload_is_reported_field_by_field()
    {
        var (appraisal, property) = Seed(a => a.AddLandAndBuildingProperty());

        await Correct(appraisal, property, "land-and-building-detail", """
            { "isRentedOut": true, "leaseAgreement": { "lesseeName": "Lessee" },
              "rentalInfo": { "numberOfYears": 3, "firstYearStartDate": "2026-01-01T00:00:00", "contractRentalFeePerYear": 1200 } }
            """);

        var changes = LoggedChanges();
        Assert.Equal("Lessee", changes.GetProperty("LeaseAgreement.LesseeName").GetProperty("to").GetString());
        Assert.Equal(3, changes.GetProperty("Rental.NumberOfYears").GetProperty("to").GetInt32());
        // The schedule is computed from those fields, so it is not listed on its own.
        Assert.DoesNotContain(changes.EnumerateObject(), p => p.Name.StartsWith("Rental.ScheduleEntries"));
    }

    [Fact]
    public async Task Stored_derived_and_audit_members_never_appear_in_the_diff()
    {
        var (appraisal, property) = Seed(a => a.AddLandProperty());

        await Correct(appraisal, property, "land-detail", """
            { "landAreaDeductions": [ { "reasonCode": "01", "areaInSqWa": 4 } ] }
            """);

        Assert.DoesNotContain(LoggedChanges().EnumerateObject(),
            p => p.Name.Contains("DeductedArea") || p.Name.EndsWith(".Id") || p.Name.Contains("CreatedAt")
                 || p.Name.Contains("UpdatedAt") || p.Name.Contains("AppraisalPropertyId"));
    }

    // ───────────────────────────── condo insurance ─────────────────────────────

    private void GivenCondoRate(string code, decimal ratePerSqm) =>
        _mediator
            .Send(Arg.Any<GetFireInsuranceRatesQuery>(), Arg.Any<CancellationToken>())
            .Returns(new GetFireInsuranceRatesResult([new FireInsuranceRateDto(code, "cond", "Condo", ratePerSqm, 1)]));

    private (AppraisalAggregate Appraisal, AppraisalProperty Property) SeedCondoWithStoredInsurance()
    {
        var seeded = Seed(a => a.AddCondoProperty());
        // Approved earlier at 100/sqm x 50 sqm; the rate has since moved to 200.
        seeded.Property.CondoDetail!.Update(
            usableArea: 50m, fireInsuranceCode: "C1", buildingInsurancePrice: 5000m, ownerName: "Owner A");
        GivenCondoRate("C1", 200m);
        return seeded;
    }

    [Fact]
    public async Task Condo_insurance_price_is_kept_when_neither_the_area_nor_the_condition_changed()
    {
        var (appraisal, property) = SeedCondoWithStoredInsurance();

        await Correct(appraisal, property, "condo-detail",
            """{ "ownerName": "Owner B", "usableArea": 50, "fireInsuranceCode": "C1" }""");

        Assert.Equal(5000m, property.CondoDetail!.BuildingInsurancePrice); // not re-priced at today's 200/sqm
        Assert.Equal(["Condo.OwnerName"], LoggedChanges().EnumerateObject().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Condo_insurance_price_is_re_derived_when_the_usable_area_changed()
    {
        var (appraisal, property) = SeedCondoWithStoredInsurance();

        await Correct(appraisal, property, "condo-detail",
            """{ "ownerName": "Owner A", "usableArea": 60, "fireInsuranceCode": "C1" }""");

        Assert.Equal(12000m, property.CondoDetail!.BuildingInsurancePrice); // 200 x 60
        Assert.True(LoggedChanges().TryGetProperty("Condo.BuildingInsurancePrice", out _));
    }

    [Fact]
    public async Task Condo_insurance_price_is_re_derived_when_the_condition_changed()
    {
        var (appraisal, property) = SeedCondoWithStoredInsurance();
        GivenCondoRate("C2", 300m);

        await Correct(appraisal, property, "condo-detail",
            """{ "ownerName": "Owner A", "usableArea": 50, "fireInsuranceCode": "C2" }""");

        Assert.Equal(15000m, property.CondoDetail!.BuildingInsurancePrice);
    }

    [Fact]
    public async Task A_changed_condition_that_is_not_a_condo_rate_is_a_bad_request()
    {
        var (appraisal, property) = SeedCondoWithStoredInsurance();

        await Assert.ThrowsAsync<BadRequestException>(() => Correct(appraisal, property, "condo-detail",
            """{ "ownerName": "Owner A", "usableArea": 50, "fireInsuranceCode": "NOPE" }"""));
    }

    // ───────────────────────────── the other suffixes ─────────────────────────────

    [Fact]
    public async Task Machinery_vehicle_and_vessel_have_a_route_each()
    {
        var (a1, machinery) = Seed(a => a.AddMachineryProperty());
        var (a2, vehicle) = Seed(a => a.AddVehicleProperty());
        var (a3, vessel) = Seed(a => a.AddVesselProperty());

        var r1 = await Correct(a1, machinery, "machinery-detail", """{ "machineName": "Press" }""");
        var r2 = await Correct(a2, vehicle, "vehicle-detail", """{ "vehicleName": "Truck" }""");
        var r3 = await Correct(a3, vessel, "vessel-detail", """{ "vesselName": "Boat" }""");

        Assert.Equal(["Machinery.MachineName"], r1.ChangedFields);
        Assert.Equal(["Vehicle.VehicleName"], r2.ChangedFields);
        Assert.Equal(["Vessel.VesselName"], r3.ChangedFields);
    }

    [Fact]
    public async Task Lease_agreement_land_corrects_the_lease_and_rental_it_carries()
    {
        var (appraisal, property) = Seed(a => a.AddLeaseAgreementLandProperty());

        var result = await Correct(appraisal, property, "lease-agreement-land-detail",
            """{ "leaseAgreement": { "lesseeName": "Lessee" } }""");

        Assert.Contains("LeaseAgreement.LesseeName", result.ChangedFields);
    }
}
