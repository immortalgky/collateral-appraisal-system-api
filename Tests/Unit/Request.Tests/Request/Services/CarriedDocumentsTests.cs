using Appraisal.Contracts.Appraisals;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Request.Application.Services;
using Request.Contracts.Requests.Dtos;
using Shared.Exceptions;

namespace Request.Tests.Request.Services;

/// <summary>
/// System-created reappraisals carry documents by the same rule as the request page: the carry-forward
/// query's defaultUse files, stamped PREV; a title file goes to the copied title it belongs to or is left out.
/// </summary>
public class CarriedDocumentsTests
{
    private static readonly Guid PriorAppraisal = Guid.NewGuid();

    // A title-level file names its title by key: collateral type + that type's number.
    private static CarryForwardDocumentDto Doc(
        string level, string type = "D001", string? collateralType = null, string? titleNumber = null,
        bool defaultUse = true, string fileName = "a.pdf") =>
        new(Guid.NewGuid(), type, type, level, Guid.NewGuid(), collateralType, titleNumber, fileName, null, null, 1,
            null, "u", "U", DateTime.Now, defaultUse);

    private static CarryForwardDocumentsResult Result(params CarryForwardDocumentDto[] docs) =>
        new(PriorAppraisal, "69000001", docs);

    private static RequestTitleDto Title(string number, string collateralType = "01") => new()
    {
        Id = Guid.NewGuid(), // a copied title's own id never matters
        CollateralType = collateralType,
        TitleNumber = collateralType == "01" ? number : null,
        LicensePlateNumber = collateralType == "10" ? number : null,
        RegistrationStatus = collateralType == "11",
        RegistrationNumber = collateralType == "11" ? number : null,
        VesselRegistrationNumber = collateralType == "12" ? number : null,
        Documents = [new RequestTitleDocumentDto { DocumentId = Guid.NewGuid(), Source = "REQUEST" }]
    };

    [Fact]
    public void A_whitespace_only_number_is_empty_so_the_title_falls_back_to_its_other_number()
    {
        // Vehicle: plate "  " is no plate (the SQL's NULLIF(x, '') reads it as empty), so the registration number is the key.
        var vehicle = new RequestTitleDto
        {
            Id = Guid.NewGuid(),
            CollateralType = "10",
            LicensePlateNumber = "   ",
            VehicleRegistrationNumber = "REG-9",
            Documents = [new RequestTitleDocumentDto { DocumentId = Guid.NewGuid(), Source = "REQUEST" }]
        };
        var doc = Doc("Title", collateralType: "10", titleNumber: "REG-9");

        var (_, titles) = CarriedDocuments.Build(Result(doc), [vehicle]);

        Assert.Equal(doc.DocumentId, Assert.Single(titles!.Single().Documents).DocumentId);
    }

    [Fact]
    public void Only_defaultUse_files_are_carried_and_all_are_stamped_PREV()
    {
        var keep = Doc("Request", "D036");
        var (requestDocs, _) = CarriedDocuments.Build(
            Result(keep, Doc("Request", defaultUse: false), Doc("Request", "D002")), null);

        Assert.Equal(2, requestDocs.Count);
        Assert.DoesNotContain(requestDocs, d => d.DocumentId != keep.DocumentId && d.DocumentType == "D001");
        Assert.All(requestDocs, d => Assert.Equal("PREV", d.Source));
        Assert.Contains(requestDocs, d => d.DocumentType == "D036");
    }

    [Fact]
    public void A_title_file_goes_to_the_one_copied_title_with_its_key_trimmed_and_case_insensitive()
    {
        var doc = Doc("Title", collateralType: "01", titleNumber: " ab-1 ");

        var (_, titles) = CarriedDocuments.Build(Result(doc), [Title("A"), Title("AB-1")]);

        Assert.Empty(titles![0].Documents);
        var carried = Assert.Single(titles[1].Documents);
        Assert.Equal(doc.DocumentId, carried.DocumentId);
        Assert.Equal("PREV", carried.Source);
    }

    [Fact]
    public void The_prior_title_id_is_information_only_it_never_places_a_file()
    {
        var copied = Title("A");
        var doc = Doc("Title", collateralType: "01", titleNumber: "OTHER") with { PriorTitleId = copied.Id };

        var (requestDocs, titles) = CarriedDocuments.Build(Result(doc), [copied]);

        Assert.Empty(requestDocs);
        Assert.Empty(titles!.Single().Documents);
    }

    [Fact]
    public void The_same_number_under_a_different_collateral_type_is_a_different_title()
    {
        var doc = Doc("Title", collateralType: "11", titleNumber: "X1");

        // A land title numbered X1 and a vehicle registered X1: only the type-11 title is the key.
        var (_, titles) = CarriedDocuments.Build(
            Result(doc), [Title("X1", "01"), Title("X1", "10"), Title("X1", "11")]);

        Assert.Empty(titles![0].Documents);
        Assert.Empty(titles[1].Documents);
        Assert.Single(titles[2].Documents);
    }

    [Theory]
    [InlineData("10")]
    [InlineData("11")]
    [InlineData("12")]
    [InlineData("01")]
    public void Each_collateral_type_is_keyed_by_its_own_number(string collateralType)
    {
        var (_, titles) = CarriedDocuments.Build(
            Result(Doc("Title", collateralType: collateralType, titleNumber: "K9")),
            [Title("other", collateralType), Title("K9", collateralType)]);

        Assert.Empty(titles![0].Documents);
        Assert.Single(titles[1].Documents);
    }

    // The request page's movableIdentity rule (FE titleList.ts), mirrored by the view's TitleNumber.
    private static int Placed(RequestTitleDto title, string type, string number)
    {
        var (_, titles) = CarriedDocuments.Build(
            Result(Doc("Title", collateralType: type, titleNumber: number)), [title with { CollateralType = type }]);
        return titles!.Single().Documents.Count;
    }

    [Fact]
    public void A_vehicle_is_keyed_by_its_plate_else_its_vehicle_registration()
    {
        var plated = new RequestTitleDto { LicensePlateNumber = "K9", VehicleRegistrationNumber = "VR-5", Documents = [] };
        var emptyPlate = new RequestTitleDto { LicensePlateNumber = "", VehicleRegistrationNumber = "VR-5", Documents = [] };

        Assert.Equal(1, Placed(plated, "10", "K9"));
        Assert.Equal(0, Placed(plated, "10", "VR-5")); // the plate wins, the registration is not a second key
        Assert.Equal(1, Placed(emptyPlate, "10", "VR-5"));
    }

    [Fact]
    public void A_machine_is_keyed_by_its_registration_number_only_when_registered()
    {
        var registered = new RequestTitleDto { RegistrationStatus = true, RegistrationNumber = "R7", Documents = [] };
        var unregistered = new RequestTitleDto { RegistrationStatus = false, RegistrationNumber = "R7", Documents = [] };

        Assert.Equal(1, Placed(registered, "11", "R7"));
        Assert.Equal(0, Placed(unregistered, "11", "R7")); // not registered: no identity, file stays unplaced
    }

    [Fact]
    public void A_vessel_is_keyed_by_its_registration_else_its_HIN()
    {
        var registered = new RequestTitleDto { VesselRegistrationNumber = "V3", HIN = "H4", Documents = [] };
        var hullOnly = new RequestTitleDto { VesselRegistrationNumber = "", HIN = "H4", Documents = [] };

        Assert.Equal(1, Placed(registered, "12", "V3"));
        Assert.Equal(0, Placed(registered, "12", "H4"));
        Assert.Equal(1, Placed(hullOnly, "12", "H4"));
    }

    [Fact]
    public void A_duplicate_key_or_a_missing_one_leaves_the_file_unplaced_never_at_request_level()
    {
        var duplicated = Doc("Title", collateralType: "01", titleNumber: "A");
        var noMatch = Doc("Title", collateralType: "01", titleNumber: "ZZZ");
        var noKey = Doc("Title", collateralType: null, titleNumber: "A");
        var noNumber = Doc("Title", collateralType: "01", titleNumber: " ");

        var (requestDocs, titles) = CarriedDocuments.Build(
            Result(duplicated, noMatch, noKey, noNumber), [Title("A"), Title("A"), Title("C")]);

        Assert.Empty(requestDocs);
        Assert.All(titles!, t => Assert.Empty(t.Documents));
    }

    [Fact]
    public void The_copied_titles_own_documents_are_no_longer_copied()
    {
        var (_, titles) = CarriedDocuments.Build(Result(), [Title("A")]);

        Assert.Empty(titles!.Single().Documents);
    }

    [Fact]
    public void A_title_file_with_no_copied_titles_is_skipped()
    {
        var (requestDocs, titles) = CarriedDocuments.Build(Result(Doc("Title", collateralType: "01", titleNumber: "A")), null);

        Assert.Empty(requestDocs);
        Assert.Null(titles);
    }

    [Fact]
    public void Nothing_carried_gives_no_documents()
    {
        var (requestDocs, titles) = CarriedDocuments.Build(null, [Title("A")]);

        Assert.Empty(requestDocs);
        Assert.Empty(titles!.Single().Documents);
    }

    private static CarriedDocuments.Placeholder Slot(string type) => new(null, null, type);

    [Fact]
    public void Empty_required_request_rows_stay_only_for_types_that_got_no_file_and_carried_rows_become_required()
    {
        var (requestDocs, _) = CarriedDocuments.Build(
            Result(Doc("Request", "D005"), Doc("Request", "D009")), null,
            [Slot("D005"), Slot("D006"), Slot("D006")]);

        var d005 = Assert.Single(requestDocs, d => d.DocumentType == "D005");
        Assert.NotNull(d005.DocumentId);
        Assert.True(d005.IsRequired); // a placeholder of that type existed on the prior request
        Assert.False(Assert.Single(requestDocs, d => d.DocumentType == "D009").IsRequired);

        var empty = Assert.Single(requestDocs, d => d.DocumentType == "D006"); // once, even if listed twice
        Assert.Null(empty.DocumentId);
        Assert.True(empty.IsRequired);
        Assert.Equal("REQUEST", empty.Source);
        Assert.Equal(3, requestDocs.Count);
    }

    [Fact]
    public void A_required_request_type_stays_on_the_checklist_when_its_file_was_not_carried()
    {
        // Required on the prior request (filled there), but its type defaults to "don't use": nothing carried.
        var (requestDocs, _) = CarriedDocuments.Build(Result(Doc("Request", "D009", defaultUse: false)), null, [Slot("D009")]);

        var empty = Assert.Single(requestDocs);
        Assert.Equal("D009", empty.DocumentType);
        Assert.Null(empty.DocumentId);
        Assert.True(empty.IsRequired);
    }

    private static CarriedDocuments.Placeholder TitleSlot(string type, string number, string collateralType = "01") =>
        new(collateralType, number, type);

    [Fact]
    public void Empty_title_rows_stay_on_the_title_with_the_same_key_for_types_without_a_carried_file()
    {
        var (_, titles) = CarriedDocuments.Build(
            Result(Doc("Title", "D010", collateralType: "01", titleNumber: "A")),
            [Title("A"), Title("B")],
            [TitleSlot("D010", "A"), TitleSlot("D011", "a"), TitleSlot("D010", "B"), TitleSlot("D012", "GONE")]);

        Assert.Equal(["D010", "D011"], titles![0].Documents.Select(d => d.DocumentType));
        Assert.NotNull(titles[0].Documents[0].DocumentId);
        Assert.Null(titles[0].Documents[1].DocumentId);
        var onlyEmpty = Assert.Single(titles[1].Documents);
        Assert.Equal("D010", onlyEmpty.DocumentType);
        Assert.Null(onlyEmpty.DocumentId);
    }

    [Fact]
    public void A_title_type_that_existed_filled_on_the_prior_title_stays_when_its_file_was_not_carried()
    {
        // D020 had a file on the prior title but defaults to "don't use": nothing carried, the type stays.
        var (_, titles) = CarriedDocuments.Build(
            Result(Doc("Title", "D020", collateralType: "01", titleNumber: "A", defaultUse: false)),
            [Title("A")], [TitleSlot("D020", "A")]);

        var row = Assert.Single(titles!.Single().Documents);
        Assert.Equal("D020", row.DocumentType);
        Assert.Null(row.DocumentId);
    }

    [Fact]
    public void Title_placeholders_follow_the_same_key_rules_duplicates_and_other_types_get_none()
    {
        var (_, titles) = CarriedDocuments.Build(
            Result(), [Title("A"), Title("A"), Title("A", "10"), Title("C")],
            [TitleSlot("D011", "A"), TitleSlot("D013", "A", "10"), TitleSlot("D014", "C")]);

        Assert.Empty(titles![0].Documents);           // key A/01 is on two copied titles
        Assert.Empty(titles[1].Documents);
        Assert.Equal("D013", Assert.Single(titles[2].Documents).DocumentType); // A under type 10 is its own key
        Assert.Equal("D014", Assert.Single(titles[3].Documents).DocumentType);
    }

    [Fact]
    public void Placeholders_survive_when_nothing_is_carried()
    {
        var (requestDocs, _) = CarriedDocuments.Build(null, null, [Slot("D005")]);

        Assert.Equal("D005", Assert.Single(requestDocs).DocumentType);
    }

    [Fact]
    public async Task No_prior_in_this_system_carries_nothing_and_asks_nobody()
    {
        var mediator = Substitute.For<ISender>();

        var result = await CarriedDocuments.TryFetchAsync(
            mediator, null, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(result);
        await mediator.DidNotReceive().Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(typeof(NotFoundException))]
    [InlineData(typeof(ConflictException))]
    public async Task A_prior_the_query_refuses_carries_nothing_without_failing(Type refusal)
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync((Exception)Activator.CreateInstance(refusal, "refused")!);

        var result = await CarriedDocuments.TryFetchAsync(
            mediator, PriorAppraisal, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Null(result);
    }

    [Fact]
    public async Task A_Completed_prior_returns_its_documents()
    {
        var expected = Result(Doc("Request"));
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>()).Returns(expected);

        var result = await CarriedDocuments.TryFetchAsync(
            mediator, PriorAppraisal, NullLogger.Instance, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        await mediator.Received(1).Send(
            Arg.Is<GetCarryForwardDocumentsQuery>(q => q.AppraisalId == PriorAppraisal), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Any_other_failure_is_not_swallowed()
    {
        var mediator = Substitute.For<ISender>();
        mediator.Send(Arg.Any<GetCarryForwardDocumentsQuery>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("db down"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => CarriedDocuments.TryFetchAsync(
            mediator, PriorAppraisal, NullLogger.Instance, TestContext.Current.CancellationToken));
    }
}
