namespace Document.Tests.Document.Documents.FeatureTests.UploadDocumentTests;

public class UploadDocumentCommandValidatorTests : DocumentServiceTestBase
{
    private readonly UploadDocumentCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_Command()
    {
        var file = CreateMockFile("valid.pdf", new byte[1024]);
        var command = new UploadDocumentCommand([file], "REQ123", 1);

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_When_Documents_Is_Null()
    {
        var command = new UploadDocumentCommand(null!, "REQ123", 1);

        var result = _validator.Validate(command);

        Assert.Contains(result.Errors,
            e => e.PropertyName == "Documents" && e.ErrorMessage.Contains("Document is required"));
    }

    [Fact]
    public void Should_Fail_When_Documents_Is_Empty()
    {
        var command = new UploadDocumentCommand([], "REQ123", 1);

        var result = _validator.TestValidate(command);

        Assert.Contains(result.Errors,
            e => e.ErrorMessage.Contains("At least one document is required."));
    }

    [Fact]
    public void Should_Fail_When_RelateRequest_Is_Null()
    {
        var file = CreateMockFile("test.pdf", new byte[512]);
        var command = new UploadDocumentCommand([file], null!, 1);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.RelateRequest)
            .WithErrorMessage("RelateRequest is required.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Should_Fail_When_RelateId_Is_Invalid(long relateId)
    {
        var file = CreateMockFile("test.pdf", new byte[512]);
        var command = new UploadDocumentCommand([file], "REQ", relateId);

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(c => c.RelateId)
            .WithErrorMessage("RelateId must be greater than 0.");
    }
}