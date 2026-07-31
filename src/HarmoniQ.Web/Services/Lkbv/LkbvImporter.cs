using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Lkbv;

/// <summary>
/// Parser für den <b>einmaligen LKBV-Import</b> (Luzerner Kantonaler Blasmusikverband, lkbv.ch). Reine
/// HTML-Extraktion (WordPress, serverseitig gerendert):
/// <list type="bullet">
/// <item>Fotogalerie (<c>/fotogalerien/</c>): je Verein ein <c>&lt;a href="/vereine/&lt;slug&gt;/"&gt;</c> mit
/// Foto (<c>&lt;img&gt;</c>) und der folgenden <c>&lt;h2&gt;</c> (Name).</item>
/// <item>Detailseite (<c>/vereine/&lt;slug&gt;/</c>): „Gründung JJJJ", „Klasse &lt;Stärke&gt;/&lt;Kat&gt;"
/// (z. B. „3/BB"), „Internet &lt;Homepage-URL&gt;".</item>
/// </list>
/// Reine Parsing-Logik – Fetch + DB übernimmt <see cref="LkbvImportService"/>.
/// </summary>
public static class LkbvImporter
{
    public const string GalerieUrl = "https://www.lkbv.ch/fotogalerien/";
    public const string FotoQuelle = "Foto: Luzerner Blasmusikverband, lkbv.ch";

    public record GalerieEintrag(string Name, string? FotoUrl, string DetailUrl);
    public record Detail(int? Gruendungsjahr, Staerkeklasse? Klasse, BandKategorie? Kategorie, string? Webseite);

    private static string Clean(string? s) =>
        s == null ? "" : Regex.Replace(WebUtility.HtmlDecode(s).Replace(' ', ' '), @"\s+", " ").Trim();

    // ── Galerie ──────────────────────────────────────────────────────────────
    public static IReadOnlyList<GalerieEintrag> ParseGalerie(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var result = new List<GalerieEintrag>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var a in doc.DocumentNode.SelectNodes("//a[contains(@href,'/vereine/')]") ?? Enumerable.Empty<HtmlNode>())
        {
            var href = a.GetAttributeValue("href", "");
            var img = a.SelectSingleNode(".//img");
            if (img == null || !href.Contains("/vereine/") || !gesehen.Add(href)) continue;
            var name = Clean(a.SelectSingleNode("following::h2[1]")?.InnerText);
            if (name.Length == 0) continue;
            result.Add(new GalerieEintrag(name, Leer(img.GetAttributeValue("src", "")), href));
        }
        return result;
    }

    // ── Detailseite ──────────────────────────────────────────────────────────
    public static Detail ParseDetail(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var txt = Clean(doc.DocumentNode.SelectSingleNode("//body")?.InnerText ?? "");

        int? jahr = null;
        var mj = Regex.Match(txt, @"Gr[üu]ndung\s+(\d{4})");
        if (mj.Success && int.TryParse(mj.Groups[1].Value, out var j) && j is > 1700 and < 2100) jahr = j;

        Staerkeklasse? klasse = null; BandKategorie? kat = null;
        var mk = Regex.Match(txt, @"Klasse\s+([^\s]+)");
        if (mk.Success)
        {
            var teile = mk.Groups[1].Value.Split('/', 2);
            klasse = StaerkeAus(teile[0]);
            if (teile.Length > 1) kat = KategorieAus(teile[1]);
        }

        // Homepage: erster externer Link, der nicht lkbv/Social/Footer ist.
        string? web = null;
        foreach (var a in doc.DocumentNode.SelectNodes("//a[@href]") ?? Enumerable.Empty<HtmlNode>())
        {
            var h = a.GetAttributeValue("href", "");
            if (Regex.IsMatch(h, @"^https?://", RegexOptions.IgnoreCase)
                && !Regex.IsMatch(h, "lkbv\\.ch|facebook|instagram|youtube|twitter|rettenmund|googletag|gmpg|w3\\.org", RegexOptions.IgnoreCase))
            { web = h.Trim(); break; }
        }
        return new Detail(jahr, klasse, kat, web);
    }

    // ── Mappings ─────────────────────────────────────────────────────────────
    /// <summary>LKBV-Stärkeklasse („1"–„4", „Höchstklasse") → <see cref="Staerkeklasse"/>. Unbekannt/leer → null.</summary>
    public static Staerkeklasse? StaerkeAus(string? wert)
    {
        var w = (wert ?? "").Trim().ToLowerInvariant();
        if (w.Contains("höchst") || w.Contains("hoechst")) return Staerkeklasse.Hoechstklasse;
        return w switch { "1" => Staerkeklasse.Klasse1, "2" => Staerkeklasse.Klasse2, "3" => Staerkeklasse.Klasse3, "4" => Staerkeklasse.Klasse4, _ => null };
    }

    /// <summary>LKBV-Kategorie-Kürzel (BB/HA/FA…) → <see cref="BandKategorie"/>. Unbekannt → null (nicht raten).</summary>
    public static BandKategorie? KategorieAus(string? kuerzel)
    {
        var k = (kuerzel ?? "").Trim().ToUpperInvariant();
        return k switch
        {
            "BB" => BandKategorie.Brassband,
            "HA" or "H" => BandKategorie.Harmonie,
            "FA" or "F" => BandKategorie.Fanfare,
            _ => null
        };
    }

    /// <summary>Order-/diakritik-unabhängiger Namensschlüssel (Wörter sortiert) – matcht „Feldmusik Adligenswil"
    /// mit „Adligenswil Feldmusik". Für den Abgleich mit bestehenden Bands.</summary>
    public static string WortSchluessel(string name)
    {
        var d = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        var worte = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2).OrderBy(w => w, StringComparer.Ordinal);
        return string.Join(" ", worte);
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
