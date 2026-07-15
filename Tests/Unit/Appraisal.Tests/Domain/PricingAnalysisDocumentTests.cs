using Appraisal.Domain.Appraisals;
using Shared.Exceptions;
using Appraisal.Domain.Appraisals.Events;

namespace Appraisal.Tests.Domain;

/// <summary>
/// Domain-level tests for PricingAnalysis document attach/update/remove — no EF, no HTTP,
/// just the aggregate. Locks in which domain event fires for each Link/Update/Unlink transition,
/// since that's what the outbox → RabbitMQ → Document-module chain depends on downstream.
/// </summary>
public class PricingAnalysisDocumentTests
{
    private static PricingAnalysis CreateAnalysis() =>
        PricingAnalysis.CreateForPropertyGroup(Guid.NewGuid());

    [Fact]
    public void AddDocument_WithDocumentId_RaisesDocumentLinkedEvent()
    {
        var analysis = CreateAnalysis();

        var document = analysis.AddDocument(new PricingAnalysisDocumentData(
            DocumentId: Guid.NewGuid(),
            FileName: "survey.pdf",
            FilePath: null,
            UploadedBy: "211",
            UploadedByName: "John",
            UploadedAt: DateTime.UtcNow));

        Assert.Single(analysis.Documents);
        Assert.Same(document, analysis.Documents[0]);

        var domainEvent = Assert.Single(analysis.DomainEvents);
        var linked = Assert.IsType<DocumentLinkedEvent>(domainEvent);
        Assert.Equal(analysis.Id, linked.PricingId);
        Assert.Equal(document.DocumentId, linked.DocumentId);
    }

    [Fact]
    public void AddDocument_WithoutDocumentId_RaisesNoEvent()
    {
        // Placeholder entry (e.g. a required-document slot not yet uploaded) — nothing to link yet.
        var analysis = CreateAnalysis();

        analysis.AddDocument(new PricingAnalysisDocumentData(
            DocumentId: null,
            FileName: null,
            FilePath: null,
            UploadedBy: null,
            UploadedByName: null,
            UploadedAt: null));

        Assert.Empty(analysis.DomainEvents);
    }

    [Fact]
    public void UpdateDocument_ReplacingDocumentId_RaisesDocumentUpdatedEvent()
    {
        var analysis = CreateAnalysis();
        var firstDocumentId = Guid.NewGuid();
        var document = analysis.AddDocument(new PricingAnalysisDocumentData(
            firstDocumentId, "v1.pdf", null, "211", "John", DateTime.UtcNow));
        analysis.ClearDomainEvents();

        var secondDocumentId = Guid.NewGuid();
        analysis.UpdateDocument(document.Id, new PricingAnalysisDocumentData(
            secondDocumentId, "v2.pdf", null, "212", "Jane", DateTime.UtcNow));

        var domainEvent = Assert.Single(analysis.DomainEvents);
        var updated = Assert.IsType<DocumentUpdatedEvent>(domainEvent);
        Assert.Equal(firstDocumentId, updated.PreviousDocumentId);
        Assert.Equal(secondDocumentId, updated.DocumentId);
    }

    [Fact]
    public void UpdateDocument_ClearingDocumentId_RaisesDocumentUnlinkedEvent()
    {
        var analysis = CreateAnalysis();
        var documentId = Guid.NewGuid();
        var document = analysis.AddDocument(new PricingAnalysisDocumentData(
            documentId, "v1.pdf", null, "211", "John", DateTime.UtcNow));
        analysis.ClearDomainEvents();

        analysis.UpdateDocument(document.Id, new PricingAnalysisDocumentData(
            null, null, null, null, null, null));

        var domainEvent = Assert.Single(analysis.DomainEvents);
        var unlinked = Assert.IsType<DocumentUnlinkedEvent>(domainEvent);
        Assert.Equal(documentId, unlinked.DocumentId);
    }

    [Fact]
    public void UpdateDocument_UnknownEntryId_ThrowsDomainException()
    {
        var analysis = CreateAnalysis();

        Assert.Throws<DomainException>(() =>
            analysis.UpdateDocument(Guid.NewGuid(), new PricingAnalysisDocumentData(
                Guid.NewGuid(), "x.pdf", null, "211", "John", DateTime.UtcNow)));
    }

    [Fact]
    public void RemoveDocument_WithLinkedDocument_RaisesDocumentUnlinkedEvent()
    {
        var analysis = CreateAnalysis();
        var documentId = Guid.NewGuid();
        var document = analysis.AddDocument(new PricingAnalysisDocumentData(
            documentId, "v1.pdf", null, "211", "John", DateTime.UtcNow));
        analysis.ClearDomainEvents();

        analysis.RemoveDocument(document.Id);

        Assert.Empty(analysis.Documents);
        var domainEvent = Assert.Single(analysis.DomainEvents);
        var unlinked = Assert.IsType<DocumentUnlinkedEvent>(domainEvent);
        Assert.Equal(documentId, unlinked.DocumentId);
    }

    [Fact]
    public void RemoveDocument_UnknownEntryId_ThrowsDomainException()
    {
        var analysis = CreateAnalysis();

        Assert.Throws<DomainException>(() => analysis.RemoveDocument(Guid.NewGuid()));
    }
}
