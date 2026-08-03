using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace HarmoniQ.Web.Services.Kmvw;

/// <summary>
/// Parser für den <b>einmaligen KMVW-Import</b> (Kantonaler Musikverband Wallis, kmvw.ch). Serverseitig
/// gerendertes HTML; je Verein ein <c>&lt;div class="entry"&gt;</c> mit Logo, Name (<c>h2</c>), Ort (<c>h3</c>),
/// Kontakt-Links (E-Mail/Webseite/Facebook) und Tabs „Präsident"/„Dirigent" (Name in <c>&lt;strong&gt;</c> +
/// <c>mailto:</c>). Reine Parsing-Logik – Fetch + DB übernimmt <see cref="KmvwImportService"/>.
/// </summary>
public static class KmvwImporter
{
    public const string SeiteUrl = "https://www.kmvw.ch/de/mitglieder/vereine";
    public const string LogoQuelle = "Logo: Kantonaler Musikverband Wallis, kmvw.ch";

    public record Funktionaer(string Name, string? EMail);
    public record VereinRoh(string Name, string? Ort, string? LogoUrl, string? EMail, string? Webseite,
        string? Facebook, Funktionaer? Praesident, Funktionaer? Dirigent);

    private static string Clean(string? s) =>
        s == null ? "" : Regex.Replace(WebUtility.HtmlDecode(s).Replace(' ', ' '), @"\s+", " ").Trim();
    private static string? Leer(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    public static IReadOnlyList<VereinRoh> ParseSeite(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var result = new List<VereinRoh>();

        foreach (var e in doc.DocumentNode.SelectNodes("//div[@class='entry']") ?? Enumerable.Empty<HtmlNode>())
        {
            var name = Clean(e.SelectSingleNode(".//div[@class='content']/h2")?.InnerText);
            if (name.Length == 0) continue;
            var ort = Clean(e.SelectSingleNode(".//div[@class='content']/h3")?.InnerText);
            var logo = Leer(e.SelectSingleNode(".//div[@class='picture']//img")?.GetAttributeValue("src", ""));

            string? mail = null, web = null, fb = null;
            foreach (var a in e.SelectNodes(".//ul[@class='contact']//a") ?? Enumerable.Empty<HtmlNode>())
            {
                var h = a.GetAttributeValue("href", "").Trim();
                if (h.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) mail ??= h[7..];
                else if (Regex.IsMatch(h, "facebook|instagram", RegexOptions.IgnoreCase)) fb ??= h;
                else if (Regex.IsMatch(h, "^https?://", RegexOptions.IgnoreCase)) web ??= h;
            }

            // Tabs: Label (li[@data="tab_N"]) ↔ Inhalt (div.tab_content_N).
            Funktionaer? praesi = null, dir = null;
            foreach (var li in e.SelectNodes(".//ul[@class='tab_list']/li") ?? Enumerable.Empty<HtmlNode>())
            {
                var nr = li.GetAttributeValue("data", "").Replace("tab_", "");
                var lab = Clean(li.InnerText).ToLowerInvariant();
                var content = e.SelectSingleNode($".//div[contains(@class,'tab_content_{nr}')]");
                var f = FunktionaerAus(content);
                if (f == null) continue;
                if (lab.Contains("dirig")) dir = f;
                else if (lab.Contains("präsident") || lab.Contains("president")) praesi = f;
            }

            result.Add(new VereinRoh(name, Leer(ort), logo, Leer(mail), web, fb, praesi, dir));
        }
        return result;
    }

    private static Funktionaer? FunktionaerAus(HtmlNode? content)
    {
        if (content == null) return null;
        var name = Clean(content.SelectSingleNode(".//strong")?.InnerText);
        if (name.Length == 0 || name == "-") return null;
        var mail = content.SelectSingleNode(".//a[starts-with(@href,'mailto:')]")?.GetAttributeValue("href", "");
        return new Funktionaer(name, Leer(mail?.Replace("mailto:", "")));
    }

    /// <summary>Order-/diakritik-unabhängiger Namensschlüssel (sortierte, normalisierte Wörter) für den Abgleich.</summary>
    public static string WortSchluessel(string name)
    {
        var d = name.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in d)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 2).OrderBy(w => w, StringComparer.Ordinal));
    }
}
