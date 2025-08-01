using Request.Requests.Features.AddCommentToRequest;

namespace Request.Tests.Request.Requests.Features.UpdateRequestComment;

public class UpdateRequestCommentCommandValidatorTests
{
    [Fact]
    public void Validate_CommentTooLong_ShouldHaveError()
    {
        var validator = new AddCommentToRequestCommandValidator();
        var input = new AddCommentToRequestCommand(1, new string('A', 251));
        var result = validator.Validate(input);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "Comment");
    }
}