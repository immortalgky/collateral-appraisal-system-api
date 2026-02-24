using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Shared.Data.RunningNumbers;

namespace Appraisal.Infrastructure;

/// <summary>
/// Unit of Work implementation for the Appraisal module.
/// Coordinates transactions across repositories and auto-generates running numbers.
/// </summary>
public class AppraisalUnitOfWork : IAppraisalUnitOfWork
{
    private readonly AppraisalDbContext _context;
    private IDbContextTransaction? _currentTransaction;

    public AppraisalUnitOfWork(AppraisalDbContext context)
    {
        _context = context;
    }

    public bool HasActiveTransaction => _currentTransaction is not null;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await GenerateRunningNumbersAsync(cancellationToken);
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is not null)
            throw new InvalidOperationException("A transaction is already in progress");

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            throw new InvalidOperationException("No transaction in progress");

        try
        {
            await SaveChangesAsync(cancellationToken);
            await _currentTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await RollbackTransactionAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction is null)
            return;

        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            await DisposeTransactionAsync();
        }
    }

    public IRepository<T, TId> Repository<T, TId>() where T : class, IEntity<TId>
    {
        throw new NotImplementedException(
            "Use dependency injection to resolve repositories. " +
            "Inject IAppraisalRepository, IAppraisalCollateralRepository, etc. directly.");
    }

    private async Task GenerateRunningNumbersAsync(CancellationToken cancellationToken)
    {
        var thaiYear = DateTime.Now.Year + 543;

        // Appraisals: format {yy}{000000} e.g. "69000001"
        var newAppraisals = _context.ChangeTracker
            .Entries<Domain.Appraisals.Appraisal>()
            .Where(e => e.State == EntityState.Added && e.Entity.AppraisalNumber == null)
            .Select(e => e.Entity)
            .ToList();

        foreach (var appraisal in newAppraisals)
        {
            var next = await GetNextRunningNumberAsync(RunningNumberType.APPRAISAL, thaiYear, cancellationToken);
            var thaiYearShort = thaiYear % 100; // e.g. 69
            appraisal.SetAppraisalNumber($"{thaiYearShort}{next:D6}");
        }

        // MarketComparables: format MKC-{000001}-{YYYY} e.g. "MKC-000001-2569"
        var newComparables = _context.ChangeTracker
            .Entries<Domain.MarketComparables.MarketComparable>()
            .Where(e => e.State == EntityState.Added && e.Entity.ComparableNumber == null)
            .Select(e => e.Entity)
            .ToList();

        foreach (var comparable in newComparables)
        {
            var next = await GetNextRunningNumberAsync(RunningNumberType.MARKET_COMPARABLE, thaiYear, cancellationToken);
            comparable.SetComparableNumber($"MKC-{next:D6}-{thaiYear}");
        }

        // QuotationRequests: format QTN-{000001}-{YYYY} e.g. "QTN-000001-2569"
        var newQuotations = _context.ChangeTracker
            .Entries<Domain.Quotations.QuotationRequest>()
            .Where(e => e.State == EntityState.Added && e.Entity.QuotationNumber == null)
            .Select(e => e.Entity)
            .ToList();

        foreach (var quotation in newQuotations)
        {
            var next = await GetNextRunningNumberAsync(RunningNumberType.QUOTATION, thaiYear, cancellationToken);
            quotation.SetQuotationNumber($"QTN-{next:D6}-{thaiYear}");
        }
    }

    private async Task<int> GetNextRunningNumberAsync(
        RunningNumberType type,
        int year,
        CancellationToken cancellationToken)
    {
        const string sql = """
            DECLARE @NextNumber INT;

            UPDATE dbo.RunningNumbers WITH (UPDLOCK, ROWLOCK, HOLDLOCK)
            SET @NextNumber = CurrentNumber = CurrentNumber + 1,
                UpdatedOn = GETUTCDATE()
            WHERE Type = {0} AND Year = {1};

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.RunningNumbers (Type, Prefix, CurrentNumber, Year, CreatedOn)
                VALUES ({0}, {2}, 1, {1}, GETUTCDATE());
                SET @NextNumber = 1;
            END

            SELECT @NextNumber;
            """;

        var (typeName, prefix) = type switch
        {
            RunningNumberType.APPRAISAL => ("APPRAISAL", "A"),
            RunningNumberType.MARKET_COMPARABLE => ("MARKET_COMPARABLE", "MKC"),
            RunningNumberType.QUOTATION => ("QUOTATION", "QTN"),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        var result = await _context.Database
            .SqlQueryRaw<int>(sql, typeName, year, prefix)
            .ToListAsync(cancellationToken);

        return result.First();
    }

    private async Task DisposeTransactionAsync()
    {
        if (_currentTransaction is not null)
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public void Dispose()
    {
        _currentTransaction?.Dispose();
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}
