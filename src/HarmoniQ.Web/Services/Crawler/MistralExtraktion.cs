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
        "\"vonJahr\":null,\"bisJahr\":null}]," +
        "\"verein\":{\"name\":\"\",\"aliase\":[],\"land\":\"|null\",\"webseite\":\"|null\"," +
        "\"gruendungsjahr\":null,\"kategorie\":\"Harmonie|Brassband|Fanfare|Unterhaltung|Jugendmusik|" +
        "Bläserensemble|null\",\"staerkeklasse\":\"|null\",\"geschichte\":\"|null\",\"instagram\":\"|null\"," +
        "\"facebook\":\"|null\",\"youtube\":\"|null\",\"x\":\"|null\",\"wikipedia\":\"|null\"," +
        "\"email\":\"|null\",\"mobile\":\"|null\"}," +
        "\"funktionaere\":[{\"personName\":\"\",\"funktion\":\"\",\"gremium\":\"Vorstand|Muko\"," +
        "\"email\":\"|null\",\"instrument\":\"|null\"}]}\n" +
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
        "- Enthält die Admin-Anweisung eine EINSCHRÄNKUNG – z. B. nur ab einem Jahr, nur ein Ort/Lokal, " +
        "nur ein Land, nur eine Stärkeklasse (z. B. Höchstklasse/Elite/1. Klasse), nur eine Kategorie/" +
        "Besetzung (Harmonie/Brassband/Fanfare …), nur mit Stück-Angaben – dann gib AUSSCHLIESSLICH " +
        "passende Funde zurück und lass alle anderen weg. Funde, bei denen das geforderte Merkmal im Text " +
        "NICHT vorkommt oder nicht erkennbar ist, ebenfalls weglassen.\n" +
        "- verein: NUR ausfüllen, wenn die Seite die EIGENE Seite eines Vereins ist (Vereins-Domain). Dann " +
        "die Daten DIESES Vereins: offizieller name, alternative Namen als aliase[], land, webseite, " +
        "gruendungsjahr, kategorie (Besetzungsart), staerkeklasse, kurze geschichte/Beschreibung, " +
        "Social-Media-Links. Bei Fest-/Ranglisten-/Fremdseiten verein WEGLASSEN (null).\n" +
        "- funktionaere: NUR ausfüllen, wenn die Anweisung Vorstand und/oder Musikkommission (Muko) verlangt. " +
        "Vorstand = Präsident/Vizepräsident/Kassier:in/Aktuar:in/Beisitzer:in usw. (gremium=\"Vorstand\"); " +
        "Muko = Musikkommission (gremium=\"Muko\"). funktion = die konkrete Rolle, email/instrument nur falls " +
        "genannt. Sonst leeres Array. (Dirigent:innen gehören weiterhin in leitungen, NICHT in funktionaere.)";

    public async Task<ExtraktionsErgebnis> ExtrahiereAsync(ExtraktionsAnfrage anfrage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(anfrage.Text)) return ExtraktionsErgebnis.Leer();

        // Große Seiten in überlappende Abschnitte teilen und je Abschnitt das LLM aufrufen,
        // statt nach MaxTextZeichen abzuschneiden. Die Teil-Antworten werden anschließend zusammengeführt.
        var chunks = Teile(anfrage.Text, MaxTextZeichen, ChunkUeberlappung, MaxChunks);
        if (chunks.Count >= MaxChunks && anfrage.Text.Length > AbgedeckteLaenge(MaxChunks))
            logger.LogWarning("Inhalt {Url}: {Len} Zeichen, nur ~{Abged} in {Max} Abschnitten extrahiert – Rest verworfen.",
                anfrage.QuellUrl, anfrage.Text.Length, AbgedeckteLaenge(MaxChunks), MaxChunks);
        if (chunks.Count > 1)
            logger.LogInformation("Großer Inhalt {Url}: {Len} Zeichen → {Chunks} LLM-Abschnitte.",
                anfrage.QuellUrl, anfrage.Text.Length, chunks.Count);

        var antworten = new List<MistralAntwort>();
        string? fehler = null;
        foreach (var chunk in chunks)
        {
            ct.ThrowIfCancellationRequested();
            var (a, err) = await EinChunkAsync(chunk, anfrage, ct);
            if (a != null) antworten.Add(a);
            else fehler ??= err;
        }
        if (antworten.Count == 0) return new ExtraktionsErgebnis([], fehler ?? "Keine LLM-Antwort.");
        return new ExtraktionsErgebnis(AlsFunde(Zusammenfuehren(antworten), anfrage).ToList());
    }

    /// <summary>Ein LLM-Aufruf für einen Textabschnitt. Liefert (Antwort, Fehlertext).</summary>
    private async Task<(MistralAntwort? Antwort, string? Fehler)> EinChunkAsync(
        string text, ExtraktionsAnfrage anfrage, CancellationToken ct)
    {
        var kontext = new System.Text.StringBuilder();
        kontext.Append($"Quelle: {anfrage.QuellUrl} (Typ: {anfrage.QuelleTyp}).");
        if (anfrage.QuelleTyp == CrawlQuelleTyp.BandDomain && !string.IsNullOrWhiteSpace(anfrage.BandName))
            kontext.Append($" Diese Seite gehört der Band „{anfrage.BandName}\". Ist bei einem Konzert/" +
                           "Programm die spielende Band nicht ausdrücklich genannt, ist es diese Band – " +
                           "trage sie dann als bandName ein.");
        if (!string.IsNullOrWhiteSpace(anfrage.Hinweis))
            kontext.Append($"\nZUSÄTZLICHE ANWEISUNG DES ADMINS (unbedingt befolgen): {anfrage.Hinweis.Trim()}");
        if (anfrage.VorstandGewuenscht || anfrage.MukoGewuenscht)
        {
            var gremien = (anfrage.VorstandGewuenscht, anfrage.MukoGewuenscht) switch
            {
                (true, true) => "Vorstand UND Musikkommission (Muko)",
                (true, false) => "Vorstand",
                _ => "Musikkommission (Muko)"
            };
            kontext.Append($"\nErfasse zusätzlich die Mitglieder von: {gremien} (Feld funktionaere, mit Funktion/E-Mail/Instrument falls genannt).");
        }
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
                return (null, $"Mistral HTTP {(int)resp.StatusCode}");
            }

            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return (null, "Leere LLM-Antwort.");

            if (_llm.LogCalls)
                logger.LogInformation("Mistral-RESULT [{Url}]:\n{Content}", anfrage.QuellUrl, inhalt);

            var antwort = JsonSerializer.Deserialize<MistralAntwort>(inhalt, MistralJson);
            return antwort == null ? (null, "LLM-Antwort nicht lesbar.") : (antwort, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Mistral-Extraktion fehlgeschlagen für {Url}", anfrage.QuellUrl);
            return (null, ex.Message);
        }
    }

    public async Task<IReadOnlyList<string>> FiltereVereineAsync(
        IReadOnlyList<VereinKandidat> kandidaten, string kriterium, CancellationToken ct = default)
    {
        var treffer = new List<string>();
        const int proChunk = 250;
        for (var start = 0; start < kandidaten.Count; start += proChunk)
        {
            var teil = kandidaten.Skip(start).Take(proChunk).ToList();
            var sb = new System.Text.StringBuilder();
            for (var i = 0; i < teil.Count; i++)
                sb.Append(i + 1).Append(") ").Append(teil[i].Url).Append(" | ").Append(teil[i].Kategorie ?? "?").Append('\n');

            var sys = "Du filterst eine Vereinsliste nach einem Kriterium. Jeder Eintrag: Nummer) URL | Kategorie " +
                "(Disziplin, Stärkeklasse, Besetzung). Gib AUSSCHLIESSLICH die Nummern der Einträge zurück, deren " +
                "Kategorie das Kriterium erfüllt, als JSON {\"treffer\":[1,2,...]}. Keine Erklärungen.";
            var user = $"Kriterium: {kriterium}\n\nEinträge:\n{sb}";

            var body = new
            {
                model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
                temperature = 0.0,
                response_format = new { type = "json_object" },
                messages = new object[] { new { role = "system", content = sys }, new { role = "user", content = user } }
            };
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
                req.Content = JsonContent.Create(body);
                using var resp = await http.SendAsync(req, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    logger.LogWarning("Mistral-Filter HTTP {Code} – Chunk wird unfiltriert übernommen.", (int)resp.StatusCode);
                    treffer.AddRange(teil.Select(k => k.Url));
                    continue;
                }
                var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
                var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
                var antwort = inhalt == null ? null : JsonSerializer.Deserialize<FilterAntwort>(inhalt, MistralJson);
                foreach (var n in antwort?.Treffer ?? Enumerable.Empty<int>())
                    if (n >= 1 && n <= teil.Count) treffer.Add(teil[n - 1].Url);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Mistral-Filter fehlgeschlagen – Chunk unfiltriert übernommen.");
                treffer.AddRange(teil.Select(k => k.Url));
            }
        }
        return treffer;
    }

    private record FilterAntwort(List<int>? Treffer);

    // ── Chunking großer Seiten + Zusammenführung der Teil-Antworten ──────────
    private const int ChunkUeberlappung = 1500;
    private const int MaxChunks = 8;

    private static int AbgedeckteLaenge(int chunks) =>
        MaxTextZeichen + (chunks - 1) * (MaxTextZeichen - ChunkUeberlappung);

    private static List<string> Teile(string text, int size, int overlap, int max)
    {
        if (text.Length <= size) return [text];
        var list = new List<string>();
        var pos = 0;
        while (pos < text.Length && list.Count < max)
        {
            var len = Math.Min(size, text.Length - pos);
            list.Add(text.Substring(pos, len));
            if (pos + len >= text.Length) break;
            pos += size - overlap;
        }
        return list;
    }

    /// <summary>Führt Teil-Antworten zusammen: Konzerte gleicher Identität (Datum/Name/Ort) werden zu
    /// EINEM Konzert verschmolzen (Programmzeilen vereinigt + dedupliziert, sonst ginge beim Runner-Dedup
    /// die Hälfte verloren). Übrige Listen werden konkateniert (Dubletten erledigt der Runner-Dedup).</summary>
    private static MistralAntwort Zusammenfuehren(List<MistralAntwort> teile)
    {
        var konzerte = teile.SelectMany(t => t.Konzerte ?? Enumerable.Empty<KonzertDto>())
            .GroupBy(k => (k.Datum, NormKey(k.Name), NormKey(k.Ort)))
            .Select(g =>
            {
                var programm = g.SelectMany(k => k.Programm ?? Enumerable.Empty<ProgrammDto>())
                    .GroupBy(p => (NormKey(p.StueckTitel), NormKey(p.BandName)))
                    .Select(pg => pg.First()).ToList();
                return g.First() with { Programm = programm };
            }).ToList();

        return new MistralAntwort(
            konzerte,
            teile.SelectMany(t => t.Leitungen ?? Enumerable.Empty<LeitungDto>()).ToList(),
            teile.SelectMany(t => t.Stuecke ?? Enumerable.Empty<StueckDto>()).ToList(),
            teile.SelectMany(t => t.Komponisten ?? Enumerable.Empty<KomponistDto>()).ToList(),
            teile.Select(t => t.Verein).FirstOrDefault(v => v != null),
            teile.SelectMany(t => t.Funktionaere ?? Enumerable.Empty<FunktionaerDto>()).ToList());
    }

    private static string NormKey(string? s) => (s ?? "").Trim().ToLowerInvariant();

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
            // Auf einer Vereinsseite ist die Leitung die DIESER Band → Quell-Band bevorzugen.
            var daten = new LeitungFundDaten(l.PersonName!.Trim(), standardBand ?? Leer(l.BandName),
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

        // Vereins-Stammdaten nur bei der eigenen Vereinsseite (BandDomain).
        if (anfrage.QuelleTyp == CrawlQuelleTyp.BandDomain && a.Verein is { } v && !string.IsNullOrWhiteSpace(v.Name))
        {
            var aliase = (v.Aliase ?? []).Select(Leer).Where(x => x != null).Select(x => x!).Distinct().ToList();
            var daten = new BandFundDaten(
                v.Name!.Trim(), Leer(v.Land), Leer(v.Webseite), Leer(anfrage.LogoUrl),
                MapKategorie(v.Kategorie), MapStaerke(v.Staerkeklasse), v.Gruendungsjahr, Leer(v.Geschichte),
                Leer(v.Instagram), Leer(v.Facebook), Leer(v.YouTube), Leer(v.X), Leer(v.Wikipedia),
                Leer(v.EMail), Leer(v.Mobile), aliase);
            yield return new ExtrahierterFund(CrawlFundTyp.Band, CrawlDaten.Serialisiere(daten));
        }

        // Vorstand/Muko: nur wenn angefordert. Als Leitung-Fund (Funktion = Rolle) → BandMitgliedschaft.
        if (anfrage.VorstandGewuenscht || anfrage.MukoGewuenscht)
            foreach (var fnk in a.Funktionaere ?? Enumerable.Empty<FunktionaerDto>())
            {
                if (string.IsNullOrWhiteSpace(fnk.PersonName)) continue;
                var istMuko = NormKey(fnk.Gremium).Contains("muko") || NormKey(fnk.Gremium).Contains("kommission");
                if (istMuko ? !anfrage.MukoGewuenscht : !anfrage.VorstandGewuenscht) continue;
                var funktion = !string.IsNullOrWhiteSpace(fnk.Funktion) ? fnk.Funktion!.Trim() : (istMuko ? "Muko" : "Vorstand");
                var daten = new LeitungFundDaten(fnk.PersonName!.Trim(), standardBand, funktion,
                    EMail: Leer(fnk.EMail), InstrumentName: Leer(fnk.Instrument));
                yield return new ExtrahierterFund(CrawlFundTyp.Leitung, CrawlDaten.Serialisiere(daten));
            }
    }

    private static BandKategorie? MapKategorie(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.ToLowerInvariant();
        var jugend = t.Contains("jugend");
        if (t.Contains("brass")) return jugend ? BandKategorie.JugendmusikBrassband : BandKategorie.Brassband;
        if (t.Contains("harmonie")) return jugend ? BandKategorie.JugendmusikHarmonie : BandKategorie.Harmonie;
        if (t.Contains("fanfare")) return BandKategorie.Fanfare;
        if (t.Contains("unterhaltung")) return BandKategorie.Unterhaltung;
        if (t.Contains("ensemble") || t.Contains("bläser") || t.Contains("blaeser")) return BandKategorie.Blaeserensemble;
        return null;
    }

    private static Staerkeklasse? MapStaerke(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.ToLowerInvariant();
        if (t.Contains("höchst") || t.Contains("hoechst")) return Staerkeklasse.Hoechstklasse;
        if (t.Contains("elite")) return Staerkeklasse.Elite;
        if (t.Contains("ober")) return Staerkeklasse.Oberstufe;
        if (t.Contains("mittel")) return Staerkeklasse.Mittelstufe;
        if (t.Contains("unter")) return Staerkeklasse.Unterstufe;
        var m = Regex.Match(t, "[1-4]");
        return m.Success ? m.Value switch
        {
            "1" => Staerkeklasse.Klasse1,
            "2" => Staerkeklasse.Klasse2,
            "3" => Staerkeklasse.Klasse3,
            _ => Staerkeklasse.Klasse4
        } : null;
    }

    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    private static string Kurz(string s, int n = 300) => s.Length > n ? s[..n] : s;

    // ── DTOs für Mistral-Antwort ────────────────────────────────────────────
    private record ChatResponse([property: JsonPropertyName("choices")] List<Choice>? Choices);
    private record Choice([property: JsonPropertyName("message")] Message? Message);
    private record Message([property: JsonPropertyName("content")] string? Content);

    private record MistralAntwort(
        List<KonzertDto>? Konzerte, List<LeitungDto>? Leitungen,
        List<StueckDto>? Stuecke, List<KomponistDto>? Komponisten, VereinDto? Verein,
        List<FunktionaerDto>? Funktionaere);
    private record FunktionaerDto(string? PersonName, string? Funktion, string? Gremium, string? EMail, string? Instrument);
    private record VereinDto(
        string? Name, List<string>? Aliase, string? Land, string? Webseite, int? Gruendungsjahr,
        string? Kategorie, string? Staerkeklasse, string? Geschichte,
        string? Instagram, string? Facebook, string? YouTube, string? X, string? Wikipedia,
        string? EMail, string? Mobile);
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
