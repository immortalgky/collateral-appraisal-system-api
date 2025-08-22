using Document.Documents.Models;

namespace Document.Tests.TestData
{
    public static class ModelsTestData
    {
        public static Documents.Models.Document DocumentGeneral(string fileName) => Documents.Models.Document.Create(
            "Request",
            1,
            "DocType",
            fileName,
            DateTime.Now,
            "Set",
            1,
            "Comment",
            $".../{fileName}"
        );
    }
}