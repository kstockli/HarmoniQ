using System.Text.Json;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Sonderfall <b>EMF-Vereinsverzeichnis</b> (emf26.ch/vereine): Die Seite ist eine schwere Wix-SPA,
/// deren Daten aus einer sauberen öffentlichen <b>JSON-API</b> stammen. Statt die Seite zu rendern
/// (teuer; im Container OOM → „Target crashed"), holen wir die API direkt – schnell, deterministisch
/// filterbar, ohne Browser. Liefert pro Verein Name, Kategorie/Klasse/Besetzung, Website &amp; Socials.
/// </summary>
public static class EmfVereinImporter
{
    public const string ApiUrl = "https://emf26-api.ch/public/verein?locale=de";

    /// <summary>Greift bei der EMF-Vereinsübersicht (Host emf26.ch, Pfad enthält „verein").</summary>
    public static bool IstZustaendig(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Host.EndsWith("emf26.ch", StringComparison.OrdinalIgnoreCase)
        && u.AbsolutePath.Contains("verein", StringComparison.OrdinalIgnoreCase);

    public sealed record Verein(
        string? name, string? kategorie, string? website,
        string? klasse, string? besetzung, string? direktion,
        string? facebook, string? instagram);

    public static List<Verein> Parse(string json)
    {
        var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<Verein>>(json, opt) ?? [];
    }

    /// <summary>Deterministischer Hinweis-Filter auf der Kategorie-Zeichenkette (z. B.
    /// „Höchstklasse, Harmonie"): jedes im Hinweis genannte bekannte Merkmal (Klasse/Besetzung) muss
    /// in der Kategorie des Vereins vorkommen. Ohne erkennbares Merkmal greift kein Filter.</summary>
    public static bool PasstZuHinweis(string? kategorie, string? hinweis)
    {
        if (string.IsNullOrWhiteSpace(hinweis)) return true;
        var k = Norm(kategorie);
        var h = Norm(hinweis);
        string[] merkmale =
        [
            "hoechstklasse", "elite", "1klasse", "2klasse", "3klasse", "4klasse",
            "harmonie", "brassband", "fanfare"
        ];
        var gefordert = merkmale.Where(m => h.Contains(m)).ToList();
        return gefordert.All(k.Contains);
    }

    /// <summary>Klein schreiben, Umlaute entschärfen, Leerzeichen/Punkte entfernen
    /// („Brass Band" → „brassband", „2. Klasse" → „2klasse", „Höchstklasse" → „hoechstklasse").</summary>
    private static string Norm(string? s)
    {
        s = (s ?? "").ToLowerInvariant()
            .Replace("ö", "oe").Replace("ä", "ae").Replace("ü", "ue");
        return new string(s.Where(c => !char.IsWhiteSpace(c) && c != '.').ToArray());
    }
}
