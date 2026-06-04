using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace Reporting.Infrastructure.BrowserPool;

/// <summary>
/// Singleton, long-lived Chromium browser pool.
///
/// Startup (IHostedService.StartAsync):
///   - If <see cref="ReportingConfiguration.ChromiumExecutablePath"/> is set, use it.
///   - Otherwise, call <c>BrowserFetcher.DownloadAsync()</c> once to provision the
///     default Chromium revision to the default local cache (~/.local/share/puppeteer
///     on Linux, %LOCALAPPDATA%\puppeteer on Windows).
///   - Launch the browser headless with --no-sandbox.
///
/// Page acquisition:
///   - Callers await <see cref="AcquirePageAsync"/>, which gates on a SemaphoreSlim
///     (capacity = <see cref="ReportingConfiguration.MaxConcurrentPages"/>).
///   - Returns a <see cref="LeasedPage"/>; disposing it closes the page and releases
///     the semaphore slot.
///
/// Reconnect:
///   - If the browser is disconnected (e.g. IIS app-pool recycle killed the child
///     process), <see cref="AcquirePageAsync"/> calls <see cref="EnsureBrowserAsync"/>
///     which re-launches transparently.
/// </summary>
public sealed class PuppeteerBrowserPool : IBrowserPool, IHostedService, IAsyncDisposable
{
    private readonly ReportingConfiguration _config;
    private readonly ILogger<PuppeteerBrowserPool> _logger;
    private readonly SemaphoreSlim _pageSemaphore;

    // Guard for lazy relaunch
    private readonly SemaphoreSlim _launchLock = new(1, 1);
    private IBrowser? _browser;

    // Shutdown coordination: track background warmup so DisposeAsync can await it,
    // and a flag so a launch that completes mid-shutdown closes itself instead of leaking.
    private Task? _warmupTask;
    private volatile bool _disposed;

    public PuppeteerBrowserPool(
        IOptions<ReportingConfiguration> options,
        ILogger<PuppeteerBrowserPool> logger)
    {
        _config = options.Value;
        _logger = logger;
        _pageSemaphore = new SemaphoreSlim(_config.MaxConcurrentPages, _config.MaxConcurrentPages);
    }

    // ── IHostedService ───────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Warm up OFF the startup critical path. Launching (and on a fresh box,
        // BrowserFetcher.DownloadAsync ~150MB) must never block host startup — on IIS
        // that would trip the app-pool startup time limit. If warmup fails or is slow,
        // the first report request still launches Chromium lazily via EnsureBrowserAsync.
        _logger.LogInformation("Warming up Chromium browser pool in background (max pages: {Max})",
            _config.MaxConcurrentPages);

        _warmupTask = Task.Run(async () =>
        {
            try
            {
                await EnsureBrowserAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Chromium warmup failed; will retry lazily on first report request");
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => DisposeAsync().AsTask();

    // ── IBrowserPool ─────────────────────────────────────────────────────────

    public async Task<LeasedPage> AcquirePageAsync(CancellationToken cancellationToken)
    {
        await _pageSemaphore.WaitAsync(cancellationToken);
        try
        {
            var browser = await EnsureBrowserAsync(cancellationToken);
            var page = await browser.NewPageAsync();
            return new LeasedPage(page, _pageSemaphore);
        }
        catch
        {
            _pageSemaphore.Release();
            throw;
        }
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private async Task<IBrowser> EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
            return _browser;

        await _launchLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock
            if (_browser is { IsConnected: true })
                return _browser;

            _logger.LogInformation("Launching Chromium...");

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-dev-shm-usage", "--disable-gpu"]
            };

            if (!string.IsNullOrWhiteSpace(_config.ChromiumExecutablePath))
            {
                launchOptions.ExecutablePath = _config.ChromiumExecutablePath;
                _logger.LogInformation("Using configured Chromium at {Path}",
                    _config.ChromiumExecutablePath);
            }
            else
            {
                _logger.LogInformation("Provisioning Chromium via BrowserFetcher...");
                var fetcher = new BrowserFetcher();
                var info = await fetcher.DownloadAsync();
                launchOptions.ExecutablePath = info.GetExecutablePath();
                _logger.LogInformation("Chromium ready at {Path}", launchOptions.ExecutablePath);
            }

            var launched = await Puppeteer.LaunchAsync(launchOptions);

            // If the pool was disposed while this launch was in flight, close the new
            // browser immediately rather than assigning (and leaking) it.
            if (_disposed)
            {
                try { await launched.CloseAsync(); } catch { /* best-effort */ }
                launched.Dispose();
                throw new ObjectDisposedException(nameof(PuppeteerBrowserPool));
            }

            _browser?.Dispose();
            _browser = launched;
            _browser.Disconnected += (_, _) =>
                _logger.LogWarning("Chromium browser disconnected — will relaunch on next request");

            _logger.LogInformation("Chromium browser launched (PID: {Pid})",
                _browser.Process?.Id);
            return _browser;
        }
        finally
        {
            _launchLock.Release();
        }
    }

    // ── IAsyncDisposable ─────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        // Wait (bounded) for any in-flight background warmup so it can't assign a browser
        // after we've torn down. If it overruns the timeout, the _disposed re-check inside
        // EnsureBrowserAsync still prevents an orphaned Chromium process.
        if (_warmupTask is { IsCompleted: false })
        {
            try { await _warmupTask.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch { /* timeout or warmup error — safe to proceed */ }
        }

        if (_browser is not null)
        {
            try { await _browser.CloseAsync(); }
            catch { /* best-effort */ }
            _browser.Dispose();
            _browser = null;
        }
        _pageSemaphore.Dispose();
        _launchLock.Dispose();
    }
}
