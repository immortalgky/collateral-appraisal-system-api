using Request.Application.Services;

namespace Request.Tests.Request.Services;

/// <summary>A client may say REQUEST or PREV; FOLLOWUP is the server's (only echoed for a stored FOLLOWUP file); everything else is REQUEST.</summary>
public class ClientDocumentSourceTests
{
    [Theory]
    [InlineData("REQUEST", "REQUEST")]
    [InlineData("PREV", "PREV")]
    [InlineData("FOLLOWUP", "REQUEST")] // a client never creates a FOLLOWUP row
    [InlineData("prev", "PREV")]
    [InlineData(" Prev ", "PREV")]
    [InlineData("request", "REQUEST")]
    [InlineData(" Request\t", "REQUEST")]
    [InlineData("SOMETHING-TOO-LONG", "REQUEST")]
    [InlineData("", "REQUEST")]
    [InlineData(null, "REQUEST")]
    public void Normalize_lets_a_client_say_REQUEST_or_PREV_only(string? source, string expected) =>
        Assert.Equal(expected, ClientDocumentSource.Normalize(source));

    [Theory]
    [InlineData("FOLLOWUP", "FOLLOWUP")]
    [InlineData("PREV", "PREV")]
    [InlineData("junk", "REQUEST")]
    public void Normalize_keeps_FOLLOWUP_only_when_it_echoes_a_stored_FOLLOWUP_file(string source, string expected) =>
        Assert.Equal(expected, ClientDocumentSource.Normalize(source, echoesStoredFollowUp: true));

    [Theory]
    [InlineData("FOLLOWUP", true)]
    [InlineData("FollowUp", true)] // written by older builds
    [InlineData(" followup ", true)]
    [InlineData("REQUEST", false)]
    [InlineData(null, false)]
    public void A_stored_FOLLOWUP_label_is_recognised_in_any_case(string? stored, bool expected) =>
        Assert.Equal(expected, ClientDocumentSource.IsFollowUp(stored));

    [Fact]
    public void An_echoed_legacy_cased_FOLLOWUP_comes_back_canonical() =>
        Assert.Equal("FOLLOWUP", ClientDocumentSource.Normalize("FollowUp", echoesStoredFollowUp: true));
}
