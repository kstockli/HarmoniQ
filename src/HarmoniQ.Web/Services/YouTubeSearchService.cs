using System.Net;
using System.Text.Json;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Sucht Videos über die YouTube Data API v3. Benötigt einen API-Key in der Konfiguration
/// unter "YouTube:ApiKey" (z. B. via user-secrets). Ohne Key ist <see cref="Verfuegbar"/> false
/// und der Import-Assistent fällt auf manuelle Link-Eingabe zurück.
/// </summary>
public class YouTubeSearchService(HttpClient http, IConfiguration config, ILogger<YouTubeSearchService> logger)
{
    private readonly string? _apiKey = config["YouTube:ApiKey"];

    public bool Verfuegbar => !string.IsNullOrWhiteSpace(_apiKey);

    public record Treffer(string VideoId, string Titel, string Kanal, string ThumbnailUrl);
    public record SuchErgebnis(List<Treffer> Treffer, string? NextPageToken);

    /// <summary>
    /// Sucht Videos. <paramref name="pageToken"/> (aus einem vorigen Ergebnis) lädt die
    /// nächste Trefferseite – so können „weitere Treffer" nachgeladen werden.
    /// </summary>
    public async Task<SuchErgebnis> SucheAsync(string query, int maxResults = 6, string? pageToken = null, CancellationToken ct = default)
    {
        if (!Verfuegbar) return new SuchErgebnis([], null);
        try
        {
            var url = "https://www.googleapis.com/youtube/v3/search"
                + "?part=snippet&type=video&maxResults=" + maxResults
                + "&q=" + WebUtility.UrlEncode(query)
                + (string.IsNullOrEmpty(pageToken) ? "" : "&pageToken=" + pageToken)
                + "&key=" + _apiKey;

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("YouTube-Suche fehlgeschlagen ({Status}) für '{Query}'.", response.StatusCode, query);
                return new SuchErgebnis([], null);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var treffer = new List<Treffer>();
            if (root.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var videoId = item.GetProperty("id").GetProperty("videoId").GetString() ?? "";
                    var snippet = item.GetProperty("snippet");
                    var titel = snippet.GetProperty("title").GetString() ?? "";
                    var kanal = snippet.GetProperty("channelTitle").GetString() ?? "";
                    var thumb = snippet.GetProperty("thumbnails").GetProperty("medium").GetProperty("url").GetString() ?? "";
                    if (!string.IsNullOrEmpty(videoId))
                        treffer.Add(new Treffer(videoId, WebUtility.HtmlDecode(titel), kanal, thumb));
                }
            }
            var next = root.TryGetProperty("nextPageToken", out var np) ? np.GetString() : null;
            return new SuchErgebnis(treffer, next);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "YouTube-Suche für '{Query}' fehlgeschlagen.", query);
            return new SuchErgebnis([], null);
        }
    }
}
