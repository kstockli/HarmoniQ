using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// HTML-Hilfen für den Crawler: interne Links für die Crawl-Frontier ernten und den Hauptinhalt
/// in bereinigten Text wandeln (Nav/Footer/Script entfernt), bevor er an die Extraktion geht.
/// </summary>
public static class CrawlHtmlHelfer
{
    /// <summary>Linearisiert eine Video-Seite (SBBW §4.2b) für die LLM-Zuordnung: ersetzt jeden
    /// Embed-iframe durch einen Marker <c>[[VIDEO:id]]</c> und gibt den Textfluss (Überschriften +
    /// Beschriftungen in Dokumentreihenfolge) zurück. So kann das LLM jedes Video Kategorie/Band/Stück
    /// zuordnen, egal wie uneinheitlich die Captions ausgezeichnet sind.</summary>
    public static string VideoSeiteOutline(string html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var s = Regex.Replace(html, @"<iframe[^>]*embed/([a-z0-9]+)[^>]*>", "\n[[VIDEO:$1]]\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"(?is)<script.*?</script>|<style.*?</style>", " ");
        s = Regex.Replace(s, @"(?i)</(div|p|section|h[1-6]|li|tr|td)>", "\n");
        s = Regex.Replace(s, "<[^>]+>", " ");
        s = WebUtility.HtmlDecode(s);
        var zeilen = s.Split('\n')
            .Select(l => Regex.Replace(l, @"\s+", " ").Trim())
            .Where(l => l.Length > 0);
        return string.Join("\n", zeilen);
    }

    /// <summary>Absolute, interne (gleiche Host) Links aus dem HTML – dedupliziert, ohne Fragmente.</summary>
    public static List<string> InterneLinks(string html, Uri basis)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (nodes == null) return [];

        var ergebnis = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "").Trim();
            if (href.Length == 0 || href.StartsWith('#')
                || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase)
                || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!Uri.TryCreate(basis, href, out var abs)) continue;
            if (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) continue;
            if (!string.Equals(abs.Host, basis.Host, StringComparison.OrdinalIgnoreCase)) continue;

            var ohneFragment = abs.GetLeftPart(UriPartial.Query);
            if (gesehen.Add(ohneFragment)) ergebnis.Add(ohneFragment);
        }
        return ergebnis;
    }

    /// <summary>
    /// Bester Logo-Kandidat der Seite (absolute URL): zuerst <c>og:image</c>/<c>twitter:image</c>
    /// (Site-Vorschaubild, meist Logo/Plakat), sonst ein <c>&lt;img&gt;</c> mit „logo" in src/alt/class/id,
    /// sonst <c>apple-touch-icon</c>. Inline-<c>data:</c>-URIs werden ignoriert. Null, wenn nichts passt.
    /// </summary>
    public static string? LogoUrl(string html, Uri basis)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // 1) og:image / twitter:image
        var meta = doc.DocumentNode.SelectSingleNode(
            "//meta[@property='og:image' or @property='og:image:url' or @name='og:image' or @name='twitter:image']");
        if (Aufloesen(meta?.GetAttributeValue("content", ""), basis) is { } og) return og;

        // 2) <img> mit „logo" in src/alt/class/id
        var imgs = doc.DocumentNode.SelectNodes("//img");
        if (imgs != null)
            foreach (var img in imgs)
            {
                var merkmale = img.GetAttributeValue("src", "") + " " + img.GetAttributeValue("alt", "")
                    + " " + img.GetAttributeValue("class", "") + " " + img.GetAttributeValue("id", "");
                if (merkmale.Contains("logo", StringComparison.OrdinalIgnoreCase)
                    && Aufloesen(img.GetAttributeValue("src", ""), basis) is { } l)
                    return l;
            }

        // 3) apple-touch-icon als Fallback
        var icon = doc.DocumentNode.SelectSingleNode("//link[contains(@rel,'apple-touch-icon')]");
        return Aufloesen(icon?.GetAttributeValue("href", ""), basis);
    }

    private static string? Aufloesen(string? url, Uri basis)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        url = HtmlEntity.DeEntitize(url).Trim();
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return null; // keine Inline-Daten-URIs
        return Uri.TryCreate(basis, url, out var abs)
               && (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps)
            ? abs.ToString() : null;
    }

    // Plattformen/Dienste, die keine Vereins-Webseiten sind (Vereins-Link-Ernte).
    private static readonly string[] LinkRauschen =
    [
        "facebook.", "instagram.", "twitter.", "x.com", "youtube.", "youtu.be", "linkedin.", "tiktok.",
        "google.", "goo.gl", "wikipedia.", "spotify.", "apple.com", "paypal.", "twint.", "issuu.",
        "doodle.", "eventfrog.", "ticketcorner.", "starticket.", "cms.", "wordpress.org", "wix.com",
        "jimdo.com", "cdn.", "fonts.", "gstatic.", "vimeo.", "flickr.", "whatsapp."
    ];

    /// <summary>Ausgehende, fremde (andere Host) http(s)-Links – auf die Domain-Wurzel normalisiert,
    /// je Host nur einmal, gängige Plattformen/Dienste herausgefiltert (Vereins-Link-Ernte, Spec §4.1).</summary>
    public static List<string> ExterneLinks(string html, Uri basis)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (nodes == null) return [];

        var ergebnis = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            var href = node.GetAttributeValue("href", "").Trim();
            if (href.Length == 0 || href.StartsWith('#')) continue;
            if (!Uri.TryCreate(basis, href, out var abs)) continue;
            if (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) continue;

            var host = abs.Host;
            if (string.Equals(host, basis.Host, StringComparison.OrdinalIgnoreCase)) continue; // intern
            if (LinkRauschen.Any(r => host.Contains(r, StringComparison.OrdinalIgnoreCase))) continue;
            if (!gesehen.Add(host)) continue; // je Host nur einmal

            ergebnis.Add($"{abs.Scheme}://{host}/"); // Domain-Wurzel als Crawl-Start
        }
        return ergebnis;
    }

    /// <summary>
    /// Wie <see cref="ExterneLinks"/>, ordnet aber jedem Vereins-Link die zuletzt vorausgehende
    /// **Kategorie-Überschrift** zu (Dokumentreihenfolge) – z. B. „Konzertmusik, Höchstklasse, Harmonie".
    /// So liefern Verzeichnis-Seiten (EMF: Logos gruppiert nach Klasse/Besetzung) je Verein die Kategorie.
    /// </summary>
    public static List<(string Url, string? Kategorie)> ExterneLinksMitKategorie(string html, Uri basis)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ergebnis = new List<(string, string?)>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? kategorie = null;

        foreach (var node in doc.DocumentNode.Descendants())
        {
            if (node.NodeType != HtmlNodeType.Element) continue;

            var direkt = DirektText(node);
            if (direkt.Length is > 0 and < 70 && IstKategorie(direkt)) kategorie = direkt;

            if (node.Name != "a") continue;
            var href = node.GetAttributeValue("href", "").Trim();
            if (href.Length == 0 || href.StartsWith('#')) continue;
            if (!Uri.TryCreate(basis, href, out var abs)) continue;
            if (abs.Scheme != Uri.UriSchemeHttp && abs.Scheme != Uri.UriSchemeHttps) continue;
            if (string.Equals(abs.Host, basis.Host, StringComparison.OrdinalIgnoreCase)) continue;
            if (LinkRauschen.Any(r => abs.Host.Contains(r, StringComparison.OrdinalIgnoreCase))) continue;
            if (!gesehen.Add(abs.Host)) continue;

            ergebnis.Add(($"{abs.Scheme}://{abs.Host}/", kategorie));
        }
        return ergebnis;
    }

    /// <summary>Direkter Textinhalt eines Elements (nur eigene Text-Kindknoten, ohne Nachfahren).</summary>
    private static string DirektText(HtmlNode node)
    {
        var sb = new StringBuilder();
        foreach (var c in node.ChildNodes)
            if (c.NodeType == HtmlNodeType.Text) sb.Append(c.InnerText);
        var t = HtmlEntity.DeEntitize(sb.ToString());
        return string.Join(' ', t.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IstKategorie(string t) =>
        Regex.IsMatch(t, "Konzertmusik|Marschmusik|Parademusik", RegexOptions.IgnoreCase)
        || (Regex.IsMatch(t, @"Höchstklasse|Elite|[1-4]\.\s*Klasse|stufe", RegexOptions.IgnoreCase)
            && Regex.IsMatch(t, @"Harmonie|Brass\s*Band|Brassband|Fanfare", RegexOptions.IgnoreCase));

    /// <summary>Kleine Seiten-Vorschau ohne LLM: <c>&lt;title&gt;</c> und Meta-/og-Beschreibung.</summary>
    public static (string? Titel, string? Beschreibung) SeitenInfo(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var titel = doc.DocumentNode.SelectSingleNode("//title")?.InnerText;
        var descNode = doc.DocumentNode.SelectSingleNode(
            "//meta[@name='description' or @property='og:description' or @name='og:description']");
        var desc = descNode?.GetAttributeValue("content", "");
        return (Sauber(titel), Sauber(desc));
    }

    private static string? Sauber(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        s = HtmlEntity.DeEntitize(s);
        s = string.Join(' ', s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return s.Length == 0 ? null : s;
    }

    /// <summary>Bereinigter Haupttext: Script/Style/Nav/Footer/Header entfernt, Whitespace normalisiert.</summary>
    public static string TextBereinigen(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var weg = doc.DocumentNode.SelectNodes("//script | //style | //nav | //footer | //header | //noscript");
        if (weg != null)
            foreach (var n in weg) n.Remove();

        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText ?? "");
        var zeilen = text.Split('\n')
            .Select(z => string.Join(' ', z.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)))
            .Where(z => z.Length > 0);
        return string.Join('\n', zeilen);
    }
}
