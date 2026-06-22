namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Platzhalter-Extraktor ohne LLM: liefert keine automatischen Funde, hält aber die Pipeline
/// end-to-end lauffähig (Fetch → Review → Übernehmen). Aktiv, solange kein LLM-Anbieter
/// konfiguriert ist (<see cref="CrawlerOptions.Llm"/>). Die Admin-Review zeigt dann den
/// Rohtext zur manuellen Erfassung. Wird durch die echte LLM-Implementierung ersetzt (C3).
/// </summary>
public class StubExtraktion : IExtraktion
{
    public Task<ExtraktionsErgebnis> ExtrahiereAsync(ExtraktionsAnfrage anfrage, CancellationToken ct = default) =>
        Task.FromResult(ExtraktionsErgebnis.Leer(
            "Kein LLM konfiguriert – automatische Extraktion deaktiviert (manuelle Erfassung im Review)."));
}
