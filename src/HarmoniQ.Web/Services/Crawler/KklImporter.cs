using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Spezial-Handler <b>KKL Luzern</b> (Veranstalter, Spec §4.3): Die Eventseite (Next.js/Vercel) lädt
/// die Events vom Ticketing-Anbieter <b>vivenu</b> nach. Discovery via gerenderter Eventliste
/// (Netzwerk-Capture der <c>vivenu.com/api/events/info</c>-Antworten), Daten aus dem vivenu-JSON,
/// Stil-Filter + Band-Erkennung via LLM. Liefert je Event saubere Felder (Titel, Datum, Saal, Bild,
/// Beschreibung). Dedup über Läufe via vivenu-Event-ID (<see cref="CrawlFund.ExternKey"/>).
/// </summary>
public static class KklImporter
{
    public const string EventsUrl = "https://www.kkl-luzern.ch/events";
    public const string VivenuApiFilter = "vivenu.com/api/events/info";

    public static bool IstZustaendig(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Host.EndsWith("kkl-luzern.ch", StringComparison.OrdinalIgnoreCase);

    /// <summary>Die von der KKL-Eventseite unterstützten Kategorien (URL-Parameter <c>?genre=</c>).</summary>
    public static readonly string[] Genres =
        ["Klassik", "Jazz", "Rock & Pop", "Comedy", "Filmmusik", "Weltmusik", "Blasmusik", "Volksmusik", "Musical", "Weihnachtsmusik"];

    /// <summary>Leitet aus dem Stil-Hinweis (z. B. „Blasmusik / Brassband") eine KKL-Kategorie ab, sofern er
    /// zu einer passt → dann filtert die Website selbst (kein LLM-Filter nötig). Null = kein Treffer.</summary>
    public static string? GenreAusHinweis(string? hinweis)
    {
        if (string.IsNullOrWhiteSpace(hinweis)) return null;
        var h = hinweis.ToLowerInvariant();
        if (h.Contains("brass")) return "Blasmusik"; // Brassband zählt zur Kategorie Blasmusik
        foreach (var g in Genres)
            if (h.Contains(g.ToLowerInvariant())) return g;
        return null;
    }

    /// <summary>Eventlisten-URL mit optionalem Kategorie-Filter (<c>?genre=…</c>).</summary>
    public static string ListeUrl(string baseUrl, string? genre) =>
        string.IsNullOrWhiteSpace(genre) ? baseUrl
        : $"{baseUrl}{(baseUrl.Contains('?') ? '&' : '?')}genre={Uri.EscapeDataString(genre)}";

    /// <summary>Schneidet aus dem (nach Tab-Klick sichtbaren) Seitentext den Abschnitt nach der
    /// <paramref name="ueberschrift"/> bis zur nächsten bekannten <paramref name="enden"/>-Überschrift
    /// heraus. Die Überschrift kommt mehrfach vor (Top-Navigation „Programm &amp; Tickets", Tab, Footer);
    /// daher werden nur <b>eigenständige</b> Vorkommen (Wortgrenzen – schließt „Programmänderungen" aus)
    /// betrachtet und der <b>längste</b> resultierende Abschnitt gewählt (= der echte Tab-Inhalt; die
    /// Navigations-Vorkommen liefern dank der End-Marken nur sehr kurze Schnipsel).</summary>
    public static string? Abschnitt(string? seitenText, string ueberschrift, params string[] enden)
    {
        if (string.IsNullOrWhiteSpace(seitenText)) return null;
        string? bestes = null;
        var pos = -1;
        while ((pos = seitenText.IndexOf(ueberschrift, pos + 1, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            // Nur echte Überschrift = steht auf einer eigenen Zeile (nicht im Fließtext wie „… gestaltetes
            // Programm, das …" und nicht in „Programm & Tickets" der Navigation).
            if (!StehtAufEigenerZeile(seitenText, pos, ueberschrift.Length)) continue;

            var nachIdx = pos + ueberschrift.Length;
            var ende = seitenText.Length;
            foreach (var e in enden)
            {
                var j = seitenText.IndexOf(e, nachIdx, StringComparison.OrdinalIgnoreCase);
                if (j >= 0 && j < ende) ende = j;
            }
            var s = seitenText[nachIdx..ende].Trim();
            if (s.Length > (bestes?.Length ?? 0)) bestes = s;
        }
        return string.IsNullOrWhiteSpace(bestes) ? null : bestes;
    }

    /// <summary>Steht das Wort bei <paramref name="pos"/> allein auf seiner Zeile (nur Whitespace davor/danach
    /// bis zum Zeilenumbruch)? So unterscheiden wir die Tab-Überschrift von gleichnamigen Wörtern im Fließtext.</summary>
    private static bool StehtAufEigenerZeile(string t, int pos, int len)
    {
        var a = pos - 1;
        while (a >= 0 && (t[a] == ' ' || t[a] == '\t')) a--;
        if (a >= 0 && t[a] != '\n' && t[a] != '\r') return false;
        var b = pos + len;
        while (b < t.Length && (t[b] == ' ' || t[b] == '\t')) b++;
        return b >= t.Length || t[b] == '\n' || t[b] == '\r';
    }

    /// <summary>Ein aus dem vivenu-JSON extrahiertes Event (Kernfelder für den Konzert-Fund).</summary>
    public record Event(string Id, string Name, string? Beschreibung, string? Saal, string? Bild, DateOnly? Datum, TimeOnly? Uhrzeit, string? Slug);

    /// <summary>Klickbare KKL-Detail-URL (über den Slug), sonst die Eventliste mit ID-Anker.</summary>
    public static string DetailUrl(Event ev) =>
        string.IsNullOrWhiteSpace(ev.Slug) ? $"{EventsUrl}?ev={ev.Id}" : $"{EventsUrl}/{ev.Slug}";

    /// <summary>Bestimmt die <b>echte</b> KKL-Detail-URL: Der vivenu-Slug (<c>url</c>) weicht vom KKL-Slug ab
    /// (anderes 6-Zeichen-Suffix, andere Umlaut-Schreibung wie „gotz" statt „goetz") und führt auf eine leere
    /// Seite. Daher wird aus den auf der Liste gefundenen Detail-Links der dem Event-<b>Namen</b> ähnlichste
    /// gewählt (Levenshtein über normalisierte Strings). Kein hinreichend ähnlicher Link → vivenu-Slug-Fallback.</summary>
    public static string DetailUrl(Event ev, IReadOnlyList<string> links)
    {
        var treffer = BesterLink(ev.Name, links);
        if (treffer != null && Uri.TryCreate(new Uri(EventsUrl), treffer, out var abs)) return abs.ToString();
        return DetailUrl(ev);
    }

    private static string? BesterLink(string name, IReadOnlyList<string> links)
    {
        var nn = Normalisiere(name);
        if (nn.Length == 0 || links is null || links.Count == 0) return null;

        var kandidaten = links
            .Select(h => h.Split('?')[0].TrimEnd('/'))
            .Where(h => { var seg = h.Split('/'); return seg.Length > 2 && seg[^1].Length > 0; })
            .Distinct().ToList();
        if (kandidaten.Count == 0) return null;

        string? bester = null; var beste = int.MaxValue;
        foreach (var h in kandidaten)
        {
            var dist = LevenshteinDistanz(nn, Normalisiere(h.Split('/')[^1]));
            if (dist < beste) { beste = dist; bester = h; }
        }
        // Nur akzeptieren, wenn wirklich ähnlich (sonst kein passender Link auf der Seite → Fallback).
        return beste <= Math.Max(4, nn.Length * 2 / 5) ? bester : null;
    }

    private static string Normalisiere(string s)
    {
        var d = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static int LevenshteinDistanz(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
            {
                var kosten = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + kosten);
            }
        return d[a.Length, b.Length];
    }

    /// <summary>Parst eine vivenu-<c>events/info</c>-JSON-Antwort in ein <see cref="Event"/> (oder null).</summary>
    public static Event? Parse(string json)
    {
        try
        {
            var r = JsonDocument.Parse(json).RootElement;
            if (r.ValueKind != JsonValueKind.Object) return null;
            var id = Str(r, "_id") ?? Str(r, "id");
            var name = Str(r, "name");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) return null;

            var (kDatum, kZeit) = DatumZeitAusStart(Str(r, "start"));
            return new Event(
                id!, name!.Trim(),
                Beschreibung: FlattenBeschreibung(r),
                Saal: SaalAusVenue(StrIn(r, "meta", "venue")),
                Bild: Str(r, "image"),
                Datum: kDatum,
                Uhrzeit: kZeit,
                Slug: Str(r, "url"));
        }
        catch { return null; }
    }

    private static string? Str(JsonElement o, string name) =>
        o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>Liest <paramref name="prop"/> aus dem verschachtelten Objekt <paramref name="objName"/> (z. B. meta.venue).</summary>
    private static string? StrIn(JsonElement o, string objName, string prop) =>
        o.TryGetProperty(objName, out var inner) && inner.ValueKind == JsonValueKind.Object ? Str(inner, prop) : null;

    /// <summary>venue-Slug → Saal-Name. Laut Vorgabe: Konzertsaal = „Weisser Saal".</summary>
    private static string? SaalAusVenue(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var s = slug.ToLowerInvariant().Replace("vernue", "venue"); // bekannter Tippfehler in den Daten
        if (s.Contains("konzertsaal")) return "Weisser Saal";
        if (s.Contains("luzernersaal") || s.Contains("luzerner-saal")) return "Luzerner Saal";
        if (s.Contains("auditorium")) return "Auditorium";
        // Fallback: „venue-xyz" → „Xyz"
        var rest = Regex.Replace(s, "^venue-?", "").Replace('-', ' ').Trim();
        return rest.Length == 0 ? null : System.Globalization.CultureInfo.GetCultureInfo("de-CH").TextInfo.ToTitleCase(rest);
    }

    /// <summary>vivenu-Rich-Text (Portable-Text-Array als JSON-String/-Element) → Klartext.</summary>
    private static string? FlattenBeschreibung(JsonElement root)
    {
        if (!root.TryGetProperty("description", out var d)) return null;
        string raw = d.ValueKind == JsonValueKind.String ? d.GetString() ?? "" : d.GetRawText();
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var sb = new StringBuilder();
        try
        {
            // raw kann selbst ein JSON-Array (Portable Text) sein – Text-Spans einsammeln.
            using var doc = JsonDocument.Parse(raw);
            void Walk(JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.Object)
                {
                    if (e.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        sb.Append(t.GetString());
                    foreach (var p in e.EnumerateObject()) Walk(p.Value);
                }
                else if (e.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in e.EnumerateArray()) Walk(c);
                    sb.Append('\n');
                }
            }
            Walk(doc.RootElement);
        }
        catch
        {
            sb.Append(raw); // kein JSON → als Text behandeln
        }
        var text = Regex.Replace(sb.ToString(), "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"[ \t]+", " ");
        text = Regex.Replace(text, @"\n\s*\n+", "\n").Trim();
        return text.Length == 0 ? null : text;
    }

    private static (DateOnly? Datum, TimeOnly? Zeit) DatumZeitAusStart(string? startIso)
    {
        if (string.IsNullOrWhiteSpace(startIso)) return (null, null);
        if (!DateTimeOffset.TryParse(startIso, null,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dto)) return (null, null);
        // In Schweizer Zeit umrechnen (Datum/Zeit kann sonst bei späten Events kippen).
        foreach (var tzId in new[] { "Europe/Zurich", "W. Europe Standard Time" })
            try
            {
                var lokal = TimeZoneInfo.ConvertTime(dto, TimeZoneInfo.FindSystemTimeZoneById(tzId)).DateTime;
                return (DateOnly.FromDateTime(lokal), TimeOnly.FromDateTime(lokal));
            }
            catch { }
        var utc = dto.UtcDateTime;
        return (DateOnly.FromDateTime(utc), TimeOnly.FromDateTime(utc));
    }
}
