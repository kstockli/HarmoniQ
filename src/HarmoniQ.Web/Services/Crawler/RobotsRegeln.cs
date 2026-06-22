namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Minimaler robots.txt-Parser (Spec §3). Wertet die für unseren Bot zutreffende Gruppe aus
/// (spezifisches User-agent-Token vor „*“) und entscheidet per Longest-Match (Allow gewinnt bei
/// gleicher Länge). Unterstützt <c>Disallow</c>, <c>Allow</c> und <c>Crawl-delay</c>.
/// Fehlt/leer/unlesbar → alles erlaubt (defensiv: lieber höflich crawlen als gar nicht).
/// </summary>
public sealed class RobotsRegeln
{
    private readonly List<(bool Allow, string Pfad)> _regeln;

    /// <summary>Vom Server gewünschter Mindestabstand (Sekunden), falls angegeben.</summary>
    public double? CrawlDelay { get; }

    private RobotsRegeln(List<(bool, string)> regeln, double? crawlDelay)
    {
        _regeln = regeln;
        CrawlDelay = crawlDelay;
    }

    /// <summary>Alles erlaubt (keine robots.txt vorhanden).</summary>
    public static RobotsRegeln Alles { get; } = new([], null);

    /// <summary>
    /// Parst robots.txt für das angegebene Bot-Token (z. B. „HarmoniQBot“). Zieht die spezifische
    /// Gruppe der „*“-Gruppe vor; existiert keine, wird „alles erlaubt“ zurückgegeben.
    /// </summary>
    public static RobotsRegeln Parse(string inhalt, string botToken)
    {
        if (string.IsNullOrWhiteSpace(inhalt)) return Alles;
        botToken = botToken.Trim().ToLowerInvariant();

        // Zeilen in Gruppen je User-agent sammeln.
        var gruppen = new List<(List<string> Agents, List<(bool Allow, string Pfad)> Regeln, double? Delay)>();
        List<string>? aktAgents = null;
        List<(bool, string)>? aktRegeln = null;
        double? aktDelay = null;
        bool letzteWarRegel = false;

        void GruppeAbschliessen()
        {
            if (aktAgents is { Count: > 0 })
                gruppen.Add((aktAgents, aktRegeln ?? [], aktDelay));
            aktAgents = null; aktRegeln = null; aktDelay = null;
        }

        foreach (var rohzeile in inhalt.Split('\n'))
        {
            var zeile = rohzeile;
            var kommentar = zeile.IndexOf('#');
            if (kommentar >= 0) zeile = zeile[..kommentar];
            zeile = zeile.Trim();
            if (zeile.Length == 0) continue;

            var dp = zeile.IndexOf(':');
            if (dp <= 0) continue;
            var feld = zeile[..dp].Trim().ToLowerInvariant();
            var wert = zeile[(dp + 1)..].Trim();

            switch (feld)
            {
                case "user-agent":
                    // Nach einer Regel beginnt eine neue Gruppe; mehrere aufeinanderfolgende
                    // User-agent-Zeilen gehören zur selben Gruppe.
                    if (letzteWarRegel) GruppeAbschliessen();
                    aktAgents ??= [];
                    aktRegeln ??= [];
                    aktAgents.Add(wert.ToLowerInvariant());
                    letzteWarRegel = false;
                    break;
                case "disallow":
                    aktRegeln ??= [];
                    aktRegeln.Add((false, wert));
                    letzteWarRegel = true;
                    break;
                case "allow":
                    aktRegeln ??= [];
                    aktRegeln.Add((true, wert));
                    letzteWarRegel = true;
                    break;
                case "crawl-delay":
                    if (double.TryParse(wert, System.Globalization.NumberStyles.Any,
                            System.Globalization.CultureInfo.InvariantCulture, out var cd))
                        aktDelay = cd;
                    letzteWarRegel = true;
                    break;
            }
        }
        GruppeAbschliessen();

        if (gruppen.Count == 0) return Alles;

        // Beste passende Gruppe: spezifisches Token vor „*“.
        var spezifisch = gruppen.FirstOrDefault(g => g.Agents.Any(a => botToken.Contains(a) || a == botToken));
        var stern = gruppen.FirstOrDefault(g => g.Agents.Contains("*"));
        var gewaehlt = spezifisch.Agents != null ? spezifisch
            : (stern.Agents != null ? stern : default);

        if (gewaehlt.Regeln == null) return Alles;
        return new RobotsRegeln(gewaehlt.Regeln, gewaehlt.Delay);
    }

    /// <summary>Ob der gegebene Pfad (inkl. Query) abgerufen werden darf.</summary>
    public bool DarfAbrufen(string pfad)
    {
        if (_regeln.Count == 0) return true;
        if (string.IsNullOrEmpty(pfad)) pfad = "/";

        (bool Allow, int Laenge)? beste = null;
        foreach (var (allow, regelPfad) in _regeln)
        {
            // Leerer Disallow = keine Einschränkung; leerer Allow ist bedeutungslos.
            if (regelPfad.Length == 0)
            {
                if (!allow) continue; // „Disallow:“ leer → erlaubt alles (keine Regel)
                continue;
            }
            if (PfadPasst(pfad, regelPfad) && (beste is null || regelPfad.Length > beste.Value.Laenge))
                beste = (allow, regelPfad.Length);
        }
        return beste?.Allow ?? true;
    }

    /// <summary>Match mit Unterstützung für „*“ (Wildcard) und „$“ (Zeilenende).</summary>
    private static bool PfadPasst(string pfad, string muster)
    {
        // Schnellpfad: kein Wildcard → Präfix-Vergleich.
        if (!muster.Contains('*') && !muster.EndsWith('$'))
            return pfad.StartsWith(muster, StringComparison.Ordinal);

        var endeVerankert = muster.EndsWith('$');
        if (endeVerankert) muster = muster[..^1];

        var teile = muster.Split('*');
        var pos = 0;
        for (var i = 0; i < teile.Length; i++)
        {
            var teil = teile[i];
            if (teil.Length == 0) continue;
            var idx = pfad.IndexOf(teil, pos, StringComparison.Ordinal);
            if (i == 0 && idx != 0) return false;          // erster Teil muss am Anfang stehen
            if (idx < 0) return false;
            pos = idx + teil.Length;
        }
        return !endeVerankert || pos == pfad.Length;
    }
}
