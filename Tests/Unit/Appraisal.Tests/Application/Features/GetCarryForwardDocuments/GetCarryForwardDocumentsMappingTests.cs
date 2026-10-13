using Appraisal.Application.Features.Appraisals.GetCarryForwardDocuments;

namespace Appraisal.Tests.Application.Features.GetCarryForwardDocuments;

public class GetCarryForwardDocumentsMappingTests
{
    private static GetCarryForwardDocumentsQueryHandler.DocumentRow Row(
        string level, string type, Guid? documentId = null, bool? byDefault = null, Guid? titleId = null,
        string? collateralType = null, string? titleNumber = null) =>
        new()
        {
            Level = level,
            DocumentType = type,
            DocumentId = documentId ?? Guid.NewGuid(),
            PriorTitleId = titleId,
            CollateralType = collateralType,
            TitleNumber = titleNumber,
            CarryForwardByDefault = byDefault
        };

    [Theory]
    [InlineData(false, "Progressive", "D042")]
    [InlineData(false, "New", "D043")]
    [InlineData(false, "ReAppraisal", "D043")]
    [InlineData(false, "PreAppraisal", "D043")]
    [InlineData(false, null, "D043")]
    // Block wins over Progressive, exactly as AppraisalSummaryAutoAttachJob files it.
    [InlineData(true, "Progressive", "D043")]
    [InlineData(true, "New", "D043")]
    public void SummaryCodeFor_matches_the_code_the_auto_attach_job_files(
        bool projectExists, string? appraisalType, string expected)
        => Assert.Equal(expected, GetCarryForwardDocumentsQueryHandler.SummaryCodeFor(projectExists, appraisalType));

    [Theory]
    [InlineData("D042")]
    [InlineData("D043")]
    public void Summary_rows_are_retyped_as_D036_keeping_the_source_code_and_always_default_to_use(string source)
    {
        // The current summary is used even when the document type's flag says false.
        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map([Row("Appraisal", source, byDefault: false)], source));

        Assert.Equal("D036", doc.DocumentType);
        Assert.Equal(source, doc.SourceDocumentType);
        Assert.Equal("Request", doc.Level);
        Assert.True(doc.DefaultUse);
    }

    [Fact]
    public void Every_file_of_the_summary_code_is_kept()
    {
        var rows = new[] { Row("Appraisal", "D043"), Row("Appraisal", "D043"), Row("Appraisal", "D043") };

        var docs = GetCarryForwardDocumentsQueryHandler.Map(rows, "D043");

        Assert.Equal(3, docs.Count);
        Assert.All(docs, d => Assert.True(d.DefaultUse));
    }

    [Fact]
    public void The_summary_code_of_the_other_type_keeps_its_code_and_is_not_used_by_default()
    {
        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map([Row("Appraisal", "D042")], "D043"));

        Assert.Equal("D042", doc.DocumentType);
        Assert.Equal("D042", doc.SourceDocumentType);
        Assert.Equal("Request", doc.Level);
        Assert.False(doc.DefaultUse);
    }

    [Theory]
    [InlineData("Request", "D036")]
    [InlineData("Request", "D042")]
    [InlineData("Request", "D043")]
    [InlineData("Title", "D036")]
    [InlineData("Title", "D042")]
    [InlineData("Title", "D043")]
    [InlineData("Appraisal", "D036")]
    [InlineData("Request", "D036 ")]
    [InlineData("Title", "d043")]
    public void Summary_coded_files_other_than_the_current_summary_keep_their_code_and_are_not_used_by_default(
        string level, string code)
    {
        // Even when the parameter says true: these codes are locked to true for the current summary only.
        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map([Row(level, code, byDefault: true)], "D043"));

        Assert.Equal(code, doc.DocumentType);
        Assert.Equal(code, doc.SourceDocumentType);
        Assert.Equal(level == "Title" ? "Title" : "Request", doc.Level);
        Assert.False(doc.DefaultUse);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void Appraisal_level_files_of_other_codes_follow_the_document_type_flag_as_request_level(
        bool? flag, bool expected)
    {
        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map([Row("Appraisal", "D001", byDefault: flag)], "D043"));

        Assert.Equal("D001", doc.DocumentType);
        Assert.Equal("D001", doc.SourceDocumentType);
        Assert.Equal("Request", doc.Level);
        Assert.Equal(expected, doc.DefaultUse);
    }

    [Fact]
    public void Placeholder_rows_without_a_document_id_are_excluded()
    {
        var placeholder = Row("Request", "D001");
        placeholder.DocumentId = null;

        var docs = GetCarryForwardDocumentsQueryHandler.Map([placeholder, Row("Request", "D002")], "D043");

        Assert.Equal("D002", Assert.Single(docs).DocumentType);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, true)]
    public void DefaultUse_follows_the_document_type_flag_and_defaults_to_true_when_unknown(bool? flag, bool expected)
    {
        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map([Row("Request", "D010", byDefault: flag)], "D043"));

        Assert.Equal(expected, doc.DefaultUse);
        Assert.Equal("D010", doc.SourceDocumentType);
    }

    [Fact]
    public void Title_rows_keep_their_level_and_prior_title_id()
    {
        var titleId = Guid.NewGuid();

        var doc = Assert.Single(GetCarryForwardDocumentsQueryHandler.Map(
            [Row("Title", "D005", titleId: titleId, collateralType: "10", titleNumber: "1กข-1234")], "D043"));

        Assert.Equal("Title", doc.Level);
        Assert.Equal(titleId, doc.PriorTitleId);
        Assert.Equal("10", doc.CollateralType);
        Assert.Equal("1กข-1234", doc.TitleNumber);
        Assert.Equal(1, doc.Set);
    }
}
