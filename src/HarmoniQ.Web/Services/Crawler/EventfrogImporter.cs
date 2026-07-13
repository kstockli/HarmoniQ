using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Spezial-Handler <b>Eventfrog</b> (Veranstalter, Spec §4.4): liest Blasmusik-Konzerte schweizweit über
/// die <b>Eventfrog Public API</b> (REST, <b>kein</b> Rendering). Auth = <c>Authorization: Bearer &lt;key&gt;</c>
/// (Key in <c>Crawler:Eventfrog:ApiKey</c>, user-secrets/Railway). Die Rubrik „Blasmusik" wird dynamisch aus
/// <c>/rubrics.json</c> ermittelt (nicht hartcodiert; Fallback-ID 63). Events via <c>/events.json?rubId=…</c>,
/// Orte via <c>/locations.json</c>. Programm liefert Eventfrog nicht (reine Ticketing-Plattform).
/// Dedup/Inkrementell über <see cref="HarmoniQ.Web.Data.Models.CrawlFund.ExternKey"/> = „eventfrog:{id}".
/// </summary>
public class EventfrogImporter(HttpClient http, IConfiguration config, ILogger<EventfrogImporter> logger)
{
    private const string Basis = "https://api.eventfrog.net/api/v1";
    private readonly string? _key = config["Crawler:Eventfrog:ApiKey"];

    /// <summary>Nur nutzbar, wenn ein Public-API-Key konfiguriert ist.</summary>
    public bool Verfuegbar => !string.IsNullOrWhiteSpace(_key);

    /// <summary>Zuständig für Eventfrog-Quellen (StartUrl enthält „eventfrog").</summary>
    public static bool IstZustaendig(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Contains("eventfrog", StringComparison.OrdinalIgnoreCase);

    public record EfEvent(string Id, string? Titel, DateOnly? Datum, TimeOnly? Uhrzeit,
        string? Veranstalter, string? Url, string? Beschreibung, string? BildUrl,
        IReadOnlyList<string> LocationIds, bool Abgesagt);

    public record EfLocation(string? Titel, string? Plz, string? Ort, string? Adresse);

    /// <summary>Sucht die Rubrik-ID nach deutschem Titel (z. B. „Blasmusik"). Null, wenn nicht gefunden.</summary>
    public async Task<int?> RubrikIdAsync(string titelDe, CancellationToken ct = default)
    {
        using var doc = await HoleAsync("/rubrics.json", "", ct);
        if (doc is null || !doc.RootElement.TryGetProperty("rubrics", out var arr)) return null;
        foreach (var r in arr.EnumerateArray())
            if (string.Equals(LokDe(r, "title"), titelDe, StringComparison.OrdinalIgnoreCase)
                && r.TryGetProperty("id", out var id) && id.TryGetInt32(out var i))
                return i;
        return null;
    }

    /// <summary>Holt eine Seite Events der Rubrik (ab <paramref name="from"/>). Gibt Events + Gesamtzahl zurück.</summary>
    public async Task<(List<EfEvent> Events, int Total)> EventeAsync(
        int rubId, DateOnly from, int page, int perPage, CancellationToken ct = default)
    {
        var q = $"rubId={rubId}&from={from.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)}&page={page}&perPage={perPage}";
        using var doc = await HoleAsync("/events.json", q, ct);
        var liste = new List<EfEvent>();
        if (doc is null) return (liste, 0);
        var root = doc.RootElement;
        var total = root.TryGetProperty("totalNumberOfResources", out var t) && t.TryGetInt32(out var n) ? n : 0;
        if (root.TryGetProperty("events", out var evs) && evs.ValueKind == JsonValueKind.Array)
            foreach (var e in evs.EnumerateArray())
            {
                var id = Str(e, "id");
                if (string.IsNullOrEmpty(id)) continue;
                var (d, u) = DatumZeit(Str(e, "begin"));
                liste.Add(new EfEvent(
                    id!, LokDe(e, "title"), d, u,
                    Veranstalter: Str(e, "organizerName"),
                    Url: Str(e, "url"),
                    Beschreibung: LokDe(e, "shortDescription"),
                    BildUrl: e.TryGetProperty("emblemToShow", out var img) && img.ValueKind == JsonValueKind.Object ? Str(img, "url") : null,
                    LocationIds: e.TryGetProperty("locationIds", out var lids) && lids.ValueKind == JsonValueKind.Array
                        ? lids.EnumerateArray().Select(x => x.GetString() ?? "").Where(s => s.Length > 0).ToList()
                        : [],
                    Abgesagt: e.TryGetProperty("cancelled", out var c) && c.ValueKind == JsonValueKind.True));
            }
        return (liste, total);
    }

    /// <summary>Holt Locations nach Id (in Blöcken zu 100) → Map Id→Location für die Ort-Anzeige.</summary>
    public async Task<Dictionary<string, EfLocation>> LocationsAsync(IReadOnlyCollection<string> ids, CancellationToken ct = default)
    {
        var map = new Dictionary<string, EfLocation>();
        foreach (var block in ids.Distinct().Chunk(100))
        {
            var q = string.Join("&", block.Select(i => "id=" + WebUtility.UrlEncode(i))) + "&perPage=100";
            using var doc = await HoleAsync("/locations.json", q, ct);
            if (doc is null || !doc.RootElement.TryGetProperty("locations", out var arr)) continue;
            foreach (var l in arr.EnumerateArray())
            {
                var id = Str(l, "id");
                if (!string.IsNullOrEmpty(id))
                    map[id!] = new EfLocation(LokDe(l, "title"), Str(l, "zip"), Str(l, "city"), Str(l, "addressLine"));
            }
        }
        return map;
    }

    private async Task<JsonDocument?> HoleAsync(string edge, string query, CancellationToken ct)
    {
        var url = string.IsNullOrEmpty(query) ? $"{Basis}{edge}" : $"{Basis}{edge}?{query}";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _key);
        try
        {
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Eventfrog {Edge} → HTTP {Status}.", edge, (int)resp.StatusCode);
                return null;
            }
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Eventfrog {Edge} fehlgeschlagen.", edge);
            return null;
        }
    }

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Deutscher Wert aus einem lokalisierten Feld ({de,en,fr}); Fallback: erster nicht-leerer.</summary>
    private static string? LokDe(JsonElement o, string name)
    {
        if (!o.TryGetProperty(name, out var v)) return null;
        if (v.ValueKind == JsonValueKind.String) return v.GetString();
        if (v.ValueKind != JsonValueKind.Object) return null;
        if (v.TryGetProperty("de", out var de) && de.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(de.GetString()))
            return de.GetString();
        foreach (var p in v.EnumerateObject())
            if (p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(p.Value.GetString()))
                return p.Value.GetString();
        return null;
    }

    /// <summary>ISO-8601-Zeitstempel (mit Offset) → Schweizer Datum + Uhrzeit.</summary>
    private static (DateOnly?, TimeOnly?) DatumZeit(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)
            || !DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return (null, null);
        foreach (var tz in new[] { "Europe/Zurich", "W. Europe Standard Time" })
            try
            {
                var l = TimeZoneInfo.ConvertTime(dto, TimeZoneInfo.FindSystemTimeZoneById(tz)).DateTime;
                return (DateOnly.FromDateTime(l), TimeOnly.FromDateTime(l));
            }
            catch { /* nächste TZ-ID versuchen */ }
        var d = dto.DateTime;
        return (DateOnly.FromDateTime(d), TimeOnly.FromDateTime(d));
    }
}
