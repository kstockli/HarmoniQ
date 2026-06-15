using HtmlAgilityPack;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Liest eine Komponisten-Webseite und extrahiert Kandidaten für Stück-Titel.
/// Da jede Webseite anders aufgebaut ist, werden bewusst viele Kandidaten gesammelt
/// (Links, Listen, Überschriften, Tabellenzellen) und der Admin kuratiert anschließend.
/// </summary>
public class WebseitenScraper(HttpClient http, ILogger<WebseitenScraper> logger)
{
    private static readonly string[] Rauschen =
    [
        "home", "menu", "kontakt", "contact", "about", "über", "impressum", "datenschutz",
        "privacy", "login", "search", "suche", "newsletter", "shop", "warenkorb", "cart",
        "facebook", "instagram", "twitter", "youtube", "mehr", "more", "weiter", "next",
        "zurück", "back", "news", "blog", "bio", "biography", "press", "presse", "music",
        "works", "werke", "store", "commissions", "calendar", "termine"
    ];

    public async Task<List<string>> HoleKandidatenAsync(string url, CancellationToken ct = default)
    {
        var html = await http.GetStringAsync(url, ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Text aus typischen "Listen"-Elementen einsammeln.
        var nodes = doc.DocumentNode.SelectNodes(
            "//a | //li | //h2 | //h3 | //h4 | //td | //div[contains(@class,'title')]");
        if (nodes == null) return [];

        var kandidaten = new List<string>();
        var gesehen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in nodes)
        {
            var text = HtmlEntity.DeEntitize(node.InnerText ?? "").Trim();
            // Mehrfache Leerzeichen/Zeilenumbrüche zusammenfassen.
            text = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            if (!IstPlausibel(text)) continue;
            if (!gesehen.Add(text)) continue;
            kandidaten.Add(text);
        }

        logger.LogInformation("{Count} Kandidaten von {Url} extrahiert.", kandidaten.Count, url);
        return kandidaten;
    }

    private static bool IstPlausibel(string text)
    {
        if (text.Length is < 3 or > 120) return false;
        if (!text.Any(char.IsLetter)) return false;
        if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return false;
        // Reine Navigations-/Rausch-Begriffe ausschließen.
        if (Rauschen.Contains(text, StringComparer.OrdinalIgnoreCase)) return false;
        return true;
    }
}
