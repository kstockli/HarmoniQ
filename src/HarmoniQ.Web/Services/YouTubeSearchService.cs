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

    public record KanalInfo(string UploadsPlaylistId, string KanalTitel);

    /// <summary>
    /// Löst eine YouTube-Kanal-URL (Handle <c>/@name</c>, <c>/channel/UC…</c> oder <c>/user/name</c>)
    /// zur Uploads-Playlist auf. Kostet nur 1 Kontingent-Einheit (vs. 100 bei der Suche) und ist präzise
    /// (genau die Uploads dieses Kanals). <c>null</c>, wenn nicht auflösbar.
    /// </summary>
    public async Task<KanalInfo?> KanalAufloesenAsync(string kanalUrl, CancellationToken ct = default)
    {
        if (!Verfuegbar || string.IsNullOrWhiteSpace(kanalUrl)) return null;
        var q = KanalQuery(kanalUrl);
        if (q is null) { logger.LogInformation("YouTube-Kanal-URL nicht auflösbar: {Url}", kanalUrl); return null; }
        try
        {
            var url = "https://www.googleapis.com/youtube/v3/channels"
                + "?part=contentDetails,snippet"
                + "&" + q.Value.Param + "=" + WebUtility.UrlEncode(q.Value.Wert)
                + "&key=" + _apiKey;

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("YouTube-Kanal-Auflösung fehlgeschlagen ({Status}) für '{Url}'.", response.StatusCode, kanalUrl);
                return null;
            }
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("items", out var items) || items.GetArrayLength() == 0) return null;
            var item = items[0];
            var uploads = item.GetProperty("contentDetails").GetProperty("relatedPlaylists").GetProperty("uploads").GetString();
            var titel = item.GetProperty("snippet").GetProperty("title").GetString() ?? "";
            return string.IsNullOrEmpty(uploads) ? null : new KanalInfo(uploads, titel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "YouTube-Kanal-Auflösung für '{Url}' fehlgeschlagen.", kanalUrl);
            return null;
        }
    }

    /// <summary>Liefert die neuesten Videos einer (Uploads-)Playlist – neueste zuerst. 1 Einheit/Aufruf.</summary>
    public async Task<List<Treffer>> PlaylistVideosAsync(string playlistId, int maxResults = 25, CancellationToken ct = default)
    {
        if (!Verfuegbar || string.IsNullOrWhiteSpace(playlistId)) return [];
        try
        {
            var url = "https://www.googleapis.com/youtube/v3/playlistItems"
                + "?part=snippet&maxResults=" + Math.Clamp(maxResults, 1, 50)
                + "&playlistId=" + WebUtility.UrlEncode(playlistId)
                + "&key=" + _apiKey;

            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("YouTube-Playlist-Abruf fehlgeschlagen ({Status}) für '{Playlist}'.", response.StatusCode, playlistId);
                return [];
            }
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var treffer = new List<Treffer>();
            if (doc.RootElement.TryGetProperty("items", out var items))
            {
                foreach (var item in items.EnumerateArray())
                {
                    var snippet = item.GetProperty("snippet");
                    var videoId = snippet.TryGetProperty("resourceId", out var rid) && rid.TryGetProperty("videoId", out var vid)
                        ? vid.GetString() ?? "" : "";
                    if (string.IsNullOrEmpty(videoId)) continue;
                    var titel = snippet.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var kanal = snippet.TryGetProperty("channelTitle", out var c) ? c.GetString() ?? "" : "";
                    var thumb = snippet.TryGetProperty("thumbnails", out var th) && th.TryGetProperty("medium", out var m)
                        ? m.GetProperty("url").GetString() ?? "" : "";
                    // Gelöschte/private Uploads erscheinen als Platzhalter – überspringen.
                    if (titel is "Deleted video" or "Private video") continue;
                    treffer.Add(new Treffer(videoId, WebUtility.HtmlDecode(titel), kanal, thumb));
                }
            }
            return treffer;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "YouTube-Playlist-Abruf für '{Playlist}' fehlgeschlagen.", playlistId);
            return [];
        }
    }

    /// <summary>Neueste Videos eines Kanals (URL) – kombiniert Auflösung + Uploads-Abruf. Leer, wenn
    /// der Kanal nicht auflösbar ist (Aufrufer fällt dann auf die Namenssuche zurück).</summary>
    public async Task<List<Treffer>> KanalVideosAsync(string kanalUrl, int maxResults = 25, CancellationToken ct = default)
    {
        var info = await KanalAufloesenAsync(kanalUrl, ct);
        return info is null ? [] : await PlaylistVideosAsync(info.UploadsPlaylistId, maxResults, ct);
    }

    /// <summary>Zerlegt eine YouTube-Kanal-URL in den passenden channels.list-Parameter
    /// (<c>id</c>/<c>forHandle</c>/<c>forUsername</c>). Moderne Custom-URLs werden als Handle behandelt.</summary>
    private static (string Param, string Wert)? KanalQuery(string url)
    {
        url = url.Trim();
        // Reines Handle „@name" oder „UC…"-ID ohne Host zulassen.
        if (url.StartsWith('@')) return ("forHandle", url);
        if (System.Text.RegularExpressions.Regex.IsMatch(url, "^UC[0-9A-Za-z_-]{20,}$")) return ("id", url);

        var host = url.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase);
        if (host < 0) return null;
        var pfad = url[(host + "youtube.com".Length)..].TrimStart('/');
        // Query/Fragment abschneiden.
        foreach (var trenner in new[] { '?', '#' })
        {
            var i = pfad.IndexOf(trenner);
            if (i >= 0) pfad = pfad[..i];
        }
        var teile = pfad.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (teile.Length == 0) return null;

        var erst = teile[0];
        if (erst.StartsWith('@')) return ("forHandle", erst);
        if (erst.Equals("channel", StringComparison.OrdinalIgnoreCase) && teile.Length > 1) return ("id", teile[1]);
        if (erst.Equals("user", StringComparison.OrdinalIgnoreCase) && teile.Length > 1) return ("forUsername", teile[1]);
        if (erst.Equals("c", StringComparison.OrdinalIgnoreCase) && teile.Length > 1) return ("forHandle", "@" + teile[1]);
        // Bare Custom-URL (youtube.com/Name) → moderne YouTube-Handles.
        return ("forHandle", erst.StartsWith('@') ? erst : "@" + erst);
    }
}
