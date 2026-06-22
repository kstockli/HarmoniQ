using HtmlAgilityPack;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// HTML-Hilfen für den Crawler: interne Links für die Crawl-Frontier ernten und den Hauptinhalt
/// in bereinigten Text wandeln (Nav/Footer/Script entfernt), bevor er an die Extraktion geht.
/// </summary>
public static class CrawlHtmlHelfer
{
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
