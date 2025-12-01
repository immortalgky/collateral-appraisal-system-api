using System;

namespace Request.RequestDocuments.ValueObjects;

public class DocumentClassification : ValueObject
{
    public string DocumentType { get; }
    public bool IsRequired { get; }

    private DocumentClassification(string documentType, bool isRequired)
    {
        DocumentType = documentType;
        IsRequired = isRequired;
    }

    public static DocumentClassification Create(string documentType, bool isRequired)
    {
        return new DocumentClassification(documentType, isRequired);
    }
}
