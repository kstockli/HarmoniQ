using System.Text.RegularExpressions;

namespace HarmoniQ.Web.Services.Crawler;

/// <summary>
/// Spezial-Handler <b>Schweizer Brass Band Wettbewerb</b> (SBBW, swissbrass.ch, Spec §4.2).
/// Erkennung der Quelle + Hilfen zum Auflösen der Jahres-PDFs. Die eigentliche Orchestrierung
/// (PDF holen → LLM-Rangliste → Konzert-Funde) liegt im <c>CrawlRunner</c>; die Video-Verknüpfung
/// folgt in Teil 2b.
/// </summary>
public static partial class SbbwImporter
{
    /// <summary>Greift bei der SBBW-Resultate-Übersicht oder einem Jahres-Ergebnis-PDF.</summary>
    public static bool IstZustaendig(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u)
        && u.Host.EndsWith("swissbrass.ch", StringComparison.OrdinalIgnoreCase)
        && (u.AbsolutePath.Contains("resultate-sbbw", StringComparison.OrdinalIgnoreCase)
            || ResultsPdf().IsMatch(u.AbsoluteUri));

    [GeneratedRegex(@"results_(\d{4})\.pdf", RegexOptions.IgnoreCase)]
    private static partial Regex ResultsPdf();

    /// <summary>Jahr aus einer Ergebnis-PDF-URL (results_&lt;jahr&gt;.pdf), sonst null.</summary>
    public static int? JahrAusUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = ResultsPdf().Match(url);
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    /// <summary>Absolute results_&lt;jahr&gt;.pdf-Links aus dem HTML der Resultate-Übersicht.</summary>
    public static List<string> PdfLinks(string html, Uri basis)
    {
        var res = new List<string>();
        foreach (Match m in Regex.Matches(html, @"href=""([^""]*results_\d{4}\.pdf)""", RegexOptions.IgnoreCase))
            if (Uri.TryCreate(basis, m.Groups[1].Value, out var abs))
                res.Add(abs.ToString());
        return res.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
