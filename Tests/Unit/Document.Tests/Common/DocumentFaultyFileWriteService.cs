using Document.Contracts.Documents.Dtos;
using Document.Documents.Features.UploadDocument;

public class FaultyFileWriteService : IDocumentService
{
    private readonly IDocumentRepository _repo;

    public FaultyFileWriteService(IDocumentRepository repo)
    {
        _repo = repo;
    }

    public Task<bool> DeleteFileAsync(long id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<(Document.Documents.Models.Document, UploadResultDto)> ProcessFileAsync(IFormFile file, string request, long id, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public async Task<UploadDocumentResult> UploadAsync(IReadOnlyList<IFormFile> files, string relateRequest, long relateId, CancellationToken cancellationToken = default)
    {
        var results = new List<UploadResultDto>();

        foreach (var file in files)
        {
            try
            {
                throw new IOException("Simulated disk full error", unchecked((int)0x80070070));
            }
            catch (IOException ex) when ((uint)ex.HResult == 0x80070070)
            {
                results.Add(new UploadResultDto(false, "Storage full. Cannot upload file at this time."));
            }
            catch (Exception ex)
            {
                results.Add(new UploadResultDto(false, ex.Message));
            }
        }

        return new UploadDocumentResult(results);
    }
}