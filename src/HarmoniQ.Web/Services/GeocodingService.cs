using System.Globalization;
using System.Text.Json;

namespace HarmoniQ.Web.Services;

/// <summary>
/// Adress-Geocoding über <b>Nominatim/OpenStreetMap</b> (gratis, kein API-Key). Wandelt einen
/// Adress-/Ortstext in Koordinaten (Lat/Lng) um – für die Koordinaten-Erfassung im Lokal-CRUD.
/// Nominatim verlangt einen aussagekräftigen User-Agent (aus <c>Crawler:UserAgent</c>) und max.
/// ~1 Anfrage/Sekunde – für interaktive Einzel-Lookups unkritisch.
/// </summary>
public class GeocodingService(HttpClient http)
{
    public async Task<(double Lat, double Lng)?> GeocodeAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var url = "https://nominatim.openstreetmap.org/search?format=jsonv2&limit=1&q="
                  + Uri.EscapeDataString(query);
        try
        {
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            if (double.TryParse(first.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(first.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
                return (lat, lng);
        }
        catch { /* Netz/Format-Fehler → kein Treffer */ }
        return null;
    }
}
