namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Billige Relevanz-Triage (Spec §4 Stufe 2, §8 Kostenkontrolle): erkennt anhand von
/// Schlüsselwörtern in URL oder Text, ob eine Seite Konzert-/Leitungs-Inhalte enthalten könnte.
/// Nur relevante Seiten gehen an die (teure) LLM-Extraktion. Bewusst eine reine Heuristik –
/// keine Strukturierung.
/// </summary>
public static class SeitenFilter
{
    private static readonly string[] Stichwoerter =
    [
        "konzert", "programm", "besetzung", "vorstand", "leitung", "dirigent", "direktion",
        "agenda", "termine", "anlass", "anlaesse", "jahreskonzert", "repertoire", "werke",
        "wettspiel", "wertung", "spielplan", "auftritt"
    ];

    public static bool IstRelevant(string url, string text)
    {
        var u = url.ToLowerInvariant();
        if (Stichwoerter.Any(w => u.Contains(w))) return true;

        // Im Text nur den Anfang prüfen (günstig) – relevante Seiten nennen die Begriffe meist früh.
        var probe = text.Length > 4000 ? text[..4000] : text;
        probe = probe.ToLowerInvariant();
        return Stichwoerter.Any(w => probe.Contains(w));
    }
}
