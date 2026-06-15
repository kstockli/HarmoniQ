using System.Text.RegularExpressions;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Interpretiert eine rohe Stück-Zeile (aus Scan oder manueller Eingabe):
/// extrahiert Jahr und Schwierigkeit (deutsch/englisch) und liefert einen
/// bereinigten Titel – ohne Jahr, Schwierigkeit, Label-Wörter ("Difficulty", "Dauer"),
/// Dauer-Angaben (5:30, 8 min) und gängige Besetzungs-Phrasen (Wind Ensemble, …).
/// </summary>
public static partial class StueckParser
{
    public record Ergebnis(string Titel, int? Jahr, Schwierigkeitsgrad Grad);

    [GeneratedRegex(@"\b(1[89]\d{2}|20\d{2})\b")]
    private static partial Regex JahrPattern();

    // Reihenfolge wichtig: spezifischere Begriffe (sehr schwer / very hard) zuerst prüfen.
    private static readonly (Schwierigkeitsgrad Grad, string[] Begriffe)[] GradBegriffe =
    [
        (Schwierigkeitsgrad.SehrSchwer, ["sehr schwer", "very hard", "really hard", "very difficult", "extremely difficult"]),
        (Schwierigkeitsgrad.Schwer,     ["schwer", "hard", "difficult", "advanced", "anspruchsvoll"]),
        (Schwierigkeitsgrad.Mittel,     ["mittel", "medium", "intermediate", "moderate", "moderately"]),
        (Schwierigkeitsgrad.Leicht,     ["leicht", "easy", "beginner", "elementary", "einfach"]),
    ];

    // Dauer-Angaben: 5:30, 12:00, 8 min, 8 Minuten, 8', 8′
    private static readonly Regex[] DauerPatterns =
    [
        new(@"\b\d{1,2}:\d{2}\b", RegexOptions.Compiled),
        new(@"\b\d{1,3}\s*(?:min(?:ute[ns]?)?|minutes?|minuten)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        new(@"\b\d{1,2}\s*['′]", RegexOptions.Compiled),
        new(@"(?:ca\.?|circa|approx\.?|~)\s*\d{1,3}", RegexOptions.Compiled | RegexOptions.IgnoreCase),
    ];

    // Label-Wörter, die in Tabellen vorkommen, aber nicht zum Titel gehören.
    private static readonly string[] LabelWoerter =
    [
        "difficulty", "schwierigkeit", "schwierigkeitsgrad", "grade", "level",
        "duration", "dauer", "length", "länge", "besetzung", "instrumentation", "scoring", "year", "jahr"
    ];

    // Mehrwort-Besetzungsphrasen (eindeutig Ensemble-Angaben, selten echte Titel).
    private static readonly string[] BesetzungsPhrasen =
    [
        "wind ensemble", "concert band", "symphonic band", "symphonic winds", "wind band",
        "wind symphony", "wind orchestra", "blasorchester", "sinfonisches blasorchester",
        "brass band", "brass ensemble", "brass choir", "saxophone quartet", "saxophone ensemble",
        "sax quartet", "sax ensemble", "saxophonquartett", "string orchestra", "string quartet",
        "streichorchester", "streichquartett", "chamber winds", "chamber ensemble",
        "trombone choir", "posaunenchor", "percussion ensemble", "flexible besetzung", "clarinet choir"
    ];

    public static Ergebnis Parse(string roh)
    {
        var text = roh?.Trim() ?? "";
        if (text.Length == 0) return new Ergebnis("", null, Schwierigkeitsgrad.Unbekannt);

        var titel = text;

        // 1) Jahr extrahieren.
        int? jahr = null;
        var jm = JahrPattern().Match(titel);
        if (jm.Success && int.TryParse(jm.Value, out var j))
        {
            jahr = j;
            titel = titel.Remove(jm.Index, jm.Length);
        }

        // 2) Schwierigkeit erkennen + Begriff entfernen.
        var grad = Schwierigkeitsgrad.Unbekannt;
        foreach (var (g, begriffe) in GradBegriffe)
        {
            var treffer = begriffe.FirstOrDefault(b => WortVorhanden(titel, b));
            if (treffer != null)
            {
                grad = g;
                titel = WortEntfernen(titel, treffer);
                break;
            }
        }

        // 3) Dauer-Angaben entfernen.
        foreach (var p in DauerPatterns)
            titel = p.Replace(titel, " ");

        // 4) Besetzungs-Phrasen entfernen.
        foreach (var b in BesetzungsPhrasen)
            titel = WortEntfernen(titel, b);

        // 5) Label-Wörter entfernen.
        foreach (var l in LabelWoerter)
            titel = WortEntfernen(titel, l);

        titel = Bereinige(titel);
        if (titel.Length == 0) titel = text; // Fallback: nichts kaputt machen
        return new Ergebnis(titel, jahr, grad);
    }

    private static bool WortVorhanden(string text, string wort) =>
        Regex.IsMatch(text, $@"(?<![\p{{L}}]){Regex.Escape(wort)}(?![\p{{L}}])", RegexOptions.IgnoreCase);

    private static string WortEntfernen(string text, string wort) =>
        Regex.Replace(text, $@"(?<![\p{{L}}]){Regex.Escape(wort)}(?![\p{{L}}])", " ", RegexOptions.IgnoreCase);

    private static string Bereinige(string s)
    {
        s = Regex.Replace(s, @"\(\s*\)", "");          // leere Klammern
        s = Regex.Replace(s, @"\[\s*\]", "");
        s = Regex.Replace(s, @"\s{2,}", " ");           // Mehrfach-Leerzeichen
        s = s.Trim(' ', '-', '–', '—', '•', '·', ':', ',', ';', '|', '/', '(', ')', '[', ']');
        s = Regex.Replace(s, @"\s{2,}", " ");
        return s.Trim();
    }
}
