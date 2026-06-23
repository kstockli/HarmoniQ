using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Fetch-Stufe der Pipeline (Spec §4, Stufe 1): lädt eine URL höflich herunter und liefert den
/// Inhalt als Text. Beherrscht <b>HTML</b> und <b>PDF</b> (Text-Extraktion via PdfPig). Höfliches
/// Crawling: robots.txt respektieren, Rate-Limit pro Domain, klar erkennbarer User-Agent,
/// Größenobergrenze. Hält keinen DB-Zustand – Dedup/Provenienz (CrawlSeite) macht der Orchestrator.
/// </summary>
public class CrawlFetchService
{
    private readonly HttpClient _http;
    private readonly CrawlerOptions _opt;
    private readonly ILogger<CrawlFetchService> _logger;
    private readonly ISeitenRenderer _renderer;
    private readonly string _botToken;

    private readonly ConcurrentDictionary<string, RobotsRegeln> _robotsCache = new();
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _hostLocks = new();
    private readonly ConcurrentDictionary<string, DateTime> _letzterAbruf = new();

    public CrawlFetchService(HttpClient http, IOptions<CrawlerOptions> opt, ILogger<CrawlFetchService> logger,
        ISeitenRenderer renderer)
    {
        _http = http;
        _opt = opt.Value;
        _logger = logger;
        _renderer = renderer;
        _http.Timeout = TimeSpan.FromSeconds(_opt.RequestTimeoutSekunden);
        // Bot-Token = Produktname vor dem „/“ (z. B. „HarmoniQBot/1.0 …“ → „harmoniqbot“).
        _botToken = (_opt.UserAgent.Split('/', 2)[0]).Trim().ToLowerInvariant();
    }

    public record FetchErgebnis(
        bool Erfolg,
        string Url,
        string? ContentType,
        bool IstPdf,
        string? Text,
        string? InhaltsHash,
        bool DurchRobotsGesperrt,
        string? Fehler,
        bool Gerendert = false)
    {
        public static FetchErgebnis Gesperrt(string url) =>
            new(false, url, null, false, null, null, true, "Durch robots.txt gesperrt.");
        public static FetchErgebnis Fehlschlag(string url, string fehler) =>
            new(false, url, null, false, null, null, false, fehler);
    }

    /// <summary>Lädt eine URL höflich und gibt den extrahierten Text zurück. <paramref name="rendern"/>
    /// rendert die Seite per Headless-Browser (für SPA/Event), sofern Rendering aktiv ist.</summary>
    public async Task<FetchErgebnis> HoleAsync(string url, bool rendern = false, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return FetchErgebnis.Fehlschlag(url, "Ungültige oder nicht-HTTP(S)-URL.");

        var host = uri.Host;

        // 1) robots.txt prüfen.
        var robots = _opt.RobotsBeachten
            ? await RobotsHolenAsync(uri, ct)
            : RobotsRegeln.Alles;
        if (!robots.DarfAbrufen(uri.PathAndQuery))
        {
            _logger.LogInformation("robots.txt verbietet {Url}", url);
            return FetchErgebnis.Gesperrt(url);
        }

        // 2) Pro Domain serialisieren + Rate-Limit einhalten.
        var sem = _hostLocks.GetOrAdd(host, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            await RateLimitAbwartenAsync(host, robots.CrawlDelay, ct);
            try
            {
                // JS-Rendering (falls gewünscht & aktiv); bei Fehler Fallback auf HTTP.
                if (rendern && _opt.RenderingAktiv)
                {
                    var html = await _renderer.RenderAsync(url, ct);
                    if (html != null)
                        return new FetchErgebnis(true, url, "text/html", false, html, Hash(html), false, null, Gerendert: true);
                    _logger.LogWarning("Rendern lieferte nichts – Fallback HTTP für {Url}", url);
                }
                else if (rendern)
                {
                    _logger.LogWarning("Rendering für {Url} gewünscht, aber Crawler:RenderingAktiv=false " +
                        "– es kommt nur die (evtl. leere) HTML-Hülle. RenderingAktiv aktivieren.", url);
                }
                return await AbrufenAsync(uri, ct);
            }
            finally
            {
                _letzterAbruf[host] = DateTime.UtcNow;
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task<FetchErgebnis> AbrufenAsync(Uri uri, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, uri);
            req.Headers.UserAgent.ParseAdd(_opt.UserAgent);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode)
                return FetchErgebnis.Fehlschlag(uri.ToString(), $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}");

            var contentType = resp.Content.Headers.ContentType?.MediaType;
            var istPdf = string.Equals(contentType, "application/pdf", StringComparison.OrdinalIgnoreCase)
                         || uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

            var bytes = await ByteMitLimitLesenAsync(resp, ct);
            if (bytes == null)
                return FetchErgebnis.Fehlschlag(uri.ToString(),
                    $"Inhalt überschreitet Limit ({_opt.MaxInhaltBytes} Bytes).");

            string text;
            if (istPdf)
            {
                text = PdfTextExtrahieren(bytes);
            }
            else
            {
                var enc = EncodingErmitteln(resp.Content.Headers.ContentType?.CharSet);
                text = enc.GetString(bytes);
            }

            var hash = Hash(text);
            return new FetchErgebnis(true, uri.ToString(), contentType, istPdf, text, hash, false, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Fehler beim Abruf von {Url}", uri);
            return FetchErgebnis.Fehlschlag(uri.ToString(), ex.Message);
        }
    }

    private async Task<RobotsRegeln> RobotsHolenAsync(Uri uri, CancellationToken ct)
    {
        var host = uri.Host;
        if (_robotsCache.TryGetValue(host, out var vorhanden)) return vorhanden;

        var robots = RobotsRegeln.Alles;
        try
        {
            var robotsUrl = $"{uri.Scheme}://{uri.Authority}/robots.txt";
            using var req = new HttpRequestMessage(HttpMethod.Get, robotsUrl);
            req.Headers.UserAgent.ParseAdd(_opt.UserAgent);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
            if (resp.IsSuccessStatusCode)
            {
                var inhalt = await resp.Content.ReadAsStringAsync(ct);
                robots = RobotsRegeln.Parse(inhalt, _botToken);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "robots.txt für {Host} nicht abrufbar – erlaube alles.", host);
        }

        _robotsCache[host] = robots;
        return robots;
    }

    private async Task RateLimitAbwartenAsync(string host, double? crawlDelay, CancellationToken ct)
    {
        var sollSek = Math.Max(_opt.RateLimitSekunden, crawlDelay ?? 0);
        if (sollSek <= 0) return;
        if (_letzterAbruf.TryGetValue(host, out var last))
        {
            var soll = TimeSpan.FromSeconds(sollSek);
            var vergangen = DateTime.UtcNow - last;
            if (vergangen < soll)
                await Task.Delay(soll - vergangen, ct);
        }
    }

    /// <summary>Liest den Body, bricht aber ab, sobald <see cref="CrawlerOptions.MaxInhaltBytes"/>
    /// überschritten würde (gibt dann <c>null</c> zurück).</summary>
    private async Task<byte[]?> ByteMitLimitLesenAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var ms = new MemoryStream();
        var puffer = new byte[81920];
        int gelesen;
        while ((gelesen = await stream.ReadAsync(puffer, ct)) > 0)
        {
            if (ms.Length + gelesen > _opt.MaxInhaltBytes) return null;
            ms.Write(puffer, 0, gelesen);
        }
        return ms.ToArray();
    }

    private string PdfTextExtrahieren(byte[] bytes)
    {
        var sb = new StringBuilder();
        using var doc = PdfDocument.Open(bytes);
        foreach (var page in doc.GetPages())
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
        return sb.ToString();
    }

    private static Encoding EncodingErmitteln(string? charset)
    {
        if (string.IsNullOrWhiteSpace(charset)) return Encoding.UTF8;
        try { return Encoding.GetEncoding(charset.Trim('"', ' ')); }
        catch { return Encoding.UTF8; }
    }

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
