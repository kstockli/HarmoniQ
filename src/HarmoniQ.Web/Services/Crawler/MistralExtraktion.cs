using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// LLM-Extraktion über Mistral „La Plateforme" (Chat-Completions, JSON-Modus). Wandelt bereinigten
/// Seiten-/PDF-Text in strukturierte Fund-Vorschläge (Konzert/Leitung/Stück/Komponist). Bewusst
/// faktentreu: nur tatsächlich Genanntes, keine Halluzination – fehlende Felder bleiben leer.
/// </summary>
public class MistralExtraktion(HttpClient http, IOptions<CrawlerOptions> opt, ILogger<MistralExtraktion> logger)
    : IExtraktion
{
    private const string Endpoint = "https://api.mistral.ai/v1/chat/completions";
    private const int MaxTextZeichen = 24_000; // Kostenbremse + Kontextgrenze

    private readonly LlmOptions _llm = opt.Value.Llm;

    /// <summary>JSON-Optionen für die LLM-Antwort: tolerant gegenüber unsauberen Datums-/Zahlwerten
    /// (z. B. „1935-00-00", „2024", Zahl als String) – kein Crash, sondern bestmögliche Auswertung.</summary>
    private static readonly JsonSerializerOptions MistralJson = ErstelleOptionen();

    private static JsonSerializerOptions ErstelleOptionen()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        o.Converters.Add(new ToleranterDatumConverter());
        o.Converters.Add(new ToleranterIntConverter());
        return o;
    }

    private const string SystemPrompt =
        "Du bist ein präziser Extraktor für Webseiten und PDFs von Blasmusik-Vereinen und " +
        "Musikfesten (Deutsch/Schweizerdeutsch). Extrahiere ausschließlich Fakten, die im Text " +
        "WÖRTLICH vorkommen. Rate NIE; ist etwas nicht genannt, lass das Feld weg oder null. " +
        "Trenne sauber Stück-Titel und Komponist:in (auch bei „arr.\"-Bearbeitungen: Titel = Werk, " +
        "komponistName = die genannte Person). Antworte AUSSCHLIESSLICH mit gültigem JSON in genau " +
        "diesem Schema (leere Arrays wenn nichts gefunden):\n" +
        "{\"konzerte\":[{\"datum\":\"YYYY-MM-DD|null\",\"name\":\"|null\",\"ort\":\"|null\"," +
        "\"programm\":[{\"stueckTitel\":\"\",\"komponistName\":\"|null\",\"arrangeurName\":\"|null\"," +
        "\"bandName\":\"|null\",\"reihenfolge\":null}]}]," +
        "\"leitungen\":[{\"personName\":\"\",\"bandName\":\"|null\",\"funktion\":\"Dirigent\"," +
        "\"vonJahr\":null,\"bisJahr\":null}]}\n" +
        "Regeln:\n" +
        "- Jedes gespielte Stück gehört als Programmzeile in sein Konzert: Komponist:in in komponistName, " +
        "spielende Band/Verein in bandName.\n" +
        "- Bei Bearbeitungen (\"arr. X\", \"Bearbeitung: X\", \"arrangiert von X\"): komponistName = " +
        "ursprüngliche:r Komponist:in des Werks, arrangeurName = die bearbeitende Person. NIE beide in " +
        "ein Feld (also komponistName=\"Robbie Williams\", arrangeurName=\"Evi Güdel Tanner\").\n" +
        "- Bei Wettspielen/Festen: gruppiere nach Datum+Ort zu Konzerten; je Programmzeile die spielende Band eintragen.\n" +
        "- leitungen: nur Dirigent:innen/musikalische Leitung einer Band (KEIN Vorstand/Präsident). " +
        "Ist eine Amtszeit/Jahre genannt (z. B. \"Josef Baumann (1885-1887)\" oder \"1924-1958 Robert " +
        "Isenegger\"), trage vonJahr/bisJahr als Zahlen ein (1885/1887). Bei mehreren Perioden " +
        "(z. B. \"1876-1885, 1887-1893\"): vonJahr = frühestes Anfangsjahr, bisJahr = spätestes Endjahr.\n" +
        "- Liste Komponist:innen NICHT separat auf – sie stehen bereits in den Programmzeilen.\n" +
        "- Personennamen IMMER als \"Vorname Nachname\" (z. B. \"Roger Meier\", NICHT \"Meier Roger\") – " +
        "gilt für komponistName UND personName.\n" +
        "- reihenfolge je Programmzeile: die Startzeit als Zahl ohne Doppelpunkt (14:40 → 1440, 8.00 → 800, " +
        "8.17 → 817). Ist keine Zeit genannt, nummeriere fortlaufend in der Reihenfolge des Auftretens " +
        "(1, 2, 3, …). Behalte die zeitliche/Programm-Reihenfolge bei.\n" +
        "- datum: vollständiges Datum YYYY-MM-DD. Ist nur das Jahr (oder Jahr+Monat) bekannt, NICHT mit " +
        "Nullen auffüllen – fehlende Teile weglassen (also \"2024\" oder \"2024-06\", nicht \"2024-00-00\").\n" +
        "- Enthält die Admin-Anweisung eine EINSCHRÄNKUNG (z. B. nur Konzerte ab einem Jahr, nur ein " +
        "bestimmter Ort/Lokal, nur mit Stück-Angaben), dann gib AUSSCHLIESSLICH dazu passende Funde " +
        "zurück und lass alle anderen weg – auch wenn sie im Text stehen.";

    public async Task<ExtraktionsErgebnis> ExtrahiereAsync(ExtraktionsAnfrage anfrage, CancellationToken ct = default)
    {
        var text = anfrage.Text.Length > MaxTextZeichen ? anfrage.Text[..MaxTextZeichen] : anfrage.Text;
        if (string.IsNullOrWhiteSpace(text)) return ExtraktionsErgebnis.Leer();

        var kontext = new System.Text.StringBuilder();
        kontext.Append($"Quelle: {anfrage.QuellUrl} (Typ: {anfrage.QuelleTyp}).");
        if (anfrage.QuelleTyp == CrawlQuelleTyp.BandDomain && !string.IsNullOrWhiteSpace(anfrage.BandName))
            kontext.Append($" Diese Seite gehört der Band „{anfrage.BandName}\". Ist bei einem Konzert/" +
                           "Programm die spielende Band nicht ausdrücklich genannt, ist es diese Band – " +
                           "trage sie dann als bandName ein.");
        if (!string.IsNullOrWhiteSpace(anfrage.Hinweis))
            kontext.Append($"\nZUSÄTZLICHE ANWEISUNG DES ADMINS (unbedingt befolgen): {anfrage.Hinweis.Trim()}");
        kontext.Append("\n\nExtrahiere die Funde aus folgendem Text:\n\n").Append(text);
        var anfrageKontext = kontext.ToString();

        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.1,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user", content = anfrageKontext }
            }
        };

        if (_llm.LogCalls)
            logger.LogInformation("Mistral-CALL [{Url}] Typ={Typ} Band={Band} Hinweis={Hinweis} TextLen={Len}\nTextprobe: {Probe}",
                anfrage.QuellUrl, anfrage.QuelleTyp, anfrage.BandName ?? "-", anfrage.Hinweis ?? "-",
                text.Length, Kurz(text, 800));

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var fehlertext = await resp.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Mistral HTTP {Code}: {Body}", (int)resp.StatusCode, Kurz(fehlertext));
                return new ExtraktionsErgebnis([], $"Mistral HTTP {(int)resp.StatusCode}");
            }

            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return ExtraktionsErgebnis.Leer("Leere LLM-Antwort.");

            if (_llm.LogCalls)
                logger.LogInformation("Mistral-RESULT [{Url}]:\n{Content}", anfrage.QuellUrl, inhalt);

            var antwort = JsonSerializer.Deserialize<MistralAntwort>(inhalt, MistralJson);
            if (antwort == null) return ExtraktionsErgebnis.Leer("LLM-Antwort nicht lesbar.");

            return new ExtraktionsErgebnis(AlsFunde(antwort, anfrage).ToList());
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Mistral-Extraktion fehlgeschlagen für {Url}", anfrage.QuellUrl);
            return new ExtraktionsErgebnis([], ex.Message);
        }
    }

    private static IEnumerable<ExtrahierterFund> AlsFunde(MistralAntwort a, ExtraktionsAnfrage anfrage)
    {
        // Bei BandDomain ist die Quell-Band die Standard-Band, wenn keine genannt wird.
        var standardBand = anfrage.QuelleTyp == CrawlQuelleTyp.BandDomain ? Leer(anfrage.BandName) : null;

        foreach (var k in a.Konzerte ?? [])
        {
            var programm = (k.Programm ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.StueckTitel))
                .Select(p => new ProgrammZeileDaten(
                    p.StueckTitel!.Trim(), Leer(p.KomponistName), Leer(p.BandName) ?? standardBand,
                    p.Reihenfolge, Leer(p.ArrangeurName)))
                .ToList();
            // Nur sinnvolle Konzert-Funde (Datum oder Programm vorhanden).
            if (k.Datum is null && programm.Count == 0) continue;
            var daten = new KonzertFundDaten(k.Datum, Leer(k.Name), Leer(k.Ort), null, programm);
            yield return new ExtrahierterFund(CrawlFundTyp.Konzert, CrawlDaten.Serialisiere(daten));
        }

        foreach (var l in a.Leitungen ?? [])
        {
            if (string.IsNullOrWhiteSpace(l.PersonName)) continue;
            var daten = new LeitungFundDaten(l.PersonName!.Trim(), Leer(l.BandName) ?? standardBand,
                string.IsNullOrWhiteSpace(l.Funktion) ? "Dirigent" : l.Funktion!.Trim(),
                l.VonJahr, l.BisJahr);
            yield return new ExtrahierterFund(CrawlFundTyp.Leitung, CrawlDaten.Serialisiere(daten));
        }

        foreach (var s in a.Stuecke ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Titel)) continue;
            var daten = new StueckFundDaten(s.Titel!.Trim(), Leer(s.KomponistName), s.Jahr);
            yield return new ExtrahierterFund(CrawlFundTyp.Stueck, CrawlDaten.Serialisiere(daten));
        }

        foreach (var k in a.Komponisten ?? [])
        {
            if (string.IsNullOrWhiteSpace(k.Name)) continue;
            var daten = new KomponistFundDaten(k.Name!.Trim(), Leer(k.Biografie), null, k.Geburtsjahr, Leer(k.WikipediaUrl));
            yield return new ExtrahierterFund(CrawlFundTyp.Komponist, CrawlDaten.Serialisiere(daten));
        }
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Kurz(string s, int n = 300) => s.Length > n ? s[..n] : s;

    // ── DTOs für Mistral-Antwort ────────────────────────────────────────────
    private record ChatResponse([property: JsonPropertyName("choices")] List<Choice>? Choices);
    private record Choice([property: JsonPropertyName("message")] Message? Message);
    private record Message([property: JsonPropertyName("content")] string? Content);

    private record MistralAntwort(
        List<KonzertDto>? Konzerte, List<LeitungDto>? Leitungen,
        List<StueckDto>? Stuecke, List<KomponistDto>? Komponisten);
    private record KonzertDto(DateOnly? Datum, string? Name, string? Ort, List<ProgrammDto>? Programm);
    private record ProgrammDto(string? StueckTitel, string? KomponistName, string? BandName, int? Reihenfolge, string? ArrangeurName);
    private record LeitungDto(string? PersonName, string? BandName, string? Funktion, int? VonJahr, int? BisJahr);
    private record StueckDto(string? Titel, string? KomponistName, int? Jahr);
    private record KomponistDto(string? Name, string? Biografie, int? Geburtsjahr, string? WikipediaUrl);

    // ── Tolerante JSON-Konverter (LLM liefert manchmal unsaubere Werte) ──────

    /// <summary>Liest Datumswerte tolerant: „2024-06-17", „2024-06", „2024", „1935-00-00"
    /// (00 = unbekannt → auf 01). Unlesbares → null. Verhindert FormatException-Crashes.</summary>
    private sealed class ToleranterDatumConverter : JsonConverter<DateOnly?>
    {
        public override DateOnly? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType != JsonTokenType.String) { reader.Skip(); return null; }
            var s = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            var m = Regex.Match(s, @"(\d{4})(?:-(\d{1,2}))?(?:-(\d{1,2}))?");
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var jahr) || jahr is < 1000 or > 3000)
                return null;
            var monat = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
            var tag = m.Groups[3].Success ? int.Parse(m.Groups[3].Value) : 1;
            if (monat is < 1 or > 12) monat = 1;
            if (tag < 1 || tag > DateTime.DaysInMonth(jahr, monat)) tag = 1;
            return new DateOnly(jahr, monat, tag);
        }

        public override void Write(Utf8JsonWriter w, DateOnly? v, JsonSerializerOptions o)
        {
            if (v is null) w.WriteNullValue(); else w.WriteStringValue(v.Value.ToString("yyyy-MM-dd"));
        }
    }

    /// <summary>Liest Ganzzahlen tolerant: Zahl, Dezimalzahl (abgeschnitten) oder String mit Ziffern
    /// (z. B. „800", „1885-1887" → 1885). Unlesbares → null.</summary>
    private sealed class ToleranterIntConverter : JsonConverter<int?>
    {
        public override int? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    return null;
                case JsonTokenType.Number:
                    if (reader.TryGetInt32(out var i)) return i;
                    if (reader.TryGetDouble(out var d)) return (int)d;
                    return null;
                case JsonTokenType.String:
                    var m = Regex.Match(reader.GetString() ?? "", @"-?\d+");
                    return m.Success && int.TryParse(m.Value, out var v) ? v : null;
                default:
                    reader.Skip();
                    return null;
            }
        }

        public override void Write(Utf8JsonWriter w, int? v, JsonSerializerOptions o)
        {
            if (v is null) w.WriteNullValue(); else w.WriteNumberValue(v.Value);
        }
    }
}
