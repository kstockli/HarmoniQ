using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// JS-Rendering via Playwright/Chromium (headless). Singleton: der Browser wird einmal (lazy) gestartet
/// und wiederverwendet. Ist Chromium nicht installiert/startbar, wird Rendering still deaktiviert und
/// <c>null</c> geliefert (der Fetch fällt dann auf HTTP zurück – kein Crash). Browser installieren:
/// <c>pwsh src/HarmoniQ.Web/bin/Debug/net10.0/playwright.ps1 install chromium</c> (Docker: siehe DEPLOY.md).
/// </summary>
public sealed class PlaywrightRenderer(IOptions<CrawlerOptions> opt, ILogger<PlaywrightRenderer> logger)
    : ISeitenRenderer, IAsyncDisposable
{
    private readonly CrawlerOptions _opt = opt.Value;
    private readonly SemaphoreSlim _init = new(1, 1);
    private IPlaywright? _pw;
    private IBrowser? _browser;
    private bool _fehlgeschlagen;

    public async Task<string?> RenderAsync(string url, CancellationToken ct = default)
    {
        var browser = await BrowserHolenAsync(ct);
        if (browser == null) return null;

        IBrowserContext? context = null;
        try
        {
            context = await browser.NewContextAsync(new BrowserNewContextOptions { UserAgent = _opt.UserAgent });
            var page = await context.NewPageAsync();
            var timeout = Math.Max(_opt.RequestTimeoutSekunden * 1000f, 45000f);

            await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout });
            var links = await WartenBisStabilAsync(page);

            // Verdächtig wenige Links → meist nur die SPA-Hülle (XHR der Liste verpasst): einmal neu laden.
            if (links < 25)
            {
                logger.LogInformation("Nur {Links} Links bei {Url} – lade einmal neu …", links, url);
                await page.ReloadAsync(new PageReloadOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = timeout });
                links = Math.Max(links, await WartenBisStabilAsync(page));
            }

            logger.LogInformation("Gerendert: {Url} – {Links} Links.", url, links);
            return await page.ContentAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Rendern fehlgeschlagen: {Url}", url);
            return null;
        }
        finally
        {
            if (context != null) await context.CloseAsync();
        }
    }

    /// <summary>Wartet, bis SPA-Inhalte geladen sind: erst Netzwerk-Ruhe (XHR der Liste), dann bis die
    /// Link-Anzahl nicht mehr wächst (Mindestwartezeit, damit nicht auf der leeren Hülle abgebrochen wird).
    /// Kein Scrollen (würde virtualisierte Listen ausdünnen). Gibt die maximale Link-Anzahl zurück.</summary>
    private static async Task<int> WartenBisStabilAsync(IPage page)
    {
        try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 20000 }); }
        catch { /* Wix pingt evtl. dauernd → Timeout ok, weiter pollen */ }

        var max = 0; var ohneWachstum = 0;
        for (var i = 0; i < 30; i++)
        {
            await page.WaitForTimeoutAsync(1000);
            var anzahl = await page.Locator("a[href]").CountAsync();
            if (anzahl > max) { max = anzahl; ohneWachstum = 0; }
            else ohneWachstum++;
            if (i >= 4 && ohneWachstum >= 4 && max > 0) break; // min. ~5 s, dann 4 s ohne Zuwachs
        }
        return max;
    }

    private async Task<IBrowser?> BrowserHolenAsync(CancellationToken ct)
    {
        if (_browser is { IsConnected: true }) return _browser;
        if (_fehlgeschlagen) return null;

        await _init.WaitAsync(ct);
        try
        {
            if (_browser is { IsConnected: true }) return _browser;
            // Browser war da, ist aber abgestürzt/getrennt → aufräumen und neu starten.
            if (_browser != null) { try { await _browser.DisposeAsync(); } catch { } _browser = null; }
            if (_fehlgeschlagen) return null;

            _pw ??= await Playwright.CreateAsync();
            _browser = await _pw.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            logger.LogInformation("Playwright/Chromium gestartet (JS-Rendering aktiv).");
            return _browser;
        }
        catch (Exception ex)
        {
            _fehlgeschlagen = true;
            logger.LogWarning(ex, "Playwright/Chromium nicht verfügbar – Rendering deaktiviert (Fallback auf HTTP). " +
                "Browser installieren: 'pwsh .../playwright.ps1 install chromium'.");
            return null;
        }
        finally
        {
            _init.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_browser != null) await _browser.DisposeAsync();
        _pw?.Dispose();
        _init.Dispose();
    }
}
