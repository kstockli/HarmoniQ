using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services.Emf;

/// <summary>
/// Parser für den <b>einmaligen Import der EMF-2026-Parademusik-Videos</b> (Eidg. Musikfest Biel/Bienne)
/// von RTR/SRG „Play". Die Play-Seite bezieht ihre Daten aus einer öffentlichen JSON-API (kein Browser,
/// kein Auth nötig):
/// <list type="bullet">
/// <item><c>…/play/v3/api/rtr/production/show-page?showUrn=…</c> → die <b>Sections</b> = je „TT.MM.JJJJ:
/// Strasse - Rue" (ein Tag + eine Marschstrecke = ein Konzert).</item>
/// <item><c>…/play/v3/api/rtr/production/media-section?sectionId=…</c> → die <b>Videos</b> der Section;
/// Titel-Schema „<i>Band</i> – <i>Stück</i>  I EMF26 Biel Bienne", dazu <c>urn:rtr:video:&lt;id&gt;</c>, Datum.</item>
/// </list>
/// Videos werden über den offiziellen SRG-Embed-Player eingebunden (<see cref="Data.Models.VideoPlattform.SrgPlay"/>),
/// nicht heruntergeladen. Reine Parsing-Logik – das Schreiben übernimmt <see cref="EmfImportService"/>.
/// </summary>
public static class EmfImporter
{
    public const string BasisUrl = "https://www.rtr.ch";
    public const string ShowUrn = "urn:rtr:show:tv:cd575ac1-6d1c-45b9-8592-e8d3f049925e";

    public static string ShowPageUrl =>
        $"{BasisUrl}/play/v3/api/rtr/production/show-page?showUrn={Uri.EscapeDataString(ShowUrn)}&preview=false";
    public static string MediaSectionUrl(string sectionId) =>
        $"{BasisUrl}/play/v3/api/rtr/production/media-section?sectionId={sectionId}&preview=false&next=";

    public record Section(string SectionId, DateOnly Datum, string Strasse, string Titel);
    public record Video(string Urn, string Titel, string? Band, string? Stueck, DateOnly? Datum, string? BildUrl);

    // ── Sections (Tag + Strasse) aus der show-page ────────────────────────────
    public static IReadOnlyList<Section> ParseSections(string showPageJson)
    {
        var result = new List<Section>();
        var gesehen = new HashSet<string>();
        using var doc = JsonDocument.Parse(showPageJson);
        // Section-Objekt: { "id": "<guid>", "representation": { "title": "TT.MM.JJJJ: Strasse …" } }
        foreach (var o in ObjekteMit(doc.RootElement, "id", "representation"))
        {
            var id = Str(o, "id");
            var titel = o.TryGetProperty("representation", out var rep) && rep.ValueKind == JsonValueKind.Object
                ? Str(rep, "title") : null;
            if (id is null || titel is null) continue;
            var m = Regex.Match(titel, @"^(\d{2})\.(\d{2})\.(\d{4})\s*:\s*(.+)$");
            if (!m.Success || !gesehen.Add(id)) continue;
            var datum = new DateOnly(int.Parse(m.Groups[3].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[1].Value));
            var strasse = m.Groups[4].Value.Trim();
            result.Add(new Section(id, datum, strasse, titel.Trim()));
        }
        return result;
    }

    // ── Videos einer Section ──────────────────────────────────────────────────
    public static IReadOnlyList<Video> ParseVideos(string mediaSectionJson)
    {
        var result = new List<Video>();
        var gesehen = new HashSet<string>();
        using var doc = JsonDocument.Parse(mediaSectionJson);
        foreach (var o in ObjekteMit(doc.RootElement, "urn"))
        {
            var urn = Str(o, "urn");
            if (urn is null || !urn.StartsWith("urn:rtr:video:", StringComparison.OrdinalIgnoreCase)) continue;
            if (!gesehen.Add(urn)) continue;
            var titel = Str(o, "title") ?? "";
            var (band, stueck) = TitelZerlegen(titel);
            // Per-Video-Standbild (imageUrl am Video-Objekt, mit Frame-Suffix „…-588s48ms.png").
            result.Add(new Video(urn, titel, band, stueck, DatumAus(Str(o, "date")), Str(o, "imageUrl")));
        }
        return result;
    }

    /// <summary>„Musikverein Niederwil – FÜÜRIO!  I EMF26 Biel Bienne" → Band „Musikverein Niederwil",
    /// Stück „FÜÜRIO!". Ohne erkennbare Band (z. B. Trailer/Impressionen) → (null, null).</summary>
    public static (string? Band, string? Stueck) TitelZerlegen(string titel)
    {
        if (string.IsNullOrWhiteSpace(titel)) return (null, null);
        // Suffix „ I EMF26 Biel Bienne" / „ | EMF26 …" abschneiden.
        var ohneSuffix = Regex.Replace(titel, @"\s*[I|/]\s*EMF\s?26.*$", "", RegexOptions.IgnoreCase).Trim();
        // Band – Stück (Gedankenstrich – oder Bindestrich, von Leerzeichen umgeben).
        var m = Regex.Match(ohneSuffix, @"^(.+?)\s[–—-]\s(.+)$");
        if (m.Success)
        {
            var band = m.Groups[1].Value.Trim();
            var stueck = m.Groups[2].Value.Trim();
            return (band.Length > 0 ? band : null, stueck.Length > 0 ? stueck : null);
        }
        return (null, null); // kein „Band – Stück"-Muster → kein Auftritt
    }

    // ── Hilfen ────────────────────────────────────────────────────────────────
    private static string? Str(JsonElement o, string name) =>
        o.ValueKind == JsonValueKind.Object && o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static DateOnly? DatumAus(string? iso) =>
        DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dto)
            ? DateOnly.FromDateTime(dto.DateTime) : null;

    /// <summary>Durchläuft den JSON-Baum und liefert alle Objekte, die ALLE genannten Properties besitzen.</summary>
    private static IEnumerable<JsonElement> ObjekteMit(JsonElement e, params string[] props)
    {
        switch (e.ValueKind)
        {
            case JsonValueKind.Object:
                if (props.All(p => e.TryGetProperty(p, out _))) yield return e;
                foreach (var p in e.EnumerateObject())
                    foreach (var x in ObjekteMit(p.Value, props)) yield return x;
                break;
            case JsonValueKind.Array:
                foreach (var c in e.EnumerateArray())
                    foreach (var x in ObjekteMit(c, props)) yield return x;
                break;
        }
    }
}
