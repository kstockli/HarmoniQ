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

    /// <summary>Filtert eine Vereins-Kandidatenliste (URL + Kategorie/Klasse) nach einem Freitext-Kriterium
    /// (z. B. „Höchstklasse, Harmonie") und gibt die passenden URLs zurück. Ohne LLM: alle (Passthrough).</summary>
    Task<IReadOnlyList<string>> FiltereVereineAsync(
        IReadOnlyList<VereinKandidat> kandidaten, string kriterium, CancellationToken ct = default);

    /// <summary>SBBW (§4.2): strukturiert den Text eines Jahres-Ergebnis-PDFs in Kategorien mit Rangliste.
    /// Ohne LLM (Stub): leer/null.</summary>
    Task<SbbwRangliste?> SbbwRanglisteAsync(string pdfText, CancellationToken ct = default);

    /// <summary>SBBW (§4.2 Teil 2b): ordnet die Videos einer linearisierten Video-Seite (Marker
    /// [[VIDEO:id]] im Textfluss) je Kategorie/Band/Stück zu. Ohne LLM (Stub): leer.</summary>
    Task<IReadOnlyList<SbbwVideo>> SbbwVideosAsync(string seitenOutline, CancellationToken ct = default);
}

/// <summary>Ein Video der SBBW-Video-Seite mit (best-effort) Zuordnung zu Kategorie/Band/Stück.</summary>
public record SbbwVideo(
    string? Id,
    string? Kategorie,
    string? Band,
    string? StueckTitel,
    string? StueckTyp);

/// <summary>Ein Vereins-Kandidat aus der Link-Ernte: Webseite + (falls erkannt) Kategorie/Stärkeklasse.</summary>
public record VereinKandidat(string Url, string? Kategorie);

/// <summary>Eine Kategorie-Seite eines SBBW-Jahres-PDFs als strukturierte Rangliste.</summary>
public record SbbwKategorie(
    string? Kategorie,
    DateOnly? Datum,
    string? Ort,
    string? AufgabestueckTitel,
    string? AufgabestueckKomponist,
    List<SbbwZeile>? Zeilen);

/// <summary>Eine Band-Zeile der SBBW-Rangliste.</summary>
public record SbbwZeile(
    int? Rang,
    string? Band,
    string? Kanton,
    string? Dirigent,
    int? Punkte,
    string? SelbstwahlTitel,
    string? SelbstwahlKomponist);

/// <summary>Alle Kategorien eines SBBW-Jahres-PDFs.</summary>
public record SbbwRangliste(List<SbbwKategorie>? Kategorien);

/// <summary>Eingabe für die Extraktion: was wurde gefunden und in welchem Kontext.</summary>
public record ExtraktionsAnfrage(
    CrawlQuelleTyp QuelleTyp,
    string QuellUrl,
    string Text,
    bool IstPdf,
    string? BandName = null,
    string? Hinweis = null,
    string? LogoUrl = null,
    bool VorstandGewuenscht = false,
    bool MukoGewuenscht = false);

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
