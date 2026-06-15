using System.Text.Json;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Liest öffentliche Metadaten eines YouTube-Videos über die oEmbed-Schnittstelle
/// (kein API-Key nötig). Liefert u. a. den Videotitel und den Kanalnamen.
/// </summary>
public class YouTubeMetadataService(HttpClient http, ILogger<YouTubeMetadataService> logger)
{
    public record Metadaten(string? Titel, string? KanalName, string? KanalUrl);

    public async Task<Metadaten?> HoleAsync(string videoId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return null;
        try
        {
            var url = $"https://www.youtube.com/oembed?url=https://www.youtube.com/watch?v={videoId}&format=json";
            using var response = await http.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode) return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            var titel = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var kanal = root.TryGetProperty("author_name", out var a) ? a.GetString() : null;
            var kanalUrl = root.TryGetProperty("author_url", out var u) ? u.GetString() : null;
            return new Metadaten(titel, kanal, kanalUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "YouTube-Metadaten für {VideoId} konnten nicht geladen werden.", videoId);
            return null;
        }
    }

    public async Task<string?> HoleTitelAsync(string videoId, CancellationToken ct = default)
        => (await HoleAsync(videoId, ct))?.Titel;
}
