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

    // Ohne LLM kein Filtern – alle Kandidaten durchreichen.
    public Task<IReadOnlyList<string>> FiltereVereineAsync(
        IReadOnlyList<VereinKandidat> kandidaten, string kriterium, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>(kandidaten.Select(k => k.Url).ToList());

    // Ohne LLM keine SBBW-Strukturierung.
    public Task<SbbwRangliste?> SbbwRanglisteAsync(string pdfText, CancellationToken ct = default) =>
        Task.FromResult<SbbwRangliste?>(null);

    public Task<IReadOnlyList<SbbwVideo>> SbbwVideosAsync(string seitenOutline, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SbbwVideo>>([]);

    public Task<string?> KomponistAusSucheAsync(string stueckTitel, string suchText, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    // Ohne LLM kein Stil-Filter (alles durchlassen) und keine Band-Erkennung.
    public Task<KklEventInfo> KklEventAsync(string titel, string? beschreibung, string? stilKriterium, CancellationToken ct = default) =>
        Task.FromResult(new KklEventInfo(true, null));

    // Ohne LLM keine Programm-Strukturierung.
    public Task<KklProgramm> KklProgrammAsync(string titel, string? programmText, string? mitwirkendeText, CancellationToken ct = default) =>
        Task.FromResult(new KklProgramm([], [], null));
}
