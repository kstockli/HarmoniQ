using System.Text.Json;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Reichert eine Person aus Wikipedia an (kein API-Key nötig): Biografie-Auszug, Bild und
/// Artikel-Link aus der REST-Summary-API; Geburtsjahr (best effort) aus Wikidata (P569).
/// Mehrdeutige/Begriffsklärungs-Treffer werden bewusst verworfen (nicht raten).
/// </summary>
public class WikipediaService(HttpClient http, ILogger<WikipediaService> logger)
{
    public record Ergebnis(string? Biografie, string? BildUrl, int? Geburtsjahr, string? Url);

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
            return new Ergebnis(Leer(bio), Leer(bild), jahr, Leer(url));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Wikipedia-Anreicherung fehlgeschlagen für {Name}", name);
            return null;
        }
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
