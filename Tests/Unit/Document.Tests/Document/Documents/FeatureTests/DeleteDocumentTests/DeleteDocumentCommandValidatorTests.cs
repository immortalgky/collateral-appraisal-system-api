namespace Document.Tests.Document.Documents.FeatureTests.DeleteDocumentTests;

public class DeleteDocumentCommandValidatorTests
{
    private readonly DeleteDocumentCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Id_Is_Valid()
    {
        // Arrange
        var command = new DeleteDocumentCommand(1);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_Id_Is_EmptyGuid()
    {
        // Arrange
        var command = new DeleteDocumentCommand(0);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id)
              .WithErrorMessage("Id must be greater than 0.");
    }
}