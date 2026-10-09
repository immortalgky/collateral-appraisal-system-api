using Appraisal.Application.Configurations;
using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.CorrectAppraisalDocuments;

/// <summary>
/// Admin correction of the valuation documents on a Completed appraisal: attach (<see cref="Add"/> only),
/// delete (<see cref="RemoveId"/> only) or replace (both). <see cref="Reason"/> is mandatory and is stored
/// on the audit row, which is written in the same transaction as the document change.
/// </summary>
public record CorrectAppraisalDocumentsCommand(
    Guid AppraisalId,
    string Reason,
    Guid? RemoveId,
    DocumentToAttach? Add
) : ICommand<CorrectAppraisalDocumentsResult>, ITransactionalCommand<IAppraisalUnitOfWork>;

/// <summary>
/// Only the type and the uploaded document's id: name, mime and size are read from the stored upload, so
/// the audit row can never name a file other than the one attached.
/// </summary>
public record DocumentToAttach(string DocumentTypeCode, Guid DocumentId);
