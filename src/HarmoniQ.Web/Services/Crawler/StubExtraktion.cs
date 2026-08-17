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

    // Ohne LLM keine Paraphrase → Original behalten.
    public Task<string?> ParaphrasiereAsync(string text, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    // Ohne LLM kein Stil-Filter (alles durchlassen) und keine Band-Erkennung.
    public Task<KklEventInfo> KklEventAsync(string titel, string? beschreibung, string? stilKriterium, CancellationToken ct = default) =>
        Task.FromResult(new KklEventInfo(true, null));

    // Ohne LLM keine Programm-Strukturierung.
    public Task<KklProgramm> KklProgrammAsync(string titel, string? programmText, string? mitwirkendeText, CancellationToken ct = default) =>
        Task.FromResult(new KklProgramm([], [], null));

    // Ohne LLM keine Titel-Analyse – Nutzer:in erfasst Stück/Komponist:in/Ort/Anlass manuell.
    public Task<VideoAnalyse> VideoTitelAnalysierenAsync(string videoTitel, string? bandName = null,
        string? beschreibung = null, CancellationToken ct = default) =>
        Task.FromResult(new VideoAnalyse(null, null));

    // Ohne LLM keine Auswertung der Web-Suchergebnisse.
    public Task<VideoAnalyse> VideoAusSucheAsync(string videoTitel, string? bandName, string suchText, CancellationToken ct = default) =>
        Task.FromResult(new VideoAnalyse(null, null));

    // Ohne LLM keine Band-Erkennung aus Event-Titel/Beschreibung.
    public Task<string?> EventBandAsync(string titel, string? beschreibung, string? veranstalter, CancellationToken ct = default) =>
        Task.FromResult<string?>(null);

    // Ohne LLM keine Moderation → freigeben (fail-open).
    public Task<BeitragPruefung> BeitragPruefenAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(new BeitragPruefung(true, null));
}
