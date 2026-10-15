namespace Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;

public record CorrectAppraisalDocumentsRequest(string Reason, Guid? RemoveId, DocumentToAttach? Add);
