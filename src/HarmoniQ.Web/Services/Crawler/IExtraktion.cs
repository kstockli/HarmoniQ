using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Extraktions-Stufe der Pipeline (Spec §4, Stufe 3): wandelt bereinigten Seiten-/PDF-Text in
/// strukturierte Fund-Vorschläge. Anbieter-neutral – Implementierungen: Stub (ohne LLM),
/// später ein konkreter LLM-Anbieter (entschieden: Mistral „La Plateforme"). Auswahl per
/// Konfiguration (<see cref="CrawlerOptions.Llm"/>).
/// </summary>
public interface IExtraktion
{
    Task<ExtraktionsErgebnis> ExtrahiereAsync(ExtraktionsAnfrage anfrage, CancellationToken ct = default);
}

/// <summary>Eingabe für die Extraktion: was wurde gefunden und in welchem Kontext.</summary>
public record ExtraktionsAnfrage(
    CrawlQuelleTyp QuelleTyp,
    string QuellUrl,
    string Text,
    bool IstPdf,
    string? BandName = null,
    string? Hinweis = null,
    string? LogoUrl = null);

/// <summary>Ein vom Extraktor vorgeschlagener Fund (Typ + serialisierter <c>DatenJson</c>-Vertrag).</summary>
public record ExtrahierterFund(
    CrawlFundTyp Typ,
    string DatenJson,
    Konfidenz Konfidenz = Konfidenz.Mittel);

/// <summary>Ergebnis eines Extraktionslaufs über einen Text. <see cref="Fehler"/> gesetzt = fehlgeschlagen.</summary>
public record ExtraktionsErgebnis(
    IReadOnlyList<ExtrahierterFund> Funde,
    string? Fehler = null,
    string? Meldung = null)
{
    public static ExtraktionsErgebnis Leer(string? meldung = null) => new([], null, meldung);
}
