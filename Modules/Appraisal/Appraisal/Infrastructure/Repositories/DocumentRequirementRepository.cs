namespace Appraisal.Infrastructure.Repositories;

public class DocumentRequirementRepository : IDocumentRequirementRepository
{
    private readonly AppraisalDbContext _context;

    public DocumentRequirementRepository(AppraisalDbContext context)
    {
        _context = context;
    }

    #region DocumentType Operations

    public async Task<IReadOnlyList<DocumentType>> GetAllDocumentTypesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.DocumentTypes
            .Where(d => d.IsActive)
            .OrderBy(d => d.SortOrder)
            .ThenBy(d => d.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentType?> GetDocumentTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentTypes
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<DocumentType?> GetDocumentTypeByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.DocumentTypes
            .FirstOrDefaultAsync(d => d.Code == code.ToUpperInvariant(), cancellationToken);
    }

    public void AddDocumentType(DocumentType documentType)
    {
        _context.DocumentTypes.Add(documentType);
    }

    public void UpdateDocumentType(DocumentType documentType)
    {
        _context.DocumentTypes.Update(documentType);
    }

    #endregion

    #region DocumentRequirement Operations

    public async Task<IReadOnlyList<DocumentRequirement>> GetAllRequirementsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentRequirements
            .Include(r => r.DocumentType)
            .Where(r => r.IsActive)
            .OrderBy(r => r.CollateralTypeCode ?? "")
            .ThenBy(r => r.DocumentType.SortOrder)
            .ThenBy(r => r.DocumentType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<DocumentRequirement?> GetRequirementByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentRequirements
            .Include(r => r.DocumentType)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentRequirement>> GetApplicationLevelRequirementsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.DocumentRequirements
            .Include(r => r.DocumentType)
            .Where(r => r.IsActive && r.CollateralTypeCode == null)
            .OrderBy(r => r.DocumentType.SortOrder)
            .ThenBy(r => r.DocumentType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByCollateralTypeAsync(
        string collateralTypeCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = collateralTypeCode.ToUpperInvariant();

        return await _context.DocumentRequirements
            .Include(r => r.DocumentType)
            .Where(r => r.IsActive && r.CollateralTypeCode == normalizedCode)
            .OrderBy(r => r.DocumentType.SortOrder)
            .ThenBy(r => r.DocumentType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentRequirement>> GetRequirementsByCollateralTypesAsync(
        IEnumerable<string> collateralTypeCodes,
        CancellationToken cancellationToken = default)
    {
        var normalizedCodes = collateralTypeCodes.Select(c => c.ToUpperInvariant()).ToList();

        return await _context.DocumentRequirements
            .Include(r => r.DocumentType)
            .Where(r => r.IsActive && r.CollateralTypeCode != null && normalizedCodes.Contains(r.CollateralTypeCode))
            .OrderBy(r => r.CollateralTypeCode)
            .ThenBy(r => r.DocumentType.SortOrder)
            .ThenBy(r => r.DocumentType.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> RequirementExistsAsync(
        Guid documentTypeId,
        string? collateralTypeCode,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = collateralTypeCode?.ToUpperInvariant();

        return await _context.DocumentRequirements
            .AnyAsync(r =>
                    r.DocumentTypeId == documentTypeId &&
                    r.CollateralTypeCode == normalizedCode,
                cancellationToken);
    }

    public void AddRequirement(DocumentRequirement requirement)
    {
        _context.DocumentRequirements.Add(requirement);
    }

    public void UpdateRequirement(DocumentRequirement requirement)
    {
        _context.DocumentRequirements.Update(requirement);
    }

    public void DeleteRequirement(DocumentRequirement requirement)
    {
        _context.DocumentRequirements.Remove(requirement);
    }

    #endregion
}
