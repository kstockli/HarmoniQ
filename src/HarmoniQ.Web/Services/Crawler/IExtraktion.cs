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

    /// <summary>Liest aus Web-Suchergebnis-Text den Komponisten/die Komponistin eines Stücks heraus
    /// (nur wenn klar belegt, sonst null – nicht raten). Ohne LLM (Stub): null.</summary>
    Task<string?> KomponistAusSucheAsync(string stueckTitel, string suchText, CancellationToken ct = default);

    /// <summary>Formuliert einen fremden Beschreibungstext in <b>eigenen deutschen Worten</b> neu und knapp
    /// (Urheberrecht: keine Formulierungen übernehmen; nur belegte Fakten). Für importierte Band-Bios / Konzert-
    /// Beschreibungen (z. B. WMC-Bios EN→DE, KKL-Beschreibungen). Ohne LLM (Stub): null (Original behalten).</summary>
    Task<string?> ParaphrasiereAsync(string text, CancellationToken ct = default);

    /// <summary>Veranstalter-Event (KKL §4.3): klassifiziert anhand Titel/Beschreibung, ob das Event zum
    /// Stil-Kriterium passt (z. B. „Blasmusik/Brassband"), und liest die auftretende Band/Ensemble heraus.
    /// Ohne LLM (Stub): passt=true (kein Filter), Band=null. Wird nur als Fallback gebraucht, wenn der
    /// Stil-Hinweis zu keiner KKL-Kategorie (<c>?genre=</c>) passt.</summary>
    Task<KklEventInfo> KklEventAsync(string titel, string? beschreibung, string? stilKriterium, CancellationToken ct = default);

    /// <summary>Veranstalter-Detail (KKL §4.3): strukturiert den Text der Detailseiten-Tabs „Programm" und
    /// „Mitwirkende" in Stücke (mit Komponist:in), die auftretenden Bands/Ensembles und – bei genau einer
    /// Band – die Dirigentin/den Dirigenten. Bei Wettbewerben treten mehrere Bands auf (dann kein einzelner
    /// Dirigent). Vorspann-Texte, Pausen, Kategorie-Überschriften und Gesprächs-/Moderations-Einträge sind
    /// keine Stücke. Ohne LLM (Stub): leeres Programm.</summary>
    Task<KklProgramm> KklProgrammAsync(string titel, string? programmText, string? mitwirkendeText, CancellationToken ct = default);

    /// <summary>Liest aus TITEL (+ optional Beschreibung) eines Blasmusik-YouTube-Videos das gespielte Stück,
    /// – falls klar genannt – die Komponist:in sowie Aufführungs-Ort und Anlass heraus (nicht raten). Optionaler
    /// Bandname hilft, ihn vom Stück zu trennen. Ohne LLM (Stub): alles null.</summary>
    Task<VideoAnalyse> VideoTitelAnalysierenAsync(string videoTitel, string? bandName = null,
        string? beschreibung = null, CancellationToken ct = default);

    /// <summary>Bestimmt aus <b>Web-Suchergebnissen</b> zu einem Blasmusik-YouTube-Video das gespielte Stück
    /// und – falls belegt – die Komponist:in. Grounded: nur wenn die Treffer eindeutig EIN konkretes Werk als
    /// Inhalt dieses Videos belegen; ein ganzes Konzert / mehrere Stücke / unklar → beides null (kein Raten).
    /// Ohne LLM (Stub): beides null.</summary>
    Task<VideoAnalyse> VideoAusSucheAsync(string videoTitel, string? bandName, string suchText, CancellationToken ct = default);

    /// <summary>Liest aus Titel + Beschreibung (+ Veranstalter nur als Hinweis) eines Blasmusik-Events die
    /// AUFTRETENDE Formation heraus (Blasorchester/Brass Band/Ensemble/Orchester) – nicht Veranstalter/
    /// Sponsor/Ort und keine Einzelperson. Steht oft im Titel vor einem Doppelpunkt oder in der Beschreibung
    /// („… spielt das X"). Null, wenn nicht klar genannt (nicht raten). Ohne LLM (Stub): null.</summary>
    Task<string?> EventBandAsync(string titel, string? beschreibung, string? veranstalter, CancellationToken ct = default);

    /// <summary>Prüft einen öffentlichen Beitrag (Bewertungs-Kommentar) gegen die Verhaltensregeln
    /// (sachlich; keine Schmähkritik/Beleidigung/Diskriminierung/rechtswidrigen Inhalte).
    /// Ok=true → sofort veröffentlichen; Ok=false → zur Freigabe. Ohne LLM (Stub) / bei Fehler: Ok=true.</summary>
    Task<BeitragPruefung> BeitragPruefenAsync(string text, CancellationToken ct = default);
}

/// <summary>Ergebnis der KI-Moderation eines Beitrags.</summary>
public record BeitragPruefung(bool Ok, string? Grund);

/// <summary>Aus Videotitel/-beschreibung erkanntes Stück + Komponist:in + Aufführungs-Ort + Anlass
/// (jeweils null, wenn nicht klar erkennbar).</summary>
public record VideoAnalyse(string? StueckTitel, string? Komponist, string? Ort = null, string? Anlass = null);

/// <summary>LLM-Einschätzung zu einem Veranstalter-Event (KKL): passt zum Stil-Kriterium + erkannte Band.</summary>
public record KklEventInfo(bool Passt, string? Band);

/// <summary>Strukturiertes Programm/Besetzung eines KKL-Events: Stücke, auftretende Bands/Ensembles
/// (bei Wettbewerben mehrere) und – nur bei genau einer Band – die Dirigentin/der Dirigent.</summary>
public record KklProgramm(IReadOnlyList<KklStueck> Stuecke, IReadOnlyList<string> Bands, string? Dirigent);

/// <summary>Ein Programm-Stück eines KKL-Events: Titel + (falls genannt) Komponist:in.</summary>
public record KklStueck(string Titel, string? Komponist);

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
