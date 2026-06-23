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
            await page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = Math.Max(_opt.RequestTimeoutSekunden * 1000f, 45000f)
            });

            // SPA-Inhalte (Wix etc.) laden asynchron per XHR nach. Zuerst großzügig auf Netzwerk-Ruhe warten
            // (XHR der Liste abwarten), dann auf WACHSTUMS-Stillstand der Link-Anzahl pollen – mit
            // Mindestwartezeit, damit wir nicht auf der noch leeren Hülle abbrechen. Kein Scrollen
            // (würde virtualisierte Listen ausdünnen).
            try { await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions { Timeout = 15000 }); }
            catch { /* Wix pingt evtl. dauernd → Timeout ok, weiter pollen */ }

            var max = 0; var ohneWachstum = 0; var sekunden = 0;
            for (var i = 0; i < 25; i++)
            {
                await page.WaitForTimeoutAsync(1000);
                sekunden++;
                var anzahl = await page.Locator("a[href]").CountAsync();
                if (anzahl > max) { max = anzahl; ohneWachstum = 0; }
                else ohneWachstum++;
                if (i >= 3 && ohneWachstum >= 3 && max > 0) break; // min. ~4 s, dann 3 s ohne Zuwachs
            }
            logger.LogInformation("Gerendert: {Url} – {Links} Links nach ~{Sek}s.", url, max, sekunden);
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

    private async Task<IBrowser?> BrowserHolenAsync(CancellationToken ct)
    {
        if (_browser != null) return _browser;
        if (_fehlgeschlagen) return null;

        await _init.WaitAsync(ct);
        try
        {
            if (_browser != null) return _browser;
            if (_fehlgeschlagen) return null;

            _pw = await Playwright.CreateAsync();
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
