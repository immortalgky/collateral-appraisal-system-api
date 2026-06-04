using PuppeteerSharp;

namespace Reporting.Infrastructure.BrowserPool;

/// <summary>
/// Manages a single pooled Chromium browser instance for PDF rendering.
/// Registered as a singleton + IHostedService to warm up on startup.
/// </summary>
public interface IBrowserPool
{
    /// <summary>
    /// Returns an <see cref="IPage"/> from the pool.
    /// The page is exclusively leased to the caller; the caller MUST dispose it
    /// when done (which closes it and releases the semaphore slot).
    /// </summary>
    Task<LeasedPage> AcquirePageAsync(CancellationToken cancellationToken);
}

/// <summary>
/// A leased Chromium <see cref="IPage"/> that releases its semaphore slot on dispose.
/// </summary>
public sealed class LeasedPage(IPage page, SemaphoreSlim semaphore) : IAsyncDisposable
{
    public IPage Page { get; } = page;

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Page.CloseAsync();
        }
        catch
        {
            // Ignore close errors — browser may already be disconnected
        }
        finally
        {
            semaphore.Release();
        }
    }
}
