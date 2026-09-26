using System.Text.Json;
using Mapster;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// The PMA pages post one form object to create, to the full save and to the draft save, so all
/// three requests have to bind the same fields with the same types. Nothing else enforces it: a
/// name the request does not declare is dropped by System.Text.Json, a name the command does not
/// declare is dropped by Mapster, and neither says a word.
///
/// The condo draft declared BuiltOnTitleNumber after the full save had moved to TitleNumber. The
/// form sends titleNumber, so the draft received null — and CondoPmaApplier writes the title
/// number unconditionally, so saving a draft erased a deed number that was already saved.
/// </summary>
public class PmaDraftBindingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private static string[] Shape(Type t) =>
        t.GetProperties().Select(p => $"{p.Name}:{p.PropertyType}").Order().ToArray();

    /// <summary>Each request the page can post, paired with the full save it must match.</summary>
    public static TheoryData<Type, Type> RequestsSharingOneForm => new()
    {
        { typeof(SaveCondoPMAPropertyDraftRequest), typeof(UpdateCondoPMAPropertyRequest) },
        { typeof(CreateCondoPMAPropertyRequest), typeof(UpdateCondoPMAPropertyRequest) },
        { typeof(SaveLandPMAPropertyDraftRequest), typeof(UpdateLandPMAPropertyRequest) },
        { typeof(CreateLandPMAPropertyRequest), typeof(UpdateLandPMAPropertyRequest) },
    };

    [Theory]
    [MemberData(nameof(RequestsSharingOneForm))]
    public void Every_request_the_page_posts_binds_the_same_fields_with_the_same_types(
        Type request, Type fullSave)
    {
        Assert.Equal(Shape(fullSave), Shape(request));
    }

    /// <summary>Each request and the command its endpoint adapts it into.</summary>
    public static TheoryData<Type, Type> RequestToCommand => new()
    {
        { typeof(SaveCondoPMAPropertyDraftRequest), typeof(SaveCondoPMAPropertyDraftCommand) },
        { typeof(UpdateCondoPMAPropertyRequest), typeof(UpdateCondoPMAPropertyCommand) },
        { typeof(CreateCondoPMAPropertyRequest), typeof(CreateCondoPMAPropertyCommand) },
        { typeof(SaveLandPMAPropertyDraftRequest), typeof(SaveLandPMAPropertyDraftCommand) },
        { typeof(UpdateLandPMAPropertyRequest), typeof(UpdateLandPMAPropertyCommand) },
        { typeof(CreateLandPMAPropertyRequest), typeof(CreateLandPMAPropertyCommand) },
    };

    [Theory]
    [MemberData(nameof(RequestToCommand))]
    public void Every_request_field_survives_the_adapt_into_its_command(Type request, Type command)
    {
        // The command also carries route ids (AppraisalId, PropertyId, GroupId), so it is a
        // superset — but every field the request binds has to arrive with the same type.
        var missing = Shape(request).Except(Shape(command)).ToArray();

        Assert.Empty(missing);
    }

    [Fact]
    public void Condo_draft_carries_the_title_number_the_form_sends_through_to_the_command()
    {
        // Shaped like CondoPMAPage's getValues(): camelCase, titleNumber.
        const string body = """{ "titleNumber": "12345", "roomNumber": "8/1" }""";

        var request = JsonSerializer.Deserialize<SaveCondoPMAPropertyDraftRequest>(body, Web)!;
        var command = request.Adapt<SaveCondoPMAPropertyDraftCommand>();

        Assert.Equal("12345", command.TitleNumber);
        Assert.Equal("8/1", command.RoomNumber);
    }
}
