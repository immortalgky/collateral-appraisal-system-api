namespace Document.Tests.Document.Documents.FeatureTests.GetDocumentByIdTests;

public class GetDocumentByIdQueryValidatorTests
{
    private readonly GetDocumentByIdQueryValidator _validator;

    public GetDocumentByIdQueryValidatorTests()
    {
        _validator = new GetDocumentByIdQueryValidator();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-999)]
    public void Should_HaveValidationError_When_IdIsZeroOrNegative(long id)
    {
        // Arrange
        var query = new GetDocumentByIdQuery(id);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_NotHaveValidationError_When_IdIsValid()
    {
        // Arrange
        var query = new GetDocumentByIdQuery(100);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_HaveExpectedErrorMessage_When_IdIsZero()
    {
        var result = _validator.Validate(new GetDocumentByIdQuery(0));

        Assert.Contains(result.Errors, x => x.ErrorMessage == "Id must be greater than 0.");
    }
}