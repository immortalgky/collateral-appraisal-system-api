namespace Document.Tests.Document.Documents.FeatureTests.UpdateDocumentTests;

public class UpdateDocumentCommandValidatorTests
{
    private readonly UpdateDocumentCommandValidator _validator = new();

    [Fact]
    public void Should_HaveError_When_IdIsZero()
    {
        var command = new UpdateDocumentCommand(0, "Valid comment");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id)
              .WithErrorMessage("Id must be greater than 0.");
    }

    [Fact]
    public void Should_HaveError_When_IdIsNegative()
    {
        var command = new UpdateDocumentCommand(-5, "Valid comment");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.Id)
              .WithErrorMessage("Id must be greater than 0.");
    }

    [Fact]
    public void Should_HaveError_When_CommentIsNull()
    {
        var command = new UpdateDocumentCommand(1, null!);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewComment)
              .WithErrorMessage("Comment is required.");
    }

    [Fact]
    public void Should_NotHaveError_When_CommandIsValid()
    {
        var command = new UpdateDocumentCommand(1, "This is a comment");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }
}
