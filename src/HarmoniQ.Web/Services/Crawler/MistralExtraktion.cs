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
        o.Converters.Add(new ToleranterZeitConverter());
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
        "{\"konzerte\":[{\"datum\":\"YYYY-MM-DD|null\",\"uhrzeit\":\"HH:MM|null\",\"name\":\"|null\",\"ort\":\"|null\"," +
        "\"webseite\":\"|null\"," +
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
        "- uhrzeit: Startzeit des Konzerts als \"HH:MM\" (24h, z. B. \"19:30\", \"20:00\"), NUR wenn im Text " +
        "ausdrücklich genannt. Nicht raten – ist keine Zeit angegeben, null.\n" +
        "- webseite: absolute URL zur offiziellen Konzert-/Event- bzw. Ticketseite, wenn im Text/als Link " +
        "vorhanden (z. B. Detailseite des Konzerts, Ticketanbieter). Sonst null. Keine URL erfinden.\n" +
        "- Enthält die Admin-Anweisung eine EINSCHRÄNKUNG – z. B. nur ab einem Jahr, nur ein Ort/Lokal, " +
        "nur ein Land, nur eine Stärkeklasse (z. B. Höchstklasse/Elite/1. Klasse), nur eine Kategorie/" +
        "Besetzung (Harmonie/Brassband/Fanfare …), nur mit Stück-Angaben – dann gib AUSSCHLIESSLICH " +
        "passende Funde zurück und lass alle anderen weg. Funde, bei denen das geforderte Merkmal im Text " +
        "NICHT vorkommt oder nicht erkennbar ist, ebenfalls weglassen.\n" +
        "- verein: NUR ausfüllen, wenn die Seite die EIGENE Seite eines Vereins ist (Vereins-Domain). Dann " +
        "die Daten DIESES Vereins: offizieller name, alternative Namen als aliase[], land, webseite, " +
        "gruendungsjahr, kategorie (Besetzungsart), staerkeklasse, kurze geschichte/Beschreibung, " +
        "Social-Media-Links. WICHTIG (Urheberrecht): die geschichte in EIGENEN Worten neu formulieren " +
        "(2-3 Saetze, nur die Fakten aus dem Text) - Formulierungen der Website NIE woertlich uebernehmen. " +
        "Bei Fest-/Ranglisten-/Fremdseiten verein WEGLASSEN (null).\n" +
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
        var eigeneVereinsseite = anfrage.QuelleTyp is CrawlQuelleTyp.BandDomain or CrawlQuelleTyp.BandKonzertVorschau;
        if (eigeneVereinsseite && !string.IsNullOrWhiteSpace(anfrage.BandName))
            kontext.Append($" Diese Seite gehört der Band „{anfrage.BandName}\". Ist bei einem Konzert/" +
                           "Programm die spielende Band nicht ausdrücklich genannt, ist es diese Band – " +
                           "trage sie dann als bandName ein.");
        if (anfrage.QuelleTyp == CrawlQuelleTyp.BandKonzertVorschau)
            kontext.Append("\nZIEL: NUR KÜNFTIGE, ECHTE KONZERTE dieser Band (Jahresvorschau/Agenda). " +
                "Als Konzert gelten: Jahreskonzert, Frühlings-/Herbst-/Gala-/Kirchen-/Advents-/Weihnachts-/" +
                "Muttertagskonzert, Unterhaltungskonzert/-abend, Serenade, Musical, Platzkonzert (im Zweifel ja). " +
                "KEINE Konzerte sind: Kilbi/Chilbi, Ständchen, Auftritte in Alters-/Pflegeheimen, Fasnacht/" +
                "Guggen, Umzug/Marsch/Parade, Generalversammlung, Bazar/Basar, Lotto/Loto, Risotto-/Spaghetti-/" +
                "Vereinsessen, Papiersammlung – diese WEGLASSEN. Gib nur Konzerte mit erkennbarem (künftigem) " +
                "Datum zurück; ein Programm/Stücke sind NICHT erforderlich. Vergangene Termine weglassen.");
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

    private const string SbbwSystemPrompt = """
        Du strukturierst den Text eines Ergebnis-PDFs des Schweizerischen Brass Band Wettbewerbs (SBBW).
        Das PDF hat pro Wettbewerbs-KATEGORIE eine Seite: Höchstklasse (Excellence), Elite, 1.–4. Kategorie.
        Gib AUSSCHLIESSLICH ein JSON-Objekt zurück:
        {"kategorien":[{"kategorie":"...","datum":"YYYY-MM-DD","ort":"...","aufgabestueckTitel":"...",
        "aufgabestueckKomponist":"...","zeilen":[{"rang":1,"band":"...","kanton":"VS","dirigent":"...",
        "punkte":null,"selbstwahlTitel":null,"selbstwahlKomponist":null}]}]}

        Regeln:
        - kategorie: deutscher Name (Höchstklasse, Elite, 1. Kategorie, 2. Kategorie, 3. Kategorie, 4. Kategorie).
        - datum: das Datum DIESER Kategorie-Seite (z. B. "Samstag 29. November 2025" → 2025-11-29).
        - ort: der Saal/Ort aus dem Seitenkopf, falls vorhanden, sonst null.
        - aufgabestueckTitel / aufgabestueckKomponist: aus dem Kopf ("Pièce imposée"/"Aufgabestück").
          Komponist OHNE Länder-Kürzel in Klammern (z. B. "Thomas Doss", nicht "Thomas Doss (AU)").
        - zeilen: je Band eine Zeile, in der Reihenfolge der ENDPLATZIERUNG.
        - rang = offizielle ENDplatzierung (Spalte "Rang"/"Klassierung"/"Total"), NICHT die Startnummer
          (Prog. Nr./Startnr.) am Zeilenanfang. Beispiel "10 Valaisia ... 6 5 1 1 2 1 Mnemosyne Phrases":
          Startnr.=10, Endrang=1.
        - band: Vereinsname OHNE Kanton-Kürzel; kanton = das Kürzel in Klammern (z. B. "(VS)" → "VS"), sonst null.
        - dirigent: Name der Dirigentin/des Dirigenten.
        - punkte: die Gesamtwertung der Zeile. In Elite und 1.–4. Kategorie = die erreichten Punkte
          (höher = besser). In der Höchstklasse = die SUMME der Teilränge (Spalte „Total"/„Klassierung";
          tiefer = besser), z. B. Zeile „… 1 1 2 1 …" → 2. Wenn keine Wertung ausgewiesen ist, null.
        - selbstwahlTitel: NUR Höchstklasse (Spalte "Pièce à choix"/"Selbstwahlstück"), sonst null.
        - selbstwahlKomponist: nur wenn du den Komponisten des Selbstwahlstücks SICHER kennst, sonst null (nicht raten).
        - Fakten wörtlich übernehmen, nichts erfinden. Keine Erklärungen, nur das JSON.
        """;

    public async Task<SbbwRangliste?> SbbwRanglisteAsync(string pdfText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfText)) return null;
        var text = pdfText.Length > MaxTextZeichen ? pdfText[..MaxTextZeichen] : pdfText;

        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SbbwSystemPrompt },
                new { role = "user", content = "Strukturiere dieses SBBW-Ergebnis-PDF:\n\n" + text }
            }
        };

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Mistral(SBBW) HTTP {Code}: {Body}", (int)resp.StatusCode,
                    Kurz(await resp.Content.ReadAsStringAsync(ct)));
                return null;
            }
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return null;
            if (_llm.LogCalls) logger.LogInformation("Mistral(SBBW)-RESULT:\n{Content}", inhalt);
            return JsonSerializer.Deserialize<SbbwRangliste>(inhalt, MistralJson);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Mistral(SBBW)-Strukturierung fehlgeschlagen.");
            return null;
        }
    }

    private const string SbbwVideoSystemPrompt = """
        Das ist die Video-Galerie eines Brass-Band-Wettbewerbs (SBBW) als Text in Dokumentreihenfolge.
        Jede Zeile [[VIDEO:id]] ist ein eingebettetes Video. Ordne jedem Video anhand der umgebenden
        Überschriften (Kategorie + Aufgabestück) und Beschriftungen (evtl. Bandname und/oder
        Selbstwahlstück-Titel) eine Zuordnung zu. Gib AUSSCHLIESSLICH JSON zurück:
        {"videos":[{"id":"...","kategorie":"...","band":null,"stueckTitel":null,"stueckTyp":"Aufgabe|Selbstwahl"}]}
        Regeln:
        - id: exakt die id aus [[VIDEO:id]].
        - kategorie: Höchstklasse, Elite, 1. Kategorie, 2. Kategorie, 3. Kategorie oder 4. Kategorie
          (aus der nächstgelegenen Abschnitts-Überschrift).
        - band: Verein, falls aus Beschriftung/Reihenfolge ableitbar, sonst null. OHNE Kanton-Kürzel.
        - stueckTyp: "Aufgabe" (Pflicht-/Aufgabestück) oder "Selbstwahl" (Pièce à choix).
        - stueckTitel: NUR der reine Werktitel OHNE Komponist und OHNE Länder-Kürzel (z. B. "Genetic Code",
          nicht "Genetic Code - Thomas Doss (AU)"). Bei Selbstwahl der genannte Titel; bei Aufgabe der
          Aufgabestück-Titel der Kategorie. Falls unklar null. Nichts erfinden.
        """;

    public async Task<IReadOnlyList<SbbwVideo>> SbbwVideosAsync(string seitenOutline, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(seitenOutline)) return [];
        var text = seitenOutline.Length > MaxTextZeichen ? seitenOutline[..MaxTextZeichen] : seitenOutline;
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SbbwVideoSystemPrompt },
                new { role = "user", content = text }
            }
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("Mistral(SBBW-Video) HTTP {Code}", (int)resp.StatusCode);
                return [];
            }
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return [];
            var antwort = JsonSerializer.Deserialize<SbbwVideoAntwort>(inhalt, MistralJson);
            return antwort?.Videos ?? [];
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Mistral(SBBW-Video)-Zuordnung fehlgeschlagen.");
            return [];
        }
    }

    private record SbbwVideoAntwort(List<SbbwVideo>? Videos);

    public async Task<KklEventInfo> KklEventAsync(string titel, string? beschreibung, string? stilKriterium, CancellationToken ct = default)
    {
        var sys = "Du beurteilst ein Konzert/Event eines Veranstaltungshauses. Gib AUSSCHLIESSLICH JSON zurück: " +
            "{\"passt\":true|false,\"band\":\"...\"|null}. passt=true nur, wenn das Event dem Stil-Kriterium " +
            "entspricht (anhand Titel/Beschreibung). band = der auftretende Verein/das Ensemble/Orchester " +
            "(z. B. Blasorchester, Brass Band) als Name, falls erkennbar, sonst null. Personen (Dirigent:in, " +
            "Solist:in) sind KEINE Band. Nichts erfinden.";
        var krit = string.IsNullOrWhiteSpace(stilKriterium) ? "(kein Kriterium – passt=true)" : stilKriterium.Trim();
        var besch = beschreibung is { Length: > 1200 } ? beschreibung[..1200] : beschreibung;
        var user = $"Stil-Kriterium: {krit}\n\nTitel: {titel}\n\nBeschreibung: {besch}";
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
            if (!resp.IsSuccessStatusCode) return new KklEventInfo(true, null); // im Zweifel behalten (Review entscheidet)
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return new KklEventInfo(true, null);
            var a = JsonSerializer.Deserialize<KklAntwort>(inhalt, MistralJson);
            return new KklEventInfo(a?.Passt ?? true, Leer(a?.Band));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "KKL-Event-Klassifikation fehlgeschlagen: {Titel}", titel); return new KklEventInfo(true, null); }
    }

    private record KklAntwort(bool? Passt, string? Band);

    public async Task<string?> EventBandAsync(string titel, string? beschreibung, string? veranstalter, CancellationToken ct = default)
    {
        var sys = "Du extrahierst die AUFTRETENDE Musikformation eines Konzert-Events (Blasorchester, Brass Band, " +
            "Ensemble, Orchester, Musikgesellschaft, Guggenmusik). Gib AUSSCHLIESSLICH JSON zurück: " +
            "{\"band\":\"...\"|null}. band = der Name der Formation, DIE SPIELT. Sie steht oft im Titel vor einem " +
            "Doppelpunkt (z. B. \"Harmonic Brass: Big Trip\" -> \"Harmonic Brass\") oder in der Beschreibung " +
            "(z. B. \"... spielt das Christoph Walter Orchestra\", \"mit dem X\"). NICHT der Veranstalter/Sponsor/" +
            "Serviceclub (z. B. Lions Club, Rotary), NICHT der Ort, NICHT eine Einzelperson (Dirigent:in/Solist:in). " +
            "Wenn keine Formation klar genannt ist: null. Nichts erfinden.";
        var besch = beschreibung is { Length: > 1200 } ? beschreibung[..1200] : beschreibung;
        var user = $"Titel: {titel}\n\nVeranstalter (nur Hinweis, evtl. NICHT die Band): {veranstalter}\n\nBeschreibung: {besch}";
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[] { new { role = "system", content = sys }, new { role = "user", content = user } }
        };
        // Bei Massen-Läufen (z. B. Eventfrog: viele Events) trifft man Mistral-Rate-Limits (429).
        // Deshalb mit Backoff wiederholen (Retry-After beachten), statt still null zu liefern.
        for (var versuch = 0; ; versuch++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
                req.Content = JsonContent.Create(body);
                using var resp = await http.SendAsync(req, ct);
                if (((int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500) && versuch < 5)
                {
                    var warte = resp.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, versuch)));
                    logger.LogInformation("Event-Band-Extraktion: HTTP {Status}, warte {Sek}s (Versuch {V}).",
                        (int)resp.StatusCode, warte.TotalSeconds, versuch + 1);
                    await Task.Delay(warte, ct);
                    continue;
                }
                if (!resp.IsSuccessStatusCode) return null;
                var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
                var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(inhalt)) return null;
                return Leer(JsonSerializer.Deserialize<BandAntwort>(inhalt, MistralJson)?.Band);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "Event-Band-Extraktion fehlgeschlagen: {Titel}", titel); return null; }
        }
    }

    private record BandAntwort(string? Band);

    public async Task<KklProgramm> KklProgrammAsync(string titel, string? programmText, string? mitwirkendeText, CancellationToken ct = default)
    {
        var leer = new KklProgramm([], [], null);
        if (string.IsNullOrWhiteSpace(programmText) && string.IsNullOrWhiteSpace(mitwirkendeText)) return leer;

        var sys = "Du strukturierst Programm und Besetzung eines Konzerts aus zwei Textabschnitten einer " +
            "Veranstaltungs-Detailseite. Gib AUSSCHLIESSLICH JSON zurück: " +
            "{\"stuecke\":[{\"titel\":\"...\",\"komponist\":\"...\"|null}],\"bands\":[\"...\"],\"dirigent\":\"...\"|null}. " +
            "Regeln: stuecke = nur echte Musikstücke aus dem Programm (mit Komponist:in, falls genannt). Der " +
            "Komponist steht je nach Seite mit Lebensdaten vor dem Titel (z. B. 'George Gershwin (1898-1937)' dann " +
            "neue Zeile 'Cuban Overture') ODER als 'Komponist: Titel' mit Doppelpunkt (z. B. 'Bert Appermont: A " +
            "Brussels Requiem' -> komponist='Bert Appermont', titel='A Brussels Requiem'). KEINE Stücke sind: " +
            "Kategorie-/Abschnitts-Überschriften (z. B. 'TESTSTUECK (Vormittag)', 'SELBSTWAHLSTUECKE (Nachmittag)'), " +
            "Vorspann-/Werbetexte, Pausen, Einführungen sowie Gespräche/Moderationen (z. B. 'X im Gespräch mit Y'). " +
            "bands = ALLE auftretenden Bands/Ensembles/Orchester als Namen (bei einem normalen Konzert genau eine; " +
            "bei einem Wettbewerb/Contest mehrere – dann jede gelistete Band aufnehmen). Einzelpersonen (Solist:in, " +
            "Instrumentalist:in mit Instrument) sind KEINE Band. dirigent = Name der Dirigentin/des Dirigenten NUR " +
            "wenn genau eine Band auftritt und er klar genannt ist (Zeile endet oft auf '- Dirigent'/'Leitung'); " +
            "bei mehreren Bands null. Nichts erfinden; fehlt etwas, null bzw. leere Liste.";
        string K(string? s, int max) => string.IsNullOrWhiteSpace(s) ? "(keiner)" : (s.Length > max ? s[..max] : s);
        var user = $"Event: {titel}\n\n=== Programm ===\n{K(programmText, 4000)}\n\n=== Mitwirkende ===\n{K(mitwirkendeText, 2000)}";
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
            if (!resp.IsSuccessStatusCode) return leer;
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(inhalt)) return leer;
            var a = JsonSerializer.Deserialize<KklProgrammAntwort>(inhalt, MistralJson);
            var stuecke = (a?.Stuecke ?? [])
                .Where(s => !string.IsNullOrWhiteSpace(s.Titel))
                .Select(s => new KklStueck(s.Titel!.Trim(), Leer(s.Komponist)))
                .ToList();
            var bands = (a?.Bands ?? [])
                .Select(Leer).Where(b => b != null).Select(b => b!)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            // Dirigent nur sinnvoll bei genau einer Band.
            var dirigent = bands.Count == 1 ? Leer(a?.Dirigent) : null;
            return new KklProgramm(stuecke, bands, dirigent);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "KKL-Programm-Strukturierung fehlgeschlagen: {Titel}", titel); return leer; }
    }

    private record KklProgrammAntwort(List<KklStueckAntwort>? Stuecke, List<string>? Bands, string? Dirigent);
    private record KklStueckAntwort(string? Titel, string? Komponist);

    public async Task<string?> ParaphrasiereAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var sys = "Du formulierst einen fremden Beschreibungstext in EIGENEN deutschen Worten neu (Grund: " +
            "Urheberrecht - es duerfen keine Formulierungen woertlich uebernommen werden). Regeln: gib NUR die " +
            "deutsche Neufassung zurueck (kein Vorspann, keine Anfuehrungszeichen); sachlich und knapp (2-3 Saetze); " +
            "nur Fakten, die im Ausgangstext stehen (nichts erfinden); falls der Ausgangstext englisch ist, ins " +
            "Deutsche uebersetzen und dabei umschreiben. Entferne Ticketing-/Werbe-Floskeln.";
        var eingabe = text.Length > 4000 ? text[..4000] : text;
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.3,
            messages = new object[] { new { role = "system", content = sys }, new { role = "user", content = eingabe } }
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var inhalt = chat?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            return string.IsNullOrWhiteSpace(inhalt) ? null : inhalt.Trim('"', ' ', '\n', '\r');
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Paraphrase fehlgeschlagen."); return null; }
    }

    public async Task<VideoAnalyse> VideoAusSucheAsync(string videoTitel, string? bandName, string suchText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suchText)) return new VideoAnalyse(null, null);
        var sys = "Du bestimmst aus WEB-SUCHERGEBNISSEN zu einem Blasmusik-/Brass-Band-YouTube-Video das " +
            "gespielte STÜCK und die KOMPONIST:IN. Antworte AUSSCHLIESSLICH mit JSON: " +
            "{\"stueckTitel\":\"|null\",\"komponist\":\"|null\"}.\n" +
            "Regeln (streng, kein Raten):\n" +
            "- stueckTitel NUR, wenn die Treffer eindeutig EIN konkretes Werk als Inhalt DIESES Videos belegen " +
            "(reiner Werktitel, ohne Bandname/Jahr/Ort/Zusätze). Ist das Video ein ganzes Konzert, mehrere " +
            "Stücke, eine Playlist oder unklar → null.\n" +
            "- komponist \"Vorname Nachname\" NUR, wenn klar belegt (bei Bearbeitung der ursprüngliche Komponist, " +
            "nicht der Arrangeur). Sonst null. NICHT den Bandnamen/die Dirigentin nehmen.";
        var text = suchText.Length > 6000 ? suchText[..6000] : suchText;
        var user = string.IsNullOrWhiteSpace(bandName)
            ? $"Videotitel: \"{videoTitel}\"\n\nSuchergebnisse:\n{text}"
            : $"Videotitel: \"{videoTitel}\"\n(Spielende Band: \"{bandName}\" – NICHT der Stücktitel.)\n\nSuchergebnisse:\n{text}";
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = sys },
                new { role = "user", content = user }
            }
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new VideoAnalyse(null, null);
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var json = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(json)) return new VideoAnalyse(null, null);
            var dto = JsonSerializer.Deserialize<VideoTitelDto>(json, MistralJson);
            return new VideoAnalyse(Plausibel(dto?.StueckTitel, 2, 160), Plausibel(dto?.Komponist, 3, 60));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Video-Such-Analyse fehlgeschlagen für {Titel}", videoTitel); return new VideoAnalyse(null, null); }
    }

    public async Task<string?> KomponistAusSucheAsync(string stueckTitel, string suchText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(suchText)) return null;
        var sys = "Du ermittelst aus Web-Suchergebnissen den Komponisten/die Komponistin eines Blasmusik-/" +
            "Brass-Band-Stücks. Antworte AUSSCHLIESSLICH mit dem Namen (Vorname Nachname) – oder mit dem Wort " +
            "unbekannt, wenn die Suchergebnisse den Komponisten nicht klar belegen. Nicht raten, keine Erklärung.";
        var text = suchText.Length > 6000 ? suchText[..6000] : suchText;
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            messages = new object[]
            {
                new { role = "system", content = sys },
                new { role = "user", content = $"Stück: \"{stueckTitel}\"\n\nSuchergebnisse:\n{text}" }
            }
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var name = chat?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
            if (string.IsNullOrWhiteSpace(name)) return null;
            name = name.Trim('"', '.', ' ', '\n', '\r');
            // Nur einen plausiblen Namen akzeptieren (nicht „unbekannt", keine Sätze).
            if (name.Length is < 3 or > 60) return null;
            if (name.Contains("unbekannt", StringComparison.OrdinalIgnoreCase)) return null;
            return name;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Komponist-Extraktion fehlgeschlagen für {Titel}", stueckTitel); return null; }
    }

    public async Task<VideoAnalyse> VideoTitelAnalysierenAsync(string videoTitel, string? bandName = null,
        string? beschreibung = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoTitel)) return new VideoAnalyse(null, null);
        var sys = "Du extrahierst aus TITEL und (falls vorhanden) BESCHREIBUNG eines Blasmusik-/Brassband-" +
            "YouTube-Videos das gespielte Stück, – falls klar genannt – die Komponist:in, den Aufführungs-Ort " +
            "und den Anlass. Antworte AUSSCHLIESSLICH mit JSON: " +
            "{\"stueckTitel\":\"|null\",\"komponist\":\"|null\",\"ort\":\"|null\",\"anlass\":\"|null\"}.\n" +
            "Regeln:\n" +
            "- stueckTitel: der reine Werktitel – OHNE Bandname, OHNE Jahr, OHNE Ort, OHNE Zusätze wie " +
            "\"live\", \"Jahreskonzert\", \"HD\", Kanal-/Reihennamen. Ist kein Stück erkennbar: null.\n" +
            "- komponist: \"Vorname Nachname\", NUR wenn genannt oder eindeutig (z. B. in Klammern/" +
            "nach \"by\"/\"von\"). Sonst null. NICHT raten, NICHT den Bandnamen oder die Dirigentin nehmen.\n" +
            "- Bei Bearbeitungen zählt der ursprüngliche Komponist des Werks, nicht der Arrangeur.\n" +
            "- ort: Aufführungsort (z. B. \"KKL Luzern\", \"Mehrzweckhalle Sempach\"), NUR wenn genannt. Sonst null.\n" +
            "- anlass: der Anlass/das Konzert (z. B. \"Jahreskonzert 2024\", \"Kirchenkonzert\", \"Galakonzert\"), " +
            "NUR wenn genannt. Sonst null. NICHT raten.";
        var user = string.IsNullOrWhiteSpace(bandName)
            ? $"Videotitel: \"{videoTitel}\""
            : $"Videotitel: \"{videoTitel}\"\n(Spielende Band: \"{bandName}\" – das ist NICHT der Stücktitel.)";
        if (!string.IsNullOrWhiteSpace(beschreibung))
            user += $"\n\nBeschreibung (Auszug):\n{beschreibung.Trim()[..Math.Min(beschreibung.Trim().Length, 1200)]}";
        var body = new
        {
            model = string.IsNullOrWhiteSpace(_llm.Model) ? "mistral-large-latest" : _llm.Model,
            temperature = 0.0,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = sys },
                new { role = "user", content = user }
            }
        };
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _llm.ApiKey);
            req.Content = JsonContent.Create(body);
            using var resp = await http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return new VideoAnalyse(null, null);
            var chat = await resp.Content.ReadFromJsonAsync<ChatResponse>(MistralJson, ct);
            var json = chat?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(json)) return new VideoAnalyse(null, null);
            var dto = JsonSerializer.Deserialize<VideoTitelDto>(json, MistralJson);
            return new VideoAnalyse(Plausibel(dto?.StueckTitel, 2, 160), Plausibel(dto?.Komponist, 3, 60),
                Plausibel(dto?.Ort, 2, 80), Plausibel(dto?.Anlass, 3, 80));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogWarning(ex, "Videotitel-Analyse fehlgeschlagen für {Titel}", videoTitel); return new VideoAnalyse(null, null); }
    }

    // Nimmt einen Wert nur an, wenn er kein „null"/leer ist und in einer plausiblen Länge liegt.
    private static string? Plausibel(string? s, int min, int max)
    {
        s = s?.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (s.Equals("null", StringComparison.OrdinalIgnoreCase) || s.Equals("unbekannt", StringComparison.OrdinalIgnoreCase)) return null;
        return s.Length >= min && s.Length <= max ? s : null;
    }

    private record VideoTitelDto(string? StueckTitel, string? Komponist, string? Ort, string? Anlass);

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
        // Auf einer Vereinsseite (BandDomain/Konzert-Vorschau) ist die Quell-Band die Standard-Band, wenn keine genannt wird.
        var standardBand = anfrage.QuelleTyp is CrawlQuelleTyp.BandDomain or CrawlQuelleTyp.BandKonzertVorschau
            ? Leer(anfrage.BandName) : null;
        var heute = DateOnly.FromDateTime(DateTime.Today);

        foreach (var k in a.Konzerte ?? [])
        {
            var programm = (k.Programm ?? [])
                .Where(p => !string.IsNullOrWhiteSpace(p.StueckTitel))
                .Select(p => new ProgrammZeileDaten(
                    p.StueckTitel!.Trim(), Leer(p.KomponistName), Leer(p.BandName) ?? standardBand,
                    p.Reihenfolge, Leer(p.ArrangeurName)))
                .ToList();
            // Konzert-Vorschau (BandKonzertVorschau): nur KÜNFTIGE Termine, dafür OHNE Programm-Pflicht –
            // es geht um möglichst viele angekündigte Konzerte. Vergangene/datumslose weglassen.
            if (anfrage.QuelleTyp == CrawlQuelleTyp.BandKonzertVorschau)
            {
                if (k.Datum is not { } dat || dat < heute) continue;
            }
            // Vereinsseiten (BandDomain): nur Konzerte mit mindestens einem Programm-Stück – ein blosser
            // Termin ohne Stücke ist für eine Vereins-Konzertliste zu wenig aussagekräftig. Sonst (Veranstalter-/
            // Lokal-Seiten) genügt Datum oder Programm.
            else if (anfrage.QuelleTyp == CrawlQuelleTyp.BandDomain)
            {
                if (programm.Count == 0) continue;
            }
            else if (k.Datum is null && programm.Count == 0) continue;
            var daten = new KonzertFundDaten(k.Datum, k.Uhrzeit, Leer(k.Name), Leer(k.Ort), null, programm, Webseite: Leer(k.Webseite));
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
    private record KonzertDto(DateOnly? Datum, TimeOnly? Uhrzeit, string? Name, string? Ort, string? Webseite, List<ProgrammDto>? Programm);
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

    /// <summary>Liest Uhrzeiten tolerant: „19:30", „19.30", „20 Uhr", „0800", „8" → TimeOnly.
    /// Unlesbares oder außerhalb 00:00–23:59 → null. Verhindert FormatException-Crashes.</summary>
    private sealed class ToleranterZeitConverter : JsonConverter<TimeOnly?>
    {
        public override TimeOnly? Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
        {
            if (reader.TokenType == JsonTokenType.Null) return null;
            if (reader.TokenType != JsonTokenType.String) { reader.Skip(); return null; }
            var s = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(s)) return null;

            // Erlaubt Trenner „:" / „." / keiner (0800). Minuten optional.
            var m = Regex.Match(s, @"(\d{1,2})\s*[:.hu]?\s*(\d{2})?");
            if (!m.Success || !int.TryParse(m.Groups[1].Value, out var std)) return null;
            var min = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
            if (std is < 0 or > 23 || min is < 0 or > 59) return null;
            return new TimeOnly(std, min);
        }

        public override void Write(Utf8JsonWriter w, TimeOnly? v, JsonSerializerOptions o)
        {
            if (v is null) w.WriteNullValue(); else w.WriteStringValue(v.Value.ToString("HH\\:mm"));
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
