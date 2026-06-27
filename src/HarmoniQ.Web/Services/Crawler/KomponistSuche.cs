using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Ermittelt Komponist:in eines Stücks per <b>Web-Suche</b> + <b>LLM-Extraktion</b> über die
/// Treffer-Snippets (grounded → zuverlässig, kein Raten). Hauptzweck: fehlende Selbstwahlstück-
/// Komponist:innen beim SBBW (§4.2).
/// <para><b>Provider:</b> Google Programmable Search JSON API. Aktiv, sobald ein API-Key
/// (<c>Crawler:KomponistSuche:GoogleApiKey</c> ODER ersatzweise der vorhandene <c>YouTube:ApiKey</c>)
/// und die Such-ID <c>Crawler:KomponistSuche:GoogleCx</c> gesetzt sind (Gratis-Kontingent 100/Tag
/// genügt). Ohne Konfiguration ist die Suche inaktiv und liefert <c>null</c> –
/// bewusst KEIN Raten aus reinem LLM-Wissen (halluziniert bei Nischen-Repertoire).</para>
/// Freie key-lose Quellen (DuckDuckGo-Scraping, MusicBrainz, Wikipedia) sind ungeeignet: DDG blockt
/// Bots, die DBs liefern für Brass-Band-Wettstücke falsche Treffer.
/// </summary>
public class KomponistSuche(HttpClient http, IExtraktion extraktion, IConfiguration config, ILogger<KomponistSuche> logger)
{
    // Eigener Key – oder ersatzweise der bereits vorhandene Google-Key der YouTube Data API
    // (gleiches Google-Projekt; „Custom Search API" muss dort aktiviert und für den Key erlaubt sein).
    private readonly string? _apiKey = config["Crawler:KomponistSuche:GoogleApiKey"] ?? config["YouTube:ApiKey"];
    private readonly string? _cx = config["Crawler:KomponistSuche:GoogleCx"];
    private bool _hinweisGeloggt;

    public bool Aktiv => !string.IsNullOrWhiteSpace(_apiKey) && !string.IsNullOrWhiteSpace(_cx);

    public async Task<string?> KomponistAsync(string stueckTitel, CancellationToken ct = default)
    {
        var titel = stueckTitel?.Trim();
        if (string.IsNullOrWhiteSpace(titel)) return null;
        if (!Aktiv)
        {
            if (!_hinweisGeloggt)
            {
                logger.LogInformation("Komponist-Suche inaktiv – es fehlt {Was}.",
                    string.IsNullOrWhiteSpace(_apiKey)
                        ? "ein Google-API-Key (Crawler:KomponistSuche:GoogleApiKey oder YouTube:ApiKey)"
                        : "die Such-ID Crawler:KomponistSuche:GoogleCx");
                _hinweisGeloggt = true;
            }
            return null;
        }

        var snippets = await SucheSnippetsAsync(titel, ct);
        if (string.IsNullOrWhiteSpace(snippets)) return null;

        var name = await extraktion.KomponistAusSucheAsync(titel, snippets, ct);
        if (!string.IsNullOrWhiteSpace(name))
            logger.LogInformation("Komponist-Suche: {Titel} -> {Name}", titel, name);
        return name;
    }

    private async Task<string?> SucheSnippetsAsync(string titel, CancellationToken ct)
    {
        try
        {
            var q = Uri.EscapeDataString($"\"{titel}\" brass band composer");
            var url = $"https://www.googleapis.com/customsearch/v1?key={_apiKey}&cx={_cx}&num=5&q={q}";
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Google-Suche HTTP {Code} für {Titel}", (int)resp.StatusCode, titel);
                return null;
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("items", out var items)) return null;

            var sb = new StringBuilder();
            foreach (var it in items.EnumerateArray())
            {
                if (it.TryGetProperty("title", out var t)) sb.AppendLine(t.GetString());
                if (it.TryGetProperty("snippet", out var s)) sb.AppendLine(s.GetString());
            }
            return sb.ToString();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Google-Komponistensuche fehlgeschlagen für {Titel}", titel); return null; }
    }
}
