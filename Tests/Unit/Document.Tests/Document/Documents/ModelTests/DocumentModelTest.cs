namespace Document.Tests.Document.Documents.ModelTests;

public class DocumentModelTest
{
    [Fact]
    public void Should_Create_Document_Successfully()
    {
        var document = global::Document.Documents.Models.Document.Create(
            RelateRequest: "REQ001",
            RelateId: 123,
            docType: "PDF",
            filename: "file1.pdf",
            uploadTime: DateTime.UtcNow,
            prefix: "PX",
            set: 1,
            comment: "Initial doc",
            filePath: "/upload/file1.pdf"
        );

        Assert.Equal("REQ001", document.RelateRequest);
        Assert.Equal(123, document.RelateId);
        Assert.Equal("PDF", document.DocType);
        Assert.Equal("file1.pdf", document.Filename);
        Assert.Equal("PX", document.Prefix);
        Assert.Equal(1, document.Set);
        Assert.Equal("Initial doc", document.Comment);
        Assert.Equal("/upload/file1.pdf", document.FilePath);
    }

    [Theory]
    [InlineData(null, "PDF", "file.pdf", "PX", "Some comment", "/upload/path")]
    [InlineData("REQ001", null, "file.pdf", "PX", "Some comment", "/upload/path")]
    [InlineData("REQ001", "PDF", null, "PX", "Some comment", "/upload/path")]
    [InlineData("REQ001", "PDF", "file.pdf", null, "Some comment", "/upload/path")]
    [InlineData("REQ001", "PDF", "file.pdf", "PX", null, "/upload/path")]
    [InlineData("REQ001", "PDF", "file.pdf", "PX", "Some comment", null)]
    public void Should_Throw_If_Any_Required_Parameter_IsNull(
            string? relateRequest,
            string? docType,
            string? filename,
            string? prefix,
            string? comment,
            string? filePath
        )
    {
        Assert.Throws<ArgumentNullException>(() =>
            global::Document.Documents.Models.Document.Create(
                relateRequest,
                1,
                docType,
                filename,
                uploadTime: DateTime.UtcNow,
                prefix,
                1,
                comment,
                filePath
            ));
    }

    [Fact]
    public void Should_Update_Comment_Successfully()
    {
        var document = global::Document.Documents.Models.Document.Create(
            "REQ002", 321, "PDF", "doc.pdf", DateTime.UtcNow, "PX", 2, "Old comment", "/upload/doc.pdf");

        document.UpdateComment("New comment");

        Assert.Equal("New comment", document.Comment);
    }

    [Fact]
    public void Should_Throw_When_UpdatingCommentToNull()
    {
        var document = global::Document.Documents.Models.Document.Create(
            "REQ002", 321, "PDF", "doc.pdf", DateTime.UtcNow, "PX", 2, "Old comment", "/upload/doc.pdf");

        Assert.Throws<ArgumentNullException>(() => document.UpdateComment(null!));
    }
}