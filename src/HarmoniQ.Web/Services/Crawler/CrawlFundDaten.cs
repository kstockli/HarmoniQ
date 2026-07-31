using System.Text.Json;
using System.Text.Json.Serialization;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Vertrag für <see cref="HarmoniQ.Web.Data.Models.CrawlFund.DatenJson"/>: die strukturierten
/// Vorschläge, welche die Extraktion (Heuristik in C1, LLM in C3) füllt und die Review-UI beim
/// Übernehmen auf die bestehenden Find-or-create-Services mappt. Bewusst flexibel/optional –
/// Teildaten sind erlaubt (Spec §4.1: „nicht raten“).
/// </summary>
public static class CrawlDaten
{
    /// <summary>Gemeinsame JSON-Optionen (DateOnly wird von System.Text.Json nativ unterstützt).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        // Enums (z. B. BandKategorie/Staerkeklasse) als lesbare Namen statt Zahlen serialisieren.
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialisiere<T>(T daten) => JsonSerializer.Serialize(daten, Json);

    public static T? Deserialisiere<T>(string json) =>
        string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, Json);
}

/// <summary>Eine Programmzeile eines Konzert-Funds: Stück + optional Komponist:in / Arrangeur:in / Band.</summary>
public record ProgrammZeileDaten(
    string StueckTitel,
    string? KomponistName = null,
    string? BandName = null,
    int? Reihenfolge = null,
    string? ArrangeurName = null);

/// <summary>
/// Konzert-Fund (Typ <see cref="HarmoniQ.Web.Data.Models.CrawlFundTyp.Konzert"/>): mappt beim
/// Übernehmen auf <c>KonzertErfassungService.Eingabe</c>. <see cref="Datum"/> ist beim Crawlen
/// optional (kann unsicher sein); bei der Übernahme ist es Pflicht und ggf. im Review zu ergänzen.
/// </summary>
public record KonzertFundDaten(
    DateOnly? Datum = null,
    TimeOnly? Uhrzeit = null,
    string? Name = null,
    string? Ort = null,
    string? Beschreibung = null,
    IReadOnlyList<ProgrammZeileDaten>? Programm = null,
    string? Notiz = null,
    IReadOnlyList<RangZeileDaten>? Raenge = null,
    IReadOnlyList<KonzertVideoDaten>? Videos = null,
    string? BildUrl = null,
    string? Webseite = null);

/// <summary>Eine Rangliste-Zeile eines Wettbewerbs-Konzerts (SBBW §4.2): Band + Platzierung/Punkte
/// + Dirigent:in. Mappt beim Übernehmen auf <c>KonzertBand.Rang/Punkte</c> + <c>KonzertPerson</c> (Dirigent).</summary>
public record RangZeileDaten(
    string Band,
    int? Rang = null,
    int? Punkte = null,
    string? Dirigent = null,
    string? Kanton = null);

/// <summary>Video-Referenz eines Konzert-Funds (z. B. SBBW Infomaniak-VOD): wird beim Übernehmen zu
/// einem <c>Video</c> (Plattform + ExternId), verknüpft mit Stück und – falls bekannt – Band.</summary>
public record KonzertVideoDaten(
    HarmoniQ.Web.Data.Models.VideoPlattform Plattform,
    string ExternId,
    string? Band = null,
    string? StueckTitel = null);

/// <summary>
/// Leitung-Fund (Typ <see cref="HarmoniQ.Web.Data.Models.CrawlFundTyp.Leitung"/>): mappt beim
/// Übernehmen auf eine <c>BandMitgliedschaft</c> (Person als Dirigent:in). Fehlt <see cref="BandName"/>,
/// wird auf die Ziel-Band der Quelle (<c>CrawlQuelle.BandId</c>) zurückgegriffen.
/// </summary>
public record LeitungFundDaten(
    string PersonName,
    string? BandName = null,
    string Funktion = "Dirigent",
    int? VonJahr = null,
    int? BisJahr = null,
    string? Notiz = null,
    string? EMail = null,
    string? InstrumentName = null);

/// <summary>
/// Stück-Fund (Typ <see cref="CrawlFundTyp.Stueck"/>): ein einzelnes Stück aus einer Repertoire-/
/// Werkliste (ohne Konzertbezug). Mappt beim Übernehmen auf <c>Stueck</c> (+ optional Komponist:in
/// als <c>StueckBeitrag</c>). Die Quell-URL wird als <c>Stueck.OriginalUrl</c> mitgeführt.
/// </summary>
public record StueckFundDaten(
    string Titel,
    string? KomponistName = null,
    int? Jahr = null,
    Schwierigkeitsgrad? Schwierigkeit = null,
    string? Besetzung = null,
    string? Beschreibung = null,
    string? Notiz = null);

/// <summary>
/// Komponist:in-Fund (Typ <see cref="CrawlFundTyp.Komponist"/>): eine Person zum Anlegen oder
/// <b>Anreichern</b> (z. B. aus Wikipedia: Biografie, Bild, Geburtsjahr, Artikel-Link). Mappt beim
/// Übernehmen auf <c>Person</c> (Rolle Komponist:in); bestehende, kuratierte Felder werden dabei
/// <b>nicht</b> überschrieben – nur leere Felder gefüllt.
/// </summary>
public record KomponistFundDaten(
    string Name,
    string? Biografie = null,
    string? BildUrl = null,
    int? Geburtsjahr = null,
    string? WikipediaUrl = null,
    string? Notiz = null,
    string? BildAttribution = null);

/// <summary>
/// Band-/Vereins-Fund (Typ <see cref="CrawlFundTyp.Band"/>): Stammdaten eines Vereins, meist von dessen
/// eigener Webseite. Mappt beim Übernehmen auf eine <c>Band</c> (find-or-create über Name/Alias) und füllt
/// nur leere Felder; <see cref="Aliase"/> und Social-Links werden ergänzt.
/// </summary>
public record BandFundDaten(
    string Name,
    string? Land = null,
    string? Webseite = null,
    string? BildUrl = null,
    BandKategorie? Kategorie = null,
    Staerkeklasse? Staerkeklasse = null,
    int? Gruendungsjahr = null,
    string? Geschichte = null,
    string? Instagram = null,
    string? Facebook = null,
    string? YouTube = null,
    string? X = null,
    string? Wikipedia = null,
    string? EMail = null,
    string? Mobile = null,
    IReadOnlyList<string>? Aliase = null);

/// <summary>
/// Entdeckte Vereins-Webseite (Typ <see cref="CrawlFundTyp.Webseite"/>) aus der Link-Ernte einer
/// Event-Seite, mit kleiner Vorschau (Seitentitel/Beschreibung, ohne LLM) zur Entscheidung. Übernahme
/// legt eine inaktive <c>CrawlQuelle</c> Typ BandDomain (Vorschlag) an.
/// </summary>
public record WebseiteFundDaten(
    string Url,
    string? VereinName = null,
    string? Titel = null,
    string? Beschreibung = null,
    string? Kategorie = null);
