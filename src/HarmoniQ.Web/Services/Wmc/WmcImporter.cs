using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Wmc;

/// <summary>
/// Parser für den <b>einmaligen WMC-2026-Import</b> (World Music Contest Kerkrade). Beide Seiten sind
/// serverseitig gerendert → reines HTML-Parsing (kein Browser nötig):
/// <list type="bullet">
/// <item>Running-Order-Liste: je Division ein <c>&lt;details&gt;</c>; darin Datums-Köpfe (mal <c>&lt;h4&gt;</c>,
/// mal <c>&lt;p&gt;&lt;strong&gt;</c>, EN „Friday July 10 - Kerkrade Theatre" oder NL „Zaterdag 18 juli - …")
/// und Zeilen <c>&lt;li&gt;&lt;a href="/nl/band-xxxx"&gt;Zeit - Band (LAND)&lt;/a&gt;</c>. Datumserkennung
/// erfolgt daher <b>inhaltsbasiert</b> (nicht über den Tag-Namen).</item>
/// <item>Detailseite je Band: <c>&lt;h1&gt;</c> Name, Untertitel (Kategorie + Division), „Bio" (mit
/// <c>&lt;li&gt;Dirigent | Name&lt;/li&gt;</c>) und „Programma" als Tabelle <c>Titel | Komponist</c> (+ Solist-Zeilen).</item>
/// </list>
/// Reine Parsing-Logik (kein DB-Zugriff) – das Schreiben übernimmt <see cref="WmcImportService"/>.
/// </summary>
public static class WmcImporter
{
    public const int Jahr = 2026;
    public const string ListenUrl = "https://www.wmc.nl/en/participants-runningorder-2026-hp3z";
    public const string BasisUrl = "https://www.wmc.nl";

    public record Stueck(string Titel, string? Komponist);

    /// <summary>Ein WMC-Auftritt einer Band (Programm + Running-Order). Mehrere Auftritte mit gleichem
    /// (Datum, Ort) bilden zusammen ein Konzert (Wettbewerbs-Session).</summary>
    public record Auftritt
    {
        public required string QuellUrl { get; init; }
        public required string BandName { get; init; }
        public string? Land { get; init; }
        public string? Kategorie { get; init; }          // Anzeige: Brass Band / Fanfare / Harmonie / Percussion
        public BandKategorie? KategorieEnum { get; init; }
        public string? Division { get; init; }           // roh, z. B. „2e Division"
        public string? DivisionLabel { get; init; }      // Höchstklasse / Elite / 2. Klasse / 3. Klasse / Oberstufe
        public Staerkeklasse? Staerke { get; init; }
        public string? Untertitel { get; init; }
        public DateOnly? Datum { get; init; }
        public string? Zeit { get; init; }
        public string? Ort { get; init; }                // kanonischer Veranstaltungsort
        public string? Bio { get; init; }
        public string? Dirigent { get; init; }
        public IReadOnlyList<Stueck> Stuecke { get; init; } = [];
        public IReadOnlyList<string> Solisten { get; init; } = [];
    }

    public record ListenZeile(string Href, string? Division, DateOnly? Datum, string? Ort, string? Zeit, string? Land);

    private static string Clean(string? s) =>
        s == null ? "" : Regex.Replace(WebUtility.HtmlDecode(s).Replace(' ', ' '), @"\s+", " ").Trim();

    // ── Liste ────────────────────────────────────────────────────────────────
    public static IReadOnlyList<ListenZeile> ParseListe(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var zeilen = new List<ListenZeile>();
        var gesehen = new HashSet<string>();

        foreach (var det in doc.DocumentNode.SelectNodes("//details") ?? Enumerable.Empty<HtmlNode>())
        {
            var division = Clean(det.SelectSingleNode(".//summary")?.InnerText);
            DateOnly? datum = null; string? ort = null;

            // In Dokumentreihenfolge laufen: Datums-Kopf (inhaltsbasiert) setzt Kontext, Teilnehmer-Links erzeugen Zeilen.
            foreach (var n in det.Descendants())
            {
                if (n.NodeType != HtmlNodeType.Element) continue;
                if (n.Name is "h1" or "h2" or "h3" or "h4" or "h5" or "p" or "strong")
                {
                    var (d, o) = ParseDatumOrt(Clean(n.InnerText));
                    if (d != null) { datum = d; ort = o; }
                    continue;
                }
                if (n.Name == "a")
                {
                    var href = n.GetAttributeValue("href", "");
                    if (!Regex.IsMatch(href, @"^/[a-z]{2}/.+-[a-z0-9]{4}$")) continue;
                    if (!gesehen.Add(href)) continue;
                    var txt = Clean(n.InnerText);
                    var zeit = Regex.Match(txt, @"^\d{1,2}[.:]\d{2}").Value.Replace('.', ':');
                    var land = Regex.Match(txt, @"\(([A-Za-z]{2,3})\)\s*$").Groups[1].Value;
                    zeilen.Add(new ListenZeile(href, division.Length > 0 ? division : null, datum, ort,
                        zeit.Length > 0 ? zeit : null, land.Length > 0 ? land.ToUpperInvariant() : null));
                }
            }
        }
        return zeilen;
    }

    private static readonly Dictionary<string, int> EnMonate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["January"] = 1, ["February"] = 2, ["March"] = 3, ["April"] = 4, ["May"] = 5, ["June"] = 6,
        ["July"] = 7, ["August"] = 8, ["September"] = 9, ["October"] = 10, ["November"] = 11, ["December"] = 12
    };
    private static readonly Dictionary<string, int> NlMonate = new(StringComparer.OrdinalIgnoreCase)
    {
        ["januari"] = 1, ["februari"] = 2, ["maart"] = 3, ["april"] = 4, ["mei"] = 5, ["juni"] = 6,
        ["juli"] = 7, ["augustus"] = 8, ["september"] = 9, ["oktober"] = 10, ["november"] = 11, ["december"] = 12
    };

    /// <summary>Erkennt einen Datums-/Ort-Kopf wie „Friday July 10 - Kerkrade Theatre", „Saturday August 1st -
    /// Rodahal" oder NL „Zaterdag 18 juli - Theater Kerkrade". Liefert (Datum 2026, kanonischer Ort) oder (null, …).</summary>
    public static (DateOnly?, string?) ParseDatumOrt(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return (null, null);
        int monat = 0, tag = 0, ende = -1;

        var en = Regex.Match(text, @"(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{1,2})(?:st|nd|rd|th)?", RegexOptions.IgnoreCase);
        if (en.Success && EnMonate.TryGetValue(en.Groups[1].Value, out monat))
        { tag = int.Parse(en.Groups[2].Value); ende = en.Index + en.Length; }
        else
        {
            var nl = Regex.Match(text, @"(\d{1,2})\s+(januari|februari|maart|april|mei|juni|juli|augustus|september|oktober|november|december)", RegexOptions.IgnoreCase);
            if (nl.Success && NlMonate.TryGetValue(nl.Groups[2].Value, out monat))
            { tag = int.Parse(nl.Groups[1].Value); ende = nl.Index + nl.Length; }
        }
        if (ende < 0 || monat < 1 || tag < 1 || tag > 31) return (null, null);

        DateOnly? datum = null;
        try { datum = new DateOnly(Jahr, monat, tag); } catch { }

        var rest = text[ende..];
        rest = Regex.Replace(rest, @"^[\s\-–—:|]+", "");           // führende Trenner weg
        rest = Regex.Replace(rest, @"\s*\([^)]*\)\s*$", "").Trim(); // „(Test piece)"/„(Own choice)" weg
        return (datum, KanonischerOrt(rest));
    }

    /// <summary>Vereinheitlicht die WMC-Veranstaltungsorte (verschiedene Schreibweisen → ein Name).</summary>
    private static string? KanonischerOrt(string? ort)
    {
        if (string.IsNullOrWhiteSpace(ort)) return null;
        var o = ort.ToLowerInvariant();
        if (o.Contains("roda")) return "Rodahal";
        if (o.Contains("theat")) return "Kerkrade Theatre"; // „Kerkrade Theatre" / „Theater Kerkrade"
        return ort.Trim();
    }

    // ── Detailseite ────────────────────────────────────────────────────────────
    public static Auftritt? ParseDetail(string html, ListenZeile zeile)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var root = doc.DocumentNode;

        var band = Clean(root.SelectSingleNode("//h1")?.InnerText);
        if (band.Length == 0) return null;
        var untertitel = Clean(root.SelectSingleNode("//h1")?.SelectSingleNode("following::*[1]")?.InnerText);

        string? dirigent = null;
        var bioSb = new StringBuilder();
        var bioH2 = root.SelectNodes("//h2")?.FirstOrDefault(h => Clean(h.InnerText).Equals("Bio", StringComparison.OrdinalIgnoreCase));
        if (bioH2 != null)
            for (var n = bioH2.NextSibling; n != null && n.Name != "h2"; n = n.NextSibling)
            {
                if (n.NodeType != HtmlNodeType.Element) continue;
                foreach (var li in n.SelectNodes(".//li") ?? Enumerable.Empty<HtmlNode>())
                {
                    var m = Regex.Match(Clean(li.InnerText), @"^(?:Dirigent|Dirigentin|Conductor)\s*\|\s*(.+)$", RegexOptions.IgnoreCase);
                    if (m.Success) dirigent = m.Groups[1].Value.Trim();
                }
                bioSb.Append(Clean(n.InnerText)).Append('\n');
            }
        var bio = Clean(bioSb.ToString());

        var stuecke = new List<Stueck>();
        var solisten = new List<string>();
        var progH2 = root.SelectNodes("//h2")?.FirstOrDefault(h => Clean(h.InnerText).StartsWith("Programma", StringComparison.OrdinalIgnoreCase));
        var table = progH2?.SelectSingleNode("following::table[1]");
        if (table != null)
            foreach (var tr in table.SelectNodes(".//tr") ?? Enumerable.Empty<HtmlNode>())
            {
                var tds = tr.SelectNodes(".//td");
                if (tds == null || tds.Count == 0) continue;
                var titel = Clean(tds[0].InnerText);
                var komp = tds.Count > 1 ? Clean(tds[1].InnerText) : "";
                if (titel.Length == 0) continue;
                if (Regex.IsMatch(titel, @"^Solo?ist", RegexOptions.IgnoreCase))
                {
                    solisten.Add(Regex.Replace(titel, @"^Solo?ist(?:en)?:?\s*", "", RegexOptions.IgnoreCase));
                    continue;
                }
                stuecke.Add(new Stueck(titel, komp.Length > 0 ? komp : null));
            }

        var kategorie = KategorieAusUntertitel(untertitel);
        var (staerke, divLabel) = DivisionInfo(zeile.Division);
        return new Auftritt
        {
            QuellUrl = BasisUrl + zeile.Href,
            BandName = band,
            Land = zeile.Land,
            Kategorie = kategorie,
            KategorieEnum = KategorieEnum(kategorie),
            Division = zeile.Division,
            DivisionLabel = divLabel,
            Staerke = staerke,
            Untertitel = untertitel.Length > 0 ? untertitel : null,
            Datum = zeile.Datum,
            Zeit = zeile.Zeit,
            Ort = zeile.Ort,
            Bio = bio.Length > 0 ? bio : null,
            Dirigent = dirigent,
            Stuecke = stuecke,
            Solisten = solisten
        };
    }

    // ── Mappings ────────────────────────────────────────────────────────────────
    private static string? KategorieAusUntertitel(string untertitel)
    {
        var u = untertitel.ToLowerInvariant();
        if (u.Contains("brass")) return "Brass Band";
        if (u.Contains("fanfare")) return "Fanfare";
        if (u.Contains("harmonie") || u.Contains("wind")) return "Harmonie";
        if (u.Contains("percussie") || u.Contains("percussion") || u.Contains("slagwerk")) return "Percussion";
        return null;
    }

    public static BandKategorie? KategorieEnum(string? kategorie) => kategorie switch
    {
        "Brass Band" => BandKategorie.Brassband,
        "Fanfare" => BandKategorie.Fanfare,
        "Harmonie" => BandKategorie.Harmonie,
        "Percussion" => BandKategorie.Perkussion,
        _ => null
    };

    /// <summary>WMC-Division → Schweizer Stärkeklasse + Anzeige-Label (Vorgabe Kuno).</summary>
    public static (Staerkeklasse?, string?) DivisionInfo(string? division)
    {
        var d = (division ?? "").ToLowerInvariant();
        if (d.Contains("concert")) return (Staerkeklasse.Hoechstklasse, "Höchstklasse");
        if (d.Contains("1st") || d.Contains("1e")) return (Staerkeklasse.Elite, "Elite");
        if (d.Contains("2nd") || d.Contains("2e")) return (Staerkeklasse.Klasse2, "2. Klasse");
        if (d.Contains("3rd") || d.Contains("3e")) return (Staerkeklasse.Klasse3, "3. Klasse");
        if (d.Contains("youth") || d.Contains("jeugd")) return (Staerkeklasse.Oberstufe, "Oberstufe");
        return (null, string.IsNullOrWhiteSpace(division) ? null : division);
    }
}
