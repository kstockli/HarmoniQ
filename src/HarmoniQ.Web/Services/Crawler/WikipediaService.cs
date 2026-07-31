using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Reichert eine Person aus Wikipedia an (kein API-Key nötig): Biografie-Auszug, Bild und
/// Artikel-Link aus der REST-Summary-API; Geburtsjahr (best effort) aus Wikidata (P569).
/// Mehrdeutige/Begriffsklärungs-Treffer werden bewusst verworfen (nicht raten).
/// </summary>
public class WikipediaService(HttpClient http, ILogger<WikipediaService> logger)
{
    public record Ergebnis(string? Biografie, string? BildUrl, int? Geburtsjahr, string? Url, string? BildAttribution);

    public async Task<Ergebnis?> AnreichernAsync(string name, CancellationToken ct = default)
    {
        name = name?.Trim() ?? "";
        if (name.Length < 3) return null;

        try
        {
            var titel = Uri.EscapeDataString(name.Replace(' ', '_'));
            using var resp = await http.GetAsync(
                $"https://de.wikipedia.org/api/rest_v1/page/summary/{titel}", ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            // Begriffsklärung / ohne Inhalt → nicht verwenden.
            if (root.TryGetProperty("type", out var typ) && typ.GetString() is "disambiguation" or "no-extract")
                return null;

            var bio = Text(root, "extract");
            var bild = root.TryGetProperty("originalimage", out var oi) && oi.TryGetProperty("source", out var os)
                ? os.GetString()
                : (root.TryGetProperty("thumbnail", out var th) && th.TryGetProperty("source", out var ts) ? ts.GetString() : null);
            var url = root.TryGetProperty("content_urls", out var cu) && cu.TryGetProperty("desktop", out var de)
                      && de.TryGetProperty("page", out var pg) ? pg.GetString() : null;
            var qid = Text(root, "wikibase_item");

            if (string.IsNullOrWhiteSpace(bio) && string.IsNullOrWhiteSpace(url)) return null;

            var jahr = string.IsNullOrWhiteSpace(qid) ? null : await GeburtsjahrAsync(qid!, ct);
            // Bild darf nur MIT Quellen-/Lizenzangabe verwendet werden (Wikimedia Commons, je-Bild-Lizenz).
            var bildAttr = string.IsNullOrWhiteSpace(bild) ? null : await BildAttributionAsync(bild!, ct);
            return new Ergebnis(Leer(bio), Leer(bild), jahr, Leer(url), Leer(bildAttr));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Wikipedia-Anreicherung fehlgeschlagen für {Name}", name);
            return null;
        }
    }

    /// <summary>Holt zu einer Wikimedia-Bild-URL die Urheber-/Lizenzangabe von der Commons-API
    /// (<c>extmetadata</c>) und baut daraus eine anzeigefertige Attribution. Null, wenn nicht ermittelbar.</summary>
    public async Task<string?> BildAttributionAsync(string bildUrl, CancellationToken ct = default)
    {
        try
        {
            if (!Uri.TryCreate(bildUrl, UriKind.Absolute, out var uri)) return null;
            // Dateiname aus dem letzten Pfadsegment; „NNNpx-"-Thumbnail-Präfix entfernen.
            var datei = Regex.Replace(Uri.UnescapeDataString(uri.Segments[^1]), @"^\d+px-", "");
            if (string.IsNullOrWhiteSpace(datei)) return null;
            var titel = Uri.EscapeDataString("File:" + datei);
            using var resp = await http.GetAsync(
                $"https://commons.wikimedia.org/w/api.php?action=query&titles={titel}&prop=imageinfo&iiprop=extmetadata&format=json", ct);
            if (!resp.IsSuccessStatusCode) return null;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            if (!doc.RootElement.TryGetProperty("query", out var q) || !q.TryGetProperty("pages", out var pages)) return null;
            foreach (var page in pages.EnumerateObject())
            {
                if (!page.Value.TryGetProperty("imageinfo", out var ii) || ii.ValueKind != JsonValueKind.Array || ii.GetArrayLength() == 0) continue;
                if (!ii[0].TryGetProperty("extmetadata", out var em)) continue;
                var urheber = EmText(em, "Artist") ?? EmText(em, "Credit");
                var lizenz = EmText(em, "LicenseShortName");
                var teile = new[] { urheber, lizenz }.Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
                var kern = string.Join(" · ", teile);
                return (kern.Length > 0 ? kern + ", " : "") + "via Wikimedia Commons";
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogDebug(ex, "Commons-Attribution fehlgeschlagen: {Url}", bildUrl); }
        return null;
    }

    /// <summary>Liest ein <c>extmetadata</c>-Feld als Klartext (HTML entfernt, entschärft, Whitespace normalisiert).</summary>
    private static string? EmText(JsonElement em, string key)
    {
        if (!em.TryGetProperty(key, out var o) || !o.TryGetProperty("value", out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        var t = Regex.Replace(v.GetString() ?? "", "<[^>]+>", " ");
        t = WebUtility.HtmlDecode(t);
        t = Regex.Replace(t, @"\s+", " ").Trim();
        return t.Length == 0 ? null : t;
    }

    private async Task<int?> GeburtsjahrAsync(string qid, CancellationToken ct)
    {
        try
        {
            using var resp = await http.GetAsync(
                $"https://www.wikidata.org/w/api.php?action=wbgetentities&ids={qid}&props=claims&format=json", ct);
            if (!resp.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var time = doc.RootElement.GetProperty("entities").GetProperty(qid)
                .GetProperty("claims").GetProperty("P569")[0]
                .GetProperty("mainsnak").GetProperty("datavalue").GetProperty("value")
                .GetProperty("time").GetString(); // z. B. "+1960-03-08T00:00:00Z"
            if (time is { Length: >= 5 } && int.TryParse(time.AsSpan(1, 4), out var jahr) && jahr is > 1000 and < 2200)
                return jahr;
        }
        catch { /* Geburtsjahr ist optional */ }
        return null;
    }

    private static string? Text(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
