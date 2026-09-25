using System.Text.Json;
using Mapster;

namespace Appraisal.Tests.Application.Features;

/// <summary>
/// The PMA pages post the same form object to the full save and to the draft save, so the two
/// requests have to bind the same field names. Nothing else enforces it: a name the request does
/// not declare is dropped by System.Text.Json, a name the command does not declare is dropped by
/// Mapster, and neither says a word.
///
/// The condo draft declared BuiltOnTitleNumber after the full save had moved to TitleNumber. The
/// form sends titleNumber, so the draft received null — and CondoPmaApplier writes the title
/// number unconditionally, so saving a draft erased a deed number that was already saved.
/// </summary>
public class PmaDraftBindingTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static TheoryData<Type, Type> DraftAndFullSaveRequests => new()
    {
        { typeof(SaveCondoPMAPropertyDraftRequest), typeof(UpdateCondoPMAPropertyRequest) },
        { typeof(SaveLandPMAPropertyDraftRequest), typeof(UpdateLandPMAPropertyRequest) },
    };

    [Theory]
    [MemberData(nameof(DraftAndFullSaveRequests))]
    public void Draft_binds_exactly_the_fields_the_full_save_binds(Type draft, Type fullSave)
    {
        static string[] Names(Type t) => t.GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.Equal(Names(fullSave), Names(draft));
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
