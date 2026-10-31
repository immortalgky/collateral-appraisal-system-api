using Auth.Contracts.Users;
using Microsoft.EntityFrameworkCore;

namespace Appraisal.Application.Features.Assignments.AssignmentEmails;

/// <param name="CreatedBy">Bank code of the admin who chose or resent it; null when the system wrote it.</param>
/// <param name="SentTo">The addresses it went to; empty until it is handed to the sender.</param>
/// <param name="CcCount">Resolved Cc addresses it went to.</param>
public record AssignmentEmailHistoryItem(
    Guid Id,
    bool IsResend,
    string Status,
    string? CreatedBy,
    string? CreatedByName,
    DateTime? CreatedAt,
    DateTime? SentAt,
    List<string> SentTo,
    int CcCount,
    int AttachmentCount);

/// <param name="SentTo">What went out, resolved. <paramref name="ExtraTo"/> / <paramref name="Cc"/> are what the admin
/// composed (Cc role chips unresolved), for ส่งอีกครั้ง to pre-fill.</param>
public record AssignmentEmailSentResult(
    Guid Id,
    bool IsResend,
    string Status,
    string? CreatedBy,
    DateTime? CreatedAt,
    DateTime? SentAt,
    List<string> SentTo,
    List<string> SentCc,
    List<string> ExtraTo,
    List<string> Cc,
    string Subject,
    string BodyHtml,
    List<AssignmentEmailAttachmentDto> Attachments);

/// <summary>The slip's "อีเมลถึงบริษัท" history (#623): every email of the case, newest first.</summary>
public record GetAssignmentEmailHistoryQuery(Guid AppraisalId) : IQuery<List<AssignmentEmailHistoryItem>>;

/// <summary>ดูฉบับที่ส่ง (#623): one email of the case as it went out.</summary>
public record GetAssignmentEmailSentQuery(Guid AppraisalId, Guid EmailId) : IQuery<AssignmentEmailSentResult>;

public class GetAssignmentEmailHistoryQueryHandler(
    AppraisalDbContext dbContext,
    IUserLookupService userLookup,
    ICurrentUserService currentUser) : IQueryHandler<GetAssignmentEmailHistoryQuery, List<AssignmentEmailHistoryItem>>
{
    public async Task<List<AssignmentEmailHistoryItem>> Handle(GetAssignmentEmailHistoryQuery query, CancellationToken ct)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);

        var rows = await dbContext.AssignmentEmails.AsNoTracking()
            .Where(e => e.AppraisalId == query.AppraisalId)
            .OrderByDescending(e => e.CreatedAt).ThenByDescending(e => e.Id)
            .Select(e => new
            {
                e.Id, e.IsResend, e.Status, e.CreatedBy, e.CreatedAt, e.SentAt, e.SentTo,
                CcCount = e.SentCc.Count, AttachmentCount = e.Attachments.Count
            })
            .ToListAsync(ct);

        var codes = rows.Select(r => r.CreatedBy).OfType<string>().Distinct().ToList();
        var users = codes.Count == 0 ? new Dictionary<string, UserLookupDto>() : await userLookup.GetByUsernamesAsync(codes, ct);

        return rows.Select(r => new AssignmentEmailHistoryItem(
            r.Id, r.IsResend, r.Status, r.CreatedBy,
            r.CreatedBy is not null && users.TryGetValue(r.CreatedBy, out var user)
                ? $"{user.FirstName} {user.LastName}".Trim() is { Length: > 0 } name ? name : null
                : null,
            r.CreatedAt, r.SentAt, r.SentTo, r.CcCount, r.AttachmentCount)).ToList();
    }
}

public class GetAssignmentEmailSentQueryHandler(
    AppraisalDbContext dbContext,
    IAssignmentEmailCaseReader caseReader,
    ICurrentUserService currentUser) : IQueryHandler<GetAssignmentEmailSentQuery, AssignmentEmailSentResult>
{
    public async Task<AssignmentEmailSentResult> Handle(GetAssignmentEmailSentQuery query, CancellationToken ct)
    {
        AssignmentEmailAccess.EnsureAllowed(currentUser);

        var email = await dbContext.AssignmentEmails.AsNoTracking()
                        .FirstOrDefaultAsync(e => e.Id == query.EmailId && e.AppraisalId == query.AppraisalId, ct)
                    ?? throw new NotFoundException("AssignmentEmail", query.EmailId);

        // Names come from document.Documents; a document deleted since is still listed, without its name.
        var documents = (await caseReader.GetDocumentsAsync(email.Attachments.Select(a => a.DocumentId).ToList(), ct))
            .ToDictionary(d => d.Id);
        var attachments = email.Attachments
            .Select(a => documents.TryGetValue(a.DocumentId, out var d)
                ? new AssignmentEmailAttachmentDto(a.DocumentId, a.Source, d.FileName, d.FileSizeBytes)
                : new AssignmentEmailAttachmentDto(a.DocumentId, a.Source, null, 0))
            .ToList();

        return new AssignmentEmailSentResult(
            email.Id, email.IsResend, email.Status, email.CreatedBy, email.CreatedAt, email.SentAt,
            email.SentTo, email.SentCc, email.ExtraTo, email.Cc, email.Subject, email.BodyHtml, attachments);
    }
}
