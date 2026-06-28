namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Konfiguration des Crawlers (appsettings-Abschnitt „Crawler“, Spec §3/§6). Der
/// <see cref="UserAgent"/> identifiziert den Bot gegenüber den abgefragten Seiten und sollte
/// eine Kontaktangabe enthalten (höfliches Crawling). Vor dem ersten produktiven Lauf hier den
/// echten Kontakt eintragen.
/// </summary>
public class CrawlerOptions
{
    public const string Section = "Crawler";

    /// <summary>Klar erkennbarer User-Agent mit Kontaktangabe (Pflicht laut §3) – für HTTP-Fetches.</summary>
    public string UserAgent { get; set; } = "HarmoniQBot/1.0 (+https://harmoniq.q-no.ch)";

    /// <summary>User-Agent für das JS-Rendering (Playwright/Chromium). Hier MUSS ein realer Browser-UA
    /// stehen: Manche Seiten (z. B. KKL/Vercel) liefern Bot-UAs nur den „Security Checkpoint" und
    /// unterdrücken die Client-XHR – das Rendering läuft dann ins Leere. Chromium IST ein echter
    /// Browser, also tritt es auch als solcher auf.</summary>
    public string RenderUserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>Mindestabstand zwischen zwei Requests an dieselbe Domain (Sekunden).</summary>
    public double RateLimitSekunden { get; set; } = 3;

    /// <summary>HTTP-Timeout je Request (Sekunden).</summary>
    public int RequestTimeoutSekunden { get; set; } = 30;

    /// <summary>Obergrenze für heruntergeladene Inhaltsgröße (Bytes) – Schutz vor Riesen-Downloads.</summary>
    public long MaxInhaltBytes { get; set; } = 15_000_000;

    /// <summary>robots.txt respektieren. Nur zu Testzwecken abschaltbar.</summary>
    public bool RobotsBeachten { get; set; } = true;

    /// <summary>JS-Rendering (Playwright/Chromium) erlauben – nur wirksam für Quellen mit
    /// <c>BrauchtRendering=true</c>. Default aus: Prod braucht installierte Browser (siehe DEPLOY.md).</summary>
    public bool RenderingAktiv { get; set; } = false;

    /// <summary>LLM-Extraktion (anbieter-neutral). Ohne konfigurierten Anbieter läuft der Stub.</summary>
    public LlmOptions Llm { get; set; } = new();
}

/// <summary>
/// Konfiguration der LLM-Extraktion (Spec §8). Anbieter-neutral; konkreter Anbieter (entschieden:
/// Mistral „La Plateforme") + API-Key + Modell per appsettings/user-secrets. <see cref="ApiKey"/>
/// gehört NICHT in appsettings.json einchecken – via user-secrets/Umgebungsvariable setzen.
/// </summary>
public class LlmOptions
{
    /// <summary>Anbieter: z. B. „mistral", „anthropic", „openai". Leer/„stub" → kein LLM.</summary>
    public string Provider { get; set; } = "";

    /// <summary>API-Key (Secret – nicht einchecken).</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Modell-ID, z. B. „mistral-large-latest".</summary>
    public string Model { get; set; } = "mistral-large-latest";

    /// <summary>Optionales Tageslimit an LLM-Aufrufen (Kostenbremse). 0 = unbegrenzt.</summary>
    public int TagesLimit { get; set; } = 0;

    /// <summary>Wenn true: Prompt (gekürzt) und vollständige LLM-Antwort werden auf Information-Level
    /// geloggt – zur Diagnose. Lokal in der Konsole/im Log sichtbar, in Prod in den Railway-Logs.</summary>
    public bool LogCalls { get; set; } = false;
}
