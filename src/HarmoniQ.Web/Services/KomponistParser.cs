using System.Text.RegularExpressions;
using HarmoniQ.Web.Data.Models;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Zerlegt Komponist:innen-/Arrangeur:innen-Felder (aus Crawler/LLM oder Eingabe) in einzelne
/// Beiträge. Erkennt Arrangeur-Marker („Arr.", „arr.", „Arrangeur", „Bearb.", „arranged by", …) –
/// die zugehörigen Namen werden dann als <see cref="StueckRolle.Arrangeur"/> statt Komponist:in
/// geführt – und trennt mehrere Namen (Komma, „&amp;", „und", „/", „;", „+").
/// Beispiel: „Arr. Filip Ceunen, Michael Story" → zwei Arrangeur-Beiträge.
/// </summary>
public static partial class KomponistParser
{
    public record Beitrag(string Name, StueckRolle Rolle);

    // Marker am Segment-Anfang: ab hier sind die Namen Arrangeur:innen (inkl. „… von"/„… by").
    [GeneratedRegex(
        @"^\s*(?:arr|arrangiert|arrangement|arrangeur|arranged|bearb|bearbeitet|bearbeitung|orch|orchestriert|orchestration)\b\.?\s*(?:von\s+|by\s+)?[:.\-–—]?\s*",
        RegexOptions.IgnoreCase)]
    private static partial Regex ArrMarker();

    // Trenner zwischen mehreren Namen.
    [GeneratedRegex(@"\s*(?:,|;|/|&|\+|\bund\b|\band\b)\s*", RegexOptions.IgnoreCase)]
    private static partial Regex Trenner();

    /// <summary>Liefert die einzelnen Beiträge aus einem Komponist- und (optional) einem
    /// separaten Arrangeur-Feld – ohne Dubletten (gleicher Name + gleiche Rolle).</summary>
    public static IReadOnlyList<Beitrag> Parse(string? komponistFeld, string? arrangeurFeld = null)
    {
        var ergebnis = new List<Beitrag>();
        FeldHinzufuegen(komponistFeld, ergebnis, immerArrangeur: false);
        FeldHinzufuegen(arrangeurFeld, ergebnis, immerArrangeur: true);
        return ergebnis;
    }

    private static void FeldHinzufuegen(string? feld, List<Beitrag> ziel, bool immerArrangeur)
    {
        if (string.IsNullOrWhiteSpace(feld)) return;
        // Rolle bleibt „klebrig": ein Marker mitten im Feld macht auch die nachfolgenden Namen zu
        // Arrangeur:innen (z. B. „Sousa, arr. Müller, Meier" → Sousa=Komponist, Müller+Meier=Arrangeur).
        var rolle = immerArrangeur ? StueckRolle.Arrangeur : StueckRolle.Komponist;
        foreach (var segment in Trenner().Split(feld))
        {
            var s = segment?.Trim() ?? "";
            if (s.Length == 0) continue;
            var m = ArrMarker().Match(s);
            if (m.Success) { rolle = StueckRolle.Arrangeur; s = s[m.Length..]; }
            var name = NameBereinigen(s);
            if (name.Length == 0) continue;
            if (!ziel.Any(b => b.Rolle == rolle && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
                ziel.Add(new Beitrag(name, rolle));
        }
    }

    private static string NameBereinigen(string n)
    {
        n = ArrMarker().Replace(n.Trim(), "");        // evtl. erneuter Marker direkt am Namen
        n = Regex.Replace(n, @"\(.*?\)", " ");          // Klammerzusätze, z. B. „(arr.)"
        n = n.Trim(' ', '-', '–', '—', '.', ',', ';', '/', '&', '+', ':', '\'', '"');
        return Regex.Replace(n, @"\s{2,}", " ").Trim();
    }
}
